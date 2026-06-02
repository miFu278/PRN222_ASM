using RAGChatBot.Application.Common.Interfaces;
using System;
using System.IO;
using System.Threading.Tasks;

namespace RAGChatBot.Infrastructure.Storage
{
    public class SupabaseFileStorageService : IFileStorageService
    {
        private readonly Supabase.Client _supabaseClient;
        private const string BucketName = "raw-documents"; // Đảm bảo bạn đã tạo bucket này trên Supabase ở trạng thái Public

        public SupabaseFileStorageService(Supabase.Client supabaseClient)
        {
            _supabaseClient = supabaseClient;
        }

        public async Task<string> SaveFileAsync(Stream fileStream, string fileName)
        {
            // 1. Tải toàn bộ Stream tệp tin vào MemoryStream để lấy mảng byte
            using var ms = new MemoryStream();
            await fileStream.CopyToAsync(ms);
            var fileBytes = ms.ToArray();
    
            // 2. Sinh tên tệp tin duy nhất tránh trùng lặp đè tệp
            var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";

            // 3. Khởi tạo Supabase Client (an toàn nếu chưa gọi trước đó)
            await _supabaseClient.InitializeAsync();

            // 4. Gọi phân hệ Storage và Bucket tương ứng
            var storage = _supabaseClient.Storage.From(BucketName);

            // 5. Thực hiện tải lên (Upload) tệp tin thô dưới dạng mảng byte
            try
            {
                await storage.Upload(fileBytes, uniqueFileName);
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi lưu trữ Supabase: Không tìm thấy Bucket '{BucketName}' hoặc bạn chưa cấp quyền. Vui lòng vào trang quản trị Supabase, tạo Storage Bucket tên là '{BucketName}' và đặt quyền là Public. Chi tiết lỗi: {ex.Message}");
            }

            // 6. Trả về đường dẫn URL công khai của tệp tin vừa tải lên để lưu vào DB
            return storage.GetPublicUrl(uniqueFileName);
        }

        public async Task DeleteFileAsync(string storagePath)
        {
            await _supabaseClient.InitializeAsync();
            var storage = _supabaseClient.Storage.From(BucketName);
            
            // Trích xuất tên tệp tin từ URL của Supabase
            // Ví dụ: https://.../raw-documents/unique_name.pdf
            var fileName = storagePath.Substring(storagePath.LastIndexOf('/') + 1);
            
            await storage.Remove(new System.Collections.Generic.List<string> { fileName });
        }
    }
}
