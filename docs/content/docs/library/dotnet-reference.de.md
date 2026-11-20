---
title: .NET-Referenz (SegLcd)
weight: 1
---

Das Repository enthält eine **.NET-11-Library** (`src/SegLcd.Core`, Namespace `SegLcd`), die dem
[Leitfaden](../) folgt. Sie ist **Native-AOT-kompatibel** (`IsAotCompatible`, AOT-Publish ohne Warnungen) und
hängt nur von [HidApi.Net](https://github.com/badcel/HidApi.Net) 1.2.0 (MIT) ab – einem AOT-fähigen Binding
der nativen C-Bibliothek [hidapi](https://github.com/libusb/hidapi).

{{< callout type="info" >}}
**Native Abhängigkeit:** hidapi ≥ 0.14 muss vorhanden sein.
Linux: Paket `hidapi` (Arch/CachyOS) bzw. `libhidapi-hidraw0` (Debian/Ubuntu) → `libhidapi-hidraw.so.0`.
Windows: `hidapi.dll` aus dem [hidapi-Release](https://github.com/libusb/hidapi/releases) neben die Anwendung legen.
{{< /callout >}}

## Projekte

| Projekt | Inhalt | Abhängigkeiten |
|---|---|---|
| `SegLcd.Core` (Namespace `SegLcd`) | Protokoll, Gerät, Session (plattformneutral) | HidApi.Net |
| `SegLcd.Linux` | Linux-Sensoren (`LinuxSensors`, `LinuxFrameSource`, `SensorSettings`, NVML, DRM, RAPL) | – |
| `SegLcd.Localization` | gettext-Lokalisierung ([Details](../../localization/)) | Karambolo.PO.Compact, M.E.Localization.Abstractions, M.E.Logging.Console |
| `SegLcd.Tool` | CLI `seglcd` | DotMake.CommandLine |
| `SegLcd.Daemon` | Dienst `seglcdd` mit D-Bus | Microsoft.Extensions.Hosting(.Systemd), Tmds.DBus.Protocol/Generator, Tomlyn |

## Bausteine

| Typ | Schicht | Aufgabe |
|---|---|---|
| `SegmentLcdFrame` | Codec | Record mit allen Werten (`double?`), `Time`, `Unit` |
| `SegmentLcdProtocol` | Codec | `EncodeUpdate`, `EncodeOff`, `ToHidReport`, Offsets-Konstanten, Feldnamen |
| `KnownDevices` | Gerät | bekannte VID/PID-Paare mit Verifikationsstatus |
| `SegmentLcdDevice` | Gerät | `Enumerate()`, `TryOpenFirst()`, `SendPacket()`, `Send(frame)`, `SendOff()`, `ReadInputReport()` |
| `SegmentLcdSession` | Session | Keep-Alive-Timer, Reconnect (max. 1×/s), `Update(frame)`, `UpdateRaw(bytes)`, `Interval` zur Laufzeit änderbar, `DevicePath`/`Model`/`LastPacket`, Aus-Paket bei `DisposeAsync` |

## Beispiel

```csharp
using SegLcd;

await using var session = new SegmentLcdSession();      // sucht das erste bekannte Gerät
session.ConnectionChanged += path => Console.WriteLine(path ?? "getrennt");
session.Update(new SegmentLcdFrame { CpuTemperature = 42.5, GpuTemperature = 51 });
session.Start();

using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
while (await timer.WaitForNextTickAsync())
{
    session.Update(new SegmentLcdFrame
    {
        CpuTemperature = ReadCpuTemp(),
        GpuTemperature = ReadGpuTemp(),
    });
}
// DisposeAsync: Timer stoppen, 0F senden, Gerät schließen
```

Nur den Codec verwenden (z. B. mit einem eigenen HID-Backend):

```csharp
byte[] packet = SegmentLcdProtocol.EncodeUpdate(frame);       // 64 Byte (Leitung)
byte[] report = SegmentLcdProtocol.ToHidReport(packet);       // 65 Byte für hidraw/hidapi/WriteFile
```

## hidapi-Eigenheiten

| Eigenschaft | Linux (hidraw-Backend) | Windows |
|---|---|---|
| `DeviceInfo.Path` | `/dev/hidraw5` | Interface-Pfad `\\?\hid#vid_5131&pid_200e…` |
| `Hid.Enumerate(vid, pid)` | liest sysfs, **öffnet nicht** → funktioniert ohne Zugriffsrechte | SetupAPI |
| `ManufacturerString`/`ProductString`/`SerialNumber` | leer (Gerät hat keine Strings) | leer |
| `UsagePage`/`Usage` | `0xFF00` / `0x0001` (aus dem Report-Deskriptor) | ebenso |
| `Device.Write` | 65 Byte inkl. Report-ID `00` | 65 Byte |
| `Device.ReadTimeout` | 64 Byte, Report-ID 0 **entfernt** | 64 Byte |
| `Device.GetReportDescriptor` | 34 Byte (hidapi ≥ 0.14) | rekonstruiert |
| Öffnen ohne Rechte | `HidException` → `TryOpen` liefert `null` | – |

`Hid.Exit()` gibt die hidapi-Ressourcen frei; das CLI ruft es am Programmende auf.

## Native AOT

- Library und CLI sind mit `IsAotCompatible`/`PublishAot` gebaut; `dotnet publish -r linux-x64` erzeugt
  ein ~5 MB großes, eigenständiges Binary (zur Laufzeit wird nur `libhidapi-hidraw.so.0` nachgeladen).
- Build-Ausgaben liegen dank `UseArtifactsOutput` (`Directory.Build.props`) gesammelt unter `artifacts/`:
  `artifacts/bin/<Projekt>/<konfiguration>`, `artifacts/obj/…`, `artifacts/publish/<Projekt>/release_linux-x64/`.
- Der AOT-Publish ist **warnungsfrei**. Ursprünglich wurde HidSharp 2.6.4 verwendet; dessen
  `netstandard2.0`-Build erzeugt eine Trim-Warnung (`IL2104`/`IL2075`, Reflection auf `Mono.Unix` in
  `NativeMethods.uname`). Der Codepfad ist unter .NET zwar tot, die Warnung aber nicht sauber unterdrückbar –
  deshalb der Wechsel zu HidApi.Net. Getestet im AOT-Binary: Enumeration, Öffnen, Deskriptor lesen, Senden.
- Die Unit-Tests (TUnit) vermeiden reflection-basierte Assertions (`IsEquivalentTo`) und sind damit ebenfalls
  AOT-tauglich.

## Bauen & Testen

```bash
dotnet build
dotnet test                                   # TUnit über Microsoft.Testing.Platform (global.json)
dotnet publish src/SegLcd.Tool -c Release -r linux-x64
# -> artifacts/publish/SegLcd.Tool/release_linux-x64/seglcd
```
