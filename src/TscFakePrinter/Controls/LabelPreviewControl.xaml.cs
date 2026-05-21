using System;
using System.Collections.Generic;
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

    // 0 = auto-fit to the control's available area (default behaviour, used by
    // the inline preview tab). > 0 = render at exactly this many DIPs per dot,
    // so the control reports a fixed natural size — used by the zoom popup so
    // its outer LayoutTransform can scale predictably.
    public static readonly DependencyProperty FixedScaleProperty =
        DependencyProperty.Register(
            nameof(FixedScale),
            typeof(double),
            typeof(LabelPreviewControl),
            new PropertyMetadata(0.0, OnFixedScaleChanged));

    public double FixedScale
    {
        get => (double)GetValue(FixedScaleProperty);
        set => SetValue(FixedScaleProperty, value);
    }

    private static void OnFixedScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LabelPreviewControl c) c.Render();
    }

    // 203 dpi TSC printers — 8 dots/mm. (300 dpi printers use 12 dots/mm but
    // mm dimensions of fonts/labels are identical; we render in dot units.)
    private const double DotsPerMm = 8.0;

    // TSPL built-in bitmap font metrics in dots (width x height per glyph
    // before XMul/YMul multipliers). Values taken from the TSC TSPL
    // programming manual for 203 dpi printers.
    private static readonly Dictionary<string, (double W, double H)> FontMetrics
        = new(StringComparer.OrdinalIgnoreCase)
        {
            ["0"] = (12, 20),
            ["1"] = (8, 12),
            ["2"] = (10, 20),
            ["3"] = (12, 24),
            ["4"] = (14, 32),
            ["5"] = (32, 48),
            ["6"] = (14, 19),
            ["7"] = (21, 27),
            ["8"] = (14, 25),
            ["A"] = (12, 24),
            ["B"] = (12, 24),
            ["C"] = (12, 24),
            ["ROMAN.TTF"] = (10, 20),
            ["TSS24.BF2"] = (24, 24),
            ["TSS16.BF2"] = (16, 16),
        };

    public LabelPreviewControl()
    {
        InitializeComponent();
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
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

        var scale = FixedScale > 0
            ? FixedScale
            : ComputeAutoFitScale(widthDots, heightDots);

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

    // Use the outer control's ActualWidth/Height (set by the parent layout)
    // rather than LabelHost — LabelHost itself sizes to its child Surface,
    // which is what we're computing, so reading from it would be circular.
    private double ComputeAutoFitScale(double widthDots, double heightDots)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || double.IsInfinity(w)) return 0.5;
        if (h <= 0 || double.IsInfinity(h)) h = w * 0.7;

        var availW = Math.Max(80, w - 20);
        var availH = Math.Max(60, h - 36);

        var s = Math.Min(availW / widthDots, availH / heightDots);
        if (s <= 0 || double.IsNaN(s) || double.IsInfinity(s)) s = 0.5;
        return Math.Min(s, 8.0);
    }

    private static (double W, double H) GetFontMetrics(string font)
    {
        var key = (font ?? "").Trim().Trim('"');
        return FontMetrics.TryGetValue(key, out var dims) ? dims : (12, 24);
    }

    private void DrawText(TsplText t, double scale)
    {
        var (_, charH) = GetFontMetrics(t.Font);
        var heightDots = charH * Math.Max(1, t.YMul);

        var tb = new TextBlock
        {
            Text = t.Content,
            FontFamily = new FontFamily("Consolas, Cascadia Mono, Courier New, monospace"),
            Foreground = Brushes.Black,
            // FontSize is em-size; capHeight on Consolas is ~0.74 em, ascender+
            // descender ~1.0 em. Scaling em-size to heightDots*scale produces a
            // glyph height that closely matches the TSPL bitmap glyph height.
            FontSize = Math.Max(1, heightDots * scale),
            LineHeight = Math.Max(1, heightDots * scale),
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
        };
        TextOptions.SetTextFormattingMode(tb, TextFormattingMode.Display);

        var group = new TransformGroup();
        if (t.XMul != t.YMul && t.YMul > 0)
        {
            group.Children.Add(new ScaleTransform((double)t.XMul / t.YMul, 1, 0, 0));
        }
        if (t.Rotation != 0)
        {
            group.Children.Add(new RotateTransform(t.Rotation, 0, 0));
        }
        if (group.Children.Count > 0)
        {
            tb.RenderTransform = group;
            tb.RenderTransformOrigin = new Point(0, 0);
        }

        Canvas.SetLeft(tb, t.X * scale);
        Canvas.SetTop(tb, t.Y * scale);
        Surface.Children.Add(tb);
    }

    private void DrawBarcode(TsplBarcode b, double scale)
    {
        try
        {
            var fmt = MapBarcodeFormat(b.Type);

            // Generate the barcode at its natural module size — 1 px per module.
            // We then scale to (totalModules * narrowDots * scale) so that each
            // narrow bar ends up exactly b.Narrow dots wide on the label.
            var writer = new BarcodeWriterPixelData
            {
                Format = fmt,
                Options = new EncodingOptions
                {
                    Height = 1,
                    Width = 0,
                    Margin = 0,
                    PureBarcode = true,
                }
            };

            var pure = writer.Write(b.Data);
            var totalDots = pure.Width * Math.Max(1, b.Narrow);
            var widthPx = totalDots * scale;
            var heightPx = Math.Max(1, b.Height) * scale;

            var bmp = PixelDataToBitmapSource(pure);

            var container = new Canvas
            {
                Width = widthPx,
                Height = heightPx + (b.HumanReadable ? 14 * scale : 0)
            };

            var img = new System.Windows.Controls.Image
            {
                Source = bmp,
                Width = widthPx,
                Height = heightPx,
                Stretch = Stretch.Fill,
                SnapsToDevicePixels = true,
            };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(img, EdgeMode.Aliased);
            Canvas.SetLeft(img, 0);
            Canvas.SetTop(img, 0);
            container.Children.Add(img);

            if (b.HumanReadable && !string.IsNullOrEmpty(b.Data))
            {
                var label = new TextBlock
                {
                    Text = b.Data,
                    FontFamily = new FontFamily("Consolas, Cascadia Mono, Courier New, monospace"),
                    FontSize = Math.Max(6, 11 * scale),
                    Foreground = Brushes.Black,
                    TextAlignment = TextAlignment.Center,
                    Width = widthPx,
                };
                TextOptions.SetTextFormattingMode(label, TextFormattingMode.Display);
                Canvas.SetLeft(label, 0);
                Canvas.SetTop(label, heightPx + 2 * scale);
                container.Children.Add(label);
            }

            if (b.Rotation != 0)
            {
                container.RenderTransform = new RotateTransform(b.Rotation, 0, 0);
                container.RenderTransformOrigin = new Point(0, 0);
            }

            Canvas.SetLeft(container, b.X * scale);
            Canvas.SetTop(container, b.Y * scale);
            Surface.Children.Add(container);
        }
        catch
        {
            DrawFallbackBox(b.X, b.Y, 120, b.Height, scale, $"[BARCODE {b.Type}: {b.Data}]");
        }
    }

    private static BarcodeFormat MapBarcodeFormat(string type) => type.ToUpperInvariant() switch
    {
        "128" or "CODE128" or "CODE-128" => BarcodeFormat.CODE_128,
        "128M" or "EAN128" => BarcodeFormat.CODE_128,
        "39" or "CODE39" or "CODE-39" => BarcodeFormat.CODE_39,
        "93" or "CODE93" => BarcodeFormat.CODE_93,
        "EAN13" or "EAN-13" => BarcodeFormat.EAN_13,
        "EAN8" or "EAN-8" => BarcodeFormat.EAN_8,
        "UPCA" or "UPC-A" => BarcodeFormat.UPC_A,
        "UPCE" or "UPC-E" => BarcodeFormat.UPC_E,
        "ITF" or "I2OF5" or "INTERLEAVED2OF5" => BarcodeFormat.ITF,
        "CODABAR" or "NW7" => BarcodeFormat.CODABAR,
        "MSI" => BarcodeFormat.MSI,
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

            // Render the QR matrix manually so the output is exactly
            // moduleCount × moduleCount dots (no quiet zone) — that's what
            // TSC's QRCODE command writes. cellWidth is TSPL "module size in
            // dots", so 1 module = q.CellWidth dots on the label.
            var bmp = QrMatrixToBitmap(data, includeQuietZone: false, out var moduleCount);

            var totalDots = moduleCount * Math.Max(1, q.CellWidth);
            var sizePx = totalDots * scale;

            var img = new System.Windows.Controls.Image
            {
                Source = bmp,
                Width = sizePx,
                Height = sizePx,
                Stretch = Stretch.Fill,
                SnapsToDevicePixels = true,
            };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(img, EdgeMode.Aliased);

            if (q.Rotation != 0)
            {
                img.RenderTransform = new RotateTransform(q.Rotation, 0, 0);
                img.RenderTransformOrigin = new Point(0, 0);
            }

            Canvas.SetLeft(img, q.X * scale);
            Canvas.SetTop(img, q.Y * scale);
            Surface.Children.Add(img);
        }
        catch
        {
            DrawFallbackBox(q.X, q.Y, 80, 80, scale, $"[QR: {q.Data}]");
        }
    }

    // QRCoder's ModuleMatrix is stored with a 4-module quiet zone padding on
    // each side. We strip it to match TSPL's "QR starts at (X,Y)" behaviour.
    private static BitmapSource QrMatrixToBitmap(QRCodeData data, bool includeQuietZone, out int moduleCount)
    {
        var matrix = data.ModuleMatrix;
        var raw = matrix.Count;
        var pad = includeQuietZone ? 0 : 4;
        moduleCount = Math.Max(1, raw - pad * 2);

        var stride = moduleCount * 4;
        var pixels = new byte[stride * moduleCount];

        for (int y = 0; y < moduleCount; y++)
        {
            var row = matrix[y + pad];
            for (int x = 0; x < moduleCount; x++)
            {
                var bit = row[x + pad];
                var i = (y * stride) + (x * 4);
                if (bit)
                {
                    pixels[i + 0] = 0x00; // B
                    pixels[i + 1] = 0x00; // G
                    pixels[i + 2] = 0x00; // R
                    pixels[i + 3] = 0xFF; // A
                }
                else
                {
                    pixels[i + 0] = 0xFF;
                    pixels[i + 1] = 0xFF;
                    pixels[i + 2] = 0xFF;
                    pixels[i + 3] = 0xFF;
                }
            }
        }

        var bmp = BitmapSource.Create(
            moduleCount, moduleCount,
            96, 96,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        bmp.Freeze();
        return bmp;
    }

    private void DrawBar(TsplBar b, double scale)
    {
        var r = new Rectangle
        {
            Width = Math.Max(1, b.Width) * scale,
            Height = Math.Max(1, b.Height) * scale,
            Fill = Brushes.Black,
            SnapsToDevicePixels = true,
        };
        RenderOptions.SetEdgeMode(r, EdgeMode.Aliased);
        Canvas.SetLeft(r, b.X * scale);
        Canvas.SetTop(r, b.Y * scale);
        Surface.Children.Add(r);
    }

    private void DrawBox(TsplBox b, double scale)
    {
        var width = Math.Max(1, b.XEnd - b.X);
        var height = Math.Max(1, b.YEnd - b.Y);
        var thickness = Math.Max(1, b.Thickness) * scale;

        var r = new Rectangle
        {
            Width = width * scale,
            Height = height * scale,
            Stroke = Brushes.Black,
            StrokeThickness = thickness,
            SnapsToDevicePixels = true,
        };
        RenderOptions.SetEdgeMode(r, EdgeMode.Aliased);
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
