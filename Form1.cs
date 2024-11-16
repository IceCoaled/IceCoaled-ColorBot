#if DEBUG
//#define GETRECOILPATTERN
#endif

using System.Drawing.Drawing2D;
using System.Text.Json;
using MaterialSkin.Controls;
using Utils;

namespace SCB
{

    internal partial class IceColorBot : MaterialSkin.Controls.MaterialForm
    {
        private Panel statusPanel;
        private Label statusLabel;
        private NotifyIcon trayIcon;
#if GETRECOILPATTERN
        internal RecoilPatternCapture? recoilPatternCapture;
#endif

        private Aimbot aimBot;
        private Bezier bezierForm;
        private Configurations configurationsForm;


        internal IceColorBot( NotifyIcon notifyIcon, ref Aimbot aimbot )
        {
            trayIcon = notifyIcon;
            aimBot = aimbot;

            // Initialize MaterialSkinManager
            var materialSkinManager = MaterialSkin.MaterialSkinManager.Instance;
            materialSkinManager.AddFormToManage( this );
            materialSkinManager.Theme = MaterialSkin.MaterialSkinManager.Themes.DARK;  // Change to DARK or LIGHT

            // Customize the color scheme (primary, accent, text shade)
            materialSkinManager.ColorScheme = new MaterialSkin.ColorScheme(
                MaterialSkin.Primary.Purple500, MaterialSkin.Primary.Purple600,
                MaterialSkin.Primary.Yellow400, MaterialSkin.Accent.Pink400,
                MaterialSkin.TextShade.WHITE );

            //set background image
            this.BackgroundImage = Properties.Resources.ResourceManager.GetObject( "$this.BackgroundImage" ) as Image;
            this.BackgroundImageLayout = ImageLayout.Center;

            // Create the tray icon
            trayIcon = new()
            {
                Text = "IceColorBot",
                Icon = Icon,
                ContextMenuStrip = new ContextMenuStrip()
            };
            trayIcon.ContextMenuStrip.Items.Add( "Exit", null, Exit );
            trayIcon.ContextMenuStrip.Items.Add( "Open", null, ReOpen );
            trayIcon.Visible = false;


            // Set the dark mode
            DarkMode.SetDarkMode( this.Handle );

            // Initialize the form components
            InitializeComponent();
            InitializeStatusBar();

            ErrorHandler.Initialize( statusPanel!, statusLabel!, this );
        }


        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager( typeof( IceColorBot ) );
            this.materialSlider1 = new MaterialSlider();
            this.materialSlider2 = new MaterialSlider();
            this.materialSwitch1 = new MaterialSwitch();
            this.materialComboBox1 = new MaterialComboBox();
            this.materialLabel1 = new MaterialLabel();
            this.materialComboBox2 = new MaterialComboBox();
            this.materialLabel2 = new MaterialLabel();
            this.materialComboBox3 = new MaterialComboBox();
            this.materialLabel3 = new MaterialLabel();
            this.materialButton1 = new MaterialButton();
            this.materialButton2 = new MaterialButton();
            this.materialButton4 = new MaterialButton();
            this.materialSwitch2 = new MaterialSwitch();
            this.materialSlider3 = new MaterialSlider();
            this.materialSlider4 = new MaterialSlider();
            this.materialButton3 = new MaterialButton();
            this.materialButton5 = new MaterialButton();
            this.SuspendLayout();
            // 
            // materialSlider1
            // 
            this.materialSlider1.BackColor = Color.Indigo;
            this.materialSlider1.BackgroundImageLayout = ImageLayout.Stretch;
            this.materialSlider1.Depth = 0;
            this.materialSlider1.Font = new Font( "Roboto", 12F, FontStyle.Regular, GraphicsUnit.Pixel );
            this.materialSlider1.FontType = MaterialSkin.MaterialSkinManager.fontType.Caption;
            this.materialSlider1.ForeColor = Color.Aquamarine;
            this.materialSlider1.Location = new Point( 2, 175 );
            this.materialSlider1.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialSlider1.Name = "materialSlider1";
            this.materialSlider1.RangeMin = 1;
            this.materialSlider1.Size = new Size( 223, 40 );
            this.materialSlider1.TabIndex = 35;
            this.materialSlider1.Text = "Aim Speed";
            this.materialSlider1.UseAccentColor = true;
            this.materialSlider1.ValueMax = 100;
            this.materialSlider1.onValueChanged += this.materialSlider1_onValueChanged;
            // 
            // materialSlider2
            // 
            this.materialSlider2.Depth = 0;
            this.materialSlider2.Font = new Font( "Roboto", 12F, FontStyle.Regular, GraphicsUnit.Pixel );
            this.materialSlider2.FontType = MaterialSkin.MaterialSkinManager.fontType.Caption;
            this.materialSlider2.ForeColor = Color.FromArgb( 222, 0, 0, 0 );
            this.materialSlider2.Location = new Point( 2, 221 );
            this.materialSlider2.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialSlider2.Name = "materialSlider2";
            this.materialSlider2.RangeMin = 1;
            this.materialSlider2.Size = new Size( 223, 40 );
            this.materialSlider2.TabIndex = 36;
            this.materialSlider2.Text = "Aim Smoothing";
            this.materialSlider2.UseAccentColor = true;
            this.materialSlider2.ValueMax = 100;
            this.materialSlider2.onValueChanged += this.materialSlider2_onValueChanged;
            // 
            // materialSwitch1
            // 
            this.materialSwitch1.AutoSize = true;
            this.materialSwitch1.Depth = 0;
            this.materialSwitch1.FlatStyle = FlatStyle.Popup;
            this.materialSwitch1.Location = new Point( 2, 276 );
            this.materialSwitch1.Margin = new Padding( 0 );
            this.materialSwitch1.MouseLocation = new Point( -1, -1 );
            this.materialSwitch1.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialSwitch1.Name = "materialSwitch1";
            this.materialSwitch1.Ripple = true;
            this.materialSwitch1.Size = new Size( 133, 37 );
            this.materialSwitch1.TabIndex = 37;
            this.materialSwitch1.Text = "Anti-Recoil";
            this.materialSwitch1.TextAlign = ContentAlignment.TopCenter;
            this.materialSwitch1.UseVisualStyleBackColor = true;
            this.materialSwitch1.CheckedChanged += this.materialSwitch1_CheckedChanged;
            // 
            // materialComboBox1
            // 
            this.materialComboBox1.AutoResize = false;
            this.materialComboBox1.BackColor = Color.FromArgb( 255, 255, 255 );
            this.materialComboBox1.Depth = 0;
            this.materialComboBox1.DrawMode = DrawMode.OwnerDrawVariable;
            this.materialComboBox1.DropDownHeight = 174;
            this.materialComboBox1.DropDownStyle = ComboBoxStyle.DropDownList;
            this.materialComboBox1.DropDownWidth = 121;
            this.materialComboBox1.Font = new Font( "Microsoft Sans Serif", 14F, FontStyle.Bold, GraphicsUnit.Pixel );
            this.materialComboBox1.ForeColor = Color.FromArgb( 222, 0, 0, 0 );
            this.materialComboBox1.FormattingEnabled = true;
            this.materialComboBox1.Hint = "select color";
            this.materialComboBox1.IntegralHeight = false;
            this.materialComboBox1.ItemHeight = 43;
            this.materialComboBox1.Items.AddRange( new object[] { "orange", "red", "green", "yellow", "purple", "cyan" } );
            this.materialComboBox1.Location = new Point( 383, 203 );
            this.materialComboBox1.MaxDropDownItems = 4;
            this.materialComboBox1.MouseState = MaterialSkin.MouseState.OUT;
            this.materialComboBox1.Name = "materialComboBox1";
            this.materialComboBox1.Size = new Size( 190, 49 );
            this.materialComboBox1.StartIndex = 0;
            this.materialComboBox1.TabIndex = 38;
            this.materialComboBox1.SelectedIndexChanged += this.materialComboBox1_SelectedIndexChanged;
            // 
            // materialLabel1
            // 
            this.materialLabel1.AutoSize = true;
            this.materialLabel1.Depth = 0;
            this.materialLabel1.Font = new Font( "Roboto", 16F, FontStyle.Regular, GraphicsUnit.Pixel );
            this.materialLabel1.FontType = MaterialSkin.MaterialSkinManager.fontType.Subtitle1;
            this.materialLabel1.HighEmphasis = true;
            this.materialLabel1.Location = new Point( 420, 181 );
            this.materialLabel1.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel1.Name = "materialLabel1";
            this.materialLabel1.Size = new Size( 118, 19 );
            this.materialLabel1.TabIndex = 39;
            this.materialLabel1.Text = "Color Selection\r\n";
            this.materialLabel1.UseAccent = true;
            // 
            // materialComboBox2
            // 
            this.materialComboBox2.AutoResize = false;
            this.materialComboBox2.BackColor = Color.FromArgb( 255, 255, 255 );
            this.materialComboBox2.Depth = 0;
            this.materialComboBox2.DrawMode = DrawMode.OwnerDrawVariable;
            this.materialComboBox2.DropDownHeight = 174;
            this.materialComboBox2.DropDownStyle = ComboBoxStyle.DropDownList;
            this.materialComboBox2.DropDownWidth = 121;
            this.materialComboBox2.Font = new Font( "Microsoft Sans Serif", 14F, FontStyle.Bold, GraphicsUnit.Pixel );
            this.materialComboBox2.ForeColor = Color.FromArgb( 222, 0, 0, 0 );
            this.materialComboBox2.FormattingEnabled = true;
            this.materialComboBox2.Hint = "select aim location";
            this.materialComboBox2.IntegralHeight = false;
            this.materialComboBox2.ItemHeight = 43;
            this.materialComboBox2.Items.AddRange( new object[] { "head", "body" } );
            this.materialComboBox2.Location = new Point( 383, 298 );
            this.materialComboBox2.MaxDropDownItems = 4;
            this.materialComboBox2.MouseState = MaterialSkin.MouseState.OUT;
            this.materialComboBox2.Name = "materialComboBox2";
            this.materialComboBox2.Size = new Size( 190, 49 );
            this.materialComboBox2.StartIndex = 0;
            this.materialComboBox2.TabIndex = 40;
            this.materialComboBox2.SelectedIndexChanged += this.materialComboBox2_SelectedIndexChanged;
            // 
            // materialLabel2
            // 
            this.materialLabel2.AutoSize = true;
            this.materialLabel2.Depth = 0;
            this.materialLabel2.Font = new Font( "Roboto", 16F, FontStyle.Regular, GraphicsUnit.Pixel );
            this.materialLabel2.FontType = MaterialSkin.MaterialSkinManager.fontType.Subtitle1;
            this.materialLabel2.HighEmphasis = true;
            this.materialLabel2.Location = new Point( 430, 276 );
            this.materialLabel2.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel2.Name = "materialLabel2";
            this.materialLabel2.Size = new Size( 98, 19 );
            this.materialLabel2.TabIndex = 41;
            this.materialLabel2.Text = "Aim Selection";
            this.materialLabel2.UseAccent = true;
            // 
            // materialComboBox3
            // 
            this.materialComboBox3.AutoResize = false;
            this.materialComboBox3.BackColor = Color.FromArgb( 255, 255, 255 );
            this.materialComboBox3.Depth = 0;
            this.materialComboBox3.DrawMode = DrawMode.OwnerDrawVariable;
            this.materialComboBox3.DropDownHeight = 174;
            this.materialComboBox3.DropDownStyle = ComboBoxStyle.DropDownList;
            this.materialComboBox3.DropDownWidth = 121;
            this.materialComboBox3.Font = new Font( "Microsoft Sans Serif", 14F, FontStyle.Bold, GraphicsUnit.Pixel );
            this.materialComboBox3.ForeColor = Color.FromArgb( 222, 0, 0, 0 );
            this.materialComboBox3.FormattingEnabled = true;
            this.materialComboBox3.Hint = "select aim key";
            this.materialComboBox3.IntegralHeight = false;
            this.materialComboBox3.ItemHeight = 43;
            this.materialComboBox3.Items.AddRange( new object[] { "left mouse button", "right mouse button", "left shift", "left alt", "left control" } );
            this.materialComboBox3.Location = new Point( 383, 115 );
            this.materialComboBox3.MaxDropDownItems = 4;
            this.materialComboBox3.MouseState = MaterialSkin.MouseState.OUT;
            this.materialComboBox3.Name = "materialComboBox3";
            this.materialComboBox3.Size = new Size( 190, 49 );
            this.materialComboBox3.StartIndex = 0;
            this.materialComboBox3.TabIndex = 42;
            this.materialComboBox3.SelectedIndexChanged += this.materialComboBox3_SelectedIndexChanged;
            // 
            // materialLabel3
            // 
            this.materialLabel3.AutoSize = true;
            this.materialLabel3.Depth = 0;
            this.materialLabel3.Font = new Font( "Roboto", 16F, FontStyle.Regular, GraphicsUnit.Pixel );
            this.materialLabel3.FontType = MaterialSkin.MaterialSkinManager.fontType.Subtitle1;
            this.materialLabel3.HighEmphasis = true;
            this.materialLabel3.Location = new Point( 444, 93 );
            this.materialLabel3.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialLabel3.Name = "materialLabel3";
            this.materialLabel3.Size = new Size( 59, 19 );
            this.materialLabel3.TabIndex = 43;
            this.materialLabel3.Text = "Aim Key";
            this.materialLabel3.UseAccent = true;
            // 
            // materialButton1
            // 
            this.materialButton1.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.materialButton1.Density = MaterialButton.MaterialButtonDensity.Default;
            this.materialButton1.Depth = 0;
            this.materialButton1.FlatStyle = FlatStyle.Popup;
            this.materialButton1.HighEmphasis = true;
            this.materialButton1.Icon = null;
            this.materialButton1.Location = new Point( 16, 434 );
            this.materialButton1.Margin = new Padding( 4, 6, 4, 6 );
            this.materialButton1.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialButton1.Name = "materialButton1";
            this.materialButton1.NoAccentTextColor = Color.Empty;
            this.materialButton1.Size = new Size( 125, 36 );
            this.materialButton1.TabIndex = 44;
            this.materialButton1.Text = "Start AimBot";
            this.materialButton1.Type = MaterialButton.MaterialButtonType.Contained;
            this.materialButton1.UseAccentColor = false;
            this.materialButton1.UseVisualStyleBackColor = true;
            this.materialButton1.Click += this.materialButton1_Click;
            // 
            // materialButton2
            // 
            this.materialButton2.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.materialButton2.Density = MaterialButton.MaterialButtonDensity.Default;
            this.materialButton2.Depth = 0;
            this.materialButton2.FlatStyle = FlatStyle.Popup;
            this.materialButton2.HighEmphasis = true;
            this.materialButton2.Icon = null;
            this.materialButton2.Location = new Point( 444, 434 );
            this.materialButton2.Margin = new Padding( 4, 6, 4, 6 );
            this.materialButton2.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialButton2.Name = "materialButton2";
            this.materialButton2.NoAccentTextColor = Color.Empty;
            this.materialButton2.Size = new Size( 117, 36 );
            this.materialButton2.TabIndex = 45;
            this.materialButton2.Text = "Stop AimBot";
            this.materialButton2.Type = MaterialButton.MaterialButtonType.Contained;
            this.materialButton2.UseAccentColor = true;
            this.materialButton2.UseVisualStyleBackColor = true;
            this.materialButton2.Click += this.materialButton2_Click;
            // 
            // materialButton4
            // 
            this.materialButton4.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.materialButton4.Density = MaterialButton.MaterialButtonDensity.Dense;
            this.materialButton4.Depth = 0;
            this.materialButton4.FlatStyle = FlatStyle.Popup;
            this.materialButton4.HighEmphasis = true;
            this.materialButton4.Icon = null;
            this.materialButton4.Location = new Point( 219, 434 );
            this.materialButton4.Margin = new Padding( 4, 6, 4, 6 );
            this.materialButton4.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialButton4.Name = "materialButton4";
            this.materialButton4.NoAccentTextColor = Color.Empty;
            this.materialButton4.Size = new Size( 123, 36 );
            this.materialButton4.TabIndex = 47;
            this.materialButton4.Text = "Run as Admin";
            this.materialButton4.Type = MaterialButton.MaterialButtonType.Outlined;
            this.materialButton4.UseAccentColor = false;
            this.materialButton4.UseVisualStyleBackColor = true;
            this.materialButton4.Click += this.materialButton4_Click;
            // 
            // materialSwitch2
            // 
            this.materialSwitch2.AutoSize = true;
            this.materialSwitch2.Depth = 0;
            this.materialSwitch2.FlatStyle = FlatStyle.Popup;
            this.materialSwitch2.Location = new Point( 2, 324 );
            this.materialSwitch2.Margin = new Padding( 0 );
            this.materialSwitch2.MouseLocation = new Point( -1, -1 );
            this.materialSwitch2.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialSwitch2.Name = "materialSwitch2";
            this.materialSwitch2.Ripple = true;
            this.materialSwitch2.Size = new Size( 129, 37 );
            this.materialSwitch2.TabIndex = 48;
            this.materialSwitch2.Text = "Prediction";
            this.materialSwitch2.TextAlign = ContentAlignment.TopCenter;
            this.materialSwitch2.UseVisualStyleBackColor = true;
            this.materialSwitch2.CheckedChanged += this.materialSwitch2_CheckedChanged;
            // 
            // materialSlider3
            // 
            this.materialSlider3.BackColor = Color.Indigo;
            this.materialSlider3.BackgroundImageLayout = ImageLayout.Stretch;
            this.materialSlider3.Depth = 0;
            this.materialSlider3.Font = new Font( "Roboto", 12F, FontStyle.Regular, GraphicsUnit.Pixel );
            this.materialSlider3.FontType = MaterialSkin.MaterialSkinManager.fontType.Caption;
            this.materialSlider3.ForeColor = Color.Aquamarine;
            this.materialSlider3.Location = new Point( 350, 382 );
            this.materialSlider3.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialSlider3.Name = "materialSlider3";
            this.materialSlider3.RangeMax = 3840;
            this.materialSlider3.RangeMin = 100;
            this.materialSlider3.Size = new Size( 223, 40 );
            this.materialSlider3.TabIndex = 49;
            this.materialSlider3.Text = "Aim Fov";
            this.materialSlider3.UseAccentColor = true;
            this.materialSlider3.Value = 100;
            this.materialSlider3.ValueMax = 3840;
            this.materialSlider3.ValueSuffix = "px";
            this.materialSlider3.onValueChanged += this.materialSlider3_onValueChanged;
            // 
            // materialSlider4
            // 
            this.materialSlider4.BackColor = Color.Indigo;
            this.materialSlider4.BackgroundImageLayout = ImageLayout.Stretch;
            this.materialSlider4.Depth = 0;
            this.materialSlider4.Font = new Font( "Roboto", 12F, FontStyle.Regular, GraphicsUnit.Pixel );
            this.materialSlider4.FontType = MaterialSkin.MaterialSkinManager.fontType.Caption;
            this.materialSlider4.ForeColor = Color.Aquamarine;
            this.materialSlider4.Location = new Point( 2, 129 );
            this.materialSlider4.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialSlider4.Name = "materialSlider4";
            this.materialSlider4.RangeMin = 1;
            this.materialSlider4.Size = new Size( 223, 40 );
            this.materialSlider4.TabIndex = 50;
            this.materialSlider4.Text = "Deadzone";
            this.materialSlider4.UseAccentColor = true;
            this.materialSlider4.ValueMax = 100;
            this.materialSlider4.onValueChanged += this.materialSlider4_onValueChanged;
            // 
            // materialButton3
            // 
            this.materialButton3.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.materialButton3.Density = MaterialButton.MaterialButtonDensity.Dense;
            this.materialButton3.Depth = 0;
            this.materialButton3.HighEmphasis = true;
            this.materialButton3.Icon = null;
            this.materialButton3.Location = new Point( 6, 382 );
            this.materialButton3.Margin = new Padding( 4, 6, 4, 6 );
            this.materialButton3.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialButton3.Name = "materialButton3";
            this.materialButton3.NoAccentTextColor = Color.Empty;
            this.materialButton3.Size = new Size( 190, 36 );
            this.materialButton3.TabIndex = 51;
            this.materialButton3.Text = "Bezier Customization";
            this.materialButton3.Type = MaterialButton.MaterialButtonType.Outlined;
            this.materialButton3.UseAccentColor = true;
            this.materialButton3.UseVisualStyleBackColor = true;
            this.materialButton3.Click += this.materialButton3_Click;
            // 
            // materialButton5
            // 
            this.materialButton5.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.materialButton5.Density = MaterialButton.MaterialButtonDensity.Dense;
            this.materialButton5.Depth = 0;
            this.materialButton5.HighEmphasis = true;
            this.materialButton5.Icon = null;
            this.materialButton5.Location = new Point( 423, 27 );
            this.materialButton5.Margin = new Padding( 4, 6, 4, 6 );
            this.materialButton5.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialButton5.Name = "materialButton5";
            this.materialButton5.NoAccentTextColor = Color.Empty;
            this.materialButton5.Size = new Size( 150, 36 );
            this.materialButton5.TabIndex = 52;
            this.materialButton5.Text = "Config Selector";
            this.materialButton5.Type = MaterialButton.MaterialButtonType.Outlined;
            this.materialButton5.UseAccentColor = true;
            this.materialButton5.UseVisualStyleBackColor = true;
            this.materialButton5.Click += this.materialButton5_Click;
            // 
            // IceColorBot
            // 
            this.BackColor = Color.MidnightBlue;
            this.BackgroundImage = ( Image ) resources.GetObject( "$this.BackgroundImage" );
            this.BackgroundImageLayout = ImageLayout.Center;
            this.ClientSize = new Size( 579, 583 );
            this.Controls.Add( this.materialButton5 );
            this.Controls.Add( this.materialButton3 );
            this.Controls.Add( this.materialSlider4 );
            this.Controls.Add( this.materialSlider3 );
            this.Controls.Add( this.materialSwitch2 );
            this.Controls.Add( this.materialButton4 );
            this.Controls.Add( this.materialButton2 );
            this.Controls.Add( this.materialButton1 );
            this.Controls.Add( this.materialLabel3 );
            this.Controls.Add( this.materialComboBox3 );
            this.Controls.Add( this.materialLabel2 );
            this.Controls.Add( this.materialComboBox2 );
            this.Controls.Add( this.materialLabel1 );
            this.Controls.Add( this.materialComboBox1 );
            this.Controls.Add( this.materialSwitch1 );
            this.Controls.Add( this.materialSlider2 );
            this.Controls.Add( this.materialSlider1 );
            this.FormBorderStyle = FormBorderStyle.Fixed3D;
            this.Icon = ( Icon ) resources.GetObject( "$this.Icon" );
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.MaximumSize = new Size( 579, 583 );
            this.MinimumSize = new Size( 579, 583 );
            this.Name = "IceColorBot";
            this.Sizable = false;
            this.ResumeLayout( false );
            this.PerformLayout();
        }

        private void InitializeStatusBar()
        {
            // Create the status panel
            statusPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 30, // Height of the status bar
                BackColor = Color.FromArgb( 30, 30, 30 ) // Darker color for contrast
            };

            // Create the status label
            statusLabel = new Label
            {
                Text = "Ready", // Initial text
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.LightGreen, // Set text color to match the theme
                BackColor = Color.Transparent
            };

            // Add the label to the panel
            statusPanel.Controls.Add( statusLabel );

            // Add the panel to the form
            this.Controls.Add( statusPanel );
        }

        private void Exit( object? sender, EventArgs e )
        {
            OnFormClosed( new( CloseReason.UserClosing ) );
        }

        private void ReOpen( object? sender, EventArgs e )
        {
            trayIcon.Visible = false;
            Show();

#if DEBUG
            Logger.Log( "Maximized From Tray" );
#endif
        }


        protected override void OnFormClosed( FormClosedEventArgs e )
        {

#if GETRECOILPATTERN
            recoilPatternCapture?.StopMonitoring();
            recoilPatternCapture?.Dispose();
#endif

            trayIcon.Visible = false;
            trayIcon.Dispose();
            bezierForm?.Dispose();
            configurationsForm?.Dispose();

            Logger.CleanUp();
            base.OnFormClosed( e );
            Application.Exit();
        }

        protected override void OnPaint( PaintEventArgs e )
        {
            base.OnPaint( e );

            // Create a new rectangle with a modified Y position (15 pixels down from the top)
            var clientRect = new Rectangle( this.ClientRectangle.X, this.ClientRectangle.Y + 25,
                                           this.ClientRectangle.Width, this.ClientRectangle.Height - 25 );

            // If there is a background image, draw it within the adjusted rectangle
            if ( this.BackgroundImage != null )
            {
                e.Graphics.DrawImage( this.BackgroundImage, clientRect );
            }

            // Create a GraphicsPath to define rounded corners
            GraphicsPath path = new GraphicsPath();
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

        private void materialComboBox1_SelectedIndexChanged( object? sender, EventArgs e )
        {
            string colorName = "";
            switch ( materialComboBox1.SelectedIndex )
            {
                case 0:
                {
                    colorName = "orange";
                }
                break;
                case 1:
                {
                    colorName = "red";
                }
                break;
                case 2:
                {
                    colorName = "green";
                }
                break;
                case 3:
                {
                    colorName = "yellow";
                }
                break;
                case 4:
                {
                    colorName = "purple";
                }
                break;
                case 5:
                {
                    colorName = "cyan";
                }
                break;
                default:
                {
                    colorName = "orange";
                }
                break;
            }

            PlayerData.SetOutlineColor( colorName );

#if DEBUG
            Logger.Log( "Color Selection Switched To: " + colorName );
#endif
        }

        private void materialComboBox2_SelectedIndexChanged( object? sender, EventArgs e )
        {
            switch ( materialComboBox2.SelectedIndex )
            {

                case 0:
                {
                    var aimLocation = AimLocation.head;
                    PlayerData.SetAimLocation( aimLocation );
                }
                break;
                case 1:
                {
                    var aimLocation = AimLocation.body;
                    PlayerData.SetAimLocation( aimLocation );
                }
                break;
                default:
                {
                    var aimLocation = AimLocation.head;
                    PlayerData.SetAimLocation( aimLocation );
                }
                break;
            }
        }

        private void materialComboBox3_SelectedIndexChanged( object? sender, EventArgs e )
        {
            var key = 0;
            switch ( materialComboBox3.SelectedIndex )
            {
                case 0:
                {
                    key = MouseInput.VK_LBUTTON;
                    PlayerData.SetAimKey( key );
                }
                break;
                case 1:
                {
                    key = MouseInput.VK_RBUTTON;
                    PlayerData.SetAimKey( key );
                }
                break;
                case 2:
                {
                    key = MouseInput.VK_LSHIFT;
                    PlayerData.SetAimKey( key );
                }
                break;
                case 3:
                {
                    key = MouseInput.VK_LMENU;
                    PlayerData.SetAimKey( key );
                }
                break;
                case 4:
                {
                    key = MouseInput.VK_LCONTROL;
                    PlayerData.SetAimKey( key );
                }
                break;
                default:
                {
                    key = MouseInput.VK_LBUTTON;
                    PlayerData.SetAimKey( key );
                }
                break;
            }
        }

        private void materialButton1_Click( object? sender, EventArgs e )
        {

#if !DEBUG
            if ( !PlayerData.PreStartDataCheck() )
            {
                MaterialMessageBox.Show( "Not all seetings have been selected", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error );
                return;
            }
#endif

#if DEBUG
#if GETRECOILPATTERN
            var gameRect = PlayerData.GetRect();
            string colorCode = "#F27AEB"; //<-- This is the color code for the crosshair
            recoilPatternCapture = new( ref gameRect, colorCode );
            recoilPatternCapture.StartMonitoring();
            Logger.Log( "Recoil Pattern Capture Enabled" );
#else
            Utils.Watch.StartCaptureWatch();
            aimBot.Start();
#endif
#endif
        }

        private void materialButton2_Click( object? sender, EventArgs e )
        {
            aimBot.Stop();
            Utils.Watch.StopCaptureWatch();
#if GETRECOILPATTERN
            recoilPatternCapture!.StopMonitoring();
#endif
        }

        private void materialButton4_Click( object? sender, EventArgs e )
        {
            Admin.CheckAndRunAdmin();
        }


        private void materialSlider1_onValueChanged( object? sender, int newValue )
        {
            double value = ( double ) newValue;
            PlayerData.SetAimSpeed( value );
        }

        private void materialSlider2_onValueChanged( object? sender, int newValue )
        {
            double value = ( double ) newValue;
            PlayerData.SetAimSmoothing( value );
        }

        private void materialSlider3_onValueChanged( object? sender, int newValue )
        {
            PlayerData.SetAimFov( newValue );
        }


        private void materialSlider4_onValueChanged( object? sender, int newValue )
        {
            PlayerData.SetDeadzone( newValue );
        }

        void UnhandledExceptionTrapper( object? sender, UnhandledExceptionEventArgs? e )
        {
            ErrorHandler.HandleExceptionNonExit( new Exception( e?.ExceptionObject is Exception ex ? ex.Message : "Unknown Exception" ) );
            OnFormClosed( new FormClosedEventArgs( CloseReason.ApplicationExitCall ) );
        }

        private void materialSwitch1_CheckedChanged( object sender, EventArgs e )
        {
            if ( materialSwitch1.Checked )
            {
                PlayerData.SetAntiRecoil( true );
            } else
            {
                PlayerData.SetAntiRecoil( false );
            }
        }

        private void materialSwitch2_CheckedChanged( object sender, EventArgs e )
        {
            if ( materialSwitch2.Checked )
            {
                PlayerData.SetPrediction( true );
            } else
            {
                PlayerData.SetPrediction( false );
            }
        }


        private void materialButton3_Click( object sender, EventArgs e )
        {
            bezierForm = new();
            bezierForm.Show();
        }

        private void materialButton5_Click( object sender, EventArgs e )
        {
            configurationsForm = new( UpdateSettingsUI! );
            configurationsForm.Show();
        }


        // Method in the MainForm to update the displayed settings using a Dictionary
        public void UpdateSettingsUI( Dictionary<string, object?> blobFields )
        {
            // Handle AimSpeed (double)
            if ( blobFields.TryGetValue( "localAimSpeed", out Object? speedValue ) && speedValue is JsonElement aimSpeedJson )
            {
                double aimSpeed = aimSpeedJson.GetDouble();
                materialSlider1.Value = ( int ) aimSpeed;
            }

            // Handle AimSmoothing (double)
            if ( blobFields.TryGetValue( "localAimSmoothing", out Object? smoothingValue ) && smoothingValue is JsonElement aimSmoothingJson )
            {
                double aimSmoothing = aimSmoothingJson.GetDouble();
                materialSlider2.Value = ( int ) aimSmoothing;
            }

            // Handle AimFov (int)
            if ( blobFields.TryGetValue( "localAimFov", out Object? fovValue ) && fovValue is JsonElement aimFovJson )
            {
                int aimFov = aimFovJson.GetInt32();
                materialSlider3.Value = aimFov;
            }

            // Handle Deadzone (int)
            if ( blobFields.TryGetValue( "localDeadzone", out Object? deadzoneValue ) && deadzoneValue is JsonElement deadzoneJson )
            {
                int deadzone = deadzoneJson.GetInt32();
                materialSlider4.Value = deadzone;
            }

            // Handle AimKey (int or string)
            if ( blobFields.TryGetValue( "localAimKey", out Object? aimKeyValue ) && aimKeyValue is JsonElement aimKeyJson )
            {
                int aimKey = aimKeyJson.GetInt32();
                materialComboBox3.SelectedItem = aimKey.ToString();
                materialComboBox3.Refresh(); // Force redraw
            }

            // Handle AimLocation (int for selected index)
            if ( blobFields.TryGetValue( "localAimLocation", out Object? aimLocValue ) && aimLocValue is JsonElement aimLocationJson )
            {
                int aimLocationIndex = aimLocationJson.GetInt32();
                materialComboBox2.SelectedIndex = aimLocationIndex;
                materialComboBox2.Refresh(); // Force redraw
            }

            // Handle ColorTolerance (string)
            if ( blobFields.TryGetValue( "localColorToleranceName", out Object? outlineColorValue ) && outlineColorValue is JsonElement colorToleranceJson )
            {
                string colorTolerance = colorToleranceJson.GetString();
                materialComboBox1.Text = colorTolerance;
                materialComboBox1.Refresh(); // Force redraw
            }

            // Handle Prediction (bool)
            if ( blobFields.TryGetValue( "localPrediction", out Object? predictionValue ) && predictionValue is JsonElement predictionJson )
            {
                bool prediction = predictionJson.GetBoolean();
                materialSwitch2.Checked = prediction;
            }

            // Handle AntiRecoil (bool)
            if ( blobFields.TryGetValue( "localAntiRecoil", out Object? antiRecoilValue ) && antiRecoilValue is JsonElement antiRecoilJson )
            {
                bool antiRecoil = antiRecoilJson.GetBoolean();
                materialSwitch1.Checked = antiRecoil;
            }

            Invalidate(); // Redraw the form to reflect the changes
        }


    }
}
