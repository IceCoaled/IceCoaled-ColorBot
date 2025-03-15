#if DEBUG
//#define RENDORDOC_DEBUG
//#define CAPTURE_DEBUG
//#define DEBUG_BUFFER
#if !RENDORDOC_DEBUG
#endif
#endif


using System.Runtime.InteropServices;
using SCB.DirectX;
using SharpGen.Runtime;
using Vortice.Direct3D11;

namespace SCB
{
    internal partial class DirectX11
    {
        /// <summary>
        /// Processes the current frame and returns the filtered result as a Bitmap.
        /// </summary>
        /// <returns>The filtered frame as a Bitmap.</returns>
        internal double GetEnemyDetails( ref List<EnemyData> detectedPlayers )
        {
#if CAPTURE_DEBUG
            string captureDebugFileName = FileManager.enemyScansFolder + new Random().Next().ToString() + "_Unfiltered.png";
            int vKmB = 0x01;
            bool captureDbg = false;
#endif
            // check if device was removed
            if ( d3d11Device!.DeviceRemovedReason.Failure )
            {
                ErrorHandler.HandleException( new Exception( $"Device removed: {d3d11Device!.DeviceRemovedReason}" ) );
            }

            // Check if window capture class is resetting
            if ( !windowCapture!.ClassResetting!.IsSet )
            {
                // If it is wait
                windowCapture.ClassResetting.Wait();
            }

            // Wait for clean buffers signal to be set If needed
            if ( !CleaningBuffersSignal!.IsSet )
            {
                CleaningBuffersSignal.Wait();
            }

            int newFrameResult;
            NewFrame?.WaitForSignaledState();
#if CAPTURE_DEBUG
            newFrameResult = ProcessNewFrame( out double captureTime );
#else
            newFrameResult = ProcessNewFrame( out double captureTime );
#endif
            // Reset thread signal so the newest frame can be captured
            NewFrame?.SetNonSignaled();

            // If the result isnt modified we dont have any targets on the screen
            // So we skip gpu related stuff
            if ( newFrameResult == -1 )
            {
                return captureTime;
            }

            try
            {
#if CAPTURE_DEBUG
                captureDbg = HidInputs.IsKeyPressed( ref vKmB );
                if ( captureDbg )
                {
                    try
                    {
                        // Map the filtered resource to access the filtered image data
                        bool mapped = d3d11Context!.Map( stagingBuffer, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None, out MappedSubresource mappedResource ).Success;

                        if ( !mapped )
                        {
                            ErrorHandler.HandleExceptionNonExit( new ExternalException( "Failed to map staging buffer" ) );
                        } else
                        {
                            // Wait for map
                            WaitForFence();
                            // This handles unmapping and flushing
                            PrintImageToFile( ref captureDebugFileName, ref mappedResource );
                        }


                    } catch ( ExternalException ex )
                    {
                        ErrorHandler.HandleExceptionNonExit( ex );
                    }
                }
#endif

                // Copy the texture to uav buffer            
                d3d11Context!.CopyResource( uaBuffer, stagingBuffer );

            } catch ( Exception ex )
            {
                ErrorHandler.HandleExceptionNonExit( ex );
            } finally
            {
                // Apply the filtering process via our shader and uav buffer
#if RENDORDOC_DEBUG
                ApplyFilterProcess( captureDbg );
                GetDetectedPlayers( ref detectedPlayers, ref captureTime );
#else
                ApplyFilterProcess();
                GetDetectedPlayers( ref detectedPlayers, ref captureTime );
#endif
            }

#if CAPTURE_DEBUG
            if ( captureDbg )
            {
                try
                {
                    // For saving image as apart of debug
                    d3d11Context!.CopyResource( stagingBuffer, uaBuffer );

                    // Map the filtered resource to access the filtered image data
                    bool mapped = d3d11Context!.Map( stagingBuffer, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None, out MappedSubresource mappedResource ).Success;

                    if ( !mapped )
                    {
                        ErrorHandler.HandleExceptionNonExit( new ExternalException( "Failed to map staging buffer" ) );
                    } else
                    {
                        // Wait for map
                        WaitForFence();
                        // This handles unmapping and flushing
                        PrintImageToFile( ref captureDebugFileName, ref mappedResource, true );
                    }


                } catch ( ExternalException ex )
                {
                    ErrorHandler.HandleExceptionNonExit( ex );
                }
            }
#endif
            // Have async thread clean buffers
            Task.Run( CleanBuffers );
            return captureTime;
        }




        /// <summary>
        /// Applies the filtering process to the captured resource using the specified shader.
        /// </summary>
        private unsafe void ApplyFilterProcess( bool captureDebug = false )
        {
#if RENDORDOC_DEBUG
            if ( captureDebug )
            {
                renderDocApi?.StartFrameCapture( d3d11Device!.NativePointer, IntPtr.Zero );
            }
#endif
            try
            {
                // Set the shader, uavs
                d3d11Context?.CSSetUnorderedAccessViews( 0, uavBufferViews?.ToArray()! );

                // Here we will set, dispatch, and wait for the shader to finish
                // Then we will unbind, and bind the next shaders in order
                d3d11Context?.CSSetShader( computeShaders?.GetValueOrDefault( DirectX.ShaderManager.ShaderType.FirstPass ) );
                d3d11Context?.Dispatch( XDispatch, YDispatch, 1 );
                WaitForFence();
                d3d11Context?.CSSetShader( computeShaders?.GetValueOrDefault( DirectX.ShaderManager.ShaderType.SecondPass ) );
                d3d11Context?.Dispatch( XDispatch, YDispatch, 1 );
                WaitForFence();
                d3d11Context?.CSSetShader( computeShaders?.GetValueOrDefault( DirectX.ShaderManager.ShaderType.ThirdPass ) );
                d3d11Context?.Dispatch( XDispatch, YDispatch, 1 );
                WaitForFence();
                d3d11Context?.CSSetShader( computeShaders?.GetValueOrDefault( DirectX.ShaderManager.ShaderType.FourthPass ) );
                d3d11Context?.Dispatch( XDispatch, YDispatch, 1 );
                WaitForFence();
#if DEBUG
                // This is debug draw shader
                d3d11Context?.CSSetShader( computeShaders?.GetValueOrDefault( DirectX.ShaderManager.ShaderType.DebugDraw ) );
                d3d11Context?.Dispatch( XDispatch, YDispatch, 1 );
                d3d11Context?.Flush();
                WaitForFence();
#endif
            } catch ( SharpGenException ex )
            {
                if ( d3d11Device!.DeviceRemovedReason.Failure )
                {
                    ErrorHandler.HandleException( new Exception( $"Device removed: {d3d11Device!.DeviceRemovedReason}", ex ) );
                } else
                {
                    // Reset the class
                    ResetDx11();
                }
            } finally
            {

                // Unset the shader, uavs
                d3d11Context?.CSSetShader( null ); //< unbind the shader i didnt see and option to UnsetShader with vortice, so we will just set it to null
                d3d11Context?.CSUnsetUnorderedAccessViews( 0, ( ( uint ) uavBufferViews?.Count! ) );

                // Flush for preventative measures
                d3d11Context?.Flush();
                // Wait for fence signal
                WaitForFence();

#if RENDORDOC_DEBUG

                if ( captureDebug )
                {
                    _ = renderDocApi?.EndFrameCapture( d3d11Device!.NativePointer, IntPtr.Zero );
                }
#endif
            }
        }

        private void CleanBuffers()
        {
            CleaningBuffersSignal.Reset();
            try
            {
                // Set the shader, uavs
                d3d11Context?.CSSetUnorderedAccessViews( 0, uavBufferViews?.ToArray()! );
                // Set the shader
                d3d11Context?.CSSetShader( computeShaders?.GetValueOrDefault( DirectX.ShaderManager.ShaderType.Cleaner ) );
                // Dispatch the shader
                d3d11Context?.Dispatch( XDispatch, YDispatch, 1 );
                // Flush for preventative measures
                d3d11Context?.Flush();
                // Wait for fence signal
                WaitForFence();
            } catch ( SharpGenException ex )
            {
                if ( d3d11Device!.DeviceRemovedReason.Failure )
                {
                    ErrorHandler.HandleException( new Exception( $"Device removed: {d3d11Device!.DeviceRemovedReason}", ex ) );
                } else
                {
                    // Reset the class
                    ResetDx11();
                }
            } finally
            {
                // Unset the shader, uavs
                d3d11Context?.CSSetShader( null ); //< unbind the shader i didnt see and option to UnsetShader with vortice, so we will just set it to null
                d3d11Context?.CSUnsetUnorderedAccessViews( 0, ( ( uint ) uavBufferViews?.Count! ) );
                // Flush for preventative measures
                d3d11Context?.Flush();
                // Wait for fence signal
                WaitForFence();
                CleaningBuffersSignal.Set();
            }
        }


        /// <summary>
        /// Extracts the detected players from the detected players buffer.
        /// </summary>
        /// <param name="detectedPlayers"></param>
        /// <param name="captureTime"></param>
        /// <exception cref="NullReferenceException"></exception>
        private void GetDetectedPlayers( ref List<EnemyData> detectedPlayers, ref double captureTime )
        {
            detectedPlayers = [];

            try
            {
                // Map the detected players buffer
                if ( d3d11Context!.Map( BufferManager?.GetUavBufferSafe<ID3D11Buffer>( DirectX.UavBufferManager.BufferType.DetectedPlayers ), 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None, out MappedSubresource mappedResource ).Success )
                {
                    // Wait for map
                    WaitForFence();

                    // De-serialize the detected players from the mapped resource
                    DetectedPlayers players = Marshal.PtrToStructure<DetectedPlayers>( mappedResource.DataPointer );

                    // Check if the de-serialization was successful
                    if ( players.SafetyCheck != uint.MaxValue )
                    {
                        throw new NullReferenceException( "Failed to de-serialize detected players from mapped resource" );
                    } else
                    {
                        // Loop through the detected players and add them to the list
                        for ( int i = 0; i < players.DetectedPlayerCount; i++ )
                        {
                            // Check if the player position is valid
                            if ( players.PlayerPositions[ i ].HeadPosition.X == 0 || players.PlayerPositions[ i ].HeadPosition.Y == 0 ||
                                players.PlayerPositions[ i ].TorsoPosition.X == 0 || players.PlayerPositions[ i ].TorsoPosition.Y == 0 ||
                                players.PlayerPositions[ i ].BoundingBox == 0 )
                            {
                                throw new Exception( "Invalid player position detected" );
                            }
                            // Add the detected player to the list
                            detectedPlayers.Add(
                                new(
                                new( players.PlayerPositions[ i ].HeadPosition.X,
                                players.PlayerPositions[ i ].HeadPosition.Y ),
                                new( players.PlayerPositions[ i ].TorsoPosition.X,
                                players.PlayerPositions[ i ].TorsoPosition.Y ),
                                ref captureTime,
                                ref players.PlayerPositions[ i ].BoundingBox,
                                ref Window ) );
                        }
                    }
                } else
                {
                    throw new NullReferenceException( "Failed to map Detected players buffer" );
                }
            } catch ( Exception ex )
            {
                ErrorHandler.HandleExceptionNonExit( ex );
            } finally
            {
                // Unmap the buffer
                d3d11Context!.Unmap( BufferManager?.GetUavBufferSafe<ID3D11Buffer>( DirectX.UavBufferManager.BufferType.DetectedPlayers ), 0 );
                WaitForFence();
            }
        }
    }
}
