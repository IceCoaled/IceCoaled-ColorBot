using System.Drawing.Drawing2D;
using Utils;

namespace SCB
{
    public partial class Configurations : MaterialSkin.Controls.MaterialForm
    {
        public Configurations()
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
        }

        private void materialButton1_Click( object sender, EventArgs e )
        {
            PlayerConfigs.SaveConfig( 1 );
        }

        private void materialButton4_Click( object sender, EventArgs e )
        {
            PlayerConfigs.SaveConfig( 2 );
        }

        private void materialButton6_Click( object sender, EventArgs e )
        {
            PlayerConfigs.SaveConfig( 3 );
        }

        private void materialButton2_Click( object sender, EventArgs e )
        {
            PlayerConfigs.LoadConfig( 1 );
        }

        private void materialButton3_Click( object sender, EventArgs e )
        {
            PlayerConfigs.LoadConfig( 2 );
        }

        private void materialButton5_Click( object sender, EventArgs e )
        {
            PlayerConfigs.LoadConfig( 3 );
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
        }
    }
}
