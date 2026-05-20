namespace TscFakePrinter.Models;

public abstract class TsplElement
{
    public int X { get; init; }
    public int Y { get; init; }
    public int Rotation { get; init; }
}

public sealed class TsplText : TsplElement
{
    public string Font { get; init; } = "1";
    public int XMul { get; init; } = 1;
    public int YMul { get; init; } = 1;
    public string Content { get; init; } = "";
}

public sealed class TsplBarcode : TsplElement
{
    public string Type { get; init; } = "128";
    public int Height { get; init; } = 60;
    public bool HumanReadable { get; init; } = true;
    public int Narrow { get; init; } = 2;
    public int Wide { get; init; } = 2;
    public string Data { get; init; } = "";
}

public sealed class TsplQrCode : TsplElement
{
    public string EccLevel { get; init; } = "M";
    public int CellWidth { get; init; } = 4;
    public string Mode { get; init; } = "A";
    public string Data { get; init; } = "";
}

public sealed class TsplBar : TsplElement
{
    public int Width { get; init; }
    public int Height { get; init; }
}

public sealed class TsplBox : TsplElement
{
    public int XEnd { get; init; }
    public int YEnd { get; init; }
    public int Thickness { get; init; } = 1;
}
