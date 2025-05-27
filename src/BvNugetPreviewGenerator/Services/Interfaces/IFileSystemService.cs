using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BvNugetPreviewGenerator.Generate
{
    public interface IFileSystemService
    {
        Task<bool> CreateDirectoryAsync(string path);
        Task DeleteDirectoryAsync(string path, bool recursive);
        Task<bool> DirectoryExistsAsync(string path);
        Task<bool> FileExistsAsync(string path);
        Task CopyFileAsync(string sourcePath, string destPath, bool overwrite);
        Task<IEnumerable<string>> FindFilesAsync(string directory, string searchPattern, bool searchSubdirectories);
        Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);
        Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default);
    }
}
