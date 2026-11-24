using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Ps1Run.Core.Editors;
using Ps1Run.Core.PowerShell;

namespace Ps1Run.Core.Infrastructure
{
    /// <summary>进程启动结果。</summary>
    public sealed class ProcessLaunchResult
    {
        public ProcessLaunchResult(bool started, int? processId, string? error)
        {
            Started = started;
            ProcessId = processId;
            Error = error;
        }

        public bool Started { get; }

        public int? ProcessId { get; }

        public string? Error { get; }
    }

    /// <summary>
    /// 目标程序启动（wiki 02 F3/F4 进程启动约定）。
    /// 工作目录固定为脚本所在目录，保证脚本内的相对路径正确。
    /// </summary>
    public sealed class ProcessLauncher
    {
        public ProcessLaunchResult StartPowerShell(PowerShellTarget target, string scriptPath)
        {
            if (target is null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            var info = new ProcessStartInfo
            {
                FileName = target.ExecutablePath,
                Arguments = "-NoProfile -File \"" + scriptPath + "\"",
                UseShellExecute = false,
                CreateNoWindow = false,
                WorkingDirectory = ResolveWorkingDirectory(scriptPath),
            };

            return Start(info);
        }

        public ProcessLaunchResult StartEditor(EditorDefinition editor, string scriptPath)
        {
            if (editor is null)
            {
                throw new ArgumentNullException(nameof(editor));
            }

            var info = new ProcessStartInfo
            {
                FileName = editor.ExecutablePath,
                Arguments = editor.BuildArguments(scriptPath),
                UseShellExecute = false,
                CreateNoWindow = false,
                WorkingDirectory = ResolveWorkingDirectory(scriptPath),
            };

            return Start(info);
        }

        private static ProcessLaunchResult Start(ProcessStartInfo info)
        {
            try
            {
                var process = Process.Start(info);
                DiagnosticsLog.Info(
                    "目标进程已启动：exe=" + info.FileName
                    + " pid=" + (process?.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "<未知>")
                    + " cwd=" + info.WorkingDirectory);
                return new ProcessLaunchResult(true, process?.Id, null);
            }
            catch (Win32Exception ex)
            {
                // 典型：ERROR_FILE_NOT_FOUND(2) / 用户取消 UAC(1223)
                DiagnosticsLog.Error(
                    "启动目标程序失败：exe=" + info.FileName
                    + " nativeErrorCode=" + ex.NativeErrorCode.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " message=" + ex.Message);
                return new ProcessLaunchResult(false, null, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return new ProcessLaunchResult(false, null, ex.Message);
            }
            catch (IOException ex)
            {
                return new ProcessLaunchResult(false, null, ex.Message);
            }
        }

        private static string ResolveWorkingDirectory(string scriptPath)
        {
            var directory = Path.GetDirectoryName(scriptPath);
            return string.IsNullOrEmpty(directory) ? Environment.CurrentDirectory : directory;
        }
    }
}
