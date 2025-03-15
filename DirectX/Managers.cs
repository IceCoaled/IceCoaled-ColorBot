#if DEBUG
//#define RENDORDOC_DEBUG
//#define CAPTURE_DEBUG
//#define DEBUG_BUFFER
#if !RENDORDOC_DEBUG
#endif
#endif


using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SharpGen.Runtime;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace SCB.DirectX
{

    /// <summary>
    /// This record is for holding the paths to the shaders
    /// This was made so the shader manager can be more dynamic,
    /// Has direct access to the shader paths, instead of
    /// Always having to go through the file manager.
    /// </summary>
    record ShaderPaths
    {
        private string ShaderFolder { get; init; }
        private string ShaderDefines { get; init; }
        private string ShaderFunctions { get; init; }
        internal Dictionary<ShaderManager.ShaderType, string> ShaderFiles { get; init; }
        internal string GetShaderFolder() => ShaderFolder;
        internal string GetShaderDefines() => ShaderDefines;
        internal string GetShaderFunctions() => ShaderFunctions;

        public ShaderPaths()
        {
            ShaderFolder = FileManager.shaderFolder;
            ShaderDefines = FileManager.shaderDefinesFile;
            ShaderFunctions = FileManager.shaderFunctionsFile;
            ShaderFiles = [];
            ShaderFiles.Add( ShaderManager.ShaderType.FirstPass, FileManager.firstPassShaderFile );
            ShaderFiles.Add( ShaderManager.ShaderType.SecondPass, FileManager.secondPassShaderFile );
            ShaderFiles.Add( ShaderManager.ShaderType.ThirdPass, FileManager.thirdPassShaderFile );
            ShaderFiles.Add( ShaderManager.ShaderType.FourthPass, FileManager.fourthPassShaderFile );
            ShaderFiles.Add( ShaderManager.ShaderType.Cleaner, FileManager.bufferCleanerShaderFile );
            ShaderFiles.Add( ShaderManager.ShaderType.DebugDraw, FileManager.debugDrawShaderFile );
        }

        internal string GetShaderPath( ShaderManager.ShaderType shaderType )
        {
            if ( ShaderFiles.TryGetValue( shaderType, out var path ) )
            {
                return path;
            }
            return string.Empty;
        }

        internal string GetShaderName( ShaderManager.ShaderType shaderType )
        {
            if ( ShaderFiles.TryGetValue( shaderType, out var path ) )
            {
                char[] fileName = Path.GetFileNameWithoutExtension( path ).ToCharArray();
                return new string( fileName[ ..^4 ] );
            }
            return string.Empty;
        }
    }


    /// <summary>
    /// This class is for holding <see cref="ID3D11ComputeShader"/>
    /// This includes the <see cref="Blob"/>
    /// </summary>
    /// <param name="computeShader"></param>
    /// <param name="shaderBlob"></param>
    internal partial class ComputeShader( ref ID3D11ComputeShader? computeShader, ref Blob? shaderBlob ) : IDisposable
    {
        private bool Disposed { get; set; } = false;
        private ID3D11ComputeShader? ComputeShaderInstance { get; init; } = computeShader;
        private Blob? ShaderBlob { get; init; } = shaderBlob;
        internal ID3D11ComputeShader? GetComputeShader() => ComputeShaderInstance;
        internal Blob? GetShaderBlob() => ShaderBlob;

        ~ComputeShader()
        {
            Dispose( false );
        }

        public void Dispose()
        {
            Dispose( true );
            GC.SuppressFinalize( this );
        }

        protected virtual void Dispose( bool disposing )
        {
            if ( disposing &&
                !Disposed )
            {
                ComputeShaderInstance?.Dispose();
                ShaderBlob?.Dispose();
            }
            Disposed = true;
        }

    }


    /// <summary>
    /// This class is for managing the shaders
    /// It creates, compiles, stores, and disposes of the shaders
    /// </summary>
    internal partial class ShaderManager : IDisposable
    {
        private bool Disposed { get; set; } = false;
        private Dictionary<ShaderType, ComputeShader> ComputeShaders { get; init; }
        private ShaderIncludehandler? IncludeHandler { get; init; }
        private ShaderFlags CompileFlags { get; init; }
        private string GpuVender { get; init; }


        private readonly ID3D11Device? D3D11Device;
        private readonly UavBufferManager? BufferManager;
        private ColorToleranceManager? ColorManager;
        private ShaderPaths ShaderPaths;

        /// <summary>
        /// Initializes a new instance of the <see cref="ShaderManager"/> class.
        /// </summary>
        /// <param name="d3d11Device"></param>
        /// <param name="colorManager"></param>
        /// <param name="bufferManager"></param>
        /// <param name="gpuVender"></param>
        internal ShaderManager( ref ID3D11Device? d3d11Device, ref ColorToleranceManager? colorManager, ref UavBufferManager? bufferManager, ref string gpuVender )
        {
            try
            {
                D3D11Device = d3d11Device ?? throw new ArgumentNullException( nameof( d3d11Device ), "ShaderManager: ID3D11Device reference null" );
                ColorManager = colorManager ?? throw new ArgumentNullException( nameof( colorManager ), "ShaderManager: ColorToleranceManager reference null" );
                BufferManager = bufferManager ?? throw new ArgumentNullException( nameof( bufferManager ), "ShaderManager: UavBufferManager reference null" );
            } catch ( ArgumentNullException ex )
            {
                ErrorHandler.HandleException( ex );
            }

            // Initialize shader paths, gpu vender, compute shaders, include handler, and compile flags
            ShaderPaths = new();
            GpuVender = gpuVender;
            ComputeShaders = [];
            IncludeHandler = new ShaderIncludehandler( ShaderPaths.GetShaderFolder() );
#if DEBUG
            CompileFlags = ShaderFlags.Debug | ShaderFlags.SkipOptimization | ShaderFlags.EnableStrictness | ShaderFlags.IeeeStrictness;
#else
            CompileFlags = ShaderFlags.OptimizationLevel3 | ShaderFlags.EnableStrictness | ShaderFlags.IeeeStrictness | ( ( ShaderFlags ) ( 1 << 21 ) ); //< ( 1 << 21 ) is flag for telling compiler all resources are bound, this helps with optimization
#endif

            // Edit the shader defines
            EditShaderDefines();
            // Compile all the shaders
            CompileAllShaders();
        }

        ~ShaderManager()
        {
            Dispose( false );
        }

        /// <summary>
        /// Gets either the <see cref="ComputeShader"/> or the <see cref="ID3D11ComputeShader"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="shaderType"></param>
        /// <returns>Chosen shader type</returns>
        internal T? GetComputeShaderSafe<T>( ShaderType shaderType ) where T : class
        {
            if ( ComputeShaders.TryGetValue( shaderType, out var shader ) && shader is not null )
            {
                if ( typeof( T ) == typeof( ID3D11ComputeShader ) )
                {
                    return shader.GetComputeShader() as T;
                } else if ( typeof( T ) == typeof( ComputeShader ) )
                {
                    return shader as T;
                }
            }
#if DEBUG
            Logger.Log( $"Failed to find compute shader: {shaderType}" );
#endif
            ErrorHandler.HandleException( new Exception( $"Failed to find compute shader: {shaderType}" ) );
            return null;
        }

        /// <summary>
        /// Gets all of either the <see cref="ComputeShader"/> or the <see cref="ID3D11ComputeShader"/>
        /// From the shader manager
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>All of the chosen shader type</returns>
        internal IEnumerable<KeyValuePair<ShaderType, T?>> GetAllComputeShadersSafe<T>() where T : class
        {
            foreach ( var shaderType in Enum.GetValues<ShaderType>() )
            {
                yield return new( shaderType, GetComputeShaderSafe<T>( shaderType ) as T );
            }
        }


        /// <summary>
        /// Edits the shader defines file
        /// </summary>
        private void EditShaderDefines()
        {
            int colorRangeCount = BufferManager!.NumOfColorRangesSint();
            if ( colorRangeCount == -1 )
            {
                ErrorHandler.HandleException( new Exception( "Failed to get number of color ranges" ) );
            }

            ShaderUtilities.EditShaderCode( ref ShaderPaths, ref ColorManager!, colorRangeCount, ( GpuVender == "AMD" ) ? 16 : 8 );
        }

        /// <summary>
        /// Compiles all the shaders.
        /// <see cref="CompileAndCreateShaderFromFile"/>
        /// </summary>
        private void CompileAllShaders()
        {
            foreach ( var shaderType in Enum.GetValues<ShaderType>() )
            {
                CompileAndCreateShaderFromFile( shaderType );
            }
        }


        /// <summary>
        /// Compiles the shader from a hlsl file, creates the shader, and adds it to the dictionary
        /// </summary>
        private unsafe void CompileAndCreateShaderFromFile( ShaderType shaderType )
        {
            // Update status bar to let user know the compiling may take a couple minutes
            ErrorHandler.PrintToStatusBar( "Compiling Shader, this may take up to 2 minutes" );

            // Compile the shader
            SharpGenException? compileException = new
            (
            Compiler.CompileFromFile(
            ShaderPaths.GetShaderPath( shaderType ),
            null,
            IncludeHandler,
            "main",
            "cs_5_0",
            CompileFlags,
            EffectFlags.None,
            out Blob? shaderBlob,
            out Blob? errorBlob ) );

            // Check if the shader compiled successfully
            if ( shaderBlob is null || compileException.ResultCode != Result.Ok )
            {
                string? error = null;
                try
                {
                    error = Marshal.PtrToStringAnsi( errorBlob!.BufferPointer ) ?? throw new ExternalException( "Failed to get error blob" );
                } catch ( ExternalException ex )
                {
                    ErrorHandler.HandleException( ex );
                }

                //Add error indicator to the start of the error message
                error = compileException.Message + error.Aggregate( " , Blob Error: ", ( acc, c ) => acc + c );

                // Create a new exception with the error message
                // This will still have all the same information as the compile exception, just with the added blob error.
                compileException = new( error, compileException );
                ErrorHandler.HandleException( compileException );
            } else
            {
                // Create the shader
                try
                {
                    var shader = D3D11Device!.CreateComputeShader( shaderBlob!.BufferPointer.ToPointer(), shaderBlob!.BufferSize, null ) ?? throw new SharpGenException( "Failed to create compute shader", compileException, new Result( Result.Fail.Code ) );
                    // Set the shader debug name
#if DEBUG
                    shader.DebugName = $"IceCoaled{shaderType}Shader";
#endif
                    ComputeShaders.Add( shaderType, new( ref shader, ref shaderBlob ) );
                } catch ( SharpGenException ex )
                {
                    ErrorHandler.HandleException( ex );
                }
            }

            // Update status bar to let user know the compiling is done
            ErrorHandler.PrintToStatusBar( $"{ShaderPaths.GetShaderName( shaderType )} Compiled Successfully" );

#if DEBUG
            Logger.Log( $"{ShaderPaths.GetShaderName( shaderType )} Compiled Successfully" );
#endif
        }

        public void Dispose()
        {
            Dispose( true );
            GC.SuppressFinalize( this );
        }

        protected virtual void Dispose( bool disposing )
        {
            if ( disposing &&
                !Disposed )
            {
                foreach ( var shader in ComputeShaders )
                {
                    shader.Value?.Dispose();
                }
                ComputeShaders.Clear();
                IncludeHandler?.Dispose();
            }
            Disposed = true;
        }


        internal enum ShaderType
        {
            FirstPass = 0xF157,
            SecondPass = 0x5EC0171D,
            ThirdPass = 0x71e14D,
            FourthPass = 0xF01471e,
            Cleaner = 0xC1EA171,
#if DEBUG
            DebugDraw = 0x0D0D0D,
#endif
        }
    }


    /// <summary>
    /// This class is for holding the <see cref="ID3D11UnorderedAccessView"/> 
    /// and <see cref="ID3D11Buffer"/>
    /// Along with the element count
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="bufferView"></param>
    /// <param name="itemCount"></param>
    internal partial class UavBuffer( ref ID3D11Buffer? buffer, ref ID3D11UnorderedAccessView? bufferView, uint itemCount ) : IDisposable
    {
        private bool Disposed { get; set; } = false;
        private ID3D11Buffer? Buffer { get; init; } = buffer;
        private ID3D11UnorderedAccessView? BufferView { get; init; } = bufferView;
        private uint BufferItemCount { get; set; } = itemCount;

        internal uint GetBufferItemCount() => BufferItemCount;

        internal ID3D11UnorderedAccessView? GetBufferView() => BufferView;
        internal ID3D11Buffer? GetBuffer() => Buffer;

        ~UavBuffer()
        {
            Dispose( false );
        }

        public void Dispose()
        {
            Dispose( true );
            GC.SuppressFinalize( this );
        }

        protected virtual void Dispose( bool disposing )
        {
            if ( disposing &&
                !Disposed )
            {
                Buffer?.Dispose();
                BufferView?.Dispose();
                BufferItemCount = 0;
            }
            Disposed = true;
        }
    }


    /// <summary>
    /// This class takes data in to create the c# version of the shader structs
    /// It then serializes the structs into memory, and creates the <see cref="ID3D11Buffer"/> and
    /// <see cref="ID3D11UnorderedAccessView"/> as <see cref="UavBuffer"/>.
    /// It manages the buffers and views, and disposes of them.
    /// </summary>
    internal partial class UavBufferManager : IDisposable
    {
        private bool Disposed { get; set; } = false;
        private Dictionary<BufferType, UavBuffer> UAVBuffers { get; init; }

        private readonly ID3D11Device? D3D11Device;
        private readonly ColorToleranceManager? ColorManager;

        private int ColorRangesCount { get; set; }

        // Just to stop casting inside other things
        public int NumOfColorRangesSint() => ColorRangesCount;
        public uint NumOfColorRangesUint() => ( ( uint ) ColorRangesCount );


        /// <summary>
        /// Initializes a new instance of the UAVBufferManager class.
        /// </summary>
        /// <param name="colorManager">Color tolerance manager</param>
        /// <param name="d3d11Device">D3d11 device</param>
        /// <param name="xThreadsSize">Number of threads that shader will use for thread groups x dimension</param>
        internal UavBufferManager( ref ColorToleranceManager? colorManager, ref ID3D11Device? d3d11Device, int xThreadsSize )
        {
            // Set the color manager and d3d11 device
            try
            {
                ColorManager = colorManager ?? throw new ArgumentNullException( nameof( colorManager ), "UavBufferManager: ColorToleranceManager reference null" );
                D3D11Device = d3d11Device ?? throw new ArgumentNullException( nameof( d3d11Device ), "UavBufferManager: ID3D11Device reference null" );
            } catch ( ArgumentNullException ex )
            {
                ErrorHandler.HandleException( ex );
            }


            // Instantiate buffer dictionary
            UAVBuffers = [];

            // Create the color range buffer and view
            CreateColorRanges( ref colorManager );

            // Create detected players buffer and view
            CreateBuffer( BufferType.DetectedPlayers, bufferItemCount: ShaderUtilities.ShaderConsts.MAX_PLAYER_DETEC_STRUCTS );

            // Create groupDetails buffer and view
            CreateBuffer( BufferType.GroupData, bufferItemCount: ShaderUtilities.ShaderConsts.MAX_SCAN_GROUPS( xThreadsSize ) );

            // Create the scan box data buffer
            var numOfUints = ShaderUtilities.ShaderConsts.MAX_SCAN_BOX_BUFFER_SIZE << 2;
            CreateBuffer( BufferType.ScanBoxData, bufferItemCount: numOfUints );

#if DEBUG_BUFFER
            // Create the group data debug buffer
            CreateBuffer( BufferType.DebugBuffer, bufferItemCount: 2 );
#endif
        }


        ~UavBufferManager()
        {
            Dispose( false );
        }


        /// <summary>
        /// Gets the <see cref="ID3D11UnorderedAccessView"/> for the buffer type.
        /// </summary>
        /// <param name="bufferType"></param>
        /// <returns>UA view</returns>
        internal ID3D11UnorderedAccessView? GetBufferViewSafe( BufferType bufferType )
        {
            if ( UAVBuffers.TryGetValue( bufferType, out var buffer ) && buffer is not null )
            {
                return buffer.GetBufferView()!;
            }

#if DEBUG
            Logger.Log( $"Failed to find buffer view: {bufferType}" );
#endif
            // This will close the application
            ErrorHandler.HandleException( new Exception( $"Failed to find buffer view: {bufferType}" ) );
            return null;
        }

        /// <summary>
        /// Gets all the <see cref="ID3D11UnorderedAccessView"/> and the matching <see cref="BufferType"/>
        /// as a <see cref="KeyValuePair{BufferType, ID3D11UnorderedAccessView}"/>
        /// </summary>
        /// <returns>All uav buffer view via yield</returns>
        internal IEnumerable<KeyValuePair<BufferType, ID3D11UnorderedAccessView?>> GetAllUavBufferViewsSafe()
        {
            foreach ( var bufferType in Enum.GetValues<BufferType>() )
            {
                if ( GetBufferViewSafe( bufferType ) is ID3D11UnorderedAccessView view )
                {
                    yield return new( bufferType, view );
                }
            }
        }

        /// <summary>
        /// Gets either the <see cref="UavBuffer"/> or the <see cref="ID3D11Buffer"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="bufferType"></param>
        /// <returns>Chosen buffer type</returns>
        internal T? GetUavBufferSafe<T>( BufferType bufferType ) where T : class
        {
            if ( UAVBuffers.TryGetValue( bufferType, out var buffer ) && buffer is not null )
            {
                if ( typeof( T ) == typeof( UavBuffer ) )
                {
                    return buffer as T;
                } else if ( typeof( T ) == typeof( ID3D11Buffer ) )
                {
                    return buffer.GetBufferView() as T;
                }
            }
#if DEBUG
            Logger.Log( $"Failed to find buffer: {bufferType}" );
#endif
            ErrorHandler.HandleException( new Exception( $"Failed to find buffer: {bufferType}" ) );
            return null;
        }


        /// <summary>
        /// Gets either the <see cref="UavBuffer"/> or the 
        /// <see cref="ID3D11Buffer"/> for all the buffers in the UAV buffer manager
        /// returns a <see cref="KeyValuePair{BufferType, T}"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>Key Value pair of the the buffer type and chosen buffer</returns>
        internal IEnumerable<KeyValuePair<BufferType, T?>> GetAllUavBuffersSafe<T>() where T : class
        {
            foreach ( var bufferType in Enum.GetValues<BufferType>() )
            {
                yield return new( bufferType, GetUavBufferSafe<T>( bufferType ) as T );
            }
        }



        /// <summary>
        /// Serializes hlsl structs to memory,
        /// see <see cref="ShaderUtilities"/> for the structs.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="numOfStructs"></param>
        /// <param name="structFactory"></param>
        /// <param name="allocHeader"></param>
        /// <param name="allocSize"></param>
        /// <param name="strideSize"></param>
        private static unsafe void SerializeStructure<T>( int numOfStructs, Func<int, T> structFactory, out IntPtr allocHeader, out int allocSize, out int strideSize ) where T : struct
        {
            strideSize = Marshal.SizeOf<T>();
            allocSize = strideSize * numOfStructs;
            IntPtr structPtr;
            allocHeader = structPtr = Marshal.AllocHGlobal( allocSize );

            if ( allocHeader != IntPtr.Zero )
            {
                // Add structures to the memory
                for ( int i = 0; i < numOfStructs; ++i, structPtr += strideSize )
                {
                    // Create a struct to be serialized
                    if ( structFactory( i ) is T temp )
                    {
                        // Serialize the struct to the memory
                        Marshal.StructureToPtr( temp, structPtr, false );

                        // Check the safety check value is correct
                        if ( Unsafe.Read<uint>( ( ( nint ) ( ( structPtr + strideSize ) - Marshal.SizeOf<uint>() ).ToInt64() ).ToPointer() ) != uint.MaxValue )
                        {
                            Marshal.FreeHGlobal( allocHeader );
                            ErrorHandler.HandleException( new Exception( $"Failed to serialize: {typeof( T ).Name} " ) );
                        }
                    } else
                    {
                        Marshal.FreeHGlobal( allocHeader );
                        ErrorHandler.HandleException( new Exception( $"Failed to create struct: {typeof( T ).Name}" ) );
                    }
                }
            } else
            {
                ErrorHandler.HandleException( new Exception( $"Failed to allocate memory for {typeof( T ).Name}'s buffer" ) );
            }
        }


        /// <summary>
        /// Creates the UA buffer and UA view, adds them to the dictionary
        /// Because we have values in the structs we need to serialize them to memory.
        /// All structs are in <see cref="ShaderUtilities"/>
        /// They have been made to be 16 byte aligned, and follow the HLSL packing rules.
        /// Ive taken the time to make sure they are all 128 byte cache line aligned,
        /// Most modern gpus have 128 byte cache lines, so this should help with performance.
        /// </summary>
        /// <param name="bufferType"></param>
        /// <param name="colorRanges"></param>
        private unsafe void CreateBuffer( BufferType bufferType, [Optional] List<Tuple<string, List<ColorRange>, Color>>? colorRanges, [Optional] int bufferItemCount )
        {
            int szAlloc = 0;
            int szStride = 0;
            IntPtr serializedHeader = IntPtr.Zero; //< Fucking dumb ass compiler again, cant use variables that arent instantiated
            void* zeroedAllocHeader = null; //< For allocs we dont need to worry about alignment(AKA only for RWByteAddress buffers essentially)

            switch ( bufferType )
            {
                case BufferType.ColorRanges:
                {
                    ColorRanges factory( int i )
                    {
                        return new ColorRanges( ( uint ) colorRanges![ i ].Item2.Count,
                            [ .. colorRanges[ i ].Item2 ], new Float4( colorRanges[ i ].Item3.R, colorRanges[ i ].Item3.G, colorRanges[ i ].Item3.B, colorRanges[ i ].Item3.A ),
                            colorRanges[ i ].Item1 );
                    }
                    SerializeStructure( colorRanges!.Count, factory, out serializedHeader, out szAlloc, out szStride );
                }
                break;
                case BufferType.DetectedPlayers:
                {
                    DetectedPlayers factory( int i )
                    {
                        return new DetectedPlayers();
                    }
                    SerializeStructure( bufferItemCount, factory, out serializedHeader, out szAlloc, out szStride );
                }
                break;
                case BufferType.GroupData:
                {

                    GroupData factory( int i )
                    {
                        return new GroupData();
                    }
                    SerializeStructure( bufferItemCount, factory, out serializedHeader, out szAlloc, out szStride );
                }
                break;
                case BufferType.ScanBoxData:
                {
                    szAlloc = bufferItemCount;
                    szStride = sizeof( uint );
                    zeroedAllocHeader = NativeMemory.AllocZeroed( ( ( nuint ) szAlloc ) );
                    if ( zeroedAllocHeader is null )
                    {
                        ErrorHandler.HandleException( new OutOfMemoryException( "Failed to allocate memory for scan box data" ) );
                    }
                }
                break;
#if DEBUG_BUFFER
                case BufferType.DebugBuffer:
                {
                    DebugBuffer factory( int i )
                    {
                        return new DebugBuffer();
                    }
                    SerializeStructure( bufferItemCount, factory, out serializedHeader, out szAlloc, out szStride );
                }
                break;
#endif
                default:
                ErrorHandler.HandleException( new Exception( "Buffer type to serialize not detected" ) );
                return;
            }

            // Check if the allocation header is null
            if ( bufferType != BufferType.ScanBoxData && serializedHeader == IntPtr.Zero )
            {
                ErrorHandler.HandleException( new ArgumentException( $"Failed to serialize: {bufferType}." ) );
            }


            // Create the buffer description
            BufferDescription bufferDesc = new()
            {
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.UnorderedAccess,
                CPUAccessFlags = bufferType == BufferType.DetectedPlayers ? CpuAccessFlags.Read : CpuAccessFlags.None,
                MiscFlags = bufferType != BufferType.ScanBoxData ? ResourceOptionFlags.BufferStructured : ResourceOptionFlags.BufferAllowRawViews,
                StructureByteStride = ( uint ) szStride,
                ByteWidth = ( uint ) szAlloc,
            };

            // Set the subresource data
            SubresourceData subRData = new( bufferType == BufferType.ScanBoxData ? zeroedAllocHeader : serializedHeader.ToPointer() );

            // Create the buffer
            if ( D3D11Device!.CreateBuffer( bufferDesc, subRData ) is ID3D11Buffer buffer && buffer is not null )
            {
                // Free the memory
                if ( bufferType == BufferType.ColorRanges )
                {
                    Marshal.FreeHGlobal( serializedHeader );

                } else
                {
                    NativeMemory.Free( zeroedAllocHeader );
                }

                var description = new UnorderedAccessViewDescription
                {
                    Format = bufferType == BufferType.ScanBoxData ? Format.R32_Typeless : Format.Unknown,
                    ViewDimension = UnorderedAccessViewDimension.Buffer,
                    Buffer = new BufferUnorderedAccessView
                    {
                        FirstElement = 0,
                        NumElements = bufferType == BufferType.ColorRanges ? ( ( uint ) colorRanges?.Count! ) : bufferType == BufferType.ScanBoxData ? ( ( uint ) bufferItemCount >> 2 ) : ( ( uint ) bufferItemCount ), //< Dont give a shit about nested ternary's
                        Flags = bufferType == BufferType.ScanBoxData ? BufferUnorderedAccessViewFlags.Raw : BufferUnorderedAccessViewFlags.None,
                    }
                };

                // Create uav
                if ( D3D11Device!.CreateUnorderedAccessView( buffer, description ) is ID3D11UnorderedAccessView uav && uav is not null )
                {
                    UAVBuffers.Add( bufferType, new UavBuffer( ref buffer!, ref uav!, description.Buffer.NumElements ) );
#if DEBUG
                    // Set the debug name
                    buffer!.DebugName = $"Ice{bufferType}Buffer";
                    uav.DebugName = $"Ice{bufferType}Uav";
#endif
                } else
                {
                    ErrorHandler.HandleException( new Exception( $"Failed to create unordered access view: {bufferType}" ) );
                }

            } else
            {
                ErrorHandler.HandleException( new Exception( $"Failed to create buffer: {bufferType}" ) );
            }


        }


        /// <summary>
        /// Setup for character feature colors, outfit colors.
        /// </summary>
        /// <param name="colorManager"> Reference to color manager class</param>
        private void CreateColorRanges( ref ColorToleranceManager colorManager )
        {

            // Get the color ranges from the color manager
            var characterFeatureColors = colorManager.CharacterFeatures;

            // Initialize our ranges.
            List<Tuple<string, List<ColorRange>, Color>> colorRanges = [];

            // Parse the character feature colors, outfit colors, and outlines
            ParseTolerances( ref characterFeatureColors, ref colorRanges );
            OutlineColor( ref colorRanges );

            // Create the buffer and view
            CreateBuffer( BufferType.ColorRanges, colorRanges );

            // Set the color ranges count
            ColorRangesCount = colorRanges!.Count;
        }



        /// <summary>
        /// Parses the color tolerances from the color manager.
        /// </summary>
        /// <param name="tolerances">Color tolerances from color manager</param>
        /// <param name="colorRanges">Color ranges the reference to the color ranges list for creation</param>
        private static void ParseTolerances( ref List<ToleranceBase>? tolerances, ref List<Tuple<string, List<ColorRange>, Color>> colorRanges )
        {
            if ( tolerances is not null )
            {
                foreach ( var tolerance in tolerances )
                {
                    var name = tolerance.GetSelected();
                    var ranges = new List<ColorRange>();
                    var swap = tolerance.SwapColor;

                    foreach ( var tBase in tolerance.Tolerances )
                    {

                        foreach ( var range in tBase.Value )
                        {
                            ranges.Add
                            (
                                new ColorRange
                                (
                                    new Range( ( uint ) range.Red!.Minimum, ( uint ) range.Red.Maximum ),
                                    new Range( ( uint ) range.Green!.Minimum, ( uint ) range.Green.Maximum ),
                                    new Range( ( uint ) range.Blue!.Minimum, ( uint ) range.Blue.Maximum )
                                )
                            );
                        }
                    }
                    colorRanges.Add( new Tuple<string, List<ColorRange>, Color>( name, ranges, swap ) );
                }
            }
        }


        /// <summary>
        /// Setup for the outline color.
        /// </summary>
        private void OutlineColor( ref List<Tuple<string, List<ColorRange>, Color>> colorRanges )
        {
            var selected = ColorManager!.CharacterOutlines.GetSelected();
            var outline = ColorManager.CharacterOutlines.GetColorTolerance( selected );
            var swap = ColorManager.CharacterOutlines.SwapColor;

            // Add the current outline color to the ranges
            colorRanges.Add( new Tuple<string, List<ColorRange>, Color>( selected,
            [
                new
                (
                    new Range( ( uint ) outline.Red!.Minimum, ( uint ) outline.Red.Maximum ),
                    new Range( ( uint ) outline.Green!.Minimum, ( uint ) outline.Green.Maximum ),
                    new Range( ( uint ) outline.Blue!.Minimum, ( uint ) outline.Blue.Maximum )
                )
            ], swap ) );
        }

        public void Dispose()
        {
            Dispose( true );
            GC.SuppressFinalize( this );
        }

        protected virtual void Dispose( bool disposing )
        {
            if ( disposing &&
                !Disposed )
            {
                foreach ( var buffer in UAVBuffers )
                {
                    buffer.Value?.Dispose();
                }
                UAVBuffers.Clear();
                ColorRangesCount = 0;
            }
            Disposed = true;
        }

        internal enum BufferType
        {
            ColorRanges = 0xC01025,
            GroupData = 0xC15732,
            ScanBoxData = 0x5C4B0A,
            DetectedPlayers = 0x9E091E,
#if DEBUG_BUFFER
            DebugBuffer = 0x0D0D0D,
#endif
        }
    }
}
