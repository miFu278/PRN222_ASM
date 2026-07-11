namespace RAGChatBot.Domain.Constants
{
    public static class RoleNames
    {
        public const string Admin = "Admin";
        public const string Lecturer = "Lecturer";
        public const string Student = "Student";
    }

    public static class SystemRoleIds
    {
        public static readonly Guid Admin = Guid.Parse("11111111-1111-1111-1111-111111111111");
        public static readonly Guid Lecturer = Guid.Parse("22222222-2222-2222-2222-222222222222");
        public static readonly Guid Student = Guid.Parse("33333333-3333-3333-3333-333333333333");
    }
}
