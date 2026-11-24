using System;
using System.IO;
using Ps1Run.Core.Settings;

namespace Ps1Run.Core.Tests
{
    internal static class SettingsStoreTests
    {
        public static void RunAll()
        {
            RoundTrip();
            CorruptedFileFallsBackAndBacksUp();
        }

        private static string NewTempPath()
        {
            var dir = Path.Combine(Path.GetTempPath(), "ps1run-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "settings.json");
        }

        private static void RoundTrip()
        {
            var path = NewTempPath();
            var original = new AppSettings
            {
                RunMode = RunMode.Edit,
                PowerShellPolicy = PowerShellPolicy.Ps7,
                CustomPs5Path = @"C:\Program Files\PowerShell\powershell.exe",
                CustomPs7Path = @"D:\Tools\pwsh ""preview"".exe",
                CustomEditorPath = @"D:\Soft\Code\VS Code\Code.exe",
            };

            var store = new SettingsStore(path);
            store.Save(original);
            var loaded = store.Load();

            Assert.Equal(RunMode.Edit, loaded.RunMode, "RunMode 往返");
            Assert.Equal(PowerShellPolicy.Ps7, loaded.PowerShellPolicy, "PowerShellPolicy 往返");
            Assert.Equal(original.CustomPs7Path, loaded.CustomPs7Path, "含引号路径往返");
            Assert.Equal(original.CustomEditorPath, loaded.CustomEditorPath, "编辑器路径往返");
            Assert.True(File.ReadAllText(path).StartsWith("{"), "文件应为 JSON 对象");
        }

        private static void CorruptedFileFallsBackAndBacksUp()
        {
            var path = NewTempPath();
            File.WriteAllText(path, "this is not json");
            var loaded = new SettingsStore(path).Load();

            Assert.Equal(RunMode.Ask, loaded.RunMode, "损坏时回退默认值");
            var backups = Directory.GetFiles(Path.GetDirectoryName(path)!, "settings.json.bad.*");
            Assert.True(backups.Length == 1, "损坏文件应被备份一次");
        }
    }
}
