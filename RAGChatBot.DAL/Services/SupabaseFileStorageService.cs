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
    
            // 2. Sinh tên tệp tin duy nhất và loại bỏ ký tự tiếng Việt / đặc biệt không hợp lệ với Supabase Storage
            var safeFileName = SanitizeFileName(fileName);
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
                var details = ex.InnerException != null ? $"{ex.Message} -> {ex.InnerException.Message}" : ex.Message;
                throw new InvalidOperationException(
                    $"Không thể lưu tệp vào bucket Supabase '{BucketName}'. Chi tiết: {details}. Hãy kiểm tra bucket, giới hạn kích thước tệp và storage policy trên Supabase Dashboard.",
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

        private static string SanitizeFileName(string fileName)
        {
            var rawFileName = Path.GetFileName(fileName);
            var extension = Path.GetExtension(rawFileName);
            var nameWithoutExt = Path.GetFileNameWithoutExtension(rawFileName);

            if (string.IsNullOrWhiteSpace(nameWithoutExt))
            {
                return $"file_{Guid.NewGuid():N}{extension}";
            }

            // Bỏ dấu tiếng Việt (Normalization Form D)
            var normalized = nameWithoutExt.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder();
            foreach (var c in normalized)
            {
                var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }
            var asciiName = sb.ToString().Normalize(System.Text.NormalizationForm.FormC);

            // Giữ lại ký tự chữ cái, chữ số, gạch ngang, gạch dưới. Các ký tự khác thay bằng '_'
            var cleanName = System.Text.RegularExpressions.Regex.Replace(asciiName, @"[^a-zA-Z0-9_\-]", "_");
            cleanName = System.Text.RegularExpressions.Regex.Replace(cleanName, @"_+", "_").Trim('_');

            if (string.IsNullOrWhiteSpace(cleanName))
            {
                cleanName = "file";
            }

            return cleanName + extension;
        }
    }
}
