using Ps1Run.Core.Settings;

namespace Ps1Run.Core.Configuration
{
    /// <summary>
    /// 合并后 config.cfg 的完整内容：程序配置（日志天数 / 主题 / 语言）+
    /// 行为设置（运行方式 / 解释器 / 编辑器 / 规则判断标准）+ schema 版本号。
    /// 配置与语言包统一为 .lang 文本格式，版本号升到 2；旧 .json 不再读取。
    /// </summary>
    public sealed record AppState
    {
        /// <summary>当前 config.cfg schema 版本。字段含义变化时递增。</summary>
        public const int CurrentVersion = 2;

        /// <summary>文件中的版本号；0 表示缺失（旧文件）。</summary>
        public int Version { get; init; }

        /// <summary>程序配置。</summary>
        public AppConfig Config { get; init; } = AppConfig.Default;

        /// <summary>行为设置。</summary>
        public AppSettings Settings { get; init; } = AppSettings.Default;

        public static AppState Default { get; } = new AppState();
    }
}
