using Microsoft.AspNetCore.Components.Forms;

namespace EgovXml2Pdf.Models
{
    public class DroppedBrowserFile : IBrowserFile
    {
        private readonly string _base64Data;
        private readonly byte[] _fileData;

        public DroppedBrowserFile(string name, long size, string base64Data)
        {
            Name = name;
            Size = size;
            LastModified = DateTimeOffset.Now;
            ContentType = "application/zip";
            _base64Data = base64Data;
            _fileData = Convert.FromBase64String(base64Data);
        }

        public string Name { get; }
        public DateTimeOffset LastModified { get; }
        public long Size { get; }
        public string ContentType { get; }

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
        {
            return new MemoryStream(_fileData);
        }
    }
}
