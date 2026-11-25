using System;

namespace Ps1Run.Core.Configuration
{
    /// <summary>
    /// 程序配置（日志天数 / 主题 / 界面语言）的读写门面。
    /// 与运行设置（<see cref="Settings.AppSettings"/>）合并存放在软件根目录的同一份 config.json，
    /// 由 <see cref="ConfigDocument"/> 统一读写，保存时不会覆盖对方的字段。
    /// </summary>
    public sealed class ConfigStore
    {
        private readonly bool _importLegacySettings;

        public ConfigStore(string? filePath = null)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                FilePath = AppPaths.ConfigFile;
                _importLegacySettings = true;
            }
            else
            {
                FilePath = filePath!;
            }
        }

        /// <summary>config.json 的完整路径（软件根目录）。</summary>
        public string FilePath { get; }

        /// <summary>读取程序配置；文件不存在时按默认值生成。</summary>
        public AppConfig Load() => LoadState().Config;

        /// <summary>读取文件中的全部内容（含行为设置与版本号）。</summary>
        public AppState LoadState() => ConfigDocument.Load(FilePath, _importLegacySettings);

        public void Save(AppConfig config)
        {
            if (config is null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            var state = LoadState();
            ConfigDocument.Save(FilePath, state with { Config = config });
        }
    }
}
