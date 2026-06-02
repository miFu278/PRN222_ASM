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

        public CourseService(ICourseRepository courseRepository)
        {
            _courseRepository = courseRepository;
        }

        public async Task<CourseDto> CreateCourseAsync(CourseDto courseDto, Guid userId)
        {
            var course = new Course
            {
                Code = courseDto.Code,
                Name = courseDto.Name,
                Description = courseDto.Description,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            };

            await _courseRepository.AddAsync(course);

            courseDto.Id = course.Id;
            courseDto.CreatedAt = course.CreatedAt;
            return courseDto;
        }

        public async Task<IEnumerable<CourseDto>> GetAllCoursesAsync()
        {
            var courses = await _courseRepository.GetAllAsync();
            return courses.Select(MapToDto);
        }

        public async Task<IEnumerable<CourseDto>> SearchCoursesAsync(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return await GetAllCoursesAsync();
            }

            var courses = await _courseRepository.SearchAsync(keyword);
            return courses.Select(MapToDto);
        }

        private static CourseDto MapToDto(Course c)
        {
            return new CourseDto
            {
                Id = c.Id,
                Code = c.Code,
                Name = c.Name,
                Description = c.Description,
                CreatedAt = c.CreatedAt
            };
        }
    }
}
