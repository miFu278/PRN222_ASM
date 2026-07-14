using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using RAGChatBot.Domain.Interfaces;
using RAGChatBot.Domain.Models;
using RAGChatBot.Domain.Enums;
using RAGChatBot.Domain.Entities;
using RAGChatBot.DAL.Context;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RAGChatBot.DAL.Services
{
    public class DocumentProcessingWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DocumentProcessingWorker> _logger;

        public DocumentProcessingWorker(
            IServiceProvider serviceProvider,
            ILogger<DocumentProcessingWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
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
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        private async Task ProcessNextDocumentAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // 1. Quét tìm tài liệu chưa được xử lý VÀ đã được phê duyệt
            var documentId = await dbContext.KnowledgeDocuments
                .AsNoTracking()
                .Where(d => d.Status == DocumentStatus.Pending && d.IsApproved)
                .OrderBy(d => d.UploadedAt)
                .Select(d => d.Id)
                .FirstOrDefaultAsync(stoppingToken);

            if (documentId == Guid.Empty)
            {
                return;
            }

            // Claim atomically so multiple worker instances cannot process the same file.
            var claimed = await dbContext.KnowledgeDocuments
                .Where(d => d.Id == documentId && d.Status == DocumentStatus.Pending && d.IsApproved)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(d => d.Status, DocumentStatus.Processing), stoppingToken);
            if (claimed == 0) return;

            var document = await dbContext.KnowledgeDocuments
                .FirstAsync(d => d.Id == documentId, stoppingToken);
            var documentEventService = scope.ServiceProvider.GetService<IDocumentEventService>();
            if (documentEventService is not null)
            {
                await documentEventService.NotifyDocumentChangedAsync(new RealtimeChangeEvent
                {
                    Type = "DocumentStatusChanged",
                    CourseCode = document.CourseCode,
                    EntityId = document.Id,
                    Status = DocumentStatus.Processing.ToString()
                }, stoppingToken);
            }

            _logger.LogInformation("Tìm thấy tài liệu chưa xử lý: ID={DocId}, FileName='{FileName}'", document.Id, document.FileName);

            try
            {
                var textExtractor = scope.ServiceProvider.GetRequiredService<ITextExtractor>();
                var chunkingService = scope.ServiceProvider.GetRequiredService<IChunkingService>();
                var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
                var benchmarkRepo = scope.ServiceProvider.GetRequiredService<IBenchmarkRepository>();
                var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
                var sw = new Stopwatch();

                // 2. Tải file từ Cloud Storage (Supabase) về Stream
                _logger.LogInformation("Đang tải file từ URL: {Url}", document.StoragePath);
                
                await using var memoryStream = await fileStorage.OpenReadAsync(document.StoragePath);
                var extension = Path.GetExtension(document.FileName);

                // 3. Trích xuất văn bản thô — Benchmark: TextExtraction
                _logger.LogInformation("Đang trích xuất chữ thô từ file...");
                sw.Restart();
                var fullText = await textExtractor.ExtractTextAsync(memoryStream, extension);
                sw.Stop();
                await benchmarkRepo.AddAsync(new PerformanceBenchmark
                {
                    OperationType = "TextExtraction",
                    DurationMs = sw.Elapsed.TotalMilliseconds,
                    DocumentName = document.FileName,
                    Notes = $"Extension: {extension}, Size: {document.FileSize} bytes"
                });

                if (string.IsNullOrWhiteSpace(fullText))
                {
                    _logger.LogWarning("Tài liệu '{FileName}' rỗng hoặc không có chữ để trích xuất.", document.FileName);
                    throw new InvalidOperationException("Tài liệu rỗng hoặc không trích xuất được văn bản.");
                }

                // 4. Chia nhỏ văn bản thành các Chunk — Benchmark: Chunking
                _logger.LogInformation("Đang chia nhỏ văn bản (Chunking) với chiến lược={Strategy}, size={Size}, overlap={Overlap}...", document.ChunkingStrategy, document.ChunkSize, document.Overlap);
                sw.Restart();
                var textChunks = chunkingService.ChunkText(fullText, document.ChunkingStrategy, document.ChunkSize, document.Overlap);
                sw.Stop();
                _logger.LogInformation("Tài liệu được chia thành {Count} chunks.", textChunks.Count);
                await benchmarkRepo.AddAsync(new PerformanceBenchmark
                {
                    OperationType = "Chunking",
                    DurationMs = sw.Elapsed.TotalMilliseconds,
                    DocumentName = document.FileName,
                    Notes = $"Strategy: {document.ChunkingStrategy}, Chunks: {textChunks.Count}"
                });

                // 5. Tạo vector nhúng và lưu vào database — Benchmark: VectorEmbedding (toàn bộ)
                sw.Restart();
                
                // Chuẩn hóa và làm sạch tất cả chunks trước khi đưa vào sinh vector hàng loạt
                var cleanChunks = new List<(string Text, int OriginalIndex)>();
                for (int i = 0; i < textChunks.Count; i++)
                {
                    var chunkText = textChunks[i].Replace("\0", string.Empty);
                    if (!string.IsNullOrWhiteSpace(chunkText))
                    {
                        cleanChunks.Add((chunkText, i));
                    }
                }

                _logger.LogInformation("Đang sinh vector hàng loạt (Batch) cho {Count}/{Total} chunks hợp lệ...", cleanChunks.Count, textChunks.Count);
                
                var chunksTexts = cleanChunks.Select(c => c.Text).ToList();
                if (chunksTexts.Count == 0)
                {
                    throw new InvalidOperationException("Tài liệu không tạo được chunk hợp lệ.");
                }
                var embeddings = await embeddingService.GenerateEmbeddingsAsync(chunksTexts);
                if (embeddings.Count != cleanChunks.Count)
                {
                    throw new InvalidOperationException("Số vector trả về không khớp với số chunk.");
                }

                // Retry must replace old chunks instead of duplicating them.
                await dbContext.DocumentChunks
                    .Where(c => c.DocumentId == document.Id)
                    .ExecuteDeleteAsync(stoppingToken);

                for (int i = 0; i < cleanChunks.Count; i++)
                {
                    var (chunkText, originalIndex) = cleanChunks[i];
                    var vectorValues = embeddings[i];

                    var chunkEntity = new DocumentChunk
                    {
                        Id = Guid.NewGuid(),
                        DocumentId = document.Id,
                        Content = chunkText,
                        ChunkIndex = originalIndex,
                        Embedding = new Pgvector.Vector(vectorValues)
                    };

                    dbContext.DocumentChunks.Add(chunkEntity);
                }
                sw.Stop();
                await benchmarkRepo.AddAsync(new PerformanceBenchmark
                {
                    OperationType = "VectorEmbedding",
                    DurationMs = sw.Elapsed.TotalMilliseconds,
                    DocumentName = document.FileName,
                    Notes = $"Chunks embedded: {cleanChunks.Count}"
                });

                // 6. Cập nhật trạng thái hoàn thành
                document.Status = DocumentStatus.Success;
                await dbContext.SaveChangesAsync(stoppingToken);
                if (documentEventService is not null)
                {
                    await documentEventService.NotifyDocumentChangedAsync(new RealtimeChangeEvent
                    {
                        Type = "DocumentStatusChanged",
                        CourseCode = document.CourseCode,
                        EntityId = document.Id,
                        Status = document.Status.ToString()
                    }, stoppingToken);
                }
                _logger.LogInformation("Đã xử lý xong tài liệu: '{FileName}'", document.FileName);

                // Tự động sinh quiz cho tài liệu mới — Benchmark: QuizGeneration
                try
                {
                    var alreadyHasQuestionBank = await dbContext.QuestionBanks
                        .AnyAsync(question => question.DocumentId == document.Id, stoppingToken);
                    if (!alreadyHasQuestionBank)
                    {
                        var quizService = scope.ServiceProvider.GetRequiredService<IQuizService>();
                        sw.Restart();
                        await quizService.GenerateQuizForDocumentAsync(document.Id);
                        sw.Stop();
                        await benchmarkRepo.AddAsync(new PerformanceBenchmark
                        {
                            OperationType = "QuizGeneration",
                            DurationMs = sw.Elapsed.TotalMilliseconds,
                            DocumentName = document.FileName,
                            Notes = $"DocumentId: {document.Id}"
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi xảy ra khi sinh Quiz tự động cho tài liệu ID={DocId}", document.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra trong quá trình xử lý tài liệu '{FileName}'", document.FileName);
                // Đánh dấu Failed để tránh loop kẹt và cho phép Retry
                document.Status = DocumentStatus.Failed;
                await dbContext.SaveChangesAsync(stoppingToken);
                if (documentEventService is not null)
                {
                    await documentEventService.NotifyDocumentChangedAsync(new RealtimeChangeEvent
                    {
                        Type = "DocumentStatusChanged",
                        CourseCode = document.CourseCode,
                        EntityId = document.Id,
                        Status = document.Status.ToString()
                    }, stoppingToken);
                }
            }
        }
    }
}
