using RAGChatBot.Application.Common.Interfaces;
using RAGChatBot.Application.DTOs;
using RAGChatBot.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RAGChatBot.Application.Services
{
    public class CourseService : ICourseService
    {
        private readonly ICourseRepository _courseRepository;
        private readonly IUserRepository _userRepository;
        private readonly IKnowledgeDocumentRepository _documentRepository;
        private readonly IFileStorageService _fileStorageService;

        public CourseService(
            ICourseRepository courseRepository, 
            IUserRepository userRepository,
            IKnowledgeDocumentRepository documentRepository,
            IFileStorageService fileStorageService)
        {
            _courseRepository = courseRepository;
            _userRepository = userRepository;
            _documentRepository = documentRepository;
            _fileStorageService = fileStorageService;
        }

        public async Task<CourseDto> CreateCourseAsync(CourseDto courseDto, Guid userId)
        {
            var course = new Course
            {
                Code = courseDto.Code.Trim().ToUpper(),
                Name = courseDto.Name.Trim(),
                Description = courseDto.Description?.Trim(),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId,
                SubjectLeaderId = courseDto.SubjectLeaderId
            };

            await _courseRepository.AddAsync(course);

            courseDto.Id = course.Id;
            courseDto.CreatedAt = course.CreatedAt;
            return courseDto;
        }

        public async Task<IEnumerable<CourseDto>> GetAllCoursesAsync()
        {
            var courses = await _courseRepository.GetAllAsync();
            var users = await _userRepository.GetAllAsync();
            var userMap = users.ToDictionary(u => u.Id, u => !string.IsNullOrEmpty(u.FullName) ? u.FullName : u.Username);
            
            return courses.Select(c => MapToDto(c, userMap));
        }

        public async Task<IEnumerable<CourseDto>> SearchCoursesAsync(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return await GetAllCoursesAsync();
            }

            var courses = await _courseRepository.SearchAsync(keyword);
            var users = await _userRepository.GetAllAsync();
            var userMap = users.ToDictionary(u => u.Id, u => !string.IsNullOrEmpty(u.FullName) ? u.FullName : u.Username);
            
            return courses.Select(c => MapToDto(c, userMap));
        }

        public async Task<bool> IsSubjectLeaderAsync(string courseCode, Guid userId)
        {
            var courses = await _courseRepository.GetAllAsync();
            var course = courses.FirstOrDefault(c => c.Code.Equals(courseCode, StringComparison.OrdinalIgnoreCase));
            return course != null && course.SubjectLeaderId == userId;
        }

        private static CourseDto MapToDto(Course c, Dictionary<Guid, string> userMap)
        {
            return new CourseDto
            {
                Id = c.Id,
                Code = c.Code,
                Name = c.Name,
                Description = c.Description,
                CreatedAt = c.CreatedAt,
                SubjectLeaderId = c.SubjectLeaderId,
                SubjectLeaderName = c.SubjectLeaderId.HasValue && userMap.TryGetValue(c.SubjectLeaderId.Value, out var name) ? name : "Chưa phân công"
            };
        }

        public async Task UpdateSubjectLeaderAsync(Guid courseId, Guid subjectLeaderId)
        {
            var course = await _courseRepository.GetByIdAsync(courseId);
            if (course == null)
            {
                throw new KeyNotFoundException("Không tìm thấy môn học!");
            }

            var user = await _userRepository.GetByIdAsync(subjectLeaderId);
            if (user == null || (!user.Role.Equals("Lecturer", StringComparison.OrdinalIgnoreCase) && !user.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException("Người dùng được chọn không hợp lệ hoặc không phải Giảng viên/Admin!");
            }

            course.SubjectLeaderId = subjectLeaderId;
            await _courseRepository.UpdateAsync(course);
        }

        public async Task UpdateCourseAsync(CourseDto courseDto)
        {
            var course = await _courseRepository.GetByIdAsync(courseDto.Id);
            if (course == null)
            {
                throw new KeyNotFoundException("Không tìm thấy môn học cần chỉnh sửa!");
            }

            var oldCode = course.Code;
            var newCode = courseDto.Code.Trim().ToUpper();

            // Nếu thay đổi mã môn, kiểm tra xem có bị trùng với môn khác không
            if (!oldCode.Equals(newCode, StringComparison.OrdinalIgnoreCase))
            {
                var courses = await _courseRepository.GetAllAsync();
                if (courses.Any(c => c.Code.Equals(newCode, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException($"Mã môn học '{newCode}' đã tồn tại!");
                }

                // Cập nhật mã môn trong tất cả tài liệu cũ sang mã mới
                var courseDocs = await _documentRepository.GetByCourseCodeAsync(oldCode);
                foreach (var doc in courseDocs)
                {
                    doc.CourseCode = newCode;
                }
                await _documentRepository.SaveChangesAsync();
            }

            // Kiểm tra phân quyền Trưởng bộ môn nếu có chỉ định
            if (courseDto.SubjectLeaderId.HasValue)
            {
                var user = await _userRepository.GetByIdAsync(courseDto.SubjectLeaderId.Value);
                if (user == null || (!user.Role.Equals("Lecturer", StringComparison.OrdinalIgnoreCase) && !user.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new ArgumentException("Trưởng bộ môn được chọn không hợp lệ hoặc không phải Giảng viên/Admin!");
                }
            }

            course.Code = newCode;
            course.Name = courseDto.Name.Trim();
            course.Description = courseDto.Description?.Trim();
            course.SubjectLeaderId = courseDto.SubjectLeaderId;

            await _courseRepository.UpdateAsync(course);
        }

        public async Task DeleteCourseAsync(Guid id)
        {
            var course = await _courseRepository.GetByIdAsync(id);
            if (course == null)
            {
                throw new KeyNotFoundException("Không tìm thấy môn học cần xóa!");
            }

            // Xóa tất cả tài liệu thuộc môn học này
            var courseDocs = await _documentRepository.GetByCourseCodeAsync(course.Code);
            foreach (var doc in courseDocs)
            {
                try
                {
                    await _fileStorageService.DeleteFileAsync(doc.StoragePath);
                }
                catch (Exception)
                {
                    // Tiếp tục dọn dẹp DB ngay cả khi tệp vật lý lỗi hoặc đã mất
                }
                await _documentRepository.DeleteAsync(doc);
            }
            await _documentRepository.SaveChangesAsync();

            // Xóa môn học
            await _courseRepository.DeleteAsync(course);
        }

        public async Task<IEnumerable<CourseDto>> GetCoursesBySubjectLeaderAsync(Guid userId)
        {
            var courses = await _courseRepository.GetBySubjectLeaderIdAsync(userId);
            var users = await _userRepository.GetAllAsync();
            var userMap = users.ToDictionary(u => u.Id, u => !string.IsNullOrEmpty(u.FullName) ? u.FullName : u.Username);

            return courses.Select(c => MapToDto(c, userMap));
        }
    }
}
