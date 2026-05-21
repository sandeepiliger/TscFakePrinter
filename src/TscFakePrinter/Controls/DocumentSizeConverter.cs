using System;
using System.Globalization;
using System.Windows.Data;
using TscFakePrinter.Models;

namespace TscFakePrinter.Controls;

public sealed class DocumentSizeConverter : IValueConverter
{
    public static readonly DocumentSizeConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is LabelDocument doc)
            return $"{doc.WidthMm:0.#} × {doc.HeightMm:0.#} mm  ·  {doc.Elements.Count} element{(doc.Elements.Count == 1 ? "" : "s")}";
        return "No label loaded";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
