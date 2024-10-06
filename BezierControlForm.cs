namespace SCB
{
    internal partial class BezierControlForm : Form
    {
        private PointF controlPoint1, controlPoint2, startPoint, endPoint;
        private bool isDragging = false;
        private string dragPointName = "";

        internal BezierControlForm()
        {
            InitializeComponent();

            // Initialize control points and set the start and end points to static positions
            startPoint = new PointF( 50, this.Height / 2 );  // Fixed on the left side
            endPoint = new PointF( this.Width - 50, this.Height / 2 ); // Fixed on the right side

            // Proportional initial positions for control points
            ControlPoint1 = new PointF(
                startPoint.X + ( endPoint.X - startPoint.X ) * 0.3f,  // 30% along the X-axis
                startPoint.Y - ( ( endPoint.X - startPoint.X ) * 0.1f ) // Slight arc upwards
            );
            ControlPoint2 = new PointF(
                startPoint.X + ( endPoint.X - startPoint.X ) * 0.6f,  // 60% along the X-axis
                startPoint.Y + ( ( endPoint.X - startPoint.X ) * -.1f ) // Slight arc downwards
            );


            this.DoubleBuffered = true; // Reduces flickering
            this.MouseDown += BezierControlForm_MouseDown!;
            this.MouseMove += BezierControlForm_MouseMove!;
            this.MouseUp += BezierControlForm_MouseUp!;
            this.Paint += BezierControlForm_Paint!;
        }

        private void BezierControlForm_MouseDown( object? sender, MouseEventArgs e )
        {
            // Allow dragging only for the control points
            if ( IsPointNear( e.Location, ControlPoint1 ) )
            {
                isDragging = true;
                dragPointName = "controlPoint1";
            } else if ( IsPointNear( e.Location, ControlPoint2 ) )
            {
                isDragging = true;
                dragPointName = "controlPoint2";
            } else if ( IsPointNear( e.Location, StartPoint ) )
            {
                isDragging = true;
                dragPointName = "startPoint";
            } else if ( IsPointNear( e.Location, EndPoint ) )
            {
                isDragging = true;
                dragPointName = "endPoint";
            }
        }

        private void BezierControlForm_MouseMove( object? sender, MouseEventArgs e )
        {
            if ( isDragging )
            {
                // Move only the control points, with optional clamping to prevent them from going off-screen
                switch ( dragPointName )
                {
                    case "controlPoint1":
                    ControlPoint1 = ClampToBounds( new PointF( e.X, e.Y ) ); // Clamp to bounds if necessary
                    UserSelected = true;
                    break;
                    case "controlPoint2":
                    ControlPoint2 = ClampToBounds( new PointF( e.X, e.Y ) ); // Clamp to bounds if necessary
                    UserSelected = true;
                    break;
                    case "startPoint":
                    StartPoint = ClampToBounds( new PointF( e.X, e.Y ) ); // Clamp to bounds if necessary
                    UserSelected = true;
                    break;
                    case "endPoint":
                    EndPoint = ClampToBounds( new PointF( e.X, e.Y ) ); // Clamp to bounds if necessary
                    UserSelected = true;
                    break;
                }
            }
        }


        private PointF ClampToBounds( PointF point )
        {
            float clampedX = Math.Max( 0, Math.Min( this.Width, point.X ) );
            float clampedY = Math.Max( 0, Math.Min( this.Height, point.Y ) );
            return new PointF( clampedX, clampedY );
        }

        private void BezierControlForm_MouseUp( object? sender, MouseEventArgs e )
        {
            isDragging = false;
            dragPointName = "";
        }

        private static bool IsPointNear( PointF point1, PointF point2, float radius = 10 )
        {
            return ( Math.Abs( point1.X - point2.X ) < radius && Math.Abs( point1.Y - point2.Y ) < radius );
        }

        private void BezierControlForm_Paint( object? sender, PaintEventArgs e )
        {
            Graphics g = e.Graphics;

            // Clear the background (important to prevent overlap)
            g.Clear( this.BackColor );

            Pen curvePen = new( Color.DarkViolet, 2 );
            Pen linePen = new( Color.Black, 1 );
            Brush controlPointBrush = new SolidBrush( Color.Blue );
            Brush startPointBrush = new SolidBrush( Color.Green );
            Brush endPointBrush = new SolidBrush( Color.Red );

            // Smaller grid lines for better control
            for ( int i = 0; i < this.Width; i += 25 )
                g.DrawLine( Pens.LightGray, new Point( i, 0 ), new Point( i, this.Height ) );
            for ( int i = 0; i < this.Height; i += 25 )
                g.DrawLine( Pens.LightGray, new Point( 0, i ), new Point( this.Width, i ) );

            // Represent the curve as if it’s moving toward the target (head-on perspective)
            PointF adjustedControlPoint1 = new PointF(
                StartPoint.X + ( ControlPoint1.X - StartPoint.X ) * 1.0f, // Keep the X influence strong
                StartPoint.Y + ( ControlPoint1.Y - StartPoint.Y ) * 1.0f   // Strong Y influence for head-on view
            );
            PointF adjustedControlPoint2 = new PointF(
                StartPoint.X + ( ControlPoint2.X - StartPoint.X ) * 1.0f,
                StartPoint.Y + ( ControlPoint2.Y - StartPoint.Y ) * 1.0f
            );

            // Draw the adjusted Bezier curve
            g.DrawBezier( curvePen, StartPoint, adjustedControlPoint1, adjustedControlPoint2, EndPoint );

            // Draw control lines between points (visual feedback)
            g.DrawLine( linePen, StartPoint, adjustedControlPoint1 );
            g.DrawLine( linePen, adjustedControlPoint1, adjustedControlPoint2 );
            g.DrawLine( linePen, adjustedControlPoint2, EndPoint );

            // Draw control points
            g.FillEllipse( startPointBrush, StartPoint.X - 5, StartPoint.Y - 5, 10, 10 );
            g.FillEllipse( endPointBrush, EndPoint.X - 5, EndPoint.Y - 5, 10, 10 );
            g.FillEllipse( controlPointBrush, adjustedControlPoint1.X - 5, adjustedControlPoint1.Y - 5, 10, 10 );
            g.FillEllipse( controlPointBrush, adjustedControlPoint2.X - 5, adjustedControlPoint2.Y - 5, 10, 10 );

            if ( UserSelected )
            {
                PlayerData.SetBezierPoints( ref startPoint, ref controlPoint1, ref controlPoint2, ref endPoint );
            }
        }

        protected override void OnFormClosed( FormClosedEventArgs e )
        {
            base.OnFormClosed( e );
            PlayerData.SetBezierPoints( ref startPoint, ref controlPoint1, ref controlPoint2, ref endPoint );
        }

        // Methods to return the control points and start/end points

        internal PointF ControlPoint1
        {
            get { return controlPoint1; }
            set
            {
                controlPoint1 = value;
                Invalidate(); // Redraw the form with the updated control points
            }
        }

        internal PointF ControlPoint2
        {
            get { return controlPoint2; }
            set
            {
                controlPoint2 = value;
                Invalidate(); // Redraw the form with the updated control points
            }
        }

        internal PointF StartPoint
        {
            get { return startPoint; }
            set
            {
                startPoint = value;
                Invalidate(); // Redraw the form with the updated start point
            }
        }

        internal PointF EndPoint
        {
            get { return endPoint; }
            set
            {
                endPoint = value;
                Invalidate(); // Redraw the form with the updated end point
            }
        }

        internal bool UserSelected { get; set; } = false;
    }
}
