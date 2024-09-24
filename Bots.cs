using static SCB.EnemyScanner;

namespace SCB
{


    /// <summary>
    /// class to handle aimbot functionality
    /// </summary>
    internal class AimBot
    {
        private bool disposed = false;
        private readonly object locker = new object();
        private int aimSpeed = 1;
        private int aimDelay = 1;
        private int aimKey = 0x01;
        private PInvoke.RECT gameRect;
        private Point centerOfGameWindow;
        private AimLocation aimLocation;


        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private Thread aimBotThread;
        public Logger logger;



        /// <summary>
        /// winapi function to check if the aim key is pressed
        /// </summary>
        /// <returns></returns>
        private bool IsAiming()
        {
            if ( ( WinApi.GetAsyncKeyState( this.aimKey ) & 0x8000 ) > 0 )
            {
                return true;
            }
            return false;
        }


        /// <summary>
        /// calculates the distance between the current mouse position and the target position
        /// </summary>
        /// <param name="targetPos"></param>
        private void AimAtTarget( Point targetPos )
        {
            targetPos.X -= this.centerOfGameWindow.X;
            targetPos.Y -= this.centerOfGameWindow.Y;

            int distance = ( int ) Math.Sqrt( Math.Pow( targetPos.X, 2 ) + Math.Pow( targetPos.Y, 2 ) );
            if ( distance == 0 )
            {
                return;
            }

            int szSteps = Math.Max( 1, distance / 10 ) * this.aimSpeed;
            int stepCount = ( distance / szSteps ) + 1;

            double stepX = ( double ) targetPos.X / stepCount;
            double stepY = ( double ) targetPos.Y / stepCount;

            for ( int i = 0; i < stepCount; i++ )
            {
                int x = this.centerOfGameWindow.X + ( int ) ( stepX * i );
                int y = this.centerOfGameWindow.Y + ( int ) ( stepY * i );

                MouseInput.MoveAbsoluteMouse( x, y );
                Thread.Sleep( ( int ) 0.01 / this.aimSpeed );
            }
        }


        /// <summary>
        /// main loop for the aimbot
        /// </summary>
        public void StartAimBot()
        {
            while ( !this.cancellation.Token.IsCancellationRequested )
            {
                Bitmap? screenCap = null;
                Task captureAndFilter = Task.Run( () => ScreenCap.CaptureAndFilter( out Bitmap screenCap ) );
                captureAndFilter.Wait();

                if ( screenCap == null )
                {
                    continue;
                }

                Point targetPos = new Point();
                Task findEnemy = Task.Run( () => EnemyScanner.ScanForEnemy( ref screenCap, ref this.centerOfGameWindow, this.aimLocation, out targetPos ) );
                findEnemy.Wait();

                if ( targetPos.X == -1 && targetPos.Y == -1 )
                {
                    continue;
                }

                if ( this.IsAiming() )
                {

#if DEBUG
                    //this swamps the loggers output, only for emergency debugging
                    //logger.Log( "Aimbot Target at: " + targetPos.X + ", " + targetPos.Y );
#endif
                    Thread.Sleep( this.aimDelay );
                    AimAtTarget( targetPos );
                }

            }
        }


        /// <summary>
        /// main entry point for the aimbot
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="screenCap"></param>
#if DEBUG
        public void Start( ref Logger logger, PInvoke.RECT rect )
        {
            this.logger = logger;
            this.gameRect = rect;

            this.centerOfGameWindow = new Point
            {
                X = this.gameRect.left + ( this.gameRect.right - this.gameRect.left ) / 2,
                Y = this.gameRect.top + ( this.gameRect.bottom - this.gameRect.top ) / 2
            };

            logger.Log( "Settings: " );
            logger.Log( "Aim Speed: " + this.aimSpeed );
            logger.Log( "Aim Delay: " + this.aimDelay );
            logger.Log( "Aim Key: " + this.aimKey );

            this.aimBotThread = new Thread( StartAimBot );
            this.aimBotThread.Start();


            logger.Log( "Aimbot started" );
        }
#else
        public void Start( PInvoke.RECT rect )
        {

            this.gameRect = rect;

            this.aimBotThread = new Thread( StartAimBot );
            this.aimBotThread.Start();
        }
#endif



        public void SetAimSpeed( ref int aimSpeed )
        {
            lock ( locker )
            {
                this.aimSpeed = aimSpeed;
            }
        }


        public void SetAimDelay( ref int aimDelay )
        {
            lock ( locker )
            {
                this.aimDelay = aimDelay;
            }
        }


        public void SetAimKey( ref int aimKey )
        {
            lock ( locker )
            {
                this.aimKey = aimKey;
            }
        }



        public int GetAimSpeed()
        {
            lock ( locker )
            {
                return this.aimSpeed;
            }
        }



        public int GetAimDelay()
        {
            lock ( locker )
            {
                return this.aimDelay;
            }
        }



        public int GetAimKey()
        {
            lock ( locker )
            {
                return this.aimKey;
            }
        }

        public void SetRect( ref PInvoke.RECT gameRect )
        {
            lock ( locker )
            {
                this.gameRect = gameRect;
            }
        }

        public PInvoke.RECT GetRect()
        {
            lock ( locker )
            {
                return this.gameRect;
            }
        }

        public void SetAimLocation( ref AimLocation aimLocation )
        {
            lock ( locker )
            {
                this.aimLocation = aimLocation;
            }
        }

        public AimLocation GetAimLocation()
        {
            lock ( locker )
            {
                return this.aimLocation;
            }
        }


        /// <summary>
        /// stops the aimbot
        /// </summary>
        public void Stop()
        {
            this.cancellation.Cancel();
            this.aimBotThread.Join();

#if DEBUG
            logger.Log( "Aimbot stopped" );
#endif
        }



        public void Dispose()
        {
            Dispose( true );
            GC.SuppressFinalize( this );
        }

        protected virtual void Dispose( bool disposing )
        {
            if ( !disposed &&
                disposing &&
                this.aimBotThread != null &&
                this.aimBotThread.IsAlive )
            {

                this.cancellation.Cancel();
                this.aimBotThread.Join();
                this.cancellation.Dispose();
            }
            disposed = true;
        }
    }




}


