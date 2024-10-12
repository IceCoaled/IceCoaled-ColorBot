using System.Drawing.Drawing2D;

namespace SCB
{
    internal partial class Bezier : MaterialSkin.Controls.MaterialForm
    {
        private PointF controlPoint1, controlPoint2, startPoint, endPoint;
        private bool isDragging = false;
        private string dragPointName = "";


        internal Bezier()
        {


            //// Initialize MaterialSkinManager
            //var materialSkinManager = MaterialSkin.MaterialSkinManager.Instance;
            //materialSkinManager.AddFormToManage( this );
            //materialSkinManager.Theme = MaterialSkin.MaterialSkinManager.Themes.DARK;  // Change to DARK or LIGHT

            //// Customize the color scheme (primary, accent, text shade)
            //materialSkinManager.ColorScheme = new MaterialSkin.ColorScheme(
            //    MaterialSkin.Primary.Purple500, MaterialSkin.Primary.Purple600,
            //    MaterialSkin.Primary.Yellow400, MaterialSkin.Accent.Yellow200,
            //    MaterialSkin.TextShade.WHITE );

            InitializeComponent();

            this.FormBorderStyle = FormBorderStyle.None;
            this.Sizable = false;

            // Hook up event handlers
            this.Load += Bezier_Load;
            this.MouseDown += BezierControlForm_MouseDown!;
            this.MouseMove += BezierControlForm_MouseMove!;
            this.MouseUp += BezierControlForm_MouseUp!;
            this.Paint += BezierControlForm_Paint!;
        }


        private void Bezier_Load( object? sender, EventArgs e )
        {
            // Initialize MaterialSkinManager
            var materialSkinManager = MaterialSkin.MaterialSkinManager.Instance;
            materialSkinManager.AddFormToManage( this );
            materialSkinManager.Theme = MaterialSkin.MaterialSkinManager.Themes.DARK;  // Change to DARK or LIGHT

            // Customize the color scheme (primary, accent, text shade)
            materialSkinManager.ColorScheme = new MaterialSkin.ColorScheme(
                MaterialSkin.Primary.Purple500, MaterialSkin.Primary.Purple600,
                MaterialSkin.Primary.Yellow400, MaterialSkin.Accent.Pink400,
                MaterialSkin.TextShade.WHITE );

            // Initialize control points and set the start and end points to static positions
            startPoint = new PointF( 50, this.Height / 2 );  // Fixed on the left side
            endPoint = new PointF( this.Width - 50, this.Height / 2 ); // Fixed on the right side

            // Proportional initial positions for control points
            controlPoint1 = new PointF(
                startPoint.X + ( endPoint.X - startPoint.X ) * 0.3f,  // 30% along the X-axis
                startPoint.Y - ( ( endPoint.X - startPoint.X ) * 0.1f ) // Slight arc upwards
            );
            controlPoint2 = new PointF(
                startPoint.X + ( endPoint.X - startPoint.X ) * 0.6f,  // 60% along the X-axis
                startPoint.Y + ( ( endPoint.X - startPoint.X ) * -0.1f ) // Slight arc downwards
            );
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

            // Use the form's existing background color (provided by MaterialSkin)
            Color backgroundColor = Color.WhiteSmoke;

            // Define a margin for the top bar (adjust to leave room for the close/minimize buttons)
            int topMargin = 25;

            // Set the clipping region to exclude the top margin area
            Rectangle drawingArea = new Rectangle( 0, topMargin, this.Width, this.Height - topMargin );
            g.SetClip( drawingArea );

            // Clear the background with the form's current background color
            g.Clear( backgroundColor );


            // Set pens and brushes
            Pen curvePen = new( Color.DarkViolet, 3 );
            Pen linePen = new( Color.CadetBlue, 2 );
            Brush controlPointBrush = new SolidBrush( Color.Blue );
            Brush startPointBrush = new SolidBrush( Color.Green );
            Brush endPointBrush = new SolidBrush( Color.Red );

            // Draw grid lines within the adjusted drawing area
            for ( int i = 0; i < this.Width; i += 25 )
            {
                g.DrawLine( Pens.DarkGray, new Point( i, topMargin ), new Point( i, this.Height ) );
            }

            for ( int i = topMargin; i < this.Height; i += 25 )
            {
                g.DrawLine( Pens.DarkGray, new Point( 0, i ), new Point( this.Width, i ) );
            }

            // Represent the curve as if it’s moving toward the target (head-on perspective)
            PointF adjustedControlPoint1 = new(
                StartPoint.X + ( ControlPoint1.X - StartPoint.X ) * 1.0f,
                StartPoint.Y + ( ControlPoint1.Y - StartPoint.Y ) * 1.0f
            );
            PointF adjustedControlPoint2 = new(
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
                PlayerData.SetBezierPoints( startPoint, controlPoint1, controlPoint2, endPoint );
            }

            // Reset the clipping region to default
            g.ResetClip();
        }


        protected override void OnPaint( PaintEventArgs e )
        {
            base.OnPaint( e );

            // Create a new rectangle with a modified Y position (15 pixels down from the top)
            var clientRect = new Rectangle( this.ClientRectangle.X, this.ClientRectangle.Y + 20,
                                           this.ClientRectangle.Width, this.ClientRectangle.Height - 15 );

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

            BezierControlForm_Paint( null, e );
        }


        protected override void OnFormClosed( FormClosedEventArgs e )
        {
            base.OnFormClosed( e );
            PlayerData.SetBezierPoints( startPoint, controlPoint1, controlPoint2, endPoint );
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
