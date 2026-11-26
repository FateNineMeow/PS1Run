using System.Globalization;
using Ps1Run.Core.Dispatch;

namespace Ps1Run.Core.Rules
{
    /// <summary>
    /// 一条「仅此脚本」规则，对应 rule.txt 的一行：
    /// 文件路径|动作|文件大小|修改时间|文件HASH
    /// 文件路径始终参与匹配，不再保存开关列；关闭的标准写 0。
    /// </summary>
    public sealed record ScriptRule
    {
        public string Path { get; init; } = string.Empty;

        public LaunchAction Action { get; init; }

        /// <summary>文件大小（字节）；全局关闭该标准时为 0。</summary>
        public long Size { get; init; }

        /// <summary>UTC 修改时间，ISO 8601 "O" 格式；全局关闭该标准时为 "0"。</summary>
        public string ModifiedTimeUtc { get; init; } = "0";

        /// <summary>小写 SHA-256；全局关闭该标准或未计算时为 "0"。</summary>
        public string Hash { get; init; } = "0";

        /// <summary>写入文件时使用的动作字面量。</summary>
        public string ActionToken => Action == LaunchAction.Edit ? "Edit" : "Run";

        public string SizeText => Size > 0
            ? Size.ToString(CultureInfo.InvariantCulture)
            : "0";

        public string HashText => string.IsNullOrEmpty(Hash) ? "0" : Hash;
    }
}
