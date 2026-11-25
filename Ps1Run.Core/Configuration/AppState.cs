using Ps1Run.Core.Settings;

namespace Ps1Run.Core.Configuration
{
    /// <summary>
    /// 合并后 config.json 的完整内容：程序配置（日志天数 / 主题 / 语言）+
    /// 行为设置（运行方式 / 解释器 / 编辑器）+ schema 版本号。
    /// 版本号用于识别旧文件（缺失 = 旧版或手工创建），并在读取时补写与迁移。
    /// </summary>
    public sealed record AppState
    {
        /// <summary>当前 config.json schema 版本。字段含义变化时递增。</summary>
        public const int CurrentVersion = 1;

        /// <summary>文件中的版本号；0 表示缺失（旧文件）。</summary>
        public int Version { get; init; }

        /// <summary>程序配置。</summary>
        public AppConfig Config { get; init; } = AppConfig.Default;

        /// <summary>行为设置。</summary>
        public AppSettings Settings { get; init; } = AppSettings.Default;

        public static AppState Default { get; } = new AppState();
    }
}
