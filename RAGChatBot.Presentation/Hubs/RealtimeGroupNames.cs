using Microsoft.AspNetCore.SignalR;

namespace RAGChatBot.Presentation.Hubs
{
    internal static class RealtimeGroupNames
    {
        public static string ForCourse(string courseCode)
        {
            var normalized = courseCode?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 50)
                throw new HubException("Mã môn học không hợp lệ.");

            return $"course:{normalized}";
        }
    }
}
