using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BvNugetPreviewGenerator.Generate
{
    public class FileSystemService : IFileSystemService
    {
        public Task<bool> CreateDirectoryAsync(string path)
        {
            Directory.CreateDirectory(path);
            return Task.FromResult(true);
        }

        public Task DeleteDirectoryAsync(string path, bool recursive)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive);
            return Task.CompletedTask;
        }

        public Task<bool> DirectoryExistsAsync(string path)
        {
            return Task.FromResult(Directory.Exists(path));
        }

        public Task<bool> FileExistsAsync(string path)
        {
            return Task.FromResult(File.Exists(path));
        }

        public async Task CopyFileAsync(string sourcePath, string destPath, bool overwrite)
        {
            using (var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
            using (var destStream = new FileStream(destPath, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true))
            {
                await sourceStream.CopyToAsync(destStream);
            }
        }

        public Task<IEnumerable<string>> FindFilesAsync(string directory, string searchPattern, bool searchSubdirectories)
        {
            var searchOption = searchSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(directory, searchPattern, searchOption);
            return Task.FromResult(files.AsEnumerable());
        }

        public async Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
        {
            using (var reader = new StreamReader(path))
            {
                return await reader.ReadToEndAsync();
            }
        }

        public async Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default)
        {
            using (var writer = new StreamWriter(path, false))
            {
                await writer.WriteAsync(contents);
            }
        }
    }
}
