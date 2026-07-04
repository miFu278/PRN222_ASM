namespace RAGChatBot.Infrastructure.Interfaces
{
    public interface IChunkingService
    {
        /// <summary>
        /// Chia nhỏ chuỗi văn bản dài thành các đoạn văn ngắn hơn để lưu trữ và tạo vector.
        /// </summary>
        /// <param name="text">Văn bản cần chia nhỏ</param>
        /// <param name="chunkSize">Độ dài ký tự tối đa của 1 chunk</param>
        /// <param name="overlap">Độ dài ký tự trùng lặp giữa 2 chunk liền kề để giữ ngữ cảnh</param>
        List<string> ChunkText(string text, int chunkSize = 500, int overlap = 50);
    }
}
