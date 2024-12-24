using SCB;
using Vortice.Direct3D;




namespace ShaderUtils
{

    public class ShaderIncludehandler() : Include
    {
        private bool disposed;
        private string ShaderDirectory { get; } = FileManager.shaderFolder;
        public string? SystemIncludeDirectory { get; } = null;

        ~ShaderIncludehandler()
        {
            disposed = false;
        }


        public Stream Open( IncludeType type, string fileName, Stream? parentStream )
        {
            string? folder = null;

            if ( type == IncludeType.System && SystemIncludeDirectory != null )
            {
                folder = SystemIncludeDirectory;
            } else
            {
                if ( parentStream is FileStream parentFileStream )
                {
                    string? parentpath = Path.GetDirectoryName( parentFileStream.Name );
                    folder = parentpath ?? ShaderDirectory;
                }

                if ( !File.Exists( folder ) )
                {
                    throw new FileNotFoundException( "Failed to find shader folfer" );
                }
            }

            string filePath = Path.Combine( folder, fileName );
            if ( !File.Exists( filePath ) )
            {
                throw new FileNotFoundException( $"Failed to find shader file : {filePath}" );
            }

            return new FileStream( filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite );
        }

        public void Close( Stream stream )
        {
            stream?.Dispose();
        }


        public void Dispose()
        {
            Dispose( true );
            GC.SuppressFinalize( this );
        }

        protected virtual void Dispose( bool disposing )
        {
            if ( !disposed && disposing )
            {
                //Clear unmanaged resources
            }
        }
    }
}
