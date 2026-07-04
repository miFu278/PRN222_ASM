using RAGChatBot.Infrastructure.Interfaces;
using RAGChatBot.Application.ServiceInterfaces;
using System;
using System.Collections.Generic;

namespace RAGChatBot.Application.BusinessComponents
{
    public class ChunkingService : IChunkingService
    {
        public List<string> ChunkText(string text, int chunkSize = 500, int overlap = 50)
        {
            var chunks = new List<string>();

            if (string.IsNullOrWhiteSpace(text))
            {
                return chunks;
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

                // Đi tới vị trí bắt đầu của chunk tiếp theo (có trừ đi phần overlap)
                start += (chunkSize - overlap);

                // Điểm dừng an toàn
                if (start >= textLength || length < chunkSize)
                {
                    break;
                }
            }

            return chunks;
        }
    }
}
