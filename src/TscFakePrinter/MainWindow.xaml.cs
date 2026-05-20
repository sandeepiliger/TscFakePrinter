using System.Windows;
using System.Windows.Controls;
using TscFakePrinter.ViewModels;

namespace TscFakePrinter;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void CommandsTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb && DataContext is MainViewModel vm && vm.AutoScroll)
        {
            tb.ScrollToEnd();
        }
    }
}
