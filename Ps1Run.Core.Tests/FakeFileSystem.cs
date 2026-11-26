using System;
using System.Collections.Generic;
using System.IO;
using Ps1Run.Core.Infrastructure;

namespace Ps1Run.Core.Tests
{
    /// <summary>无副作用的 IFileSystem 假实现：只认存入字典的路径，可指定最后修改时间。</summary>
    public sealed class FakeFileSystem : IFileSystem
    {
        private readonly Dictionary<string, byte[]> _files =
            new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, DateTime> _modified =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        public void Add(string path, byte[] content, DateTime? lastWriteTimeUtc = null)
        {
            _files[path] = content;
            _modified[path] = lastWriteTimeUtc ?? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        public bool FileExists(string path) =>
            !string.IsNullOrWhiteSpace(path) && _files.ContainsKey(path);

        public Stream OpenRead(string path)
        {
            if (!_files.TryGetValue(path, out var content))
            {
                throw new FileNotFoundException(path);
            }

            return new MemoryStream(content, writable: false);
        }

        public long GetFileLength(string path) =>
            _files.TryGetValue(path, out var content) ? content.Length : 0;

        public DateTime GetLastWriteTimeUtc(string path) =>
            _modified.TryGetValue(path, out var time) ? time : DateTime.MinValue;
    }
}
