using System;
using System.IO;

namespace Ps1Run.Core.Infrastructure
{
    /// <summary>
    /// 文件系统抽象，便于对探测链与脚本规则指纹做无副作用单元测试（wiki 03 §8）。
    /// </summary>
    public interface IFileSystem
    {
        bool FileExists(string path);

        Stream OpenRead(string path);

        /// <summary>文件大小（字节）；文件不存在时由实现抛异常或返回 0。</summary>
        long GetFileLength(string path);

        /// <summary>文件最后修改时间（UTC）。</summary>
        DateTime GetLastWriteTimeUtc(string path);
    }
}
