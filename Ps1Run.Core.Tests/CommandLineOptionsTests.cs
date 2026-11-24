using Ps1Run.Core.Dispatch;

namespace Ps1Run.Core.Tests
{
    internal static class CommandLineOptionsTests
    {
        public static void RunAll()
        {
            ExplicitRunWithSpaces();
            ExplicitAsk();
            RunEditConflict();
            LiteralDashPath();
            MachineIntents();
        }

        private static void ExplicitRunWithSpaces()
        {
            var options = CommandLineOptions.Parse(new[] { "--run", @"C:\脚本 目录\部署.ps1" });
            Assert.Equal(LaunchIntent.Run, options.Intent, "--run 意图");
            Assert.Equal(@"C:\脚本 目录\部署.ps1", options.ScriptPath, "含空格路径");
            Assert.Equal(0, options.Errors.Count, "无错误");
        }

        private static void ExplicitAsk()
        {
            var options = CommandLineOptions.Parse(new[] { "--ask", @"D:\a.ps1" });
            Assert.Equal(LaunchIntent.Ask, options.Intent, "--ask 意图");
        }

        private static void RunEditConflict()
        {
            var options = CommandLineOptions.Parse(new[] { "--run", "--edit", @"D:\a.ps1" });
            Assert.True(options.Errors.Count > 0, "--run/--edit 冲突应报错");
        }

        private static void LiteralDashPath()
        {
            var options = CommandLineOptions.Parse(new[] { "--", "-tmp.ps1" });
            Assert.Equal(LaunchIntent.None, options.Intent, "裸参数意图");
            Assert.Equal("-tmp.ps1", options.ScriptPath, "以 - 开头的文件名");
        }

        private static void MachineIntents()
        {
            Assert.Equal(
                LaunchIntent.RegisterMachine,
                CommandLineOptions.Parse(new[] { "--register-machine" }).Intent,
                "--register-machine");
            Assert.Equal(
                LaunchIntent.UnregisterMachine,
                CommandLineOptions.Parse(new[] { "--unregister-machine" }).Intent,
                "--unregister-machine");
        }
    }
}
