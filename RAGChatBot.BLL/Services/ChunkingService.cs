using RAGChatBot.Domain.Interfaces;
using System;
using System.Collections.Generic;

namespace RAGChatBot.BLL.Services
{
    // Stateless by design, so this service is safe to register as a singleton.
    public sealed class ChunkingService : IChunkingService
    {
        public List<string> ChunkText(string text, string strategy = "Character", int chunkSize = 500, int overlap = 50)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<string>();
            }

            if (chunkSize <= 0)
            {
                throw new ArgumentException("Kích thước chunk phải lớn hơn 0!", nameof(chunkSize));
            }

            if (overlap < 0 || overlap >= chunkSize)
            {
                throw new ArgumentException("Overlap phải lớn hơn hoặc bằng 0 và nhỏ hơn kích thước chunk!", nameof(overlap));
            }

            // Chuẩn hóa khoảng trắng thừa
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");

            if (string.Equals(strategy, "Paragraph", StringComparison.OrdinalIgnoreCase))
            {
                return ChunkByParagraph(text, chunkSize, overlap);
            }
            else if (string.Equals(strategy, "Word", StringComparison.OrdinalIgnoreCase))
            {
                return ChunkByWord(text, chunkSize, overlap);
            }
            else // Character
            {
                return ChunkByCharacter(text, chunkSize, overlap);
            }
        }

        private static List<string> ChunkByCharacter(string text, int chunkSize, int overlap)
        {
            var chunks = new List<string>();
            int start = 0;
            int textLength = text.Length;

            while (start < textLength)
            {
                int length = Math.Min(chunkSize, textLength - start);
                string chunk = text.Substring(start, length).Trim();

                if (!string.IsNullOrWhiteSpace(chunk))
                {
                    chunks.Add(chunk);
                }

                start += (chunkSize - overlap);

                if (start >= textLength || length < chunkSize)
                {
                    break;
                }
            }

            return chunks;
        }

        private static List<string> ChunkByWord(string text, int chunkSize, int overlap)
        {
            var chunks = new List<string>();
            var words = text.Split(new[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            int totalWords = words.Length;
            int start = 0;

            while (start < totalWords)
            {
                int count = Math.Min(chunkSize, totalWords - start);
                var chunkWords = new System.Span<string>(words, start, count);
                string chunk = string.Join(" ", chunkWords.ToArray()).Trim();

                if (!string.IsNullOrWhiteSpace(chunk))
                {
                    chunks.Add(chunk);
                }

                start += (chunkSize - overlap);

                if (start >= totalWords || count < chunkSize)
                {
                    break;
                }
            }

            return chunks;
        }

        private static List<string> ChunkByParagraph(string text, int chunkSize, int overlap)
        {
            var chunks = new List<string>();
            var paragraphs = text.Split(new[] { "\n\n", "\n \n" }, StringSplitOptions.RemoveEmptyEntries)
                                 .Select(p => p.Trim())
                                 .Where(p => !string.IsNullOrWhiteSpace(p))
                                 .ToList();

            if (paragraphs.Count == 0) return chunks;

            var currentChunk = new List<string>();
            int currentLength = 0;

            for (int i = 0; i < paragraphs.Count; i++)
            {
                var p = paragraphs[i];

                if (p.Length > chunkSize)
                {
                    if (currentChunk.Count > 0)
                    {
                        chunks.Add(string.Join("\n\n", currentChunk));
                        currentChunk.Clear();
                        currentLength = 0;
                    }

                    var subChunks = ChunkByCharacter(p, chunkSize, overlap);
                    chunks.AddRange(subChunks);
                    continue;
                }

                if (currentChunk.Count > 0 && currentLength + p.Length + 2 > chunkSize)
                {
                    chunks.Add(string.Join("\n\n", currentChunk));
                    var lastParagraph = currentChunk.Last();
                    currentChunk.Clear();

                    if (overlap > 0 && lastParagraph.Length < chunkSize)
                    {
                        currentChunk.Add(lastParagraph);
                        currentLength = lastParagraph.Length;
                    }
                    else
                    {
                        currentLength = 0;
                    }
                }

                currentChunk.Add(p);
                currentLength += (currentChunk.Count == 1 ? 0 : 2) + p.Length;
            }

            if (currentChunk.Count > 0)
            {
                chunks.Add(string.Join("\n\n", currentChunk));
            }

            return chunks;
        }
    }
}
