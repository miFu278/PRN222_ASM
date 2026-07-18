using Microsoft.Extensions.Logging;
using RAGChatBot.BLL.DTOs;
using RAGChatBot.Domain.Entities;
using RAGChatBot.Domain.Interfaces;

namespace RAGChatBot.BLL.Services
{
    public class ChatService : IChatService
    {
        private const int HistoryMessageLimit = 10;
        private readonly IChatRepository _chatRepository;
        private readonly IChatResponseService _chatResponseService;
        private readonly IChatTrackerLogRepository _chatLogRepository;
        private readonly ICreditService _creditService;
        private readonly ICourseRepository _courseRepository;
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            IChatRepository chatRepository,
            IChatResponseService chatResponseService,
            IChatTrackerLogRepository chatLogRepository,
            ICreditService creditService,
            ICourseRepository courseRepository,
            ILogger<ChatService> logger)
        {
            _chatRepository = chatRepository;
            _chatResponseService = chatResponseService;
            _chatLogRepository = chatLogRepository;
            _creditService = creditService;
            _courseRepository = courseRepository;
            _logger = logger;
        }

        public async Task<ChatReplyDto?> SendMessageAsync(
            Guid userId,
            string message,
            string? courseCode,
            Guid? threadId)
        {
            var normalizedMessage = message.Trim();
            if (normalizedMessage.Length == 0 || normalizedMessage.Length > 4000)
            {
                return new ChatReplyDto
                {
                    Reply = normalizedMessage.Length > 4000
                        ? "Câu hỏi quá dài. Vui lòng giới hạn trong 4.000 ký tự."
                        : "Vui lòng nhập câu hỏi.",
                    IsError = true
                };
            }

            ChatThread? existingThread = null;
            IReadOnlyList<ChatMessage> history = Array.Empty<ChatMessage>();

            if (threadId.HasValue && threadId.Value != Guid.Empty)
            {
                existingThread = await _chatRepository.GetThreadForUserAsync(threadId.Value, userId);
                if (existingThread is null)
                {
                    return null;
                }

                history = await _chatRepository.GetRecentMessagesAsync(
                    existingThread.Id,
                    HistoryMessageLimit);
            }

            var requestedCourseCode = courseCode?.Trim().ToUpper();
            var effectiveCourseCode = existingThread?.CourseCode.Trim().ToUpper()
                ?? requestedCourseCode;

            if (string.IsNullOrWhiteSpace(effectiveCourseCode) ||
                effectiveCourseCode.Equals("GENERAL", StringComparison.OrdinalIgnoreCase))
            {
                return new ChatReplyDto
                {
                    Reply = existingThread is null
                        ? "Vui lòng chọn môn học trước khi bắt đầu cuộc trò chuyện."
                        : "Luồng chat cũ chưa được gắn với môn học. Vui lòng tạo cuộc trò chuyện mới và chọn môn học.",
                    ThreadId = existingThread?.Id,
                    IsError = true
                };
            }

            var course = await _courseRepository.GetByCodeAsync(effectiveCourseCode);
            if (course is null)
            {
                return new ChatReplyDto
                {
                    Reply = "Môn học đã chọn không tồn tại hoặc không còn khả dụng.",
                    ThreadId = existingThread?.Id,
                    IsError = true
                };
            }

            var (allowed, remaining) = await _creditService.CheckAndDeductCreditAsync(
                userId,
                effectiveCourseCode);
            if (!allowed)
            {
                return new ChatReplyDto
                {
                    Reply = "Bạn đã dùng hết lượt hỏi hôm nay. Gói Free có 10 lượt/ngày và Premium có 50 lượt/ngày.",
                    Remaining = 0,
                    ThreadId = existingThread?.Id,
                    OutOfCredits = true
                };
            }

            var historyItems = history
                .Select(item => new ChatHistoryItem(item.Role, item.Content))
                .ToList();

            var response = await _chatResponseService.GetChatResponseAsync(
                normalizedMessage,
                effectiveCourseCode,
                historyItems);

            if (!response.IsSuccessful)
            {
                await _creditService.RefundCreditAsync(userId);
                return new ChatReplyDto
                {
                    Reply = response.Reply,
                    Remaining = remaining + 1,
                    ThreadId = existingThread?.Id,
                    IsError = true
                };
            }

            var activeThread = existingThread;
            if (activeThread is null)
            {
                var title = normalizedMessage.Length > 60
                    ? normalizedMessage[..57] + "..."
                    : normalizedMessage;

                activeThread = await _chatRepository.CreateThreadAsync(
                    userId,
                    effectiveCourseCode,
                    title,
                    DateTime.UtcNow);
            }

            await _chatRepository.AddExchangeAsync(
                activeThread.Id,
                normalizedMessage,
                response.Reply,
                DateTime.UtcNow);

            try
            {
                var log = new ChatTrackerLog
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Question = normalizedMessage,
                    Answer = response.Reply.Length > 2000 ? response.Reply[..2000] : response.Reply,
                    CourseCode = effectiveCourseCode,
                    CreatedAt = DateTime.UtcNow
                };
                await _chatLogRepository.AddAsync(log);
                await _chatLogRepository.SaveChangesAsync();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Không thể ghi audit log cho UserId={UserId}", userId);
            }

            return new ChatReplyDto
            {
                Reply = response.Reply,
                Remaining = remaining,
                ThreadId = activeThread.Id,
                Sources = response.Sources.Select(source => new ChatSourceDto
                {
                    DocumentId = source.DocumentId,
                    FileName = source.FileName,
                    CourseCode = source.CourseCode,
                    ChunkIndex = source.ChunkIndex,
                    Relevance = Math.Clamp(1d - source.Distance, 0d, 1d),
                    Content = source.Content
                }).ToList()
            };
        }

        public async Task<IReadOnlyList<ChatThreadDto>> GetThreadsAsync(Guid userId, string? courseCode)
        {
            var threads = await _chatRepository.GetThreadsByUserAsync(userId, courseCode);
            return threads
                .Select(thread => new ChatThreadDto
                {
                    Id = thread.Id,
                    Title = thread.Title,
                    CourseCode = thread.CourseCode,
                    CreatedAt = thread.CreatedAt
                })
                .ToList();
        }

        public async Task<IReadOnlyList<ChatMessageDto>?> GetMessagesAsync(Guid userId, Guid threadId)
        {
            var thread = await _chatRepository.GetThreadForUserAsync(threadId, userId);
            if (thread is null)
            {
                return null;
            }

            var messages = await _chatRepository.GetMessagesAsync(threadId);
            return messages
                .Select(message => new ChatMessageDto
                {
                    Id = message.Id,
                    Role = message.Role,
                    Content = message.Content,
                    SentAt = message.SentAt
                })
                .ToList();
        }
    }
}
