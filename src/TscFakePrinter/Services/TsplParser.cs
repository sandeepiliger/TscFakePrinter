using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TscFakePrinter.Models;

namespace TscFakePrinter.Services;

public sealed class TsplParseResult
{
    public LabelDocument Document { get; init; } = new();
    public string FormattedText { get; init; } = "";
    public int LineCount { get; init; }
    public bool HasExplicitSize { get; init; }
}

public static class TsplParser
{
    public static TsplParseResult Parse(string text)
    {
        var lines = SplitLines(text);
        var doc = new LabelDocument();
        var pretty = new StringBuilder();
        var hasCls = false;
        var hasSize = false;
        var pendingDoc = new LabelDocument();

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd('\r').Trim();
            if (line.Length == 0) continue;

            var (cmd, args) = SplitCommand(line);
            var upper = cmd.ToUpperInvariant();

            switch (upper)
            {
                case "SIZE":
                    ApplySize(pendingDoc, args);
                    hasSize = true;
                    pretty.Append("SIZE       → ").AppendLine($"{pendingDoc.WidthMm} mm x {pendingDoc.HeightMm} mm");
                    break;
                case "GAP":
                    ApplyGap(pendingDoc, args);
                    pretty.Append("GAP        → ").AppendLine($"{pendingDoc.GapMm} mm");
                    break;
                case "DIRECTION":
                    pendingDoc.Direction = TryInt(args.ElementAtOrDefault(0), 0);
                    pretty.Append("DIRECTION  → ").AppendLine(pendingDoc.Direction.ToString());
                    break;
                case "REFERENCE":
                    pendingDoc.ReferenceX = TryInt(args.ElementAtOrDefault(0), 0);
                    pendingDoc.ReferenceY = TryInt(args.ElementAtOrDefault(1), 0);
                    pretty.Append("REFERENCE  → ").AppendLine($"{pendingDoc.ReferenceX},{pendingDoc.ReferenceY}");
                    break;
                case "DENSITY":
                    pendingDoc.Density = TryInt(args.ElementAtOrDefault(0), 8);
                    pretty.Append("DENSITY    → ").AppendLine(pendingDoc.Density.ToString());
                    break;
                case "CLS":
                    pendingDoc.Elements.Clear();
                    hasCls = true;
                    pretty.AppendLine("CLS");
                    break;
                case "TEXT":
                    if (TryParseText(args, out var t)) pendingDoc.Elements.Add(t);
                    pretty.Append("TEXT       → ").AppendLine(line);
                    break;
                case "BARCODE":
                    if (TryParseBarcode(args, out var b)) pendingDoc.Elements.Add(b);
                    pretty.Append("BARCODE    → ").AppendLine(line);
                    break;
                case "QRCODE":
                    if (TryParseQrCode(args, out var q)) pendingDoc.Elements.Add(q);
                    pretty.Append("QRCODE     → ").AppendLine(line);
                    break;
                case "BAR":
                    if (TryParseBar(args, out var bar)) pendingDoc.Elements.Add(bar);
                    pretty.Append("BAR        → ").AppendLine(line);
                    break;
                case "BOX":
                    if (TryParseBox(args, out var box)) pendingDoc.Elements.Add(box);
                    pretty.Append("BOX        → ").AppendLine(line);
                    break;
                case "PRINT":
                    pretty.Append("PRINT      → ").AppendLine(string.Join(",", args));
                    doc = CloneDocument(pendingDoc);
                    break;
                default:
                    pretty.AppendLine(line);
                    break;
            }
        }

        if (hasCls && doc.Elements.Count == 0)
            doc = CloneDocument(pendingDoc);

        return new TsplParseResult
        {
            Document = doc,
            FormattedText = pretty.ToString(),
            LineCount = lines.Count(l => l.Trim().Length > 0),
            HasExplicitSize = hasSize
        };
    }

    private static LabelDocument CloneDocument(LabelDocument src)
    {
        var dst = new LabelDocument
        {
            WidthMm = src.WidthMm,
            HeightMm = src.HeightMm,
            GapMm = src.GapMm,
            Direction = src.Direction,
            ReferenceX = src.ReferenceX,
            ReferenceY = src.ReferenceY,
            Density = src.Density
        };
        dst.Elements.AddRange(src.Elements);
        return dst;
    }

    private static string[] SplitLines(string text)
        => text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

    private static (string cmd, IList<string> args) SplitCommand(string line)
    {
        var firstSpace = -1;
        for (int i = 0; i < line.Length; i++)
        {
            if (char.IsWhiteSpace(line[i])) { firstSpace = i; break; }
        }

        if (firstSpace < 0) return (line, Array.Empty<string>());

        var cmd = line.Substring(0, firstSpace);
        var rest = line.Substring(firstSpace + 1).TrimStart();
        return (cmd, SplitArgs(rest));
    }

    private static IList<string> SplitArgs(string args)
    {
        var list = new List<string>();
        var sb = new StringBuilder();
        var inQuote = false;

        foreach (var ch in args)
        {
            if (ch == '"') { inQuote = !inQuote; sb.Append(ch); continue; }
            if (ch == ',' && !inQuote)
            {
                list.Add(sb.ToString().Trim());
                sb.Clear();
                continue;
            }
            sb.Append(ch);
        }
        if (sb.Length > 0) list.Add(sb.ToString().Trim());
        return list;
    }

    private static int TryInt(string? s, int fallback)
    {
        if (string.IsNullOrWhiteSpace(s)) return fallback;
        var token = StripUnits(s);
        return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? v
            : fallback;
    }

    private static double TryDouble(string? s, double fallback)
    {
        if (string.IsNullOrWhiteSpace(s)) return fallback;
        var token = StripUnits(s);
        return double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v
            : fallback;
    }

    private static string StripUnits(string s)
    {
        var t = s.Trim();
        foreach (var unit in new[] { "mm", "MM", "inch", "INCH", "dot", "DOT" })
        {
            if (t.EndsWith(unit, StringComparison.OrdinalIgnoreCase))
                t = t.Substring(0, t.Length - unit.Length).Trim();
        }
        return t;
    }

    private static string Unquote(string s)
    {
        var t = s.Trim();
        if (t.Length >= 2 && t[0] == '"' && t[^1] == '"')
            return t.Substring(1, t.Length - 2);
        return t;
    }

    private static void ApplySize(LabelDocument d, IList<string> args)
    {
        if (args.Count == 0) return;
        d.WidthMm = TryDouble(args[0], d.WidthMm);
        if (args.Count > 1) d.HeightMm = TryDouble(args[1], d.HeightMm);
    }

    private static void ApplyGap(LabelDocument d, IList<string> args)
    {
        if (args.Count == 0) return;
        d.GapMm = TryDouble(args[0], d.GapMm);
    }

    private static bool TryParseText(IList<string> a, out TsplText text)
    {
        text = new TsplText();
        if (a.Count < 7) return false;
        text = new TsplText
        {
            X = TryInt(a[0], 0),
            Y = TryInt(a[1], 0),
            Font = Unquote(a[2]),
            Rotation = TryInt(a[3], 0),
            XMul = Math.Max(1, TryInt(a[4], 1)),
            YMul = Math.Max(1, TryInt(a[5], 1)),
            Content = Unquote(a[6])
        };
        return true;
    }

    private static bool TryParseBarcode(IList<string> a, out TsplBarcode bc)
    {
        bc = new TsplBarcode();
        if (a.Count < 9) return false;
        bc = new TsplBarcode
        {
            X = TryInt(a[0], 0),
            Y = TryInt(a[1], 0),
            Type = Unquote(a[2]),
            Height = TryInt(a[3], 60),
            HumanReadable = TryInt(a[4], 1) != 0,
            Rotation = TryInt(a[5], 0),
            Narrow = Math.Max(1, TryInt(a[6], 2)),
            Wide = Math.Max(1, TryInt(a[7], 2)),
            Data = Unquote(a[8])
        };
        return true;
    }

    private static bool TryParseQrCode(IList<string> a, out TsplQrCode qr)
    {
        qr = new TsplQrCode();
        if (a.Count < 7) return false;
        qr = new TsplQrCode
        {
            X = TryInt(a[0], 0),
            Y = TryInt(a[1], 0),
            EccLevel = Unquote(a[2]),
            CellWidth = Math.Max(1, TryInt(a[3], 4)),
            Mode = Unquote(a[4]),
            Rotation = TryInt(a[5], 0),
            Data = Unquote(a[6])
        };
        return true;
    }

    private static bool TryParseBar(IList<string> a, out TsplBar bar)
    {
        bar = new TsplBar();
        if (a.Count < 4) return false;
        bar = new TsplBar
        {
            X = TryInt(a[0], 0),
            Y = TryInt(a[1], 0),
            Width = TryInt(a[2], 1),
            Height = TryInt(a[3], 1)
        };
        return true;
    }

    private static bool TryParseBox(IList<string> a, out TsplBox box)
    {
        box = new TsplBox();
        if (a.Count < 5) return false;
        box = new TsplBox
        {
            X = TryInt(a[0], 0),
            Y = TryInt(a[1], 0),
            XEnd = TryInt(a[2], 0),
            YEnd = TryInt(a[3], 0),
            Thickness = Math.Max(1, TryInt(a[4], 1))
        };
        return true;
    }
}
