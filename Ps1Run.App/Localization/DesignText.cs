using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Ps1Run.App.Localization
{
    /// <summary>
    /// 设计期文本：<c>d:Text="保存设置"</c>。
    /// 仅在 XAML 设计器里把文字显示出来，运行时完全跳过——运行时文案一律来自
    /// lang\*.lang（通过 <see cref="TExtension"/>），两者内容保持一致。
    /// </summary>
    public static class DesignText
    {
        public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
            "Text",
            typeof(string),
            typeof(DesignText),
            new PropertyMetadata(null, OnTextChanged));

        public static void SetText(DependencyObject element, string? value) =>
            element.SetValue(TextProperty, value);

        public static string? GetText(DependencyObject element) =>
            (string?)element.GetValue(TextProperty);

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // 运行时不做任何事：设计期文本绝不参与实际渲染
            if (!DesignerProperties.GetIsInDesignMode(d))
            {
                return;
            }

            if (d is FrameworkElement element)
            {
                if (element.IsLoaded)
                {
                    Apply(d);
                }
                else
                {
                    element.Loaded += OnLoaded;
                }
            }
        }

        private static void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                element.Loaded -= OnLoaded;
                Apply(element);
            }
        }

        private static void Apply(DependencyObject d)
        {
            var text = GetText(d);
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            switch (d)
            {
                case TextBlock textBlock:
                    textBlock.Text = text;
                    break;
                case ContentControl contentControl:
                    contentControl.Content = text;
                    break;
            }
        }
    }
}
