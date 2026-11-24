using System.IO;

namespace Ps1Run.Core.Infrastructure
{
    /// <summary>
    /// 文件系统抽象，便于对探测链做无副作用单元测试（wiki 03 §8）。
    /// </summary>
    public interface IFileSystem
    {
        bool FileExists(string path);

        Stream OpenRead(string path);
    }
}
