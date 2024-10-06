#if DEBUG
//#define GETRECOILPATTERN
#endif

using Recoil;
using Utils;
using static SCB.EnemyScanner;

namespace SCB
{

    internal partial class IceColorBot : Form
    {

        private AimBot aimBot = new AimBot();
        private NotifyIcon trayIcon;
        private Logger logger;
        private Thread isGameActive;
        private Thread smartKey;
        private ColorTolerances colortolerances;
        private CancellationTokenSource activeGameCancellation;
        internal BezierControlForm? bezierControlForm;
#if GETRECOILPATTERN
        internal RecoilPatternCapture? recoilPatternCapture;
#endif


        internal IceColorBot()
        {
            InitializeComponent();
            this.DoubleBuffered = true;


            activeGameCancellation = new CancellationTokenSource();

            trayIcon = new NotifyIcon();
            trayIcon.Text = "IceColorBot";
            trayIcon.Icon = Icon;
            trayIcon.ContextMenuStrip = new ContextMenuStrip();
            trayIcon.ContextMenuStrip.Items.Add( "Exit", null, Exit );
            trayIcon.ContextMenuStrip.Items.Add( "Open", null, ReOpen );
            trayIcon.Visible = false;

            DarkMode.SetDarkMode( this.Handle );

#if DEBUG
            logger = new Logger();
            logger.Start();
            logger.Log( "Logger Initialized" );
            colortolerances = new ColorTolerances( ref logger );
#else
            colortolerances = new ColorTolerances();
#endif

            isGameActive = new Thread( () => Utils.UtilsThreads.SmartGameCheck( ref activeGameCancellation, ref aimBot, ref logger ) );
            smartKey = new Thread( () => Utils.UtilsThreads.UiSmartKey( trayIcon, this, activeGameCancellation ) );
            isGameActive.Start();
            smartKey.Start();

            toolTip1.SetToolTip( trackBar1, "Fov starts at 192 and goes up to 3840" );
            toolTip2.SetToolTip( trackBar2, "Aim speed starts at 0 and goes up to 100" );
            toolTip3.SetToolTip( trackBar3, "Aim delay starts at 10 microseconds and goes up to 1 millisecond" );
            toolTip4.SetToolTip( trackBar5, "Anti-recoil X starts at 0 and goes up to 100" );
            toolTip5.SetToolTip( trackBar6, "Anti-recoil Y starts at 0 and goes up to 100" );
            toolTip6.SetToolTip( trackBar4, "Aim smoothing starts at 0 and goes up to 100" );
            toolTip7.SetToolTip( trackBar7, "Deadzone starts at 0 and goes up to 50" );

            Utils.Mathf.SetupPermutationTable();
            RecoilPatternProcessor.ProcessAllGunPatterns( "C:\\Users\\peter\\Documents\\ColorbotOutput" );
#if DEBUG
            Task.Run( () => RecoilPatternProcessor.RecoilPatternThread( logger ) );
#else
            Task.Run( () => RecoilPatternProcessor.RecoilPatternThread() );
#endif
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager( typeof( IceColorBot ) );
            this.label1 = new Label();
            this.label2 = new Label();
            this.trackBar1 = new TrackBar();
            this.button1 = new Button();
            this.button2 = new Button();
            this.label3 = new Label();
            this.comboBox1 = new ComboBox();
            this.radioButton1 = new RadioButton();
            this.trackBar2 = new TrackBar();
            this.label4 = new Label();
            this.comboBox2 = new ComboBox();
            this.label5 = new Label();
            this.trackBar3 = new TrackBar();
            this.label6 = new Label();
            this.comboBox3 = new ComboBox();
            this.toolTip1 = new ToolTip( this.components );
            this.toolTip2 = new ToolTip( this.components );
            this.toolTip3 = new ToolTip( this.components );
            this.label7 = new Label();
            this.trackBar4 = new TrackBar();
            this.label8 = new Label();
            this.trackBar5 = new TrackBar();
            this.label9 = new Label();
            this.trackBar6 = new TrackBar();
            this.toolTip4 = new ToolTip( this.components );
            this.toolTip5 = new ToolTip( this.components );
            this.toolTip6 = new ToolTip( this.components );
            this.radioButton2 = new RadioButton();
            this.label10 = new Label();
            this.trackBar7 = new TrackBar();
            this.toolTip7 = new ToolTip( this.components );
            this.button3 = new Button();
            this.checkBox1 = new CheckBox();
            ( ( System.ComponentModel.ISupportInitialize ) this.trackBar1 ).BeginInit();
            ( ( System.ComponentModel.ISupportInitialize ) this.trackBar2 ).BeginInit();
            ( ( System.ComponentModel.ISupportInitialize ) this.trackBar3 ).BeginInit();
            ( ( System.ComponentModel.ISupportInitialize ) this.trackBar4 ).BeginInit();
            ( ( System.ComponentModel.ISupportInitialize ) this.trackBar5 ).BeginInit();
            ( ( System.ComponentModel.ISupportInitialize ) this.trackBar6 ).BeginInit();
            ( ( System.ComponentModel.ISupportInitialize ) this.trackBar7 ).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new Font( "Constantia", 12F, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point, 0 );
            this.label1.ForeColor = Color.MediumSlateBlue;
            this.label1.Location = new Point( 64, 24 );
            this.label1.Name = "label1";
            this.label1.Size = new Size( 87, 19 );
            this.label1.TabIndex = 0;
            this.label1.Text = "Aim Speed";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new Font( "Constantia", 12F, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point, 0 );
            this.label2.ForeColor = Color.MediumSlateBlue;
            this.label2.Location = new Point( 412, 24 );
            this.label2.Name = "label2";
            this.label2.Size = new Size( 95, 19 );
            this.label2.TabIndex = 1;
            this.label2.Text = "Aim Radius";
            // 
            // trackBar1
            // 
            this.trackBar1.BackColor = Color.MediumSlateBlue;
            this.trackBar1.Location = new Point( 384, 46 );
            this.trackBar1.Name = "trackBar1";
            this.trackBar1.Size = new Size( 149, 45 );
            this.trackBar1.TabIndex = 3;
            this.trackBar1.TickStyle = TickStyle.TopLeft;
            this.trackBar1.Scroll += this.trackBar1_Scroll;
            // 
            // button1
            // 
            this.button1.BackColor = Color.Indigo;
            this.button1.Font = new Font( "Constantia", 18F, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point, 0 );
            this.button1.ForeColor = Color.Aquamarine;
            this.button1.Location = new Point( 30, 390 );
            this.button1.Name = "button1";
            this.button1.Size = new Size( 199, 36 );
            this.button1.TabIndex = 4;
            this.button1.Text = "Start AimBot";
            this.button1.UseVisualStyleBackColor = false;
            this.button1.Click += this.button1_Click;
            // 
            // button2
            // 
            this.button2.BackColor = Color.Indigo;
            this.button2.Font = new Font( "Constantia", 18F, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point, 0 );
            this.button2.ForeColor = Color.Aquamarine;
            this.button2.Location = new Point( 334, 390 );
            this.button2.Name = "button2";
            this.button2.Size = new Size( 199, 36 );
            this.button2.TabIndex = 5;
            this.button2.Text = "Stop AimBot";
            this.button2.UseVisualStyleBackColor = false;
            this.button2.Click += this.button2_Click;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new Font( "Constantia", 18F, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point, 0 );
            this.label3.ForeColor = Color.MediumSlateBlue;
            this.label3.Location = new Point( 230, 111 );
            this.label3.Name = "label3";
            this.label3.Size = new Size( 106, 29 );
            this.label3.TabIndex = 6;
            this.label3.Text = "Aim Key";
            // 
            // comboBox1
            // 
            this.comboBox1.FormattingEnabled = true;
            this.comboBox1.Items.AddRange( new object[] { "Left Mouse Button", "Right Mouse Button", "Left Shift", "Left Control", "Left Alt" } );
            this.comboBox1.Location = new Point( 215, 154 );
            this.comboBox1.Name = "comboBox1";
            this.comboBox1.Size = new Size( 135, 23 );
            this.comboBox1.TabIndex = 7;
            this.comboBox1.SelectedIndexChanged += this.comboBox1_SelectedIndexChanged;
            // 
            // radioButton1
            // 
            this.radioButton1.AutoSize = true;
            this.radioButton1.BackColor = Color.Indigo;
            this.radioButton1.FlatStyle = FlatStyle.Popup;
            this.radioButton1.Font = new Font( "Constantia", 14.25F, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point, 0 );
            this.radioButton1.ForeColor = Color.Aquamarine;
            this.radioButton1.Location = new Point( 30, 357 );
            this.radioButton1.Name = "radioButton1";
            this.radioButton1.Size = new Size( 181, 27 );
            this.radioButton1.TabIndex = 11;
            this.radioButton1.Text = "Minimize To Tray";
            this.radioButton1.UseVisualStyleBackColor = false;
            this.radioButton1.CheckedChanged += this.radioButton1_CheckedChanged;
            // 
            // trackBar2
            // 
            this.trackBar2.BackColor = Color.MediumSlateBlue;
            this.trackBar2.Location = new Point( 30, 46 );
            this.trackBar2.Name = "trackBar2";
            this.trackBar2.Size = new Size( 158, 45 );
            this.trackBar2.TabIndex = 15;
            this.trackBar2.TickStyle = TickStyle.TopLeft;
            this.trackBar2.Scroll += this.trackBar2_Scroll;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new Font( "Constantia", 12F, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point, 0 );
            this.label4.ForeColor = Color.MediumSlateBlue;
            this.label4.Location = new Point( 399, 247 );
            this.label4.Name = "label4";
            this.label4.Size = new Size( 123, 19 );
            this.label4.TabIndex = 17;
            this.label4.Text = "Color Selection";
            // 
            // comboBox2
            // 
            this.comboBox2.FormattingEnabled = true;
            this.comboBox2.Items.AddRange( new object[] { "orange", "red", "green", "yellow", "purple", "cyan" } );
            this.comboBox2.Location = new Point( 389, 269 );
            this.comboBox2.Name = "comboBox2";
            this.comboBox2.Size = new Size( 144, 23 );
            this.comboBox2.TabIndex = 18;
            this.comboBox2.SelectedIndexChanged += this.comboBox2_SelectedIndexChanged;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new Font( "Constantia", 12F, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point, 0 );
            this.label5.ForeColor = Color.MediumSlateBlue;
            this.label5.Location = new Point( 64, 101 );
            this.label5.Name = "label5";
            this.label5.Size = new Size( 87, 19 );
            this.label5.TabIndex = 19;
            this.label5.Text = "Aim Delay";
            // 
            // trackBar3
            // 
            this.trackBar3.BackColor = Color.MediumSlateBlue;
            this.trackBar3.Location = new Point( 30, 123 );
            this.trackBar3.Name = "trackBar3";
            this.trackBar3.Size = new Size( 158, 45 );
            this.trackBar3.TabIndex = 20;
            this.trackBar3.TickStyle = TickStyle.TopLeft;
            this.trackBar3.Scroll += this.trackBar3_Scroll;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Font = new Font( "Constantia", 12F, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point, 0 );
            this.label6.ForeColor = Color.MediumSlateBlue;
            this.label6.Location = new Point( 409, 306 );
            this.label6.Name = "label6";
            this.label6.Size = new Size( 110, 19 );
            this.label6.TabIndex = 21;
            this.label6.Text = "Aim Location";
            // 
            // comboBox3
            // 
            this.comboBox3.FormattingEnabled = true;
            this.comboBox3.Items.AddRange( new object[] { "head", "body" } );
            this.comboBox3.Location = new Point( 393, 328 );
            this.comboBox3.Name = "comboBox3";
            this.comboBox3.Size = new Size( 140, 23 );
            this.comboBox3.TabIndex = 22;
            this.comboBox3.SelectedIndexChanged += this.comboBox3_SelectedIndexChanged;
            // 
            // toolTip1
            // 
            this.toolTip1.ToolTipIcon = ToolTipIcon.Info;
            this.toolTip1.ToolTipTitle = "Aim Radius Details";
            // 
            // toolTip2
            // 
            this.toolTip2.ToolTipIcon = ToolTipIcon.Info;
            this.toolTip2.ToolTipTitle = "Aim Speed Details";
            // 
            // toolTip3
            // 
            this.toolTip3.ToolTipIcon = ToolTipIcon.Info;
            this.toolTip3.ToolTipTitle = "Aim Delay Details";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Font = new Font( "Constantia", 12F, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point, 0 );
            this.label7.ForeColor = Color.MediumSlateBlue;
            this.label7.Location = new Point( 47, 185 );
            this.label7.Name = "label7";
            this.label7.Size = new Size( 127, 19 );
            this.label7.TabIndex = 23;
            this.label7.Text = "Aim Smoothing";
            // 
            // trackBar4
            // 
            this.trackBar4.BackColor = Color.MediumSlateBlue;
            this.trackBar4.Location = new Point( 30, 207 );
            this.trackBar4.Name = "trackBar4";
            this.trackBar4.Size = new Size( 158, 45 );
            this.trackBar4.TabIndex = 24;
            this.trackBar4.TickStyle = TickStyle.TopLeft;
            this.trackBar4.Scroll += this.trackBar4_Scroll!;
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Font = new Font( "Constantia", 12F, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point, 0 );
            this.label8.ForeColor = Color.MediumSlateBlue;
            this.label8.Location = new Point( 399, 101 );
            this.label8.Name = "label8";
            this.label8.Size = new Size( 108, 19 );
            this.label8.TabIndex = 25;
            this.label8.Text = "Anti-Recoil X";
            // 
            // trackBar5
            // 
            this.trackBar5.BackColor = Color.MediumSlateBlue;
            this.trackBar5.Location = new Point( 384, 123 );
            this.trackBar5.Name = "trackBar5";
            this.trackBar5.Size = new Size( 149, 45 );
            this.trackBar5.TabIndex = 26;
            this.trackBar5.TickStyle = TickStyle.TopLeft;
            this.trackBar5.Scroll += this.trackBar5_Scroll!;
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Font = new Font( "Constantia", 12F, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point, 0 );
            this.label9.ForeColor = Color.MediumSlateBlue;
            this.label9.Location = new Point( 399, 171 );
            this.label9.Name = "label9";
            this.label9.Size = new Size( 107, 19 );
            this.label9.TabIndex = 27;
            this.label9.Text = "Anti-Recoil Y";
            // 
            // trackBar6
            // 
            this.trackBar6.BackColor = Color.MediumSlateBlue;
            this.trackBar6.Location = new Point( 384, 193 );
            this.trackBar6.Name = "trackBar6";
            this.trackBar6.Size = new Size( 149, 45 );
            this.trackBar6.TabIndex = 28;
            this.trackBar6.TickStyle = TickStyle.TopLeft;
            this.trackBar6.Scroll += this.trackBar6_Scroll!;
            // 
            // toolTip4
            // 
            this.toolTip4.ToolTipIcon = ToolTipIcon.Info;
            this.toolTip4.ToolTipTitle = "Anti-Recoil X";
            // 
            // toolTip5
            // 
            this.toolTip5.ToolTipIcon = ToolTipIcon.Info;
            this.toolTip5.ToolTipTitle = "Anti-Recoil Y";
            // 
            // toolTip6
            // 
            this.toolTip6.ToolTipIcon = ToolTipIcon.Info;
            this.toolTip6.ToolTipTitle = "Aim Smoothing";
            // 
            // radioButton2
            // 
            this.radioButton2.AutoSize = true;
            this.radioButton2.BackColor = Color.Indigo;
            this.radioButton2.FlatStyle = FlatStyle.Popup;
            this.radioButton2.Font = new Font( "Constantia", 14.25F, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point, 0 );
            this.radioButton2.ForeColor = Color.Aquamarine;
            this.radioButton2.Location = new Point( 365, 357 );
            this.radioButton2.Name = "radioButton2";
            this.radioButton2.Size = new Size( 153, 27 );
            this.radioButton2.TabIndex = 29;
            this.radioButton2.Text = "Run As Admin";
            this.radioButton2.UseVisualStyleBackColor = false;
            this.radioButton2.CheckedChanged += this.radioButton2_CheckedChanged!;
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Font = new Font( "Constantia", 12F, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point, 0 );
            this.label10.ForeColor = Color.MediumSlateBlue;
            this.label10.Location = new Point( 68, 269 );
            this.label10.Name = "label10";
            this.label10.Size = new Size( 83, 19 );
            this.label10.TabIndex = 30;
            this.label10.Text = "Deadzone";
            // 
            // trackBar7
            // 
            this.trackBar7.BackColor = Color.MediumSlateBlue;
            this.trackBar7.Location = new Point( 30, 291 );
            this.trackBar7.Name = "trackBar7";
            this.trackBar7.Size = new Size( 158, 45 );
            this.trackBar7.TabIndex = 31;
            this.trackBar7.TickStyle = TickStyle.TopLeft;
            this.trackBar7.Scroll += this.trackBar7_Scroll!;
            // 
            // button3
            // 
            this.button3.BackColor = Color.Indigo;
            this.button3.Font = new Font( "Constantia", 12F, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point, 0 );
            this.button3.ForeColor = Color.Aquamarine;
            this.button3.Location = new Point( 365, 519 );
            this.button3.Name = "button3";
            this.button3.Size = new Size( 198, 26 );
            this.button3.TabIndex = 33;
            this.button3.Text = "Bezier Customization";
            this.button3.UseVisualStyleBackColor = false;
            this.button3.Click += this.button3_Click!;
            // 
            // checkBox1
            // 
            this.checkBox1.AutoSize = true;
            this.checkBox1.BackColor = Color.Indigo;
            this.checkBox1.Font = new Font( "Constantia", 12F, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point, 0 );
            this.checkBox1.ForeColor = Color.Aquamarine;
            this.checkBox1.Location = new Point( 212, 185 );
            this.checkBox1.Name = "checkBox1";
            this.checkBox1.Size = new Size( 138, 23 );
            this.checkBox1.TabIndex = 34;
            this.checkBox1.Text = "Humanization";
            this.checkBox1.UseVisualStyleBackColor = false;
            this.checkBox1.CheckedChanged += this.checkBox1_CheckedChanged!;
            // 
            // IceColorBot
            // 
            this.BackColor = Color.MidnightBlue;
            this.BackgroundImage = ( Image ) resources.GetObject( "$this.BackgroundImage" )!;
            this.BackgroundImageLayout = ImageLayout.Center;
            this.ClientSize = new Size( 563, 544 );
            this.Controls.Add( this.checkBox1 );
            this.Controls.Add( this.button3 );
            this.Controls.Add( this.trackBar7 );
            this.Controls.Add( this.label10 );
            this.Controls.Add( this.radioButton2 );
            this.Controls.Add( this.trackBar6 );
            this.Controls.Add( this.label9 );
            this.Controls.Add( this.trackBar5 );
            this.Controls.Add( this.label8 );
            this.Controls.Add( this.trackBar4 );
            this.Controls.Add( this.label7 );
            this.Controls.Add( this.comboBox3 );
            this.Controls.Add( this.label6 );
            this.Controls.Add( this.trackBar3 );
            this.Controls.Add( this.label5 );
            this.Controls.Add( this.comboBox2 );
            this.Controls.Add( this.label4 );
            this.Controls.Add( this.trackBar2 );
            this.Controls.Add( this.radioButton1 );
            this.Controls.Add( this.comboBox1 );
            this.Controls.Add( this.label3 );
            this.Controls.Add( this.button2 );
            this.Controls.Add( this.button1 );
            this.Controls.Add( this.trackBar1 );
            this.Controls.Add( this.label2 );
            this.Controls.Add( this.label1 );
            this.DoubleBuffered = true;
            this.Icon = ( Icon ) resources.GetObject( "$this.Icon" )!;
            this.KeyPreview = true;
            this.MaximumSize = new Size( 579, 583 );
            this.MinimumSize = new Size( 579, 583 );
            this.Name = "IceColorBot";
            ( ( System.ComponentModel.ISupportInitialize ) this.trackBar1 ).EndInit();
            ( ( System.ComponentModel.ISupportInitialize ) this.trackBar2 ).EndInit();
            ( ( System.ComponentModel.ISupportInitialize ) this.trackBar3 ).EndInit();
            ( ( System.ComponentModel.ISupportInitialize ) this.trackBar4 ).EndInit();
            ( ( System.ComponentModel.ISupportInitialize ) this.trackBar5 ).EndInit();
            ( ( System.ComponentModel.ISupportInitialize ) this.trackBar6 ).EndInit();
            ( ( System.ComponentModel.ISupportInitialize ) this.trackBar7 ).EndInit();
            this.ResumeLayout( false );
            this.PerformLayout();
        }

        private void trackBar1_Scroll( object? sender, EventArgs e )
        {

            var rad = ( trackBar1.Value * 280 );
            PlayerData.SetAimRad( ref rad );

#if DEBUG
            logger.Log( "Aim Radius Switched To: " + PlayerData.GetAimRad() );
#endif
        }


        private void button1_Click( object? sender, EventArgs e )
        {
            //            if ( aimBot == null )
            //            {
            //                aimBot = new AimBot();
            //            }

            //#if DEBUG
            //            aimBot.Start( ref logger, PlayerData.GetRect() );
            //#else
            //            aimBot.Start( PlayerData.GetRect() );
            //#endif
#if DEBUG
#if GETRECOILPATTERN
            var gameRect = PlayerData.GetRect();
            string colorCode = "#F27AEB";
            recoilPatternCapture = new( ref gameRect, colorCode );
#endif
#endif
        }

        private void button2_Click( object? sender, EventArgs e )
        {
            //aimBot.Stop();
#if GETRECOILPATTERN
            recoilPatternCapture!.StopMonitoring();
#endif
        }

        private void comboBox1_SelectedIndexChanged( object? sender, EventArgs e )
        {
            var key = 0;
            switch ( comboBox1.SelectedIndex )
            {
                case 0:
                {
                    key = 0x01;
                    PlayerData.SetAimKey( ref key, ref aimBot );
                }
                break;
                case 1:
                {
                    key = 0x02;
                    PlayerData.SetAimKey( ref key, ref aimBot );
                }
                break;
                case 2:
                {
                    key = 0x10;
                    PlayerData.SetAimKey( ref key, ref aimBot );
                }
                break;
                case 3:
                {
                    key = 0x11;
                    PlayerData.SetAimKey( ref key, ref aimBot );
                }
                break;
                case 4:
                {
                    key = 0x12;
                    PlayerData.SetAimKey( ref key, ref aimBot );
                }
                break;
                default:
                {
                    key = 0x01;
                    PlayerData.SetAimKey( ref key, ref aimBot );
                }
                break;
            }

#if DEBUG
            logger.Log( "Aim Key Switched To: " + PlayerData.GetAimKey() );
#endif
        }


        private void Exit( object? sender, EventArgs e )
        {
            trayIcon.Visible = false;

            aimBot.Dispose();
            logger.Dispose();
            colortolerances.Dispose();
            activeGameCancellation.Cancel();
            isGameActive.Join();
            activeGameCancellation.Dispose();
            smartKey.Join();
            bezierControlForm?.Dispose();
            RecoilPatternProcessor.RecoilPatternSource.Cancel();
            RecoilPatternProcessor.RecoilPatternSource.Dispose();
#if GETRECOILPATTERN
            recoilPatternCapture?.StopMonitoring();
            recoilPatternCapture?.Dispose();
#endif
            Application.Exit();
        }

        private void ReOpen( object? sender, EventArgs e )
        {
            trayIcon.Visible = false;
            Show();

#if DEBUG
            logger.Log( "Maximized From Tray" );
#endif
        }

        private void radioButton1_CheckedChanged( object? sender, EventArgs e )
        {
            trayIcon.Visible = true;

            if ( radioButton1.Checked )
            {
#if DEBUG
                logger.Log( "Minimized to tray" );
#endif
                Hide();
                radioButton1.Checked = false;
                radioButton1.Refresh();
                radioButton1.Update();
            }
        }

        private void trackBar2_Scroll( object? sender, EventArgs e )
        {
            double range = 100 - 0;
            double growthFactor = 2;
            double exponential = ( int ) Math.Pow( growthFactor, trackBar2.Value ) - 1;

            double aimSpeed = 0 + ( int ) ( range * exponential / Math.Pow( growthFactor, 10 ) );
            PlayerData.SetAimSpeed( ref aimSpeed, ref aimBot );
#if DEBUG
            logger.Log( "Aim Speed Switched To: " + aimSpeed );
#endif
        }


        private void comboBox2_SelectedIndexChanged( object? sender, EventArgs e )
        {
            switch ( comboBox2.SelectedIndex )
            {
                case 0:
                {
                    colortolerances.SetColorTolerance( "orange" );
                }
                break;
                case 1:
                {
                    colortolerances.SetColorTolerance( "red" );
                }
                break;
                case 2:
                {
                    colortolerances.SetColorTolerance( "green" );
                }
                break;
                case 3:
                {
                    colortolerances.SetColorTolerance( "yellow" );
                }
                break;
                case 4:
                {
                    colortolerances.SetColorTolerance( "purple" );
                }
                break;
                case 5:
                {
                    colortolerances.SetColorTolerance( "cyan" );
                }
                break;
                default:
                {
                    colortolerances.SetColorTolerance( "orange" );
                }
                break;
            }

            var color = colortolerances.GetColorTolerance();
            PlayerData.SetColorRange( ref color );

#if DEBUG
            logger.Log( "Color Selection Switched To: " + colortolerances.GetColorName( PlayerData.GetColorRange() ) );
#endif
        }

        private void trackBar3_Scroll( object? sender, EventArgs e )
        {

            double range = 1000 - 10;
            double growthFactor = 2;
            double exponential = Math.Pow( growthFactor, trackBar3.Value ) - 1;

            double aimDelay = 10 + ( range * exponential / Math.Pow( growthFactor, 10 ) );

            PlayerData.SetAimDelay( ref aimDelay, ref aimBot );

#if DEBUG
            logger.Log( "Aim Delay Switched To: " + PlayerData.GetAimDelay() + ", microseconds" );
#endif
        }


        protected override void OnFormClosed( FormClosedEventArgs e )
        {
            if ( aimBot != null )
            {
                aimBot.Dispose();
            }

            if ( logger != null )
            {
                logger.Dispose();
            }

            if ( colortolerances != null )
            {
                colortolerances.Dispose();
            }

            if ( isGameActive != null &&
                isGameActive.IsAlive )
            {
                activeGameCancellation.Cancel();
                isGameActive.Join();
                smartKey.Join();
                activeGameCancellation.Dispose();
                RecoilPatternProcessor.RecoilPatternSource.Cancel();
                RecoilPatternProcessor.RecoilPatternSource.Dispose();
            }
            bezierControlForm?.Dispose();
            Application.Exit();
            base.OnFormClosed( e );
        }



        private void comboBox3_SelectedIndexChanged( object? sender, EventArgs e )
        {
            switch ( comboBox3.SelectedIndex )
            {

                case 0:
                {
                    var aimLocation = AimLocation.head;
                    PlayerData.SetAimLocation( ref aimLocation, ref aimBot );
                }
                break;
                case 1:
                {
                    var aimLocation = AimLocation.body;
                    PlayerData.SetAimLocation( ref aimLocation, ref aimBot );
                }
                break;
                default:
                {
                    var aimLocation = AimLocation.head;
                    PlayerData.SetAimLocation( ref aimLocation, ref aimBot );
                }
                break;
            }

#if DEBUG
            logger.Log( "Aim Location Switched To: " + PlayerData.GetAimLocation() );
#endif
        }

        private void trackBar4_Scroll( object sender, EventArgs e )
        {
            double smoothing = ( trackBar4.Value * 10 );
            PlayerData.SetAimSmoothing( ref smoothing, ref aimBot );
#if DEBUG
            logger.Log( "Aim Smoothing Switched To: " + PlayerData.GetAimSmoothing() );
#endif
        }

        private void trackBar5_Scroll( object sender, EventArgs e )
        {
            //make recoil exponential from 0 to 25`
            double range = 100 - 0;
            double growthFactor = 1.36;
            double exponential = Math.Pow( growthFactor, trackBar5.Value ) - 1;

            double antiRecoilX = 0 + ( range * exponential / Math.Pow( growthFactor, 10 ) );

            PlayerData.SetAntiRecoilX( ref antiRecoilX, ref aimBot );

#if DEBUG
            logger.Log( "Anti-Recoil X Switched To: " + PlayerData.GetAntiRecoilX() );
#endif
        }

        private void trackBar6_Scroll( object sender, EventArgs e )
        {
            double range = 100 - 0;
            double growthFactor = 1.36;
            double exponential = Math.Pow( growthFactor, trackBar6.Value ) - 1;

            double antiRecoilY = 0 + ( range * exponential / Math.Pow( growthFactor, 10 ) );

            PlayerData.SetAntiRecoilY( ref antiRecoilY, ref aimBot );

#if DEBUG
            logger.Log( "Anti-Recoil Y Switched To: " + PlayerData.GetAntiRecoilY() );
#endif
        }

        private void radioButton2_CheckedChanged( object sender, EventArgs e )
        {
            Admin.CheckAndRunAdmin();
        }

        private void trackBar7_Scroll( object sender, EventArgs e )
        {
            //deadzone starts at zero and goes up to 50 exponetially
            double range = 50 - 1;
            double growthFactor = 2;
            double exponential = Math.Pow( growthFactor, trackBar7.Value ) - 1;

            int deadzone = ( int ) ( 1 + ( range * exponential / Math.Pow( growthFactor, 10 ) ) );
            PlayerData.SetDeadzone( ref deadzone, ref aimBot );

#if DEBUG
            logger.Log( "Deadzone Switched To: " + PlayerData.GetDeadzone() );
#endif
        }

        private void button3_Click( object sender, EventArgs e )
        {
            bezierControlForm = new BezierControlForm();
            bezierControlForm.Show();
        }

        private void checkBox1_CheckedChanged( object sender, EventArgs e )
        {
            if ( checkBox1.Checked )
            {
                PlayerData.SetHumanize( true, ref aimBot );
            } else
            {
                PlayerData.SetHumanize( false, ref aimBot );
            }

#if DEBUG
            logger.Log( "Humanization Switched To: " + PlayerData.GetHumanize() );
#endif
        }
    }

}
