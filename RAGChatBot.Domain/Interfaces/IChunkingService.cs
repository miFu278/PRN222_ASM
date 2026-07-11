namespace RAGChatBot.Domain.Interfaces
{
    public interface IChunkingService
    {
        /// <summary>
        /// Chia nhỏ chuỗi văn bản dài thành các đoạn văn ngắn hơn để lưu trữ và tạo vector.
        /// </summary>
        /// <param name="strategy">Chiến lược cắt đoạn (Character, Word, Paragraph)</param>
        /// <param name="chunkSize">Kích thước của 1 chunk (số ký tự hoặc số từ)</param>
        /// <param name="overlap">Độ trùng lặp giữa 2 chunk liền kề</param>
        List<string> ChunkText(string text, string strategy = "Character", int chunkSize = 500, int overlap = 50);
    }
}
