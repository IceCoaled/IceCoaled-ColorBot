using Gma.System.MouseKeyHook;

namespace SCB
{

    public partial class IceColorBot : Form
    {

        private AimBot aimBot = new AimBot();
        private ScreenCap screenCap;
        private NotifyIcon trayIcon;
        private Logger logger;
        Thread isGameActive;
        private ColorTolerances colortolerances;
        private IKeyboardMouseEvents keyboardEvents;
        private CancellationTokenSource activeGameCancellation;


        public IceColorBot()
        {
            InitializeComponent();

            activeGameCancellation = new CancellationTokenSource();

            trayIcon = new NotifyIcon();
            trayIcon.Text = "IceColorBot";
            trayIcon.Icon = Icon;
            trayIcon.ContextMenuStrip = new ContextMenuStrip();
            trayIcon.ContextMenuStrip.Items.Add( "Exit", null, Exit );
            trayIcon.ContextMenuStrip.Items.Add( "Open", null, ReOpen );

            if ( !WinApi.UsingLightTheme() )
            {
                WinApi.UseImmersiveDarkMode( this.Handle, true );
            }

            SubToHotKey();

#if DEBUG
            logger = new Logger();
            logger.Start();
            logger.Log( "Logger Initialized" );
            colortolerances = new ColorTolerances( ref logger );
#else
            colortolerances = new ColorTolerances();
#endif

            isGameActive = new Thread( GameCheck );
            isGameActive.Start();
        }

        private void InitializeComponent()
        {
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
            ( ( System.ComponentModel.ISupportInitialize ) this.trackBar1 ).BeginInit();
            ( ( System.ComponentModel.ISupportInitialize ) this.trackBar2 ).BeginInit();
            ( ( System.ComponentModel.ISupportInitialize ) this.trackBar3 ).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new Font( "Constantia", 12F, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point, 0 );
            this.label1.ForeColor = Color.MediumSlateBlue;
            this.label1.Location = new Point( 64, 87 );
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
            this.label2.Location = new Point( 412, 89 );
            this.label2.Name = "label2";
            this.label2.Size = new Size( 95, 19 );
            this.label2.TabIndex = 1;
            this.label2.Text = "Aim Radius";
            // 
            // trackBar1
            // 
            this.trackBar1.BackColor = Color.MediumSlateBlue;
            this.trackBar1.Location = new Point( 375, 111 );
            this.trackBar1.Name = "trackBar1";
            this.trackBar1.Size = new Size( 158, 45 );
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
            this.radioButton1.Font = new Font( "Constantia", 14.25F, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point, 0 );
            this.radioButton1.ForeColor = Color.Aquamarine;
            this.radioButton1.Location = new Point( 199, 357 );
            this.radioButton1.Name = "radioButton1";
            this.radioButton1.Size = new Size( 182, 27 );
            this.radioButton1.TabIndex = 11;
            this.radioButton1.TabStop = true;
            this.radioButton1.Text = "Minimize To Tray";
            this.radioButton1.UseVisualStyleBackColor = false;
            this.radioButton1.CheckedChanged += this.radioButton1_CheckedChanged;
            // 
            // trackBar2
            // 
            this.trackBar2.BackColor = Color.MediumSlateBlue;
            this.trackBar2.Location = new Point( 30, 111 );
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
            this.label4.Location = new Point( 384, 232 );
            this.label4.Name = "label4";
            this.label4.Size = new Size( 123, 19 );
            this.label4.TabIndex = 17;
            this.label4.Text = "Color Selection";
            // 
            // comboBox2
            // 
            this.comboBox2.FormattingEnabled = true;
            this.comboBox2.Items.AddRange( new object[] { "orange", "red", "green", "yellow", "purple", "cyan" } );
            this.comboBox2.Location = new Point( 375, 254 );
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
            this.label5.Location = new Point( 64, 232 );
            this.label5.Name = "label5";
            this.label5.Size = new Size( 87, 19 );
            this.label5.TabIndex = 19;
            this.label5.Text = "Aim Delay";
            // 
            // trackBar3
            // 
            this.trackBar3.BackColor = Color.MediumSlateBlue;
            this.trackBar3.Location = new Point( 30, 254 );
            this.trackBar3.Name = "trackBar3";
            this.trackBar3.Size = new Size( 158, 45 );
            this.trackBar3.TabIndex = 20;
            this.trackBar3.TickStyle = TickStyle.TopLeft;
            this.trackBar3.Scroll += this.trackBar3_Scroll;
            // 
            // IceColorBot
            // 
            this.BackColor = Color.MidnightBlue;
            this.BackgroundImage = ( Image ) resources.GetObject( "$this.BackgroundImage" );
            this.BackgroundImageLayout = ImageLayout.Center;
            this.ClientSize = new Size( 563, 544 );
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
            this.Icon = ( Icon ) resources.GetObject( "$this.Icon" );
            this.KeyPreview = true;
            this.MaximumSize = new Size( 579, 583 );
            this.MinimumSize = new Size( 579, 583 );
            this.Name = "IceColorBot";
            ( ( System.ComponentModel.ISupportInitialize ) this.trackBar1 ).EndInit();
            ( ( System.ComponentModel.ISupportInitialize ) this.trackBar2 ).EndInit();
            ( ( System.ComponentModel.ISupportInitialize ) this.trackBar3 ).EndInit();
            this.ResumeLayout( false );
            this.PerformLayout();
        }

        private void trackBar1_Scroll( object sender, EventArgs e )
        {

            var rad = ( trackBar1.Value * 280 );
            PlayerData.SetAimRad( ref rad, ref screenCap );

#if DEBUG
            logger.Log( "Aim Radius: " + PlayerData.GetAimRad() );
#endif
        }


        private void button1_Click( object sender, EventArgs e )
        {
#if DEBUG
            screenCap = new ScreenCap( PlayerData.GetHwnd(), PlayerData.GetRect(),
                ref logger, PlayerData.GetAimRad(), colortolerances.GetColorTolerance() );
#else
            screenCap = new ScreenCap( PlayerData.GetHwnd(), PlayerData.GetRect(),
                PlayerData.GetAimRad(), colortolerances.GetColorTolerance() );
#endif

            screenCap.Start();

#if DEBUG
            aimBot.Start( ref logger, ref screenCap, PlayerData.GetRect() );
#else
            aimBot.Start( ref screenCap, PlayerData.GetRect() );
#endif
        }

        private void button2_Click( object sender, EventArgs e )
        {
            aimBot.Stop();
            screenCap.Stop();
        }

        private void comboBox1_SelectedIndexChanged( object sender, EventArgs e )
        {
            var key = 0;
            switch ( comboBox1.SelectedIndex )
            {
                default:
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
            }

#if DEBUG
            logger.Log( "Aim Key: " + aimBot.aimKey );
#endif
        }


        private void Exit( object sender, EventArgs e )
        {
            trayIcon.Visible = false;

            aimBot.Dispose();
            screenCap.Dispose();
            logger.Dispose();
            colortolerances.Dispose();
            activeGameCancellation.Cancel();
            isGameActive.Join();
            activeGameCancellation.Dispose();
            UnsubToHotKey();
            Application.Exit();
        }

        private void ReOpen( object sender, EventArgs e )
        {
            trayIcon.Visible = false;
            Show();

#if DEBUG
            logger.Log( "Maximized From Tray" );
#endif
        }

        private void radioButton1_CheckedChanged( object sender, EventArgs e )
        {
            trayIcon.Visible = true;

            if ( radioButton1.Checked )
            {
#if DEBUG
                logger.Log( "Minimized to tray" );
#endif
                Hide();
                radioButton1.Checked = false;
            }
        }

        private void trackBar2_Scroll( object sender, EventArgs e )
        {
            var aimSpeed = trackBar2.Value * 10;
            PlayerData.SetAimSpeed( ref aimSpeed, ref aimBot );
#if DEBUG
            logger.Log( "Aim Speed: " + aimSpeed );
#endif
        }

        private void GameCheck()
        {
            nint firstHwnd = nint.Zero;
            while ( !activeGameCancellation.Token.IsCancellationRequested )
            {
                firstHwnd = WinApi.FindWindow();
                if ( firstHwnd == PlayerData.GetHwnd() )
                {
                    Thread.Sleep( 10000 );
                    continue;
                }
                if ( firstHwnd != nint.MaxValue )
                {
                    PlayerData.SetHwnd( ref firstHwnd, ref screenCap );

                    if ( !WinApi.GetWindowRect( PlayerData.GetHwnd(), ref PlayerData.RefRect() ) )
                    {
                        throw new Exception( "Error getting window rect" );
                    }
#if DEBUG
                    logger.Log( "Game is active with HWND: " + PlayerData.GetHwnd() );
#endif
                }
                Thread.Sleep( 10000 );

                if ( firstHwnd == nint.MaxValue && PlayerData.GetHwnd() != nint.MaxValue )
                {

                    PlayerData.SetHwnd( ref firstHwnd, ref screenCap );
                    aimBot.Dispose();
                    screenCap.Dispose();
#if DEBUG
                    logger.Log( "Game is not active" );
#endif
                }

            }
        }

        private void comboBox2_SelectedIndexChanged( object sender, EventArgs e )
        {
            switch ( comboBox2.SelectedIndex )
            {
                default:
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
            }

            var color = colortolerances.GetColorTolerance();
            PlayerData.SetColorRange( ref color, ref screenCap );

#if DEBUG
            logger.Log( "Color Selection: " + colortolerances.GetColorName( PlayerData.GetColorRange() ) );
#endif
        }

        private void trackBar3_Scroll( object sender, EventArgs e )
        {
            var aimDelay = trackBar3.Value;
            PlayerData.SetAimDelay( ref aimDelay, ref aimBot );

#if DEBUG
            logger.Log( "Aim Delay: " + PlayerData.GetAimDelay() );
#endif
        }


        private void GlobalHookKeyDown( object sender, KeyEventArgs e )
        {
            if ( e.KeyCode == Keys.Insert &&
                trayIcon.Visible )
            {
                trayIcon.Visible = false;
                Show();
            } else
            {
                trayIcon.Visible = true;
                Hide();
            }
        }


        private void SubToHotKey()
        {
            keyboardEvents = Hook.GlobalEvents();
            keyboardEvents.KeyDown += GlobalHookKeyDown;
        }


        private void UnsubToHotKey()
        {
            keyboardEvents.KeyDown -= GlobalHookKeyDown;
            keyboardEvents.Dispose();
        }


        protected override void OnFormClosed( FormClosedEventArgs e )
        {
            if ( aimBot != null )
            {
                aimBot.Dispose();
            }

            if ( screenCap != null )
            {
                screenCap.Dispose();
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
                activeGameCancellation.Dispose();
                isGameActive.Join();
            }

            UnsubToHotKey();
            base.OnFormClosed( e );
        }
    }






    static class PlayerData
    {
        private static readonly object locker = new object();
        private static int localAimDelay = 0;
        private static int localAimSpeed = 15;
        private static int localAimRad = 15;
        private static int localAimKey = 0x06;
        private static nint localHWnd = nint.MaxValue;
        private static PInvoke.RECT localRect = new();
        private static List<IEnumerable<int>> localColorRange = new List<IEnumerable<int>>();


        public static void SetAimDelay( ref int aimDelay, ref AimBot aimBot )
        {
            lock ( locker )
            {
                localAimDelay = aimDelay;
                if ( aimBot != null &&
                    aimBot.GetAimDelay() != localAimDelay )
                {
                    aimBot.SetAimDelay( ref aimDelay );
                }
            }
        }


        public static int GetAimDelay()
        {
            lock ( locker )
            {
                return localAimDelay;
            }
        }


        public static void SetAimSpeed( ref int aimSpeed, ref AimBot aimBot )
        {
            lock ( locker )
            {
                localAimSpeed = aimSpeed;
                if ( aimBot != null &&
                    aimBot.GetAimSpeed() != localAimSpeed )
                {
                    aimBot.SetAimSpeed( ref aimSpeed );
                }
            }
        }


        public static int GetAimSpeed()
        {
            lock ( locker )
            {
                return localAimSpeed;
            }
        }


        public static void SetAimRad( ref int aimRad, ref ScreenCap screenCap )
        {
            lock ( locker )
            {
                localAimRad = aimRad;
                if ( screenCap != null &&
                    screenCap.GetScanRadius() != localAimRad )
                {
                    screenCap.SetScanRadius( ref aimRad );
                }
            }
        }


        public static int GetAimRad()
        {
            lock ( locker )
            {
                return localAimRad;
            }
        }


        public static void SetAimKey( ref int aimKey, ref AimBot aimBot )
        {
            lock ( locker )
            {
                localAimKey = aimKey;
                if ( aimBot != null &&
                    aimBot.GetAimKey() != localAimKey )
                {
                    aimBot.SetAimKey( ref aimKey );
                }
            }
        }

        public static int GetAimKey()
        {
            lock ( locker )
            {
                return localAimKey;
            }
        }

        public static void SetHwnd( ref nint hWnd, ref ScreenCap screenCap )
        {
            lock ( locker )
            {
                localHWnd = hWnd;
                if ( screenCap != null &&
                    screenCap.GetHwnd() != localHWnd )
                {
                    screenCap.SetHwnd( ref hWnd );
                }
            }
        }

        public static nint GetHwnd()
        {
            lock ( locker )
            {
                return localHWnd;
            }
        }


        public static void SetRect( ref PInvoke.RECT rect, ref ScreenCap screenCap, ref AimBot aimBot )
        {
            lock ( locker )
            {
                localRect = rect;
                var currentRect = screenCap.GetRect();
                if ( screenCap != null &&
                    currentRect.left != rect.left ||
                    currentRect.right != rect.right ||
                    currentRect.top != rect.top ||
                    currentRect.bottom != rect.bottom )
                {
                    screenCap.SetRect( ref rect );

                    if ( aimBot != null )
                    {
                        aimBot.SetRect( ref rect );
                    }
                }
            }
        }

        public static PInvoke.RECT GetRect()
        {
            lock ( locker )
            {
                return localRect;
            }
        }

        public static ref PInvoke.RECT RefRect()
        {
            lock ( locker )
            {
                return ref localRect;
            }
        }

        public static void SetColorRange( ref List<IEnumerable<int>> colorRange, ref ScreenCap screenCap )
        {
            lock ( locker )
            {
                localColorRange = colorRange;

                if ( screenCap != null &&
                    screenCap.GetColorRange() != localColorRange )
                {
                    screenCap.SetColorRange( ref colorRange );
                }
            }
        }

        public static List<IEnumerable<int>> GetColorRange()
        {
            lock ( locker )
            {
                return localColorRange;
            }
        }
    }
}
