using System;
using Microsoft.Win32;

namespace Ps1Run.Core.Infrastructure
{
    /// <summary>
    /// 注册表读取工具：App Paths 与 Uninstall 的 InstallLocation。
    /// 本机实测：VS Code 装在 D:\Soft\Code，三个"常见默认路径"全不存在，
    /// 只有 HKCU\...\App Paths\Code.exe 能命中（wiki 08 §3、12 §3）。
    /// </summary>
    public static class RegistryReader
    {
        private const string AppPathsSubKey =
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\";

        private static readonly string[] UninstallRoots =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
        };

        public static string? ResolveFromAppPaths(string executableName)
        {
            var subKey = AppPathsSubKey + executableName;

            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
                {
                    var path = ReadAppPath(hive, view, subKey);
                    if (path is not null)
                    {
                        return path;
                    }
                }
            }

            return null;
        }

        private static string? ReadAppPath(RegistryHive hive, RegistryView view, string subKey)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var key = baseKey.OpenSubKey(subKey);
                if (key?.GetValue(null) is string raw && raw.Length > 0)
                {
                    var path = raw.Trim().Trim('"');
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        return path;
                    }
                }
            }
            catch (System.Security.SecurityException)
            {
                // 无权限的分支直接跳过
            }
            catch (System.UnauthorizedAccessException)
            {
            }

            return null;
        }

        /// <summary>
        /// 在 Uninstall 项中按 DisplayName 关键字查找安装目录。
        /// 注意：VS Code 用户级安装只在 HKCU 下有记录，两个配置单元都要遍历。
        /// </summary>
        public static string? ResolveInstallLocation(string displayNameKeyword)
        {
            foreach (var root in UninstallRoots)
            {
                var location = SearchUninstallHive(RegistryHive.CurrentUser, root, displayNameKeyword)
                               ?? SearchUninstallHive(RegistryHive.LocalMachine, root, displayNameKeyword);
                if (location is not null)
                {
                    return location;
                }
            }

            return null;
        }

        private static string? SearchUninstallHive(
            RegistryHive hive,
            string rootSubKey,
            string displayNameKeyword)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                using var uninstall = baseKey.OpenSubKey(rootSubKey);
                if (uninstall is null)
                {
                    return null;
                }

                foreach (var name in uninstall.GetSubKeyNames())
                {
                    using var item = uninstall.OpenSubKey(name);
                    if (item is null)
                    {
                        continue;
                    }

                    if (item.GetValue("DisplayName") is not string displayName
                        || displayName.IndexOf(displayNameKeyword, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    if (item.GetValue("InstallLocation") is string location
                        && !string.IsNullOrWhiteSpace(location))
                    {
                        return location.Trim().Trim('"');
                    }
                }
            }
            catch (System.Security.SecurityException)
            {
            }
            catch (System.UnauthorizedAccessException)
            {
            }

            return null;
        }

        /// <summary>
        /// 读取 PowerShell 7 的安装位置（HKLM\SOFTWARE\Microsoft\PowerShellCore\InstalledVersions）。
        /// 本机实测该键下 SemanticVersion=7.6.10、InstallLocation=C:\Program Files\PowerShell\7\。
        /// </summary>
        public static string? ResolvePowerShellCoreInstallLocation()
        {
            const string root = @"SOFTWARE\Microsoft\PowerShellCore\InstalledVersions";

            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default);
                using var versions = baseKey.OpenSubKey(root);
                if (versions is null)
                {
                    return null;
                }

                foreach (var guid in versions.GetSubKeyNames())
                {
                    using var item = versions.OpenSubKey(guid);
                    if (item?.GetValue("InstallLocation") is string location
                        && !string.IsNullOrWhiteSpace(location))
                    {
                        return location.Trim().Trim('"');
                    }
                }
            }
            catch (System.Security.SecurityException)
            {
            }
            catch (System.UnauthorizedAccessException)
            {
            }

            return null;
        }
    }
}
