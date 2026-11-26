using System;
using System.IO;

namespace Ps1Run.Core.Infrastructure
{
    public sealed class PhysicalFileSystem : IFileSystem
    {
        public static PhysicalFileSystem Instance { get; } = new PhysicalFileSystem();

        public bool FileExists(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                return File.Exists(path);
            }
            catch (IOException)
            {
                return false;
            }
            catch (System.UnauthorizedAccessException)
            {
                return false;
            }
        }

        public Stream OpenRead(string path) => File.OpenRead(path);

        public long GetFileLength(string path) => new FileInfo(path).Length;

        public DateTime GetLastWriteTimeUtc(string path) => new FileInfo(path).LastWriteTimeUtc;
    }
}
