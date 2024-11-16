using System.Diagnostics.CodeAnalysis;

namespace SCB
{
    internal static class ErrorHandler
    {
        private static Panel? statusPanel;
        private static Label? statusLabel;
        private static Control? uiControl;

        /// <summary>
        /// Initializes the error handler with the status bar controls.
        /// </summary>
        /// <param name="panel">The status bar panel.</param>
        /// <param name="label">The status bar label.</param>
        internal static void Initialize( Panel panel, Label label, Control UiControl )
        {
            statusPanel = panel;
            statusLabel = label;
            uiControl = UiControl;
        }

        /// <summary>
        /// Handles an exception, logs it, and updates the status bar, then closes the application.
        /// </summary>
        /// <param name="ex">The exception to handle.</param>
        [DoesNotReturn]
        internal static void HandleException( Exception ex )
        {
            // Log the exception (replace with your actual logging mechanism)
#if DEBUG
            Logger.Log( $"Error: {ex.Message}" );
#endif

            // write to exception log
            WriteToLog( $"Error: {ex.Message}" );

            // Update the status bar if it's available
            if ( statusLabel != null && statusPanel != null )
            {
                statusLabel.Text = $"Error: {ex.Message}";
                statusLabel.ForeColor = Color.Red; // Set color to indicate an error               
                statusPanel.Visible = true;
                uiControl!.Invalidate();
            }

            //allow the user to see the error message
            Thread.Sleep( 5000 );

            //gracefully exit the application
            PrintClose();
        }


        /// <summary>
        /// Handles an exception, logs it, and updates the status bar.
        /// </summary>
        /// <param name="ex"></param>
        internal static void HandleExceptionNonExit( Exception ex )
        {
            // Log the exception (replace with your actual logging mechanism)
#if DEBUG
            Logger.Log( $"Error: {ex.Message}" );
#endif
            // write to exception log
            WriteToLog( $"Error: {ex.Message}" );


            // Update the status bar if it's available
            if ( statusLabel != null && statusPanel != null )
            {
                statusLabel.Text = $"Error: {ex.Message}";
                statusLabel.ForeColor = Color.Red; // Set color to indicate an error
                statusPanel.Visible = true;
                uiControl!.Invalidate();
            }
        }


        /// <summary>
        /// Checks if the object was created successfully, and if not, logs an error and closes the application.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <param name="varName"></param>
        /// <returns></returns>
        internal static T? HandleObjCreation<T>( T? obj, string varName )
        {
            if ( object.Equals( obj, default( T ) ) )
            {
                ErrorHandler.HandleException( new Exception( $"Failed To Create Object: {typeof( T ).Name}, With Name: {varName}" ) );
                return default;
            }
            return obj;
        }



        /// <summary>
        /// Clears the status bar message.
        /// </summary>
        internal static void ClearStatusBar()
        {
            if ( statusLabel != null && statusPanel != null )
            {
                statusLabel.Text = "Ready";
                statusLabel.ForeColor = Color.LightGreen; // Reset to default color
                statusPanel.Visible = true;
                uiControl!.Invalidate();
            }
        }


        /// <summary>
        /// Prints a closing message to the status bar and closes the application.
        /// </summary>
        [DoesNotReturn]
        private static void PrintClose()
        {
            if ( statusLabel != null && statusPanel != null )
            {
                statusLabel.ForeColor = Color.Crimson;
                statusLabel.Text = "Closing...";
                uiControl!.Invalidate();
                for ( int i = 0; i < 5; i++ )
                {

                    statusPanel.Visible = true;
                    uiControl!.Invalidate();
                    Thread.Sleep( 500 );
                    statusPanel.Visible = false;
                    Thread.Sleep( 500 );

                    statusLabel.Text = $"Closing... in {i}";
                    uiControl!.Invalidate();
                }
            }

            // Call the exit method
            Application.Exit();

            // Freeze this thread as calling Application.Exit() will not close the application immediately
            Thread.Sleep( 10000 );

            // Call Environment.Exit() to force close the application
            // We can do this because the calling Application.Exit() goes to my override of formClosing properly cleaning everything up
            Environment.Exit( 0 );
        }



        private static void WriteToLog( string message )
        {
            string dateAndTime = DateTime.Now.ToString( "yyyy-MM-dd HH:mm:ss" );
            string logMessage = $"{dateAndTime} \n - {message}";
            File.AppendAllText( FileManager.exceptionLogFile, "\n" );
            File.AppendAllText( FileManager.exceptionLogFile, logMessage );
        }
    }
}
