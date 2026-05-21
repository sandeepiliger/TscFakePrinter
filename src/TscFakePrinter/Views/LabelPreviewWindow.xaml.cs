using System;
using System.Windows;

namespace TscFakePrinter.Views;

public partial class LabelPreviewWindow : Window
{
    private const double ZoomStep = 0.2;

    public LabelPreviewWindow()
    {
        InitializeComponent();
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e)
        => ZoomSlider.Value = Math.Min(ZoomSlider.Maximum, ZoomSlider.Value + ZoomStep);

    private void ZoomOut_Click(object sender, RoutedEventArgs e)
        => ZoomSlider.Value = Math.Max(ZoomSlider.Minimum, ZoomSlider.Value - ZoomStep);

    private void ResetZoom_Click(object sender, RoutedEventArgs e)
        => ZoomSlider.Value = 1;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
