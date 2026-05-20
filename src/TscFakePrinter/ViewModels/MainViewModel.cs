using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using TscFakePrinter.Models;
using TscFakePrinter.Services;

namespace TscFakePrinter.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly TsplListenerService _listener = new();
    private readonly DispatcherTimer _uptimeTimer;

    [ObservableProperty] private bool isListening;
    [ObservableProperty] private string statusText = "Stopped";
    [ObservableProperty] private string toggleButtonText = "Start Listener";

    [ObservableProperty] private ObservableCollection<string> interfaces = new();
    [ObservableProperty] private string? selectedInterface;
    [ObservableProperty] private int port = 9100;

    [ObservableProperty] private ListenerStatistics stats = new();
    [ObservableProperty] private string uptime = "00:00:00";

    [ObservableProperty] private string receivedText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShouldWarnNoSize))]
    private string editableText = "";

    [ObservableProperty] private string formattedText = "";
    [ObservableProperty] private LabelDocument? currentDocument;

    [ObservableProperty] private long currentBytes;
    [ObservableProperty] private int currentLines;

    [ObservableProperty] private bool autoScroll = true;

    [ObservableProperty] private bool sizeOverrideEnabled;
    [ObservableProperty] private double overrideWidthMm = 60;
    [ObservableProperty] private double overrideHeightMm = 40;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShouldWarnNoSize))]
    private bool hasExplicitSize;

    public bool ShouldWarnNoSize => !HasExplicitSize && !string.IsNullOrWhiteSpace(EditableText);

    [ObservableProperty] private string footerStatus = "Listener stopped. Press Start to begin.";

    public ObservableCollection<ConnectionEntry> History { get; } = new();

    public MainViewModel()
    {
        foreach (var iface in NetworkInterfaceService.EnumerateChoices())
            Interfaces.Add(iface);
        SelectedInterface = Interfaces.FirstOrDefault();

        _listener.ClientConnected += OnClientConnected;
        _listener.JobReceived += OnJobReceived;
        _listener.ListenerError += OnListenerError;

        _uptimeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _uptimeTimer.Tick += (_, _) => RefreshUptime();
    }

    private void RefreshUptime()
    {
        if (Stats.StartTime is { } start)
        {
            var span = DateTime.Now - start;
            Uptime = $"{(int)span.TotalHours:D2}:{span.Minutes:D2}:{span.Seconds:D2}";
        }
        else
        {
            Uptime = "00:00:00";
        }
    }

    [RelayCommand]
    private void ToggleListener()
    {
        if (IsListening) StopListener();
        else StartListener();
    }

    private void StartListener()
    {
        try
        {
            var ip = NetworkInterfaceService.Resolve(SelectedInterface ?? "");
            _listener.Start(ip, Port);

            Stats.Reset();
            Stats.StartTime = DateTime.Now;
            _uptimeTimer.Start();

            IsListening = true;
            StatusText = "Listening";
            ToggleButtonText = "Stop Listener";
            FooterStatus = $"Listener is running and ready to receive data on port {Port}.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to start listener:\n{ex.Message}",
                "TSPL Listener",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void StopListener()
    {
        _listener.Stop();
        _uptimeTimer.Stop();
        IsListening = false;
        StatusText = "Stopped";
        ToggleButtonText = "Start Listener";
        FooterStatus = "Listener stopped.";
    }

    [RelayCommand]
    private void Clear()
    {
        ReceivedText = "";
        EditableText = "";
    }

    partial void OnEditableTextChanged(string value) => RecomputePreview();
    partial void OnSizeOverrideEnabledChanged(bool value) => RecomputePreview();
    partial void OnOverrideWidthMmChanged(double value)
    {
        if (SizeOverrideEnabled) RecomputePreview();
    }
    partial void OnOverrideHeightMmChanged(double value)
    {
        if (SizeOverrideEnabled) RecomputePreview();
    }

    private void RecomputePreview()
    {
        if (string.IsNullOrEmpty(EditableText))
        {
            FormattedText = "";
            CurrentDocument = null;
            CurrentBytes = 0;
            CurrentLines = 0;
            HasExplicitSize = false;
            return;
        }

        var parsed = TsplParser.Parse(EditableText);
        var doc = parsed.Document;

        HasExplicitSize = parsed.HasExplicitSize;

        if (SizeOverrideEnabled)
        {
            doc.WidthMm = OverrideWidthMm > 0 ? OverrideWidthMm : doc.WidthMm;
            doc.HeightMm = OverrideHeightMm > 0 ? OverrideHeightMm : doc.HeightMm;
        }

        FormattedText = parsed.FormattedText;
        CurrentDocument = doc;
        CurrentBytes = System.Text.Encoding.UTF8.GetByteCount(EditableText);
        CurrentLines = parsed.LineCount;
    }

    [RelayCommand]
    private void SaveAs()
    {
        if (string.IsNullOrEmpty(ReceivedText))
        {
            MessageBox.Show("Nothing to save yet.", "TSPL Listener",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "TSPL files (*.prn)|*.prn|Text files (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = $"tspl-{DateTime.Now:yyyyMMdd-HHmmss}.prn"
        };

        if (dialog.ShowDialog() == true)
        {
            File.WriteAllText(dialog.FileName, ReceivedText);
        }
    }

    private void OnClientConnected(object? sender, IPEndPoint remote)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            Stats.TotalConnections++;
        });
    }

    private void OnJobReceived(object? sender, JobReceivedEventArgs e)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            var header = $"# {DateTime.Now:HH:mm:ss}  from {e.Remote.Address}:{e.Remote.Port}  ({e.Payload.Length} bytes)";
            var separator = ReceivedText.Length > 0 ? "\n" : "";
            ReceivedText += $"{separator}{header}\n{e.Text.TrimEnd('\r', '\n')}\n";

            var wasSame = EditableText == e.Text;
            EditableText = e.Text;
            if (wasSame) RecomputePreview();

            Stats.TotalBytes += e.Payload.Length;
            Stats.TotalCommands += CurrentLines;

            History.Insert(0, new ConnectionEntry
            {
                Index = History.Count + 1,
                ClientIp = e.Remote.Address.ToString(),
                Port = e.Remote.Port,
                ReceivedAt = DateTime.Now,
                Bytes = e.Payload.Length,
                Lines = CurrentLines,
                PreviewSnippet = BuildSnippet(e.Text),
                RawText = e.Text
            });
        });
    }

    private static string BuildSnippet(string text)
    {
        var firstLine = text
            .Replace("\r", "")
            .Split('\n')
            .FirstOrDefault(l => !string.IsNullOrWhiteSpace(l)) ?? "";
        return firstLine.Length > 48 ? firstLine[..48] + "…" : firstLine;
    }

    private void OnListenerError(object? sender, Exception ex)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            FooterStatus = $"Listener error: {ex.Message}";
            StopListener();
        });
    }
}
