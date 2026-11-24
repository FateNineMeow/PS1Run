using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using Ps1Run.Core.Infrastructure;

namespace Ps1Run.Core.Associations
{
    public enum AssociationScope
    {
        /// <summary>HKCU，无需管理员权限（默认）。</summary>
        User = 0,

        /// <summary>HKLM，需要 UAC 提升（可选，F1.9）。</summary>
        Machine = 1,
    }

    public sealed class AssociationResult
    {
        public AssociationResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public bool Success { get; }

        public string Message { get; }
    }

    /// <summary>关联状态快照（wiki 02 F5.4）。</summary>
    public sealed class AssociationStatus
    {
        public AssociationStatus(
            bool progIdRegistered,
            bool openVerbRegistered,
            bool runVerbRegistered,
            bool editVerbRegistered,
            bool extensionAdvertised,
            UserChoiceInfo userChoice,
            string executablePath)
        {
            ProgIdRegistered = progIdRegistered;
            OpenVerbRegistered = openVerbRegistered;
            RunVerbRegistered = runVerbRegistered;
            EditVerbRegistered = editVerbRegistered;
            ExtensionAdvertised = extensionAdvertised;
            UserChoice = userChoice;
            ExecutablePath = executablePath;
        }

        public bool ProgIdRegistered { get; }

        public bool OpenVerbRegistered { get; }

        public bool RunVerbRegistered { get; }

        public bool EditVerbRegistered { get; }

        public bool ExtensionAdvertised { get; }

        public UserChoiceInfo UserChoice { get; }

        public string ExecutablePath { get; }

        public bool FullyRegistered =>
            ProgIdRegistered && OpenVerbRegistered && RunVerbRegistered
            && EditVerbRegistered && ExtensionAdvertised;
    }

    /// <summary>
    /// 关联注册 / 注销 / 状态查询（wiki 06、02 F1.x）。
    /// 默认只写 HKCU；HKLM 需先经 ElevationHelper 提升。
    /// </summary>
    public sealed class AssociationService
    {
        private readonly string _executablePath;

        public AssociationService(string? executablePath = null)
        {
            _executablePath = string.IsNullOrWhiteSpace(executablePath)
                ? ResolveDefaultExecutable()
                : executablePath!;
        }

        public string ExecutablePath => _executablePath;

        public AssociationResult Register(AssociationScope scope = AssociationScope.User)
        {
            try
            {
                using var hive = OpenHive(scope, writable: true);

                using (var progId = hive.CreateSubKey(AssociationRegistration.ProgIdKeyPath))
                {
                    progId.SetValue(null, AssociationRegistration.FriendlyTypeName);
                    progId.SetValue("FriendlyTypeName", AssociationRegistration.FriendlyTypeName);

                    using (var icon = progId.CreateSubKey("DefaultIcon"))
                    {
                        // exe 已通过 <ApplicationIcon>icon.ico</ApplicationIcon> 内嵌 logo，
                        // DefaultIcon 直接指向 exe，索引 0 即新图标，无需外部 ico 文件。
                        icon.SetValue(null, AssociationRegistration.BuildDefaultIcon(_executablePath));
                    }

                    using (var shell = progId.CreateSubKey("shell"))
                    {
                        // shell 默认动词 = open（双击走 --open）
                        shell.SetValue(null, AssociationRegistration.VerbOpen);

                        WriteVerb(shell, AssociationRegistration.VerbOpen,
                            AssociationRegistration.BuildOpenCommand(_executablePath));
                        WriteVerb(shell, AssociationRegistration.VerbRun,
                            AssociationRegistration.BuildRunCommand(_executablePath));
                        WriteVerb(shell, AssociationRegistration.VerbEdit,
                            AssociationRegistration.BuildEditCommand(_executablePath));
                    }
                }

                using (var openWith = hive.CreateSubKey(AssociationRegistration.OpenWithProgIdsKeyPath))
                {
                    // REG_NONE 空值，仅作"可用程序"声明
                    openWith.SetValue(
                        AssociationRegistration.ProgId,
                        Array.Empty<byte>(),
                        RegistryValueKind.None);
                }

                ShellNotify.NotifyAssociationsChanged();
                return new AssociationResult(true, "关联已注册（" + ScopeName(scope) + "）。");
            }
            catch (UnauthorizedAccessException)
            {
                return new AssociationResult(false, "写入被拒绝，机器级注册需要管理员权限。");
            }
            catch (System.Security.SecurityException)
            {
                return new AssociationResult(false, "写入被安全策略拒绝，请以管理员身份运行。");
            }
            catch (System.IO.IOException ex)
            {
                return new AssociationResult(false, "注册表写入失败：" + ex.Message);
            }
        }

        public AssociationResult Unregister(AssociationScope scope = AssociationScope.User)
        {
            try
            {
                using var hive = OpenHive(scope, writable: true);

                DeleteTree(hive, AssociationRegistration.ProgIdKeyPath);

                using (var openWith = hive.OpenSubKey(
                    AssociationRegistration.OpenWithProgIdsKeyPath,
                    writable: true))
                {
                    if (openWith is not null
                        && openWith.GetValue(AssociationRegistration.ProgId) is not null)
                    {
                        openWith.DeleteValue(AssociationRegistration.ProgId, throwOnMissingValue: false);
                    }
                }

                ShellNotify.NotifyAssociationsChanged();
                return new AssociationResult(true, "关联已注销（" + ScopeName(scope) + "），第三方项未改动。");
            }
            catch (UnauthorizedAccessException)
            {
                return new AssociationResult(false, "删除被拒绝，机器级注销需要管理员权限。");
            }
            catch (System.Security.SecurityException)
            {
                return new AssociationResult(false, "删除被安全策略拒绝，请以管理员身份运行。");
            }
            catch (System.IO.IOException ex)
            {
                return new AssociationResult(false, "注册表删除失败：" + ex.Message);
            }
        }

        /// <summary>
        /// 调用系统"打开方式"对话框，引导用户自行勾选并确认默认程序（F1.7）。
        /// 不伪造 UserChoice 的 Hash；用一个临时 .ps1 作为对话框的上下文文件。
        /// </summary>
        public AssociationResult ShowOpenWithDialog()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), "Ps1Run-openwith-" + Guid.NewGuid().ToString("N") + ".ps1");
            try
            {
                File.WriteAllText(tempPath, string.Empty);

                var info = new NativeMethods.OPENASINFO
                {
                    pcszFile = tempPath,
                    pcszClass = AssociationRegistration.ProgId,
                    oaifInFlags = NativeMethods.OAIF_ALLOW_REGISTRATION | NativeMethods.OAIF_REGISTER_EXT,
                };

                var hr = NativeMethods.SHOpenWithDialog(IntPtr.Zero, ref info);
                if (hr < 0)
                {
                    return new AssociationResult(false, "已取消系统“打开方式”对话框（HRESULT=" + hr + "）。");
                }

                ShellNotify.NotifyAssociationsChanged();
                return new AssociationResult(true, "已在系统“打开方式”中完成选择。");
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                return new AssociationResult(false, "无法打开系统“打开方式”对话框：" + ex.Message);
            }
            catch (IOException ex)
            {
                return new AssociationResult(false, "无法准备临时脚本文件：" + ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return new AssociationResult(false, "无法准备临时脚本文件：" + ex.Message);
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        public AssociationStatus GetStatus()
        {
            bool progId = false, openVerb = false, runVerb = false, editVerb = false, advertised = false;

            try
            {
                using var hive = OpenHive(AssociationScope.User, writable: false);
                using var progIdKey = hive.OpenSubKey(AssociationRegistration.ProgIdKeyPath);
                if (progIdKey is not null)
                {
                    progId = true;
                    using var shell = progIdKey.OpenSubKey("shell");
                    openVerb = VerbExists(shell, AssociationRegistration.VerbOpen);
                    runVerb = VerbExists(shell, AssociationRegistration.VerbRun);
                    editVerb = VerbExists(shell, AssociationRegistration.VerbEdit);
                }

                using var openWith = hive.OpenSubKey(AssociationRegistration.OpenWithProgIdsKeyPath);
                advertised = openWith?.GetValue(AssociationRegistration.ProgId) is not null;
            }
            catch (System.Security.SecurityException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return new AssociationStatus(
                progId, openVerb, runVerb, editVerb, advertised,
                UserChoiceGuard.Inspect(),
                _executablePath);
        }

        private static void WriteVerb(RegistryKey shell, string verb, string commandLine)
        {
            using var command = shell.CreateSubKey(verb + "\\command");
            command.SetValue(null, commandLine);
        }

        private static bool VerbExists(RegistryKey? shell, string verb)
        {
            if (shell is null)
            {
                return false;
            }

            using var command = shell.OpenSubKey(verb + "\\command");
            return command?.GetValue(null) is string value && value.Length > 0;
        }

        private static void DeleteTree(RegistryKey hive, string path)
        {
            using var key = hive.OpenSubKey(path, writable: true);
            if (key is null)
            {
                return;
            }

            hive.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
        }

        private static RegistryKey OpenHive(AssociationScope scope, bool writable)
        {
            var hive = scope == AssociationScope.Machine
                ? RegistryHive.LocalMachine
                : RegistryHive.CurrentUser;
            return RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        }

        private static string ScopeName(AssociationScope scope) =>
            scope == AssociationScope.Machine ? "机器级 HKLM" : "用户级 HKCU";

        private static string ResolveDefaultExecutable()
        {
            try
            {
                using var process = Process.GetCurrentProcess();
                return process.MainModule?.FileName ?? "Ps1Run.exe";
            }
            catch (InvalidOperationException)
            {
                return "Ps1Run.exe";
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return "Ps1Run.exe";
            }
        }
    }
}
