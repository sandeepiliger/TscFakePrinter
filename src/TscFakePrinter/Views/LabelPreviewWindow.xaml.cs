using System;
using System.Windows;

namespace TscFakePrinter.Views;

public partial class LabelPreviewWindow : Window
{
    private const double ZoomStep = 0.2;
    private const double DotsPerMm = 8.0;

    public LabelPreviewWindow()
    {
        InitializeComponent();
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e)
        => ZoomSlider.Value = Math.Min(ZoomSlider.Maximum, ZoomSlider.Value + ZoomStep);

    private void ZoomOut_Click(object sender, RoutedEventArgs e)
        => ZoomSlider.Value = Math.Max(ZoomSlider.Minimum, ZoomSlider.Value - ZoomStep);

    private void ResetZoom_Click(object sender, RoutedEventArgs e)
    {
        var doc = Preview?.Document;
        if (doc is null || Host.ViewportWidth <= 0 || Host.ViewportHeight <= 0)
        {
            ZoomSlider.Value = 1;
            return;
        }

        var labelW = doc.WidthMm * DotsPerMm;
        var labelH = doc.HeightMm * DotsPerMm;
        if (labelW <= 0 || labelH <= 0)
        {
            ZoomSlider.Value = 1;
            return;
        }

        var availW = Math.Max(40, Host.ViewportWidth - 40);
        var availH = Math.Max(40, Host.ViewportHeight - 40);
        var fit = Math.Min(availW / labelW, availH / labelH);

        ZoomSlider.Value = Math.Clamp(fit, ZoomSlider.Minimum, ZoomSlider.Maximum);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
