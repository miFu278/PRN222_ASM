using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RAGChatBot.Application.Common.Interfaces;
using RAGChatBot.Domain.Models;
using RAGChatBot.Infrastructure.Persistence;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RAGChatBot.Infrastructure.Storage
{
    public class DocumentProcessingWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DocumentProcessingWorker> _logger;
        private readonly HttpClient _httpClient;

        public DocumentProcessingWorker(
            IServiceProvider serviceProvider,
            ILogger<DocumentProcessingWorker> logger,
            HttpClient httpClient)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _httpClient = httpClient;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("DocumentProcessingWorker đã bắt đầu hoạt động...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessNextDocumentAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi xảy ra trong vòng lặp chính của DocumentProcessingWorker");
                }

                // Chờ 10 giây trước khi quét lượt tiếp theo
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        private async Task ProcessNextDocumentAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // 1. Quét tìm tài liệu chưa được xử lý VÀ đã được phê duyệt
            var document = dbContext.KnowledgeDocuments
                .Where(d => !d.IsProcessed && d.IsApproved)
                .OrderBy(d => d.UploadedAt)
                .FirstOrDefault();

            if (document == null)
            {
                return;
            }

            _logger.LogInformation("Tìm thấy tài liệu chưa xử lý: ID={DocId}, FileName='{FileName}'", document.Id, document.FileName);

            try
            {
                var textExtractor = scope.ServiceProvider.GetRequiredService<ITextExtractor>();
                var chunkingService = scope.ServiceProvider.GetRequiredService<IChunkingService>();
                var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();

                // 2. Tải file từ Cloud Storage (Supabase) về Stream
                _logger.LogInformation("Đang tải file từ URL: {Url}", document.StoragePath);
                
                byte[] fileBytes;
                try
                {
                    fileBytes = await _httpClient.GetByteArrayAsync(document.StoragePath, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Không thể tải tệp tin từ Storage Path: {Path}", document.StoragePath);
                    // Đánh dấu là processed nhưng không tạo chunk để tránh vòng lặp lỗi vô hạn
                    document.IsProcessed = true;
                    await dbContext.SaveChangesAsync(stoppingToken);
                    return;
                }

                using var memoryStream = new MemoryStream(fileBytes);
                var extension = Path.GetExtension(document.FileName);

                // 3. Trích xuất văn bản thô
                _logger.LogInformation("Đang trích xuất chữ thô từ file...");
                var fullText = await textExtractor.ExtractTextAsync(memoryStream, extension);

                if (string.IsNullOrWhiteSpace(fullText))
                {
                    _logger.LogWarning("Tài liệu '{FileName}' rỗng hoặc không có chữ để trích xuất.", document.FileName);
                    document.IsProcessed = true;
                    await dbContext.SaveChangesAsync(stoppingToken);
                    return;
                }

                // 4. Chia nhỏ văn bản thành các Chunk (mặc định 500 ký tự, overlap 50)
                _logger.LogInformation("Đang chia nhỏ văn bản (Chunking)...");
                var textChunks = chunkingService.ChunkText(fullText, chunkSize: 500, overlap: 50);
                _logger.LogInformation("Tài liệu được chia thành {Count} chunks.", textChunks.Count);

                // 5. Tạo vector nhúng và lưu vào database
                for (int i = 0; i < textChunks.Count; i++)
                {
                    var chunkText = textChunks[i];

                    // Loại bỏ ký tự null byte (0x00) - PostgreSQL UTF-8 không chấp nhận ký tự này
                    chunkText = chunkText.Replace("\0", string.Empty);

                    // Bỏ qua chunk nếu sau khi làm sạch không còn nội dung
                    if (string.IsNullOrWhiteSpace(chunkText))
                    {
                        _logger.LogWarning("Bỏ qua chunk {Index}/{Total} vì không có nội dung hợp lệ sau khi làm sạch.", i + 1, textChunks.Count);
                        continue;
                    }

                    _logger.LogInformation("Đang sinh vector cho chunk {Index}/{Total}...", i + 1, textChunks.Count);
                    
                    var vectorValues = await embeddingService.GenerateEmbeddingAsync(chunkText);

                    var chunkEntity = new DocumentChunk
                    {
                        Id = Guid.NewGuid(),
                        DocumentId = document.Id,
                        Content = chunkText,
                        ChunkIndex = i,
                        Embedding = new Pgvector.Vector(vectorValues)
                    };

                    dbContext.DocumentChunks.Add(chunkEntity);
                }

                // 6. Cập nhật trạng thái hoàn thành
                document.IsProcessed = true;
                await dbContext.SaveChangesAsync(stoppingToken);
                _logger.LogInformation("Đã xử lý xong tài liệu: '{FileName}'", document.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra trong quá trình xử lý tài liệu '{FileName}'", document.FileName);
                // Đánh dấu true để tránh loop kẹt, hoặc có thể lưu log lỗi riêng. 
                // Ở đây ta đánh dấu true để hệ thống tiếp tục chạy các tài liệu khác.
                document.IsProcessed = true;
                await dbContext.SaveChangesAsync(stoppingToken);
            }
            finally
            {
                if (document.IsProcessed)
                {
                    var eventService = scope.ServiceProvider.GetService<IDocumentEventService>();
                    eventService?.NotifyDocumentChanged(document.CourseCode);
                }
            }
        }
    }
}
