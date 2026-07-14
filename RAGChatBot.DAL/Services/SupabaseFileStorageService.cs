using RAGChatBot.Domain.Interfaces;
using System;
using System.IO;
using System.Threading.Tasks;

namespace RAGChatBot.DAL.Services
{
    public class SupabaseFileStorageService : IFileStorageService
    {
        private readonly Supabase.Client _supabaseClient;
        private const string BucketName = "raw-documents";

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
            var safeFileName = Path.GetFileName(fileName);
            var uniqueFileName = $"{Guid.NewGuid():N}_{safeFileName}";

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
                throw new InvalidOperationException(
                    $"Không thể lưu tệp vào bucket Supabase '{BucketName}'. Hãy kiểm tra bucket và storage policy.",
                    ex);
            }

            // Store the object key, not a public URL, so private buckets are supported.
            return uniqueFileName;
        }

        public async Task<Stream> OpenReadAsync(string storagePath)
        {
            await _supabaseClient.InitializeAsync();
            var storage = _supabaseClient.Storage.From(BucketName);
            var objectPath = GetObjectPath(storagePath);
            var bytes = await storage.Download(objectPath, (EventHandler<float>?)null);
            return new MemoryStream(bytes, writable: false);
        }

        public async Task DeleteFileAsync(string storagePath)
        {
            await _supabaseClient.InitializeAsync();
            var storage = _supabaseClient.Storage.From(BucketName);
            
            // Trích xuất tên tệp tin từ URL của Supabase
            // Ví dụ: https://.../raw-documents/unique_name.pdf
            var fileName = GetObjectPath(storagePath);
            
            await storage.Remove(new System.Collections.Generic.List<string> { fileName });
        }

        private static string GetObjectPath(string storagePath)
        {
            if (Uri.TryCreate(storagePath, UriKind.Absolute, out var uri))
            {
                return Uri.UnescapeDataString(uri.Segments[^1]);
            }

            return storagePath.Trim().TrimStart('/');
        }
    }
}
