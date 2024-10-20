using System.Drawing.Drawing2D;

namespace SCB
{
    internal partial class Bezier : MaterialSkin.Controls.MaterialForm
    {
        internal bool UserSelected { get; set; } = false;

        private Utils.BezierPointCollection bezierPoints;
        private bool isDragging = false;
        private string dragPointName = "";


        internal Bezier()
        {

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
            materialSkinManager.Theme = MaterialSkin.MaterialSkinManager.Themes.DARK;

            // Customize the color scheme (primary, accent, text shade)
            materialSkinManager.ColorScheme = new MaterialSkin.ColorScheme(
                MaterialSkin.Primary.Purple500, MaterialSkin.Primary.Purple600,
                MaterialSkin.Primary.Yellow400, MaterialSkin.Accent.Pink400,
                MaterialSkin.TextShade.WHITE );

            // Initialize the start and end points to static positions
            PointF startPoint = new( this.Width / 2, this.Height - 50 );
            PointF endPoint = new( this.Width / 2, 50 );

            List<PointF> controlPoints = new();

            if ( PlayerData.BezierControlPointsSet() )
            {
                bezierPoints = PlayerData.GetBezierPoints();
            } else
            {
                // Initialize default control points
                controlPoints = new()
                {
                    new PointF(startPoint.X - 30, startPoint.Y - 50),  // A slight nudge to the left and up
                    new PointF(startPoint.X + 50, startPoint.Y - 100), // More towards the right
                    new PointF(startPoint.X - 20, startPoint.Y - 150), // A slight pull back to the left
                    new PointF(endPoint.X + 20, endPoint.Y + 150),     // A slight pull to the right near the end point
                    new PointF(endPoint.X - 50, endPoint.Y + 100),     // Another small nudge toward the left
                    new PointF(endPoint.X + 30, endPoint.Y + 50)       // Slight nudge back towards the center
                };
                bezierPoints = new Utils.BezierPointCollection( startPoint, endPoint, controlPoints );
            }


        }


        private void BezierControlForm_MouseDown( object? sender, MouseEventArgs e )
        {

            if ( IsPointNear( e.Location, bezierPoints.Start ) )
            {
                isDragging = true;
                dragPointName = "startPoint";
            } else if ( IsPointNear( e.Location, bezierPoints.End ) )
            {
                isDragging = true;
                dragPointName = "endPoint";
            }

            // Check if the mouse is near any of the control points
            for ( int i = 0; i < bezierPoints.ControlPoints.Count; i++ )
            {
                if ( IsPointNear( e.Location, bezierPoints.ControlPoints[ i ] ) )
                {
                    isDragging = true;
                    dragPointName = $"controlPoint{i + 1}";
                }
            }

            // Redraw the form
            Invalidate();
        }

        private void BezierControlForm_MouseMove( object? sender, MouseEventArgs e )
        {
            if ( isDragging )
            {
                // Move only the control points, with optional clamping to prevent them from going off-screen
                switch ( dragPointName )
                {
                    case "startPoint":
                    bezierPoints.Start = ClampToBounds( new PointF( e.X, e.Y ) ); // Clamp to bounds if necessary
                    UserSelected = true;
                    break;
                    case "endPoint":
                    bezierPoints.End = ClampToBounds( new PointF( e.X, e.Y ) ); // Clamp to bounds if necessary
                    UserSelected = true;
                    break;
                    case "controlPoint1":
                    bezierPoints.ControlPoints[ 0 ] = ClampToBounds( new PointF( e.X, e.Y ) ); // Clamp to bounds if necessary
                    UserSelected = true;
                    break;
                    case "controlPoint2":
                    bezierPoints.ControlPoints[ 1 ] = ClampToBounds( new PointF( e.X, e.Y ) ); // Clamp to bounds if necessary
                    UserSelected = true;
                    break;
                    case "controlPoint3":
                    bezierPoints.ControlPoints[ 2 ] = ClampToBounds( new PointF( e.X, e.Y ) ); // Clamp to bounds if necessary
                    UserSelected = true;
                    break;
                    case "controlPoint4":
                    bezierPoints.ControlPoints[ 3 ] = ClampToBounds( new PointF( e.X, e.Y ) ); // Clamp to bounds if necessary
                    UserSelected = true;
                    break;
                    case "controlPoint5":
                    bezierPoints.ControlPoints[ 4 ] = ClampToBounds( new PointF( e.X, e.Y ) ); // Clamp to bounds if necessary
                    UserSelected = true;
                    break;
                    case "controlPoint6":
                    bezierPoints.ControlPoints[ 5 ] = ClampToBounds( new PointF( e.X, e.Y ) ); // Clamp to bounds if necessary
                    UserSelected = true;
                    break;
                }

                // Redraw the form
                Invalidate();
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
            int topMargin = 25;
            Rectangle drawingArea = new( 0, topMargin, this.Width, this.Height - topMargin );
            g.SetClip( drawingArea );
            g.Clear( backgroundColor );

            // Set pens and brushes
            Pen curvePen = new( Color.DarkViolet, 3 );
            Pen auxiliaryPen = new( Color.Gray, 2 ) { DashStyle = DashStyle.Dash };
            Brush controlPointBrush = new SolidBrush( Color.Blue );
            Brush startPointBrush = new SolidBrush( Color.Green );
            Brush endPointBrush = new SolidBrush( Color.Red );

            // Draw grid lines
            for ( int i = 0; i < this.Width; i += 25 )
            {
                g.DrawLine( Pens.DarkGray, new Point( i, topMargin ), new Point( i, this.Height ) );
            }

            for ( int i = topMargin; i < this.Height; i += 25 )
            {
                g.DrawLine( Pens.DarkGray, new Point( 0, i ), new Point( this.Width, i ) );
            }


            // Calculate Bezier curve points
            List<PointF> pointsToDraw = bezierPoints.CalculateOcticBezierPoints( 20 );

            // Draw auxiliary lines between control points
            g.DrawLine( auxiliaryPen, bezierPoints.Start, bezierPoints.ControlPoints[ 0 ] );
            for ( int i = 0; i < bezierPoints.ControlPoints.Count - 1; i++ )
            {
                g.DrawLine( auxiliaryPen, bezierPoints.ControlPoints[ i ], bezierPoints.ControlPoints[ i + 1 ] );
                g.DrawLine( auxiliaryPen, bezierPoints.ControlPoints[ bezierPoints.ControlPoints.Count - 1 ], bezierPoints.End );
            }


            // Draw the Bezier curve
            if ( pointsToDraw.Count > 1 )
            {
                g.DrawCurve( curvePen, pointsToDraw.ToArray() );
            }


            // Draw control points
            g.FillEllipse( startPointBrush, bezierPoints.Start.X - 5, bezierPoints.Start.Y - 5, 10, 10 );
            g.FillEllipse( endPointBrush, bezierPoints.End.X - 5, bezierPoints.End.Y - 5, 10, 10 );
            foreach ( PointF controlPoint in bezierPoints.ControlPoints )
            {
                g.FillEllipse( controlPointBrush, controlPoint.X - 5, controlPoint.Y - 5, 10, 10 );
            }

            // Draw tangents at key points on the curve (midpoints between control points)
            DrawTangents( g );

            // Reset the clipping region
            g.ResetClip();
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

            BezierControlForm_Paint( null, e );
        }


        protected override void OnFormClosed( FormClosedEventArgs e )
        {
            base.OnFormClosed( e );

            PlayerData.SetBezierPoints( bezierPoints );
        }


        //Draw tangents at peaks along the curve
        private void DrawTangents( Graphics g )
        {
            Pen tangentPen = new( Color.Red, 2 );

            // We are calculating tangents using control points alignment with auxiliary lines
            for ( int i = 0; i < bezierPoints.ControlPoints.Count; i++ )
            {
                // Control points before and after to estimate the tangent
                PointF prevPoint = i == 0 ? bezierPoints.Start : bezierPoints.ControlPoints[ i - 1 ];
                PointF nextPoint = i == bezierPoints.ControlPoints.Count - 1 ? bezierPoints.End : bezierPoints.ControlPoints[ i + 1 ];

                // Compute direction for tangent using auxiliary line approach (aligning with control points)
                PointF tangent = new(
                    nextPoint.X - prevPoint.X,
                    nextPoint.Y - prevPoint.Y
                );

                // Normalize the tangent vector
                float length = ( float ) Math.Sqrt( tangent.X * tangent.X + tangent.Y * tangent.Y );
                tangent.X /= length;
                tangent.Y /= length;

                // Tangent length for drawing
                float tangentLength = 40;

                // Calculate the tangent's end point at the control point's position
                PointF tangentEnd = new(
                    bezierPoints.ControlPoints[ i ].X + tangent.X * tangentLength,
                    bezierPoints.ControlPoints[ i ].Y + tangent.Y * tangentLength
                );

                // Draw the tangent starting from the control point
                g.DrawLine( tangentPen, bezierPoints.ControlPoints[ i ], tangentEnd );
            }
        }
    }
}
