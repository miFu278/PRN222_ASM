namespace RAGChatBot.Domain.Entities
{
    public sealed class Role
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public ICollection<User> Users { get; set; } = new List<User>();
    }
}
