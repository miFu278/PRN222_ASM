using System.Threading.Tasks;

namespace RAGChatBot.Application.Common.Interfaces
{
    public interface IEmbeddingService
    {
        /// <summary>
        /// Sinh vector nhúng (embedding) cho đoạn văn bản đầu vào.
        /// </summary>
        Task<float[]> GenerateEmbeddingAsync(string text);
    }
}
