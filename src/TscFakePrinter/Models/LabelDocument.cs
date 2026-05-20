using System.Collections.Generic;

namespace TscFakePrinter.Models;

public sealed class LabelDocument
{
    public double WidthMm { get; set; } = 60;
    public double HeightMm { get; set; } = 40;
    public double GapMm { get; set; } = 2;
    public int Direction { get; set; } = 0;
    public int ReferenceX { get; set; } = 0;
    public int ReferenceY { get; set; } = 0;
    public int Density { get; set; } = 8;

    public List<TsplElement> Elements { get; } = new();
}
