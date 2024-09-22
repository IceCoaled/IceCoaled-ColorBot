namespace SCB
{
    /// <summary>
    /// class to handle color tolerances for color filtering
    /// </summary>
    class ColorTolerances : IDisposable
    {
        private bool disposed = false;
        private List<IEnumerable<int>> orange;
        private List<IEnumerable<int>> red;
        private List<IEnumerable<int>> green;
        private List<IEnumerable<int>> cyan;
        private List<IEnumerable<int>> yellow;
        private List<IEnumerable<int>> purple;
        private List<IEnumerable<int>> skinTones;
        private List<IEnumerable<int>> plateCarriers;

        private readonly Logger logger;
        private Dictionary<string, List<IEnumerable<int>>> colorDictonary;

        private List<IEnumerable<int>> selected;
        private Mutex selectedMutex;

#if DEBUG
        public ColorTolerances( ref Logger logger )
        {
            this.logger = logger;
            SetupColorTolerances();
            this.selectedMutex = new Mutex();
        }
#else
        public ColorTolerances()
        {
            SetupColorTolerances();
            this.selectedMutex = new Mutex();
        }
#endif


        ~ColorTolerances()
        {
            Dispose( false );
        }



        /// <summary>
        /// sets up the color tolerances for the different color groups
        /// </summary>
        /// <exception cref="Exception"></exception>
        private void SetupColorTolerances()
        {
            IEnumerable<int> orangeB = Enumerable.Range( 75, 108 );
            IEnumerable<int> orangeG = Enumerable.Range( 140, 244 );
            IEnumerable<int> orangeR = Enumerable.Range( 244, 255 );
            this.orange = [ orangeR, orangeG, orangeB ];
            if ( orange == null )
            {
                throw new Exception( "Orange is null" );
            } else
            {
#if DEBUG
                this.logger.Log( "Orange tolerances Set" );
#endif
            }

            IEnumerable<int> redB = Enumerable.Range( 80, 135 );
            IEnumerable<int> redG = Enumerable.Range( 100, 130 );
            IEnumerable<int> redR = Enumerable.Range( 247, 255 );
            this.red = [ redR, redG, redB ];
            if ( red == null )
            {
                throw new Exception( "Red is null" );
            } else
            {
#if DEBUG
                this.logger.Log( "Red tolerances Set" );
#endif
            }

            IEnumerable<int> greenB = Enumerable.Range( 30, 97 );
            IEnumerable<int> greenG = Enumerable.Range( 240, 255 );
            IEnumerable<int> greenR = Enumerable.Range( 30, 110 );
            this.green = [ greenR, greenG, greenB ];
            if ( green == null )
            {
                throw new Exception( "Green is null" );
            } else
            {
#if DEBUG
                this.logger.Log( "Green tolerances Set" );
#endif
            }

            IEnumerable<int> cyanB = Enumerable.Range( 246, 255 );
            IEnumerable<int> cyanG = Enumerable.Range( 246, 255 );
            IEnumerable<int> cyanR = Enumerable.Range( 66, 100 );
            this.cyan = [ cyanR, cyanG, cyanB ];
            if ( cyan == null )
            {
                throw new Exception( "Cyan is null" );
            } else
            {
#if DEBUG
                this.logger.Log( "Cyan tolerances Set" );
#endif
            }

            IEnumerable<int> yellowB = Enumerable.Range( 78, 133 );
            IEnumerable<int> yellowG = Enumerable.Range( 237, 255 );
            IEnumerable<int> yellowR = Enumerable.Range( 237, 255 );
            this.yellow = [ yellowR, yellowG, yellowB ];
            if ( yellow == null )
            {
                throw new Exception( "Yellow is null" );
            } else
            {
#if DEBUG
                this.logger.Log( "Yellow tolerances Set" );
#endif
            }

            IEnumerable<int> purpleB = Enumerable.Range( 144, 255 );
            IEnumerable<int> purpleG = Enumerable.Range( 60, 99 );
            IEnumerable<int> purpleR = Enumerable.Range( 194, 255 );
            this.purple = [ purpleR, purpleG, purpleB ];
            if ( purple == null )
            {
                throw new Exception( "Purple is null" );
            } else
            {
#if DEBUG
                this.logger.Log( "Purple tolerances Set" );
#endif
            }

            IEnumerable<int> skinToneB = Enumerable.Range( 69, 160 );
            IEnumerable<int> skinToneG = Enumerable.Range( 85, 175 );
            IEnumerable<int> skinToneR = Enumerable.Range( 148, 255 );
            this.skinTones = [ skinToneR, skinToneG, skinToneB ];
            if ( skinTones == null )
            {
                throw new Exception( "SkinTones is null" );
            } else
            {
#if DEBUG
                this.logger.Log( "SkinTones tolerances Set" );
#endif
            }
            IEnumerable<int> plateCarrierB = Enumerable.Range( 110, 177 );
            IEnumerable<int> plateCarrierG = Enumerable.Range( 120, 190 );
            IEnumerable<int> plateCarrierR = Enumerable.Range( 135, 200 );
            this.plateCarriers = [ plateCarrierR, plateCarrierG, plateCarrierB ];
            if ( plateCarriers == null )
            {
                throw new Exception( "PlateCarriers is null" );
            } else
            {
#if DEBUG
                this.logger.Log( "PlateCarriers tolerances Set" );
#endif

                this.colorDictonary = new Dictionary<string, List<IEnumerable<int>>>();
                this.colorDictonary.Add( "orange", orange );
                this.colorDictonary.Add( "red", red );
                this.colorDictonary.Add( "green", green );
                this.colorDictonary.Add( "cyan", cyan );
                this.colorDictonary.Add( "yellow", yellow );
                this.colorDictonary.Add( "purple", purple );
                this.colorDictonary.Add( "skinTones", skinTones );
                this.colorDictonary.Add( "plateCarriers", plateCarriers );
            }
        }


        public List<IEnumerable<int>> GetColorTolerance( string color )
        {
            if ( colorDictonary.ContainsKey( color ) )
            {
                return colorDictonary[ color ];
            } else
            {
                throw new Exception( "Color not found" );
            }
        }


        public List<IEnumerable<int>> GetColorTolerance()
        {
            this.selectedMutex.WaitOne();
            var result = this.selected;
            this.selectedMutex.ReleaseMutex();
            return result;
        }

        public void SetColorTolerance( string color )
        {
            this.selectedMutex.WaitOne();

            if ( colorDictonary.ContainsKey( color ) )
            {
                this.selected = colorDictonary[ color ];
            } else
            {
                this.selectedMutex.ReleaseMutex();
                throw new Exception( "Color not found" );
            }

            this.selectedMutex.ReleaseMutex();
        }


        public string GetColorName( List<IEnumerable<int>> color )
        {
            foreach ( var item in colorDictonary )
            {
                if ( item.Value == color )
                {
                    return item.Key;
                }
            }
            return "Color not found";
        }



        public void Dispose()
        {
            Dispose( true );
            GC.SuppressFinalize( this );
        }

        protected virtual void Dispose( bool disposing )
        {
            if ( !disposed &&
                disposing )
            {
                this.orange.Clear();
                this.red.Clear();
                this.green.Clear();
                this.cyan.Clear();
                this.yellow.Clear();
                this.purple.Clear();
                this.skinTones.Clear();
                this.plateCarriers.Clear();
                this.colorDictonary.Clear();
            }

            disposed = true;
        }

    }
}
