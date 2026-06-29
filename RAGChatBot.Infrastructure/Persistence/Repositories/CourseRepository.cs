using Microsoft.EntityFrameworkCore;
using RAGChatBot.Application.Common.Interfaces;
using RAGChatBot.Domain.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RAGChatBot.Infrastructure.Persistence.Repositories
{
    public class CourseRepository : ICourseRepository
    {
        private readonly AppDbContext _context;

        public CourseRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Course> AddAsync(Course course)
        {
            await _context.Courses.AddAsync(course);
            await _context.SaveChangesAsync();
            return course;
        }

        public async Task<IEnumerable<Course>> GetAllAsync()
        {
            return await _context.Courses
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Course>> SearchAsync(string keyword)
        {
            var keywordLower = keyword.ToLower();
            return await _context.Courses
                .Where(c => c.Code.ToLower().Contains(keywordLower) || c.Name.ToLower().Contains(keywordLower))
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<Course?> GetByIdAsync(System.Guid id)
        {
            return await _context.Courses.FindAsync(id);
        }

        public async Task UpdateAsync(Course course)
        {
            _context.Courses.Update(course);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Course course)
        {
            _context.Courses.Remove(course);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<Course>> GetBySubjectLeaderIdAsync(System.Guid userId)
        {
            return await _context.Courses
                .Where(c => c.SubjectLeaderId == userId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }
    }
}
