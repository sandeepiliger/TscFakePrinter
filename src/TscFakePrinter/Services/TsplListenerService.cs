using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TscFakePrinter.Services;

public sealed class JobReceivedEventArgs : EventArgs
{
    public IPEndPoint Remote { get; init; } = new(IPAddress.None, 0);
    public byte[] Payload { get; init; } = Array.Empty<byte>();
    public string Text { get; init; } = "";
}

public sealed class TsplListenerService
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoop;

    public bool IsRunning => _listener is not null;
    public IPAddress? BoundAddress { get; private set; }
    public int BoundPort { get; private set; }

    public event EventHandler<IPEndPoint>? ClientConnected;
    public event EventHandler<JobReceivedEventArgs>? JobReceived;
    public event EventHandler<Exception>? ListenerError;

    public void Start(IPAddress address, int port)
    {
        if (IsRunning)
            throw new InvalidOperationException("Listener is already running.");

        _listener = new TcpListener(address, port);
        _listener.Start();
        BoundAddress = address;
        BoundPort = port;

        _cts = new CancellationTokenSource();
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        try
        {
            _cts?.Cancel();
            _listener?.Stop();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Stop error: {ex}");
        }
        finally
        {
            _listener = null;
            BoundAddress = null;
            BoundPort = 0;
        }
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        var listener = _listener!;
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex)
            {
                ListenerError?.Invoke(this, ex);
                break;
            }

            _ = Task.Run(() => HandleClientAsync(client, ct), ct);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        var remote = (IPEndPoint)(client.Client.RemoteEndPoint ?? new IPEndPoint(IPAddress.None, 0));
        ClientConnected?.Invoke(this, remote);

        using (client)
        await using (var stream = client.GetStream())
        using (var ms = new MemoryStream())
        {
            var buffer = new byte[8192];
            const int idleMs = 500;
            var idleCts = new CancellationTokenSource(idleMs);
            var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, idleCts.Token);

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    Task<int> readTask = stream.ReadAsync(buffer, 0, buffer.Length, linked.Token);
                    int read;
                    try
                    {
                        read = await readTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (IOException)
                    {
                        break;
                    }

                    if (read <= 0) break;

                    ms.Write(buffer, 0, read);

                    idleCts.Dispose();
                    idleCts = new CancellationTokenSource(idleMs);
                    linked.Dispose();
                    linked = CancellationTokenSource.CreateLinkedTokenSource(ct, idleCts.Token);
                }
            }
            finally
            {
                idleCts.Dispose();
                linked.Dispose();
            }

            var payload = ms.ToArray();
            if (payload.Length > 0)
            {
                var text = DecodeBytes(payload);
                JobReceived?.Invoke(this, new JobReceivedEventArgs
                {
                    Remote = remote,
                    Payload = payload,
                    Text = text
                });
            }
        }
    }

    private static string DecodeBytes(byte[] bytes)
    {
        try
        {
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return Encoding.ASCII.GetString(bytes);
        }
    }
}
