using System;
using System.Collections.Generic;
using Ps1Run.Core;

namespace Ps1Run.Core.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            // net11 下 GBK 判型测试需要代码页提供程序
            CoreBootstrap.Initialize();

            var tests = new List<KeyValuePair<string, Action>>
            {
                new KeyValuePair<string, Action>("编码判型", ScriptEncodingDetectorTests.RunAll),
                new KeyValuePair<string, Action>("参数解析", CommandLineOptionsTests.RunAll),
                new KeyValuePair<string, Action>("解释器探测", PowerShellLocatorTests.RunAll),
                new KeyValuePair<string, Action>("设置持久化", SettingsStoreTests.RunAll),
                new KeyValuePair<string, Action>("日志写入与保留", DiagnosticsLogTests.RunAll),
                new KeyValuePair<string, Action>("程序配置", ConfigStoreTests.RunAll),
                new KeyValuePair<string, Action>("多语言资源", LocalizationTests.RunAll),
            };

            int failed = 0;
            foreach (var test in tests)
            {
                try
                {
                    test.Value();
                    Console.WriteLine("[PASS] " + test.Key);
                }
                catch (Exception ex)
                {
                    failed++;
                    Console.WriteLine("[FAIL] " + test.Key + " -> " + ex.Message);
                }
            }

            Console.WriteLine();
            Console.WriteLine("结果：" + (tests.Count - failed) + "/" + tests.Count + " 组通过");
            return failed == 0 ? 0 : 1;
        }
    }

    internal static class Assert
    {
        public static void Equal<T>(T expected, T actual, string message = "")
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new Exception(message + " 期望=" + expected + " 实际=" + actual);
            }
        }

        public static void True(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception(message);
            }
        }

        public static void NotNull(object? value, string message)
        {
            if (value is null)
            {
                throw new Exception(message);
            }
        }
    }
}
