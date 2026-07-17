using System.Text;
using RAGChatBot.DAL.Services;
using Xunit;

namespace RAGChatBot.Tests;

public sealed class TextExtractorTests
{
    [Fact]
    public async Task Extract_NullStreamThrows()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            new TextExtractor().ExtractTextAsync(null!, ".txt"));
    }

    [Fact]
    public async Task Extract_UnsupportedExtensionThrows()
    {
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            new TextExtractor().ExtractTextAsync(new MemoryStream(), ".exe"));
    }

    [Theory]
    [InlineData(".txt")]
    [InlineData(" .MD ")]
    public async Task Extract_PlainTextResetsSeekableStreamAndReadsUtf8(string extension)
    {
        const string expected = "Xin chào RAG chatbot";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(expected));
        stream.Position = stream.Length;

        var result = await new TextExtractor().ExtractTextAsync(stream, extension);

        Assert.Equal(expected, result);
    }
}
