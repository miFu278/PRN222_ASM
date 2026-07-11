namespace RAGChatBot.Domain.Entities
{
    public class PerformanceBenchmark
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string OperationType { get; set; } = string.Empty;
        // TextExtraction | VectorEmbedding | LLMResponse | CosineQuery
        public double DurationMs { get; set; }
        public string? DocumentName { get; set; }
        public DateTime MeasuredAt { get; set; } = DateTime.UtcNow.AddHours(7);
        public string? Notes { get; set; }
    }
}
