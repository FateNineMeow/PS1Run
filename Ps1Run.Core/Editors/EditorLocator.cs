using System;
using System.Collections.Generic;
using System.IO;
using Ps1Run.Core.Infrastructure;

namespace Ps1Run.Core.Editors
{
    /// <summary>
    /// 编辑器解析：四级回退链（wiki 08 §4）。
    /// 1 用户配置 → 2 常见硬编码路径 → 3 注册表(App Paths / Uninstall) → 4 PATH → 5 记事本兜底。
    /// </summary>
    public sealed class EditorLocator
    {
        private readonly IFileSystem _fileSystem;

        public EditorLocator(IFileSystem? fileSystem = null)
        {
            _fileSystem = fileSystem ?? PhysicalFileSystem.Instance;
        }

        public EditorDefinition? Resolve(string? customPath = null)
        {
            // 1) 用户显式配置
            if (!string.IsNullOrWhiteSpace(customPath) && _fileSystem.FileExists(customPath!))
            {
                return new EditorDefinition("自定义编辑器", customPath!, "%1", "Settings");
            }

            // 2) 常见硬编码路径
            foreach (var candidate in EnumerateKnownPaths())
            {
                if (_fileSystem.FileExists(candidate.Path))
                {
                    return new EditorDefinition(candidate.DisplayName, candidate.Path, "%1", "KnownPath");
                }
            }

            // 3) 注册表 App Paths
            foreach (var (displayName, exeName) in EnumerateRegistryTargets())
            {
                var fromAppPaths = TryProbe(() => RegistryReader.ResolveFromAppPaths(exeName));
                if (fromAppPaths is not null && _fileSystem.FileExists(fromAppPaths))
                {
                    return new EditorDefinition(displayName, fromAppPaths, "%1", "AppPaths");
                }
            }

            // 3b) 注册表 Uninstall 的 InstallLocation
            foreach (var (displayName, keyword, relativeExe) in EnumerateUninstallTargets())
            {
                var location = TryProbe(() => RegistryReader.ResolveInstallLocation(keyword));
                if (string.IsNullOrWhiteSpace(location))
                {
                    continue;
                }

                var path = SafeCombine(location!, relativeExe);
                if (path is not null && _fileSystem.FileExists(path))
                {
                    return new EditorDefinition(displayName, path, "%1", "Uninstall");
                }
            }

            // 4) PATH
            foreach (var (displayName, exeName) in EnumerateRegistryTargets())
            {
                var onPath = TryProbe(() => PathSearch.FindOnPath(_fileSystem, exeName));
                if (onPath is not null)
                {
                    return new EditorDefinition(displayName, onPath, "%1", "PATH");
                }
            }

            // 5) 记事本兜底
            var notepad = Path.Combine(PathSearch.SystemDirectory, "notepad.exe");
            if (_fileSystem.FileExists(notepad))
            {
                return new EditorDefinition("记事本（兜底）", notepad, "%1", "Fallback");
            }

            return null;
        }

        private IEnumerable<(string DisplayName, string Path)> EnumerateKnownPaths()
        {
            yield return ("Visual Studio Code", Path.Combine(PathSearch.LocalApplicationData, "Programs", "Microsoft VS Code", "Code.exe"));
            yield return ("Visual Studio Code", Path.Combine(PathSearch.ProgramFiles, "Microsoft VS Code", "Code.exe"));
            yield return ("Visual Studio Code", Path.Combine(PathSearch.ProgramFilesX86, "Microsoft VS Code", "Code.exe"));
            yield return ("Notepad++", Path.Combine(PathSearch.ProgramFiles, "Notepad++", "notepad++.exe"));
            yield return ("Notepad++", Path.Combine(PathSearch.ProgramFilesX86, "Notepad++", "notepad++.exe"));
            yield return ("Sublime Text", Path.Combine(PathSearch.ProgramFiles, "Sublime Text", "sublime_text.exe"));
            yield return ("PowerShell ISE", Path.Combine(PathSearch.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell_ise.exe"));
            yield return ("记事本", Path.Combine(PathSearch.SystemDirectory, "notepad.exe"));
        }

        private static IEnumerable<(string DisplayName, string ExeName)> EnumerateRegistryTargets()
        {
            yield return ("Visual Studio Code", "Code.exe");
            yield return ("Notepad++", "notepad++.exe");
            yield return ("Sublime Text", "sublime_text.exe");
        }

        private static IEnumerable<(string DisplayName, string Keyword, string RelativeExe)> EnumerateUninstallTargets()
        {
            yield return ("Visual Studio Code", "Visual Studio Code", "Code.exe");
            yield return ("Notepad++", "Notepad++", "notepad++.exe");
            yield return ("Sublime Text", "Sublime Text", "sublime_text.exe");
        }

        private static string? SafeCombine(string root, string relative)
        {
            try
            {
                var path = Path.Combine(root, relative);
                return Path.GetFullPath(path);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static string? TryProbe(Func<string?> probe)
        {
            try
            {
                return probe();
            }
            catch (System.Security.SecurityException)
            {
                return null;
            }
        }
    }
}
