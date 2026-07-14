namespace RAGChatBot.Domain.Interfaces
{
    public interface IFileStorageService
    {
        /// <summary>
        /// Saves a file to the physical storage and returns the relative storage path.
        /// </summary>
        Task<string> SaveFileAsync(Stream fileStream, string fileName);

        /// <summary>
        /// Opens a stored file for reading. Implementations must support private storage.
        /// </summary>
        Task<Stream> OpenReadAsync(string storagePath);

        /// <summary>
        /// Deletes a file from the storage given its storage path/URL.
        /// </summary>
        Task DeleteFileAsync(string storagePath);
    }
}
