using System.Diagnostics.CodeAnalysis;

namespace SCB
{
    internal static class ErrorHandler
    {
        // Event for status updates
        public static event Action<string, Color>? OnStatusUpdate;

        internal static void PrintToStatusBar( string msg )
        {
            OnStatusUpdate?.Invoke( $"Current Status : {msg}", Color.DarkOrange );
        }

        /// <summary>
        /// Handles an exception, logs it, and notifies the UI about the error, then closes the application.
        /// </summary>
        /// <param name="ex">The exception to handle.</param>
        [DoesNotReturn]
        internal static void HandleException( Exception ex )
        {
#if DEBUG
            Logger.Log( $"Error: {ex.Message}" );
#endif
            WriteToLog( $"Error: {ex.Message}" );

            // Notify listeners about the error
            OnStatusUpdate?.Invoke( $"Error: {ex.Message}", Color.Red );

            // Allow the user to see the error message
            Thread.Sleep( 5000 );

            // Gracefully exit the application
            // We are using a task for this to run it asynchronously
            Task.Run( async () => await PrintClose() ).Wait();
        }

        /// <summary>
        /// Handles an exception, logs it, and notifies the UI about the error.
        /// </summary>
        internal static void HandleExceptionNonExit( Exception ex )
        {
#if DEBUG
            Logger.Log( $"Error: {ex.Message}" );
#endif
            WriteToLog( $"Error: {ex.Message}" );

            // Notify listeners about the error
            OnStatusUpdate?.Invoke( $"Error: {ex.Message ?? "Message Null"}", Color.Red );
        }

        /// <summary>
        /// Clears the status bar message.
        /// </summary>
        internal static void ClearStatusBar()
        {
            // Notify listeners to clear the status bar
            OnStatusUpdate?.Invoke( "Ready", Color.LightGreen );
        }

        /// <summary>
        /// Prints a closing message and closes the application.
        /// </summary>
        [DoesNotReturn]
        private static async Task PrintClose()
        {
            for ( int i = 5; i > 0; i-- )
            {
                OnStatusUpdate?.Invoke( $"Closing... in {i}", Color.Crimson );
                Thread.Sleep( 1000 );
            }

            // Call the exit method
            Application.Exit( new()
            {
                Cancel = false
            } );

            // Freeze this thread as calling Application.Exit() will not close the application immediately
            await Task.Delay( 10000 );

            // Force close the application
            Environment.Exit( -1 );
        }

        /// <summary>
        /// Logs the message to a file.
        /// </summary>
        private static void WriteToLog( string message )
        {
            string dateAndTime = DateTime.Now.ToString( "yyyy-MM-dd HH:mm:ss" );
            string logMessage = $"\n\n{dateAndTime} \n - {message}";
            File.AppendAllTextAsync( FileManager.exceptionLogFile, logMessage );
        }
    }
}
