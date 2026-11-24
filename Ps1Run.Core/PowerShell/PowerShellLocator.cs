using System;
using System.Collections.Generic;
using System.IO;
using Ps1Run.Core.Infrastructure;

namespace Ps1Run.Core.PowerShell
{
    /// <summary>一次探测尝试（用于诊断页展示探测链）。</summary>
    public sealed record ProbeAttempt(string Candidate, string Source, bool Exists);

    /// <summary>
    /// PowerShell 解释器探测（wiki 07 §3）。
    /// 顺序：硬编码路径 → 注册表锚点 → PATH，命中第一个即返回。
    /// </summary>
    public sealed class PowerShellLocator
    {
        private readonly IFileSystem _fileSystem;

        public PowerShellLocator(IFileSystem? fileSystem = null)
        {
            _fileSystem = fileSystem ?? PhysicalFileSystem.Instance;
        }

        public PowerShellTarget? Locate(PowerShellEdition edition, string? overridePath = null)
        {
            if (!string.IsNullOrWhiteSpace(overridePath) && _fileSystem.FileExists(overridePath!))
            {
                return new PowerShellTarget(edition, overridePath!, "Settings");
            }

            foreach (var attempt in EnumerateCandidates(edition))
            {
                if (_fileSystem.FileExists(attempt.Candidate))
                {
                    return new PowerShellTarget(edition, attempt.Candidate, attempt.Source);
                }
            }

            return null;
        }

        public IReadOnlyList<ProbeAttempt> Trace(PowerShellEdition edition)
        {
            var result = new List<ProbeAttempt>();
            foreach (var attempt in EnumerateCandidates(edition))
            {
                result.Add(attempt with { Exists = _fileSystem.FileExists(attempt.Candidate) });
            }

            return result;
        }

        private IEnumerable<ProbeAttempt> EnumerateCandidates(PowerShellEdition edition)
        {
            return edition == PowerShellEdition.Ps5 ? EnumeratePs5() : EnumeratePs7();
        }

        private IEnumerable<ProbeAttempt> EnumeratePs5()
        {
            var system32 = Path.Combine(
                PathSearch.SystemDirectory,
                "WindowsPowerShell",
                "v1.0",
                "powershell.exe");
            yield return new ProbeAttempt(system32, "System32", false);

            var sysWow64 = Path.Combine(
                PathSearch.WindowsDirectory,
                "SysWOW64",
                "WindowsPowerShell",
                "v1.0",
                "powershell.exe");
            yield return new ProbeAttempt(sysWow64, "SysWOW64", false);

            var appPath = SafeResolve(() => RegistryReader.ResolveFromAppPaths("powershell.exe"));
            if (appPath is not null)
            {
                yield return new ProbeAttempt(appPath, "AppPaths", false);
            }

            var onPath = SafeResolve(() => PathSearch.FindOnPath(_fileSystem, "powershell.exe"));
            if (onPath is not null)
            {
                yield return new ProbeAttempt(onPath, "PATH", false);
            }
        }

        private IEnumerable<ProbeAttempt> EnumeratePs7()
        {
            var core = SafeResolve(() => RegistryReader.ResolvePowerShellCoreInstallLocation());
            if (!string.IsNullOrWhiteSpace(core))
            {
                var candidate = SafeCombine(core!, "pwsh.exe");
                if (candidate is not null)
                {
                    yield return new ProbeAttempt(candidate, "PowerShellCore", false);
                }
            }

            var appPath = SafeResolve(() => RegistryReader.ResolveFromAppPaths("pwsh.exe"));
            if (appPath is not null)
            {
                yield return new ProbeAttempt(appPath, "AppPaths", false);
            }

            var stable = SafeCombine(PathSearch.ProgramFiles, "PowerShell", "7", "pwsh.exe");
            if (stable is not null)
            {
                yield return new ProbeAttempt(stable, "ProgramFiles\\PowerShell\\7", false);
            }

            var preview = SafeCombine(PathSearch.ProgramFiles, "PowerShell", "7-preview", "pwsh.exe");
            if (preview is not null)
            {
                yield return new ProbeAttempt(preview, "ProgramFiles\\PowerShell\\7-preview", false);
            }

            var onPath = SafeResolve(() => PathSearch.FindOnPath(_fileSystem, "pwsh.exe"));
            if (onPath is not null)
            {
                yield return new ProbeAttempt(onPath, "PATH", false);
            }
        }

        private static string? SafeCombine(params string?[] parts)
        {
            try
            {
                var segments = new List<string>();
                foreach (var part in parts)
                {
                    if (string.IsNullOrWhiteSpace(part))
                    {
                        return null;
                    }

                    segments.Add(part!);
                }

                return Path.Combine(segments.ToArray());
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static string? SafeResolve(Func<string?> probe)
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
