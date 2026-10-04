using System.Windows;
using SecApper.FolderLocker.ViewModels;

namespace SecApper.FolderLocker.Views;

public partial class FolderDetailsDialog : Window
{
    public FolderDetailsDialog(FolderItemViewModel item)
    {
        InitializeComponent();
        DataContext = item;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
