using System.Windows;
using StayOnTarget.ViewModels;

namespace StayOnTarget
{
    public partial class ExportTransactionsDialog : Window
    {
        public ExportTransactionsDialog(ExportTransactionsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.RequestClose += (s, e) => DialogResult = true;
        }
    }
}
