namespace Ps1Run.Core.Editors
{
    /// <summary>
    /// 编辑器定义（wiki 08 §5）。
    /// ArgumentTemplate 中的 %1 会被替换为加引号的绝对文件路径。
    /// </summary>
    public sealed record EditorDefinition
    {
        public EditorDefinition(string displayName, string executablePath, string argumentTemplate, string source)
        {
            DisplayName = displayName;
            ExecutablePath = executablePath;
            ArgumentTemplate = argumentTemplate;
            Source = source;
        }

        public string DisplayName { get; }

        public string ExecutablePath { get; }

        public string ArgumentTemplate { get; }

        /// <summary>命中来源：Settings / KnownPath / AppPaths / Uninstall / PATH / Fallback。</summary>
        public string Source { get; }

        public string BuildArguments(string filePath)
        {
            var quoted = "\"" + filePath + "\"";
            if (string.IsNullOrEmpty(ArgumentTemplate))
            {
                return quoted;
            }

            return ArgumentTemplate.IndexOf("%1", System.StringComparison.Ordinal) >= 0
                ? ArgumentTemplate.Replace("%1", quoted)
                : ArgumentTemplate + " " + quoted;
        }
    }
}
