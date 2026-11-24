using System;
using System.Windows.Data;
using System.Windows.Markup;

namespace Ps1Run.App.Localization
{
    /// <summary>
    /// XAML 里取文案的标记扩展：<c>Text="{loc:T Button.Save}"</c>。
    /// 返回的是一条绑定，语言切换后自动更新，无需重建窗口。
    /// </summary>
    [MarkupExtensionReturnType(typeof(string))]
    public sealed class TExtension : MarkupExtension
    {
        public TExtension()
        {
        }

        public TExtension(string key)
        {
            Key = key;
        }

        /// <summary>语言包里的键名，例如 Button.Save。</summary>
        public string Key { get; set; } = string.Empty;

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Key))
            {
                return string.Empty;
            }

            var binding = new Binding(nameof(Translation.Value))
            {
                Source = new Translation(Key),
                Mode = BindingMode.OneWay,
            };

            return binding.ProvideValue(serviceProvider);
        }
    }
}
