namespace RAGChatBot.Domain.Interfaces
{
    public interface IEmbeddingService
    {
        /// <summary>
        /// Sinh vector nhúng (embedding) cho đoạn văn bản đầu vào.
        /// </summary>
        Task<float[]> GenerateEmbeddingAsync(string text);

        /// <summary>
        /// Sinh hàng loạt vector nhúng (batch embedding) cho danh sách các đoạn văn bản.
        /// </summary>
        Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts);
    }
}
