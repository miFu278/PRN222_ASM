using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RAGChatBot.BLL.DTOs;
using RAGChatBot.BLL.Services;
using RAGChatBot.Domain.Constants;
using RAGChatBot.Domain.Enums;
using RAGChatBot.Domain.Interfaces;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace RAGChatBot.Presentation.Pages.Courses;

[Authorize(Roles = RoleNames.Lecturer + "," + RoleNames.Student)]
public sealed class DocumentViewModel : PageModel
{
    private readonly IDocumentService _documentService;
    private readonly ICourseService _courseService;
    private readonly ITextExtractor _textExtractor;

    public DocumentViewModel(
        IDocumentService documentService,
        ICourseService courseService,
        ITextExtractor textExtractor)
    {
        _documentService = documentService;
        _courseService = courseService;
        _textExtractor = textExtractor;
    }

    [BindProperty(SupportsGet = true)]
    public string CourseCode { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public string FileName { get; private set; } = string.Empty;
    public bool IsPdf { get; private set; }
    public bool IsDocx { get; private set; }
    public string PreviewText { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync()
    {
        if (!TryGetUserId(out var userId)) return Forbid();

        try
        {
            var document = await GetAuthorizedDocumentAsync(userId);
            FileName = document.FileName;
            var extension = Path.GetExtension(document.FileName).ToLowerInvariant();
            IsPdf = extension == ".pdf";
            IsDocx = extension == ".docx";

            if (IsDocx)
            {
                var download = await _documentService.DownloadDocumentAsync(Id, userId);
                await using var content = download.Content;
                PreviewText = await _textExtractor.ExtractTextAsync(content, extension);
                if (string.IsNullOrWhiteSpace(PreviewText))
                {
                    PreviewText = "Tài liệu không có nội dung văn bản để hiển thị.";
                }
            }

            return Page();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    public async Task<IActionResult> OnGetContentAsync()
    {
        if (!TryGetUserId(out var userId)) return Forbid();

        try
        {
            var document = await GetAuthorizedDocumentAsync(userId);
            var download = await _documentService.DownloadDocumentAsync(Id, userId);
            var disposition = new ContentDispositionHeaderValue("inline")
            {
                FileNameStar = download.FileName
            };
            Response.Headers.ContentDisposition = disposition.ToString();
            return new FileStreamResult(download.Content, GetContentType(document.FileName))
            {
                EnableRangeProcessing = true
            };
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    public async Task<IActionResult> OnGetDownloadAsync()
    {
        if (!TryGetUserId(out var userId)) return Forbid();

        try
        {
            var document = await GetAuthorizedDocumentAsync(userId);
            var download = await _documentService.DownloadDocumentAsync(Id, userId);
            return new FileStreamResult(download.Content, GetContentType(document.FileName))
            {
                FileDownloadName = download.FileName,
                EnableRangeProcessing = true
            };
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private async Task<DocumentDto> GetAuthorizedDocumentAsync(Guid userId)
    {
        var document = await _documentService.GetDocumentByIdAsync(Id)
            ?? throw new KeyNotFoundException("Không tìm thấy tài liệu.");
        if (!string.Equals(document.CourseCode, CourseCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new KeyNotFoundException("Tài liệu không thuộc môn học này.");
        }

        if (User.IsInRole(RoleNames.Student))
        {
            if (!document.IsApproved || document.Status != DocumentStatus.Success)
            {
                throw new UnauthorizedAccessException("Tài liệu chưa sẵn sàng cho sinh viên.");
            }
            return document;
        }

        if (User.IsInRole(RoleNames.Lecturer) &&
            await _courseService.IsSubjectLeaderAsync(CourseCode, userId))
        {
            return document;
        }

        throw new UnauthorizedAccessException("Bạn không có quyền xem tài liệu này.");
    }

    private bool TryGetUserId(out Guid userId)
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private static string GetContentType(string fileName)
        => Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => "application/octet-stream"
        };
}
