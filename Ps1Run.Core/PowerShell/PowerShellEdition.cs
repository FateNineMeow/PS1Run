namespace Ps1Run.Core.PowerShell
{
    public enum PowerShellEdition
    {
        Ps5 = 0,
        Ps7 = 1,
    }

    /// <summary>探测成功后的解释器目标。</summary>
    public sealed record PowerShellTarget
    {
        public PowerShellTarget(PowerShellEdition edition, string executablePath, string probeSource)
        {
            Edition = edition;
            ExecutablePath = executablePath;
            ProbeSource = probeSource;
        }

        public PowerShellEdition Edition { get; }

        public string ExecutablePath { get; }

        /// <summary>命中来源（用于诊断展示，如 "System32" / "AppPaths" / "PowerShellCore"）。</summary>
        public string ProbeSource { get; }

        public string DisplayName => Edition == PowerShellEdition.Ps5 ? "Windows PowerShell 5.1" : "PowerShell 7+";
    }
}
