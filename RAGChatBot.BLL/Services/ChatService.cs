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
        private readonly IChatSessionRepository _chatSessionRepository;

        public ChatService(
            IChatRepository chatRepository,
            IChatResponseService chatResponseService,
            IChatTrackerLogRepository chatLogRepository,
            ICreditService creditService,
            IChatSessionRepository chatSessionRepository)
        {
            _chatRepository = chatRepository;
            _chatResponseService = chatResponseService;
            _chatLogRepository = chatLogRepository;
            _creditService = creditService;
            _chatSessionRepository = chatSessionRepository;
        }

        public async Task<ChatReplyDto?> SendMessageAsync(
            Guid userId,
            string message,
            string? courseCode,
            Guid? threadId)
        {
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

            var (allowed, remaining) = await _creditService.CheckAndDeductCreditAsync(userId);
            if (!allowed)
            {
                return new ChatReplyDto
                {
                    Reply = "Bạn đã hết lượt hỏi miễn phí hôm nay (10 lượt/ngày). Nâng cấp Premium để chat không giới hạn!",
                    Remaining = 0,
                    ThreadId = existingThread?.Id,
                    OutOfCredits = true
                };
            }

            var effectiveCourseCode = !string.IsNullOrWhiteSpace(courseCode)
                ? courseCode.Trim()
                : existingThread?.CourseCode ?? "General";

            var activeThread = existingThread;
            if (activeThread is null)
            {
                var normalizedMessage = message.Trim();
                var title = normalizedMessage.Length > 60
                    ? normalizedMessage[..57] + "..."
                    : normalizedMessage;

                activeThread = await _chatRepository.CreateThreadAsync(
                    userId,
                    effectiveCourseCode,
                    title,
                    DateTime.UtcNow.AddHours(7));
            }

            var historyItems = history
                .Select(item => new ChatHistoryItem(item.Role, item.Content))
                .ToList();

            var reply = await _chatResponseService.GetChatResponseAsync(
                message,
                effectiveCourseCode,
                historyItems);

            await _chatRepository.AddExchangeAsync(
                activeThread.Id,
                message,
                reply,
                DateTime.UtcNow.AddHours(7));

            var log = new ChatTrackerLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Question = message,
                Answer = reply.Length > 2000 ? reply[..2000] : reply,
                CourseCode = courseCode,
                CreatedAt = DateTime.UtcNow
            };
            await _chatLogRepository.AddAsync(log);
            await _chatLogRepository.SaveChangesAsync();
            await _chatSessionRepository.IncrementAsync(userId, effectiveCourseCode);

            return new ChatReplyDto
            {
                Reply = reply,
                Remaining = remaining,
                ThreadId = activeThread.Id
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
