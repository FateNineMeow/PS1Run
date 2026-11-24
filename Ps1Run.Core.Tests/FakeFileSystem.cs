using System;
using System.Collections.Generic;
using System.IO;
using Ps1Run.Core.Infrastructure;

namespace Ps1Run.Core.Tests
{
    /// <summary>无副作用的 IFileSystem 假实现：只认存入字典的路径。</summary>
    public sealed class FakeFileSystem : IFileSystem
    {
        private readonly Dictionary<string, byte[]> _files =
            new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        public void Add(string path, byte[] content) => _files[path] = content;

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
    }
}
