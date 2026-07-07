using System;

namespace RAGChatBot.DAL.Entities
{
    public class UserAnswerDto
    {
        public Guid QuestionId { get; set; }
        public string SelectedAnswer { get; set; } = string.Empty; // "A", "B", "C", "D"
    }
}
