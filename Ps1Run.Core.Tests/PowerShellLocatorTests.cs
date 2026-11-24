using System.IO;
using System.Text;
using Ps1Run.Core.Infrastructure;
using Ps1Run.Core.PowerShell;

namespace Ps1Run.Core.Tests
{
    internal static class PowerShellLocatorTests
    {
        public static void RunAll()
        {
            LocatePs5BySystem32();
            LocateWithOverridePath();
            MissingReturnsNull();
        }

        private static void LocatePs5BySystem32()
        {
            var expected = Path.Combine(
                PathSearch.SystemDirectory,
                "WindowsPowerShell",
                "v1.0",
                "powershell.exe");
            var fs = new FakeFileSystem();
            fs.Add(expected, Encoding.ASCII.GetBytes("MZ"));

            var target = new PowerShellLocator(fs).Locate(PowerShellEdition.Ps5);
            Assert.NotNull(target, "PS5 探测结果");
            Assert.Equal(expected, target!.ExecutablePath, "PS5 命中路径");
            Assert.Equal("System32", target.ProbeSource, "PS5 命中来源");
        }

        private static void LocateWithOverridePath()
        {
            const string custom = @"D:\Tools\pwsh.exe";
            var fs = new FakeFileSystem();
            fs.Add(custom, Encoding.ASCII.GetBytes("MZ"));

            var target = new PowerShellLocator(fs).Locate(PowerShellEdition.Ps7, custom);
            Assert.NotNull(target, "自定义 PS7 路径");
            Assert.Equal(custom, target!.ExecutablePath, "PS7 自定义路径");
            Assert.Equal("Settings", target.ProbeSource, "PS7 命中来源");
        }

        private static void MissingReturnsNull()
        {
            var target = new PowerShellLocator(new FakeFileSystem()).Locate(PowerShellEdition.Ps7);
            Assert.Equal(true, target is null, "无文件时返回 null");
        }
    }
}
