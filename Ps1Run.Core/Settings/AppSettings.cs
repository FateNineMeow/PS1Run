namespace Ps1Run.Core.Settings
{
    /// <summary>运行方式三态（wiki 01 §3 R2、05 §3）。</summary>
    public enum RunMode
    {
        Run = 0,
        Edit = 1,
        Ask = 2,
    }

    /// <summary>执行程序策略三态（wiki 07 §1）。</summary>
    public enum PowerShellPolicy
    {
        Ps5 = 0,
        Ps7 = 1,
        Auto = 2,
    }

    /// <summary>
    /// 用户设置模型。
    /// 默认运行方式取 Ask：双击未知脚本直接执行有风险（wiki 01 §3 R2）。
    /// </summary>
    public sealed record AppSettings
    {
        public RunMode RunMode { get; init; } = RunMode.Ask;

        public PowerShellPolicy PowerShellPolicy { get; init; } = PowerShellPolicy.Auto;

        public string? CustomPs5Path { get; init; }

        public string? CustomPs7Path { get; init; }

        public string? CustomEditorPath { get; init; }

        public static AppSettings Default { get; } = new AppSettings();
    }
}
