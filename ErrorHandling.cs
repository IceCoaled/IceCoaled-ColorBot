using System.Diagnostics.CodeAnalysis;
using MaterialSkin.Controls;

namespace SCB
{
    internal static class ErrorHandler
    {
        private static Panel? statusPanel;
        private static MaterialLabel? statusLabel;

        /// <summary>
        /// Initializes the error handler with the status bar controls.
        /// </summary>
        /// <param name="panel">The status bar panel.</param>
        /// <param name="label">The status bar label.</param>
        internal static void Initialize( Panel panel, MaterialLabel label )
        {
            statusPanel = panel;
            statusLabel = label;
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
                return default( T );
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
                statusLabel.ForeColor = Color.LightGray; // Reset to default color
                statusPanel.Visible = true;
            }
        }


        /// <summary>
        /// Prints a closing message to the status bar and closes the application.
        /// </summary>
        private static void PrintClose()
        {
            if ( statusLabel != null && statusPanel != null )
            {
                for ( int i = 0; i < 5; i++ )
                {
                    statusPanel.Visible = false;
                    Thread.Sleep( 500 );
                    statusPanel.Visible = true;
                    Thread.Sleep( 500 );

                    statusLabel.Text = $"Closing... in {i}";
                }
            }

            Application.Exit();
        }


        private static void WriteToLog( string message )
        {
            string dateAndTime = DateTime.Now.ToString( "yyyy-MM-dd HH:mm:ss" );
            string logMessage = $"{dateAndTime} \n - {message}";
            File.AppendAllText( Utils.FilesAndFolders.exceptionLogFile, "\n" );
            File.AppendAllText( Utils.FilesAndFolders.exceptionLogFile, logMessage );
        }
    }
}
