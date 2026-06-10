using System.Threading.Tasks;

namespace RAGChatBot.Application.Common.Interfaces
{
    public interface IChatService
    {
        /// <summary>
        /// Lấy câu trả lời từ chatbot bằng cách sinh vector câu hỏi, tìm kiếm các mảnh văn bản tương đồng 
        /// từ database và đưa dữ liệu đó làm ngữ cảnh vào prompt của mô hình ngôn ngữ (Gemini).
        /// </summary>
        /// <param name="question">Câu hỏi của người dùng/sinh viên</param>
        /// <param name="courseCode">Mã môn học được lựa chọn (tùy chọn)</param>
        /// <returns>Câu trả lời phản hồi từ Chatbot</returns>
        Task<string> GetChatResponseAsync(string question, string? courseCode);
    }
}
