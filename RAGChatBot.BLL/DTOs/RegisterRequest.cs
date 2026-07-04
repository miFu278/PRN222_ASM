using System.ComponentModel.DataAnnotations;

namespace RAGChatBot.BLL.DTOs
{
    public class RegisterRequest
    {
        [Required(ErrorMessage = "Tên tài khoản không được để trống")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu không được để trống")]
        [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vai trò không được để trống")]
        public string Role { get; set; } = "Student"; // Student, Lecturer, Admin

        [Required(ErrorMessage = "Gói đăng ký không được để trống")]
        public string SubscriptionTier { get; set; } = "Free"; // Free, Basic, Premium
    }
}
