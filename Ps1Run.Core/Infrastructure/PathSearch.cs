using System;
using System.Collections.Generic;
using System.IO;

namespace Ps1Run.Core.Infrastructure
{
    /// <summary>
    /// PATH 环境变量查找（探测链的最后一级）。
    /// </summary>
    public static class PathSearch
    {
        public static string? FindOnPath(IFileSystem fileSystem, string executableName)
        {
            var raw = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            foreach (var dir in raw.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir))
                {
                    continue;
                }

                string candidate;
                try
                {
                    candidate = Path.Combine(dir.Trim(), executableName);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                if (fileSystem.FileExists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        public static string SystemDirectory => Environment.SystemDirectory;

        public static string WindowsDirectory =>
            Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        public static string ProgramFiles =>
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        public static string ProgramFilesX86 =>
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        public static string LocalApplicationData =>
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        public static IEnumerable<string> CombineAll(string? root, params string[] relativeParts)
        {
            if (string.IsNullOrWhiteSpace(root) || relativeParts.Length == 0)
            {
                yield break;
            }

            string path = root!;
            foreach (var part in relativeParts)
            {
                path = Path.Combine(path, part);
            }

            yield return path;
        }
    }
}
