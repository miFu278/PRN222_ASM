using RAGChatBot.BLL.Services;
using Xunit;

namespace RAGChatBot.Tests;

public sealed class ChunkingServiceTests
{
    private readonly ChunkingService _service = new();

    [Fact]
    public void ChunkText_ReturnsEmpty_ForBlankInput()
        => Assert.Empty(_service.ChunkText("  \r\n  "));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 0)]
    public void ChunkText_RejectsNonPositiveChunkSize(int chunkSize, int overlap)
        => Assert.Throws<ArgumentException>(() =>
            _service.ChunkText("content", chunkSize: chunkSize, overlap: overlap));

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    [InlineData(6)]
    public void ChunkText_RejectsInvalidOverlap(int overlap)
        => Assert.Throws<ArgumentException>(() =>
            _service.ChunkText("content", chunkSize: 5, overlap: overlap));

    [Fact]
    public void CharacterStrategy_PreservesConfiguredOverlap()
    {
        var chunks = _service.ChunkText("abcdefghij", "Character", chunkSize: 6, overlap: 2);

        Assert.Equal(["abcdef", "efghij"], chunks);
    }

    [Fact]
    public void WordStrategy_PreservesConfiguredOverlap()
    {
        var chunks = _service.ChunkText("one two three four five", "Word", chunkSize: 3, overlap: 1);

        Assert.Equal(["one two three", "three four five"], chunks);
    }

    [Fact]
    public void ParagraphStrategy_SplitsOversizedParagraphWithoutLosingText()
    {
        var chunks = _service.ChunkText("abcdefghij\n\nxy", "Paragraph", chunkSize: 6, overlap: 2);

        Assert.Equal(["abcdef", "efghij", "xy"], chunks);
    }
}
