using SharpGen.Runtime;
using Vortice.Direct3D;




namespace ShaderUtils
{

    public partial class ShaderIncludehandler( string folderPath ) : CallbackBase, Include
    {
        private string Directory { get; } = folderPath;
        public Stream Open( IncludeType type, string fileName, Stream? parentStream )
        {
            return type switch
            {
                IncludeType.Local => File.OpenRead( Path.Combine( Directory, fileName ) ),
                IncludeType.System => File.OpenRead( fileName ),
                _ => throw new NotImplementedException(),
            };
        }

        public void Close( Stream stream ) => stream?.Close();
    }
}
