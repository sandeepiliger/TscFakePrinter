using System;

namespace TscFakePrinter.Models;

public sealed class ConnectionEntry
{
    public int Index { get; init; }
    public string ClientIp { get; init; } = "";
    public int Port { get; init; }
    public DateTime ReceivedAt { get; init; }
    public long Bytes { get; init; }
    public int Lines { get; init; }
    public string PreviewSnippet { get; init; } = "";
    public string RawText { get; init; } = "";

    public string ReceivedAtText => ReceivedAt.ToString("HH:mm:ss");
    public string BytesText
    {
        get
        {
            if (Bytes < 1024) return $"{Bytes} B";
            if (Bytes < 1024 * 1024) return $"{Bytes / 1024.0:0.#} KB";
            return $"{Bytes / (1024.0 * 1024):0.##} MB";
        }
    }
}
