using System.IO;
using System.Threading.Tasks;

namespace RAGChatBot.Application.Common.Interfaces
{
    public interface ITextExtractor
    {
        /// <summary>
        /// Trích xuất toàn bộ văn bản thô từ luồng dữ liệu file dựa trên đuôi mở rộng.
        /// </summary>
        Task<string> ExtractTextAsync(Stream fileStream, string fileExtension);
    }
}
