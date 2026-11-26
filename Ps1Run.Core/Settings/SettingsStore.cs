using System;
using Ps1Run.Core.Configuration;

namespace Ps1Run.Core.Settings
{
    /// <summary>
    /// 运行设置的读写门面（运行方式 / 解释器 / 编辑器 / 规则判断标准）。
    /// 设置保存在软件根目录的 config.cfg，与程序配置（日志天数 / 主题 / 语言）同文件，
    /// 由 <see cref="ConfigDocument"/> 统一读写，保存时不会覆盖对方的字段。
    /// 旧 settings.json 不再读取、不再合并。
    /// </summary>
    public sealed class SettingsStore
    {
        public SettingsStore(string? filePath = null)
        {
            FilePath = string.IsNullOrEmpty(filePath) ? AppPaths.ConfigFile : filePath!;
        }

        /// <summary>设置所在文件（与 config.cfg 是同一份）。</summary>
        public string FilePath { get; }

        public AppSettings Load() => LoadState().Settings;

        public AppState LoadState() => ConfigDocument.Load(FilePath);

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
