using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using QRCoder;
using TscFakePrinter.Models;
using ZXing;
using ZXing.Common;

namespace TscFakePrinter.Controls;

public partial class LabelPreviewControl : UserControl
{
    public static readonly DependencyProperty DocumentProperty =
        DependencyProperty.Register(
            nameof(Document),
            typeof(LabelDocument),
            typeof(LabelPreviewControl),
            new PropertyMetadata(null, OnDocumentChanged));

    public LabelDocument? Document
    {
        get => (LabelDocument?)GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    private const double DotsPerMm = 8.0; // 203 dpi default

    public LabelPreviewControl()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Render();
    }

    private static void OnDocumentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LabelPreviewControl c) c.Render();
    }

    private void Render()
    {
        Surface.Children.Clear();

        var doc = Document;
        if (doc is null)
        {
            DrawPlaceholder();
            return;
        }

        var widthDots = Math.Max(1, doc.WidthMm * DotsPerMm);
        var heightDots = Math.Max(1, doc.HeightMm * DotsPerMm);

        var available = Math.Max(120, LabelHost.ActualWidth > 0 ? LabelHost.ActualWidth - 12 : 280);
        var maxHeight = Math.Max(80, LabelHost.ActualHeight > 0 ? LabelHost.ActualHeight - 12 : 220);

        var scale = Math.Min(available / widthDots, maxHeight / heightDots);
        if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0) scale = 0.6;

        Surface.Width = widthDots * scale;
        Surface.Height = heightDots * scale;

        foreach (var el in doc.Elements)
        {
            switch (el)
            {
                case TsplText t: DrawText(t, scale); break;
                case TsplBarcode b: DrawBarcode(b, scale); break;
                case TsplQrCode q: DrawQrCode(q, scale); break;
                case TsplBar bar: DrawBar(bar, scale); break;
                case TsplBox box: DrawBox(box, scale); break;
            }
        }
    }

    private void DrawPlaceholder()
    {
        Surface.Width = 280;
        Surface.Height = 180;
        var hint = new TextBlock
        {
            Text = "Waiting for label...",
            Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
            FontSize = 12
        };
        Canvas.SetLeft(hint, 90);
        Canvas.SetTop(hint, 80);
        Surface.Children.Add(hint);
    }

    private void DrawText(TsplText t, double scale)
    {
        var fontSize = MapFontSize(t.Font) * Math.Max(1, t.XMul);
        var tb = new TextBlock
        {
            Text = t.Content,
            FontFamily = new FontFamily("Segoe UI, Arial"),
            FontSize = fontSize * scale,
            Foreground = Brushes.Black,
            FontWeight = t.XMul >= 2 ? FontWeights.SemiBold : FontWeights.Normal
        };
        if (t.Rotation != 0)
        {
            tb.LayoutTransform = new RotateTransform(t.Rotation);
        }
        Canvas.SetLeft(tb, t.X * scale);
        Canvas.SetTop(tb, t.Y * scale);
        Surface.Children.Add(tb);
    }

    private static double MapFontSize(string font) => font switch
    {
        "1" => 10,
        "2" => 12,
        "3" => 16,
        "4" => 20,
        "5" => 28,
        "TSS24.BF2" => 18,
        _ => 12
    };

    private void DrawBarcode(TsplBarcode b, double scale)
    {
        try
        {
            var fmt = MapBarcodeFormat(b.Type);
            var writer = new BarcodeWriterPixelData
            {
                Format = fmt,
                Options = new EncodingOptions
                {
                    Height = Math.Max(20, b.Height),
                    Width = Math.Max(120, (b.Data.Length + 4) * b.Narrow * 11),
                    Margin = 0,
                    PureBarcode = !b.HumanReadable
                }
            };
            var px = writer.Write(b.Data);
            var bmp = PixelDataToBitmapSource(px);

            var img = new System.Windows.Controls.Image
            {
                Source = bmp,
                Width = bmp.PixelWidth * scale * 0.5,
                Height = b.Height * scale,
                Stretch = Stretch.Fill
            };
            if (b.Rotation != 0) img.LayoutTransform = new RotateTransform(b.Rotation);
            Canvas.SetLeft(img, b.X * scale);
            Canvas.SetTop(img, b.Y * scale);
            Surface.Children.Add(img);
        }
        catch
        {
            DrawFallbackBox(b.X, b.Y, 120, b.Height, scale, $"[BARCODE {b.Type}: {b.Data}]");
        }
    }

    private static BarcodeFormat MapBarcodeFormat(string type) => type.ToUpperInvariant() switch
    {
        "128" or "CODE128" or "CODE-128" => BarcodeFormat.CODE_128,
        "39" or "CODE39" or "CODE-39" => BarcodeFormat.CODE_39,
        "93" or "CODE93" => BarcodeFormat.CODE_93,
        "EAN13" or "EAN-13" => BarcodeFormat.EAN_13,
        "EAN8" or "EAN-8" => BarcodeFormat.EAN_8,
        "UPCA" or "UPC-A" => BarcodeFormat.UPC_A,
        "UPCE" or "UPC-E" => BarcodeFormat.UPC_E,
        "ITF" or "I2OF5" or "INTERLEAVED2OF5" => BarcodeFormat.ITF,
        "CODABAR" or "NW7" => BarcodeFormat.CODABAR,
        _ => BarcodeFormat.CODE_128
    };

    private static BitmapSource PixelDataToBitmapSource(ZXing.Rendering.PixelData px)
    {
        var bmp = BitmapSource.Create(
            px.Width,
            px.Height,
            96, 96,
            PixelFormats.Bgra32,
            null,
            px.Pixels,
            px.Width * 4);
        bmp.Freeze();
        return bmp;
    }

    private void DrawQrCode(TsplQrCode q, double scale)
    {
        try
        {
            using var gen = new QRCodeGenerator();
            var ecc = q.EccLevel.ToUpperInvariant() switch
            {
                "L" => QRCodeGenerator.ECCLevel.L,
                "M" => QRCodeGenerator.ECCLevel.M,
                "Q" => QRCodeGenerator.ECCLevel.Q,
                "H" => QRCodeGenerator.ECCLevel.H,
                _ => QRCodeGenerator.ECCLevel.M
            };

            using var data = gen.CreateQrCode(q.Data, ecc);
            using var png = new PngByteQRCode(data);
            var bytes = png.GetGraphic(8);

            using var ms = new MemoryStream(bytes);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();

            var size = q.CellWidth * 10 * scale;
            var img = new System.Windows.Controls.Image
            {
                Source = bmp,
                Width = size,
                Height = size,
                Stretch = Stretch.Fill
            };
            if (q.Rotation != 0) img.LayoutTransform = new RotateTransform(q.Rotation);
            Canvas.SetLeft(img, q.X * scale);
            Canvas.SetTop(img, q.Y * scale);
            Surface.Children.Add(img);
        }
        catch
        {
            DrawFallbackBox(q.X, q.Y, 80, 80, scale, $"[QR: {q.Data}]");
        }
    }

    private void DrawBar(TsplBar b, double scale)
    {
        var r = new Rectangle
        {
            Width = b.Width * scale,
            Height = b.Height * scale,
            Fill = Brushes.Black
        };
        Canvas.SetLeft(r, b.X * scale);
        Canvas.SetTop(r, b.Y * scale);
        Surface.Children.Add(r);
    }

    private void DrawBox(TsplBox b, double scale)
    {
        var r = new Rectangle
        {
            Width = Math.Max(1, (b.XEnd - b.X)) * scale,
            Height = Math.Max(1, (b.YEnd - b.Y)) * scale,
            Stroke = Brushes.Black,
            StrokeThickness = Math.Max(1, b.Thickness * scale)
        };
        Canvas.SetLeft(r, b.X * scale);
        Canvas.SetTop(r, b.Y * scale);
        Surface.Children.Add(r);
    }

    private void DrawFallbackBox(int x, int y, int w, int h, double scale, string label)
    {
        var r = new Rectangle
        {
            Width = w * scale,
            Height = h * scale,
            Stroke = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1)),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 2, 2 },
            Fill = Brushes.Transparent
        };
        Canvas.SetLeft(r, x * scale);
        Canvas.SetTop(r, y * scale);
        Surface.Children.Add(r);

        var tb = new TextBlock
        {
            Text = label,
            FontSize = 9,
            Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B))
        };
        Canvas.SetLeft(tb, x * scale + 4);
        Canvas.SetTop(tb, y * scale + 4);
        Surface.Children.Add(tb);
    }
}
