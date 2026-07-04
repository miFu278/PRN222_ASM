using RAGChatBot.Infrastructure.Interfaces;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using UglyToad.PdfPig;

namespace RAGChatBot.Infrastructure.Storage
{
    public class TextExtractor : ITextExtractor
    {
        public async Task<string> ExtractTextAsync(Stream fileStream, string fileExtension)
        {
            if (fileStream == null)
            {
                throw new ArgumentNullException(nameof(fileStream));
            }

            var extension = fileExtension.ToLower().Trim();

            // Đảm bảo Stream đang ở vị trí bắt đầu
            if (fileStream.CanSeek)
            {
                fileStream.Position = 0;
            }

            if (extension == ".pdf")
            {
                return ExtractTextFromPdf(fileStream);
            }
            else if (extension == ".docx")
            {
                return ExtractTextFromDocx(fileStream);
            }
            else if (extension == ".txt" || extension == ".md")
            {
                return await ExtractTextFromPlainTextAsync(fileStream);
            }
            else
            {
                throw new NotSupportedException($"Định dạng file '{extension}' chưa được hỗ trợ trích xuất văn bản!");
            }
        }

        private string ExtractTextFromPdf(Stream fileStream)
        {
            var textBuilder = new StringBuilder();
            try
            {
                // Dùng PdfDocument từ thư viện UglyToad.PdfPig
                using (var pdf = PdfDocument.Open(fileStream))
                {
                    foreach (var page in pdf.GetPages())
                    {
                        var pageText = page.Text;
                        if (!string.IsNullOrWhiteSpace(pageText))
                        {
                            textBuilder.AppendLine(pageText);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi trích xuất văn bản từ tệp PDF: {ex.Message}", ex);
            }

            return textBuilder.ToString();
        }

        private string ExtractTextFromDocx(Stream fileStream)
        {
            var textBuilder = new StringBuilder();
            try
            {
                using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Read))
                {
                    var entry = archive.GetEntry("word/document.xml");
                    if (entry == null)
                    {
                        throw new FileNotFoundException("Không tìm thấy tệp word/document.xml trong file DOCX!");
                    }

                    using (var entryStream = entry.Open())
                    {
                        var xdoc = XDocument.Load(entryStream);
                        // Tìm tất cả các thẻ paragraph <w:p>
                        var paragraphs = xdoc.Descendants().Where(e => e.Name.LocalName == "p");
                        
                        foreach (var p in paragraphs)
                        {
                            // Ghép tất cả các thẻ text <w:t> trong paragraph đó lại
                            var text = string.Concat(p.Descendants().Where(e => e.Name.LocalName == "t").Select(e => e.Value));
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                textBuilder.AppendLine(text);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi trích xuất văn bản từ tệp DOCX: {ex.Message}", ex);
            }

            return textBuilder.ToString();
        }

        private async Task<string> ExtractTextFromPlainTextAsync(Stream fileStream)
        {
            try
            {
                using (var reader = new StreamReader(fileStream, Encoding.UTF8))
                {
                    return await reader.ReadToEndAsync();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi trích xuất văn bản từ tệp văn bản thô: {ex.Message}", ex);
            }
        }
    }
}
