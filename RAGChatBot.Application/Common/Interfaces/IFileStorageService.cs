using System.IO;
using System.Threading.Tasks;

namespace RAGChatBot.Application.Common.Interfaces
{
    public interface IFileStorageService
    {
        /// <summary>
        /// Saves a file to the physical storage and returns the relative storage path.
        /// </summary>
        Task<string> SaveFileAsync(Stream fileStream, string fileName);

        /// <summary>
        /// Deletes a file from the storage given its storage path/URL.
        /// </summary>
        Task DeleteFileAsync(string storagePath);
    }
}
