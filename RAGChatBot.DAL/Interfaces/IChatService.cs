namespace RAGChatBot.DAL.Interfaces
{
    public interface IChatService
    {
        /// <summary>
        /// Lấy câu trả lời từ chatbot bằng cách sinh vector câu hỏi, tìm kiếm các mảnh văn bản tương đồng 
        /// từ database và đưa dữ liệu đó làm ngữ cảnh vào prompt của mô hình ngôn ngữ.
        /// </summary>
        /// <param name="question">Câu hỏi của người dùng/sinh viên</param>
        /// <param name="threadId">ID luồng hội thoại để tải lịch sử trò chuyện (tùy chọn)</param>
        /// <returns>Câu trả lời phản hồi từ Chatbot</returns>
        Task<string> GetChatResponseAsync(string question, string? courseCode, Guid? threadId = null);
    }
}
