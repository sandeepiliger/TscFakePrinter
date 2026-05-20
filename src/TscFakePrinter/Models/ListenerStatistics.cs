using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TscFakePrinter.Models;

public partial class ListenerStatistics : ObservableObject
{
    [ObservableProperty] private int totalConnections;
    [ObservableProperty] private int totalCommands;
    [ObservableProperty] private long totalBytes;
    [ObservableProperty] private DateTime? startTime;

    public string StartTimeText => StartTime?.ToString("HH:mm:ss") ?? "—";

    partial void OnStartTimeChanged(DateTime? value) => OnPropertyChanged(nameof(StartTimeText));

    public void Reset()
    {
        TotalConnections = 0;
        TotalCommands = 0;
        TotalBytes = 0;
        StartTime = null;
    }
}
