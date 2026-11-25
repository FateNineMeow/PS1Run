using System;
using Ps1Run.Core.Configuration;

namespace Ps1Run.Core.Settings
{
    /// <summary>
    /// 运行设置的读写门面（运行方式 / 解释器 / 编辑器）。
    /// 设置保存在软件根目录的 config.json，与程序配置（日志天数 / 主题 / 语言）同文件，
    /// 由 <see cref="ConfigDocument"/> 统一读写，保存时不会覆盖对方的字段。
    /// 旧版 %APPDATA%\Ps1Run\settings.json 在首次读取时一次性合并，原文件保留。
    /// </summary>
    public sealed class SettingsStore
    {
        private readonly bool _importLegacySettings;

        public SettingsStore(string? filePath = null)
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

        /// <summary>设置所在文件（与 config.json 是同一份）。</summary>
        public string FilePath { get; }

        public AppSettings Load() => LoadState().Settings;

        public AppState LoadState() => ConfigDocument.Load(FilePath, _importLegacySettings);

        public void Save(AppSettings settings)
        {
            if (settings is null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var state = LoadState();
            ConfigDocument.Save(FilePath, state with { Settings = settings });
        }
    }
}
