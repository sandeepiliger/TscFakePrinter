# TscFakePrinter

A WPF desktop application that impersonates a TSC label printer on the
network so TSPL command streams can be developed and tested without
physical hardware. The app opens a TCP listener (default `0.0.0.0:9100`),
accepts incoming jobs, captures the raw TSPL, and renders a best-effort
label preview.

## Features

- TCP listener on a configurable interface and port (default 9100)
- Live "Received TSPL Commands" pane with Clear / Save As / Auto-Scroll
- Raw / Formatted / Preview tabs
- Best-effort renderer for `SIZE`, `GAP`, `DIRECTION`, `REFERENCE`, `CLS`,
  `TEXT`, `BARCODE`, `QRCODE`, `BAR`, `BOX`, `PRINT`
- Statistics: total connections, total commands, total bytes, start time,
  uptime
- Per-connection history table

## Build / Run

Requires the .NET 8 SDK on Windows.

```powershell
dotnet restore
dotnet build src/TscFakePrinter/TscFakePrinter.csproj -c Release
dotnet run --project src/TscFakePrinter
```

## Smoke test

With the app running and the listener started on `127.0.0.1:9100`:

```powershell
$tspl = @"
SIZE 60 mm,50 mm
GAP 2 mm,0 mm
DIRECTION 1
CLS
TEXT 10,30,"3",0,1,1,"HELLO WORLD"
TEXT 10,80,"2",0,1,1,"TSPL COMMAND TEST"
BARCODE 10,120,"128",80,1,0,2,2,"1234567890123"
QRCODE 200,10,H,8,A,0,"https://www.tscprinters.com"
PRINT 1
"@
$client = New-Object System.Net.Sockets.TcpClient("127.0.0.1",9100)
$stream = $client.GetStream()
$bytes  = [Text.Encoding]::ASCII.GetBytes($tspl)
$stream.Write($bytes,0,$bytes.Length); $stream.Flush(); $client.Close()
```

The Preview tab should show two text lines, a Code128 barcode, and a QR
code; a row should be added to Connection History.

## Open-source dependencies

All NuGet packages are permissive open source (commercial-use friendly,
no GPL/LGPL/AGPL, no "non-commercial only" restrictions):

| Package | License |
| --- | --- |
| `CommunityToolkit.Mvvm` 8.x | MIT |
| `ZXing.Net` 0.16.x | Apache 2.0 |
| `QRCoder` 1.x | MIT |

## Scope notes

This is a best-effort emulator for development and testing — it is **not**
a full TSPL emulator. `BITMAP`/`PUTBMP`, complex font metrics, codepage
conversion, and printer status responses are intentionally out of scope.
