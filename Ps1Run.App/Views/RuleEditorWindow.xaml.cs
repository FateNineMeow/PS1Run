using System.Collections.Generic;
using System.Windows;
using Ps1Run.App.Localization;
using Ps1Run.App.ViewModels;
using Ps1Run.Core.Rules;
using Ps1Run.Core.Settings;

namespace Ps1Run.App.Views
{
    /// <summary>脚本规则的新增 / 编辑弹窗；取消不写任何文件。</summary>
    public partial class RuleEditorWindow : Window
    {
        private readonly RuleEditorViewModel _viewModel;

        public RuleEditorWindow(
            AppSettings settings,
            IReadOnlyList<ScriptRule> existing,
            ScriptRule? editing = null)
        {
            InitializeComponent();
            _viewModel = new RuleEditorViewModel(settings, existing, editing);
            DataContext = _viewModel;
        }

        public ScriptRule? Rule => _viewModel.ResultRule;

        private void OnPickFileClick(object sender, RoutedEventArgs e) => _viewModel.PickFile();

        private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            if (!_viewModel.TryBuild(out var rule, out var error))
            {
                MessageBox.Show(this, error, LocalizationManager.Loc("App.Title"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_viewModel.IsOverwrite
                && MessageBox.Show(this, LocalizationManager.Loc("Rule.PathExists"),
                    LocalizationManager.Loc("App.Title"),
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            _viewModel.Accept(rule!);
            DialogResult = true;
        }
    }
}
