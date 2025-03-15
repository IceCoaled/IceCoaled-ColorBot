using System.Drawing.Drawing2D;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SCB
{
    // Delegate for the settings loaded event.
    // This delegate is used to subscribe to the event in the main form.
    // We put it here just in SCB namespace, so it can be accessed by other classes in the whole project essentially.
    public delegate void SettingsLoadedHandler( Dictionary<string, object?>? settings );

    public partial class Configurations : MaterialSkin.Controls.MaterialForm
    {
        private readonly PlayerConfigs playerConfigs;

        public Configurations( SettingsLoadedHandler? UpdateSettingUiDelegate )
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.Sizable = false;

            // Initialize MaterialSkinManager
            var materialSkinManager = MaterialSkin.MaterialSkinManager.Instance;
            materialSkinManager.AddFormToManage( this );
            materialSkinManager.Theme = MaterialSkin.MaterialSkinManager.Themes.DARK;  // Change to DARK or LIGHT

            // Customize the color scheme (primary, accent, text shade)
            materialSkinManager.ColorScheme = new MaterialSkin.ColorScheme(
                MaterialSkin.Primary.Purple500, MaterialSkin.Primary.Purple600,
                MaterialSkin.Primary.Yellow400, MaterialSkin.Accent.Pink400,
                MaterialSkin.TextShade.WHITE );

            InitializeComponent();

            // Create a new instance of the PlayerConfigs class
            playerConfigs = new( UpdateSettingUiDelegate );

        }

        private void materialButton1_Click( object sender, EventArgs e )
        {
            playerConfigs.SaveConfig( 1 );
        }

        private void materialButton4_Click( object sender, EventArgs e )
        {
            playerConfigs.SaveConfig( 2 );
        }

        private void materialButton6_Click( object sender, EventArgs e )
        {
            playerConfigs.SaveConfig( 3 );
        }

        private void materialButton2_Click( object sender, EventArgs e )
        {
            playerConfigs.LoadConfig( 1 );
        }

        private void materialButton3_Click( object sender, EventArgs e )
        {
            playerConfigs.LoadConfig( 2 );
        }

        private void materialButton5_Click( object sender, EventArgs e )
        {
            playerConfigs.LoadConfig( 3 );
        }

        protected override void OnPaint( PaintEventArgs e )
        {
            base.OnPaint( e );
            // Create a GraphicsPath to define rounded corners
            GraphicsPath path = new();
            Rectangle windowRect = this.ClientRectangle;
            int radius = 15;

            // Add the rounded rectangle to the path
            path.AddArc( windowRect.X, windowRect.Y, radius, radius, 180, 90 );
            path.AddArc( windowRect.Width - radius, windowRect.Y, radius, radius, 270, 90 );
            path.AddArc( windowRect.Width - radius, windowRect.Height - radius, radius, radius, 0, 90 );
            path.AddArc( windowRect.X, windowRect.Height - radius, radius, radius, 90, 90 );
            path.CloseAllFigures();

            // Set the form's region to the rounded rectangle
            this.Region = new Region( path );
        }


        /// <summary>
        /// Class for saving and loading player config files.
        /// </summary>
        internal class PlayerConfigs( SettingsLoadedHandler? UpdateSettingUiDelegate )
        {
            static readonly string pvokeRect = "PInvoke.RECT";

            private readonly SettingsLoadedHandler? OnSettingsLoaded = UpdateSettingUiDelegate;

            private readonly JsonSerializerOptions options = new()
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNameCaseInsensitive = true,
                Converters = { new BezierPointCollectionConverter() }
            };

            ~PlayerConfigs() { }

            /// <summary>
            /// Saves the current config to a .json file.
            /// </summary>
            internal void SaveConfig( int configNum )
            {
                // Create blob of player data           
                Type? playerDataBlob = typeof( PlayerData );

                // Get the Name of any eventsin the player data blob.
                // This is necessary because the JsonSerializer will throw an exception if it encounters an event.
                // So we get the names then exclude them from the fields we serialize.

                // Get all the fields in the player data blob
                Dictionary<string, object?>? blobFields = null;
                try
                {
                    blobFields = playerDataBlob
                     .GetFields( BindingFlags.Static | BindingFlags.NonPublic )
                     .Where( x => x.FieldType != typeof( IntPtr ) &&
                     x.FieldType != typeof( object ) &&
                     x.FieldType != typeof( EventHandler ) &&
                     !typeof( MulticastDelegate ).IsAssignableFrom( x.FieldType ) &&
                     x.FieldType.FullName != pvokeRect &&
                     !x.IsSpecialName &&
                     !x.IsPublic ) // Filter out unsupported types
                     .ToDictionary( x => x.Name, x => x.GetValue( null ) );

                } catch ( Exception ex )
                {
                    ErrorHandler.HandleExceptionNonExit( ex );
                    return;
                }

                // Serialize the blob fields to a json string
                var jsonString = JsonSerializer.Serialize( blobFields, options );

                if ( jsonString is null )
                {
                    ErrorHandler.HandleExceptionNonExit( new InvalidOperationException( "Failed to serialize settings." ) );
                    return;
                }

                // Write the json string to a config file
                File.WriteAllText( FileManager.configFolder + $"config{configNum}.json", jsonString );
            }



            /// <summary>
            /// Loads a config file and applies the settings to the player.
            /// </summary>
            internal void LoadConfig( int configNum )
            {
                // Read the json string from the file
                string? jsonString = File.ReadAllText( FileManager.configFolder + $"config{configNum}.json" );

                if ( jsonString is null )
                {
                    ErrorHandler.HandleExceptionNonExit( new InvalidOperationException( "Failed to read config file." ) );
                    return;
                }

                // Deserialize the json string to a settings object           
                Dictionary<string, object?>? settings = null;

                try
                {
                    settings = JsonSerializer.Deserialize<Dictionary<string, object?>?>( jsonString, options );
                } catch ( Exception ex )
                {
                    ErrorHandler.HandleExceptionNonExit( ex );
                    return;
                }

                // Get the player data blob
                Type? playerDataBlob = typeof( PlayerData );

                //saving the color tolerance name to set after all settings are loaded
                string colorToleranceName = "Default";


                // Loop through the settings and set the fields in the player data blob
                foreach ( var setting in settings! )
                {
                    FieldInfo? field = playerDataBlob.GetField( setting.Key, BindingFlags.Static | BindingFlags.NonPublic );

                    if ( field is null )
                    {
                        ErrorHandler.HandleExceptionNonExit( new InvalidOperationException( "Failed to get field." ) );
                        continue;
                    }

                    if ( setting.Value is null )
                    {
                        continue;
                    }

                    if ( setting.Key == "localBezierCollection" && setting.Value is JsonElement bezierCollectionJson )
                    {
                        Utils.BezierPointCollection? bezierCollection = JsonSerializer.Deserialize<Utils.BezierPointCollection>( bezierCollectionJson.GetRawText(), options );

                        if ( PlayerData.BezierControlPointsSet() )
                        {
                            PlayerData.SetBezierPoints( bezierCollection! );
                        }

                        continue;
                    }

                    // Other field handling logic for different field types
                    if ( setting.Value is JsonElement jsonElement )
                    {
                        object? nonJsonVar = null;

                        if ( field.FieldType == typeof( double ) )
                        {
                            nonJsonVar = jsonElement.GetDouble();
                        } else if ( field.FieldType == typeof( int ) )
                        {
                            nonJsonVar = jsonElement.GetInt32();
                        } else if ( field.FieldType == typeof( bool ) )
                        {
                            nonJsonVar = jsonElement.GetBoolean();
                        } else if ( field.FieldType == typeof( string ) )
                        {
                            nonJsonVar = jsonElement.GetString();

                            if ( setting.Key == "localOutlineColor" && nonJsonVar is string toleranceName )
                            {
                                colorToleranceName = toleranceName;
                            }
                        }

                        field.SetValue( null, nonJsonVar );
                    } else
                    {
                        field.SetValue( null, setting.Value );
                    }
                }

                // Set the color tolerance
                PlayerData.SetOutlineColor( colorToleranceName );


                // Just for debugging purposes
#if DEBUG
                var aimSettings = PlayerData.GetAimSettings();
                var aimFov = PlayerData.GetFov();

                //print all player data fields
                Logger.Log( "Player Data Fields:" );
                Logger.Log( "-------------------" );
                Logger.Log( "Aim Speed: " + aimSettings.aimSpeed );
                Logger.Log( "Aim Smoothing: " + aimSettings.aimSmoothing );
                Logger.Log( "Deadzone: " + aimSettings.deadZone );
                Logger.Log( "Aim Key: " + aimSettings.aimKey );
                if ( aimSettings.prediction )
                {
                    Logger.Log( "Prediction: Enabled" );
                } else
                {
                    Logger.Log( "Prediction: Disabled" );
                }

                if ( aimSettings.antiRecoil )
                {
                    Logger.Log( "Anti Recoil: Enabled" );
                } else
                {
                    Logger.Log( "Anti Recoil: Disabled" );
                }

                Logger.Log( "Aim Fov: " + aimFov );

                Logger.Log( "-------------------" );
                Logger.Log( "Color Tolerance: " + PlayerData.GetOutlineColor() );

                var debugTolerance = PlayerData.GetOutlineColor();

                Logger.Log( $"Selected Outline Color: {debugTolerance}" );

                Logger.Log( "-------------------" );

                if ( PlayerData.BezierControlPointsSet() )
                {
                    Logger.Log( "Bezier Control Points Set" );

                    Utils.BezierPointCollection debugTest = PlayerData.GetBezierPoints();

                    foreach ( var point in debugTest.ControlPoints )
                    {
                        Logger.Log( $"Control Point: {point.X}, {point.Y}" );
                    }

                    Logger.Log( $"Start Point: {debugTest.Start.X}, {debugTest.Start.Y}" );
                    Logger.Log( $"End Point: {debugTest.End.X}, {debugTest.End.Y}" );
                } else
                {
                    Logger.Log( "Bezier Control Points Not Set" );
                }

#endif

                // Invoke the settings loaded event, to set the main form controls
                OnSettingsLoaded?.Invoke( settings );
            }


            /// <summary>
            /// Custom JSON converter for serializing and deserializing BezierPointCollection objects.
            /// </summary>
            public class BezierPointCollectionConverter : JsonConverter<Utils.BezierPointCollection>
            {
                /// <summary>
                /// Reads and converts the JSON to a BezierPointCollection object.
                /// </summary>
                /// <param name="reader">The Utf8JsonReader to read the JSON from.</param>
                /// <param name="typeToConvert">The type of object being converted.</param>
                /// <param name="options">Options for customizing JSON serialization.</param>
                /// <returns>A BezierPointCollection object.</returns>
                public override Utils.BezierPointCollection Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
                {
                    // Deserialize JSON into a list of PointF objects representing the bezier points.
                    List<PointF>? points = JsonSerializer.Deserialize<List<PointF>>( ref reader, options );

                    // Error handling if points are null or insufficient to form a Bezier curve.
                    if ( points is null || points.Count < 2 )
                    {
                        ErrorHandler.HandleException( new JsonException( "Invalid BezierPointCollection data." ) );
                    }

                    // The first and last points represent the start and end points of the curve.
                    PointF start = points.First();
                    PointF end = points.Last();

                    // All intermediate points represent control points for the Bezier curve.
                    var controlPoints = points.Skip( 1 ).Take( points.Count - 2 ).ToList();

                    return new Utils.BezierPointCollection( ref start, ref end, ref controlPoints );
                }

                /// <summary>
                /// Writes the BezierPointCollection object to JSON format.
                /// </summary>
                /// <param name="writer">The Utf8JsonWriter to write the JSON to.</param>
                /// <param name="value">The BezierPointCollection object to serialize.</param>
                /// <param name="options">Options for customizing JSON serialization.</param>
                public override void Write( Utf8JsonWriter writer, Utils.BezierPointCollection value, JsonSerializerOptions options )
                {
                    // Combine start, control, and end points into a single list for serialization.
                    List<PointF>? allPoints = new()
                { value.Start };
                    allPoints.AddRange( value.ControlPoints );
                    allPoints.Add( value.End );

                    // Serialize the points list as JSON.
                    JsonSerializer.Serialize( writer, allPoints, options );
                }
            }
        }
    }
}
