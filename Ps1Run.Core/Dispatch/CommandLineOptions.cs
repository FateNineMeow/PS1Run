using System;
using System.Collections.Generic;

namespace Ps1Run.Core.Dispatch
{
    /// <summary>启动意图（wiki 05 §2 参数协议）。</summary>
    public enum LaunchIntent
    {
        /// <summary>无参数：显示主界面。</summary>
        None = 0,

        /// <summary>--open：按设置分发，RunMode=Ask 时弹选择窗。</summary>
        Open = 1,

        /// <summary>--run：强制执行，绕过运行方式。</summary>
        Run = 2,

        /// <summary>--edit：强制编辑，绕过运行方式。</summary>
        Edit = 3,

        /// <summary>--ask：强制弹选择窗。</summary>
        Ask = 4,

        /// <summary>--settings：直接打开设置界面。</summary>
        Settings = 5,

        /// <summary>--register-machine：在已提升的进程中执行 HKLM 注册。</summary>
        RegisterMachine = 6,

        /// <summary>--unregister-machine：在已提升的进程中执行 HKLM 注销。</summary>
        UnregisterMachine = 7,
    }

    /// <summary>
    /// 命令行解析结果（wiki 05 §2、02 F2.1）。
    /// 优先级：--run / --edit &gt; --ask &gt; --open &gt; 裸参数 &gt; 无参数。
    /// </summary>
    public sealed class CommandLineOptions
    {
        private CommandLineOptions(
            LaunchIntent intent,
            string? scriptPath,
            bool showHelp,
            IReadOnlyList<string> errors)
        {
            Intent = intent;
            ScriptPath = scriptPath;
            ShowHelp = showHelp;
            Errors = errors;
        }

        public LaunchIntent Intent { get; }

        public string? ScriptPath { get; }

        public bool ShowHelp { get; }

        public IReadOnlyList<string> Errors { get; }

        public static CommandLineOptions Parse(string[]? args)
        {
            args ??= Array.Empty<string>();

            var errors = new List<string>();
            var explicitIntents = new List<LaunchIntent>();
            string? path = null;
            bool help = false;
            bool literalMode = false;

            foreach (var raw in args)
            {
                if (raw is null)
                {
                    continue;
                }

                // "--" 之后全部按字面路径处理，兼容以 "-" 开头的文件名
                if (!literalMode && string.Equals(raw, "--", StringComparison.Ordinal))
                {
                    literalMode = true;
                    continue;
                }

                if (!literalMode && raw.StartsWith("--", StringComparison.Ordinal))
                {
                    switch (raw.ToLowerInvariant())
                    {
                        case "--open":
                            explicitIntents.Add(LaunchIntent.Open);
                            continue;
                        case "--run":
                            explicitIntents.Add(LaunchIntent.Run);
                            continue;
                        case "--edit":
                            explicitIntents.Add(LaunchIntent.Edit);
                            continue;
                        case "--ask":
                            explicitIntents.Add(LaunchIntent.Ask);
                            continue;
                        case "--settings":
                            explicitIntents.Add(LaunchIntent.Settings);
                            continue;
                        case "--register-machine":
                            explicitIntents.Add(LaunchIntent.RegisterMachine);
                            continue;
                        case "--unregister-machine":
                            explicitIntents.Add(LaunchIntent.UnregisterMachine);
                            continue;
                        case "--help":
                            help = true;
                            continue;
                        default:
                            errors.Add("未知参数：" + raw);
                            continue;
                    }
                }

                if (!literalMode && (string.Equals(raw, "-h", StringComparison.OrdinalIgnoreCase)
                                     || string.Equals(raw, "-?", StringComparison.Ordinal)))
                {
                    help = true;
                    continue;
                }

                // 裸参数：脚本路径。以单个 "-" 开头的也当作路径（wiki 02 F2.1）
                path = raw;
            }

            if (explicitIntents.Contains(LaunchIntent.Run) && explicitIntents.Contains(LaunchIntent.Edit))
            {
                errors.Add("参数冲突：--run 与 --edit 不能同时使用");
            }

            if (explicitIntents.Contains(LaunchIntent.RegisterMachine)
                && explicitIntents.Contains(LaunchIntent.UnregisterMachine))
            {
                errors.Add("参数冲突：--register-machine 与 --unregister-machine 不能同时使用");
            }

            var intent = ResolveIntent(explicitIntents);
            return new CommandLineOptions(intent, path, help, errors);
        }

        /// <summary>
        /// 显式意图优先级：Run/Edit &gt; Ask &gt; Open &gt; Settings。
        /// 同时出现 --run 与 --edit 视为参数冲突。
        /// </summary>
        private static LaunchIntent ResolveIntent(List<LaunchIntent> intents)
        {
            if (intents.Count == 0)
            {
                return LaunchIntent.None;
            }

            if (intents.Contains(LaunchIntent.Run) && intents.Contains(LaunchIntent.Edit))
            {
                return LaunchIntent.None;
            }

            if (intents.Contains(LaunchIntent.Run))
            {
                return LaunchIntent.Run;
            }

            if (intents.Contains(LaunchIntent.Edit))
            {
                return LaunchIntent.Edit;
            }

            if (intents.Contains(LaunchIntent.Ask))
            {
                return LaunchIntent.Ask;
            }

            if (intents.Contains(LaunchIntent.RegisterMachine))
            {
                return LaunchIntent.RegisterMachine;
            }

            if (intents.Contains(LaunchIntent.UnregisterMachine))
            {
                return LaunchIntent.UnregisterMachine;
            }

            if (intents.Contains(LaunchIntent.Open))
            {
                return LaunchIntent.Open;
            }

            return LaunchIntent.Settings;
        }
    }
}
