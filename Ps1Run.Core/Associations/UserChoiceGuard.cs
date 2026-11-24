using System;
using Microsoft.Win32;

namespace Ps1Run.Core.Associations
{
    /// <summary>UserChoice 快照（wiki 06 §3）。</summary>
    public sealed class UserChoiceInfo
    {
        public UserChoiceInfo(string? progId, string? hash, bool readFailed)
        {
            ProgId = progId;
            Hash = hash;
            ReadFailed = readFailed;
        }

        public string? ProgId { get; }

        public string? Hash { get; }

        public bool ReadFailed { get; }

        /// <summary>是否存在他方覆盖（UserChoice 会压过 ProgId 关联）。</summary>
        public bool HasOverride => !string.IsNullOrEmpty(ProgId);

        public bool PointsToUs =>
            string.Equals(ProgId, AssociationRegistration.ProgId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// UserChoice 检测（wiki 06 §3、02 F1.6）。
    /// 硬约束：Hash 由系统生成，本程序只读不写、不伪造。
    /// </summary>
    public static class UserChoiceGuard
    {
        private const string FileExtsRoot =
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\";

        public static UserChoiceInfo Inspect(string extension = AssociationRegistration.Extension)
        {
            var subKey = FileExtsRoot + extension + @"\UserChoice";

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(subKey);
                if (key is null)
                {
                    return new UserChoiceInfo(null, null, false);
                }

                var progId = key.GetValue("ProgId") as string;
                var hash = key.GetValue("Hash") as string;
                return new UserChoiceInfo(progId, hash, false);
            }
            catch (System.Security.SecurityException)
            {
                return new UserChoiceInfo(null, null, true);
            }
            catch (UnauthorizedAccessException)
            {
                return new UserChoiceInfo(null, null, true);
            }
        }

        /// <summary>给界面用的中文说明。</summary>
        public static string Describe(UserChoiceInfo info)
        {
            if (info is null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            if (info.ReadFailed)
            {
                return "无法读取 UserChoice（权限不足），默认程序状态未知。";
            }

            if (!info.HasOverride)
            {
                return "系统未记录 UserChoice，ProgId 关联可直接生效。";
            }

            if (info.PointsToUs)
            {
                return "系统默认程序已指向本程序。";
            }

            return "系统当前默认程序为 " + info.ProgId + "，本程序可能不生效；"
                   + "请在“打开方式”中手动勾选并确认（不允许伪造 Hash）。";
        }
    }
}
