namespace SCB
{
    partial class Bezier
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose( bool disposing )
        {
            if ( disposing && ( components != null ) )
            {
                components.Dispose();
            }
            base.Dispose( disposing );
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // Bezier
            // 
            this.AutoScaleDimensions = new SizeF( 7F, 15F );
            this.AutoScaleMode = AutoScaleMode.Font;
            this.BackColor = SystemColors.ActiveCaption;
            this.ClientSize = new Size( 800, 450 );
            this.DrawerUseColors = true;
            this.FormStyle = FormStyles.ActionBar_None;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Bezier";
            this.Padding = new Padding( 3, 24, 3, 3 );
            this.Text = "Bezier";
            this.ResumeLayout( false );
        }

        #endregion
    }
}