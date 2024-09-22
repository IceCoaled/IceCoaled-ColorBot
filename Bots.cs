namespace SCB
{


    /// <summary>
    /// class to handle aimbot functionality
    /// </summary>
    internal class AimBot
    {
        private bool disposed = false;
        private readonly object locker = new object();
        public int aimSpeed = 1;
        public int aimDelay = 1;
        public int aimKey = 0x01;
        private PInvoke.RECT gameRect;


        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private Thread aimBotThread;
        private ScreenCap screenCap;
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
            Point centerOfGameWindow = new Point
            {
                X = this.gameRect.left + ( this.gameRect.right - this.gameRect.left ) / 2,
                Y = this.gameRect.top + ( this.gameRect.bottom - this.gameRect.top ) / 2
            };

            int deltaX = targetPos.X - centerOfGameWindow.X;
            int deltaY = targetPos.Y - centerOfGameWindow.Y;

            SmoothAim( new Point { X = deltaX, Y = deltaY } );
        }


        /// <summary>
        /// smooth aim function, uses the aimSpeed to calculate the steps needed to reach the target
        /// </summary>
        /// <param name="distance"></param>
        private void SmoothAim( Point distance )
        {
            //smooth aim based on aim speed
            int steps = this.aimSpeed;
            int stepX = distance.X / steps;
            int stepY = distance.Y / steps;

            for ( int i = 0; i < steps; i++ )
            {
                MouseInput.MoveRelativeMouse( stepX, stepY );
            }
        }



        /// <summary>
        /// main loop for the aimbot
        /// </summary>
        public void StartAimBot()
        {
            Point targetPos;
            while ( !this.cancellation.Token.IsCancellationRequested )
            {

                if ( this.IsAiming() )
                {
                    targetPos = this.screenCap.targetPos.GetPosStack();
                    if ( targetPos.X < 1 && targetPos.Y < 1 )
                    {
                        continue;
                    }
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
        public void Start( ref Logger logger, ref ScreenCap screenCap, PInvoke.RECT rect )
        {
            this.logger = logger;
            this.screenCap = screenCap;
            this.gameRect = rect;

            logger.Log( "Settings: " );
            logger.Log( "Aim Speed: " + this.aimSpeed );
            logger.Log( "Aim Delay: " + this.aimDelay );
            logger.Log( "Aim Key: " + this.aimKey );

            this.aimBotThread = new Thread( StartAimBot );
            this.aimBotThread.Start();


            logger.Log( "Aimbot started" );
        }
#else
        public void Start( ref ScreenCap screenCap, PInvoke.RECT rect )
        {
            this.screenCap = screenCap;
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


        /// <summary>
        /// stops the aimbot
        /// </summary>
        public void Stop()
        {
            this.cancellation.Cancel();
            this.aimBotThread.Join();
            this.screenCap.Stop();

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


