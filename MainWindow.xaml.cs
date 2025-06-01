using System.ComponentModel;
using System.Windows;
using SystemMonitorApp.ViewModels;

namespace SystemMonitorApp
{
    public partial class MainWindow : Window
    {
        private readonly SystemMonitorViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new SystemMonitorViewModel();
            DataContext = _viewModel;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _viewModel.Dispose();
            base.OnClosing(e);
        }
    }
}