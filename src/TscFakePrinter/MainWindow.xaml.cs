using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TscFakePrinter.ViewModels;
using TscFakePrinter.Views;

namespace TscFakePrinter;

public partial class MainWindow : Window
{
    private LabelPreviewWindow? _previewWindow;

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

    private void PreviewArea_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        => OpenPreviewWindow();

    private void ExpandPreview_Click(object sender, RoutedEventArgs e)
        => OpenPreviewWindow();

    private void OpenPreviewWindow()
    {
        if (_previewWindow is { IsLoaded: true })
        {
            _previewWindow.Activate();
            return;
        }

        _previewWindow = new LabelPreviewWindow
        {
            Owner = this,
            DataContext = DataContext
        };
        _previewWindow.Closed += (_, _) => _previewWindow = null;
        _previewWindow.Show();
    }
}
