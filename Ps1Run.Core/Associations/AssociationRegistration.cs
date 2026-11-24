using System;

namespace Ps1Run.Core.Associations
{
    /// <summary>
    /// ProgId 与动词布局的静态定义（wiki 06 §2、12 附录）。
    /// 只负责"拼字符串"，不做任何注册表写入，便于单测与复用。
    /// </summary>
    public static class AssociationRegistration
    {
        /// <summary>本程序的 ProgId。</summary>
        public const string ProgId = "Ps1Run.ps1";

        /// <summary>资源管理器里显示的类型名。</summary>
        public const string FriendlyTypeName = "PowerShell 脚本";

        /// <summary>扩展名（含点）。</summary>
        public const string Extension = ".ps1";

        /// <summary>Classes 根（HKCU / HKLM 下同名）。</summary>
        public const string ClassesRoot = @"Software\Classes";

        public const string VerbOpen = "open";
        public const string VerbRun = "run";
        public const string VerbEdit = "edit";

        public static string ProgIdKeyPath => ClassesRoot + "\\" + ProgId;

        public static string ExtensionKeyPath => ClassesRoot + "\\" + Extension;

        public static string OpenWithProgIdsKeyPath => ExtensionKeyPath + "\\OpenWithProgids";

        /// <summary>双击动词：按设置分发，Ask 模式下弹窗。</summary>
        public static string BuildOpenCommand(string executablePath) =>
            Quote(executablePath) + " --open \"%1\"";

        /// <summary>右键"运行"：强制绕过 Ask。</summary>
        public static string BuildRunCommand(string executablePath) =>
            Quote(executablePath) + " --run \"%1\"";

        /// <summary>右键"编辑"：强制绕过 Ask。</summary>
        public static string BuildEditCommand(string executablePath) =>
            Quote(executablePath) + " --edit \"%1\"";

        public static string BuildDefaultIcon(string executablePath, string? iconPath = null)
        {
            var effective = !string.IsNullOrWhiteSpace(iconPath)
                            && System.IO.File.Exists(iconPath!)
                ? iconPath!
                : executablePath;
            return Quote(effective) + ",0";
        }

        private static string Quote(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("可执行文件路径不能为空", nameof(value));
            }

            return "\"" + value + "\"";
        }
    }
}
