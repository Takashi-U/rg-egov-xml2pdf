using Microsoft.AspNetCore.Components.Forms;

namespace EgovXml2Pdf.Services
{
    public interface IFileProcessingService
    {
        Task ProcessFilesAsync(IEnumerable<IBrowserFile> files, Action<int, string> progressCallback);
    }
}
