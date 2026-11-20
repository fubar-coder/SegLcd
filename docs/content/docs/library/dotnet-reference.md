---
title: .NET reference (SegLcd)
weight: 1
---

The repository contains a **.NET 11 library** (`src/SegLcd.Core`, namespace `SegLcd`) that follows the
[guide](../). It is **Native AOT compatible** (`IsAotCompatible`, AOT publish without warnings) and
depends only on [HidApi.Net](https://github.com/badcel/HidApi.Net) 1.2.0 (MIT) – an AOT-capable binding
of the native C library [hidapi](https://github.com/libusb/hidapi).

{{< callout type="info" >}}
**Native dependency:** hidapi ≥ 0.14 must be present.
Linux: package `hidapi` (Arch/CachyOS) or `libhidapi-hidraw0` (Debian/Ubuntu) → `libhidapi-hidraw.so.0`.
Windows: place `hidapi.dll` from the [hidapi release](https://github.com/libusb/hidapi/releases) next to the application.
{{< /callout >}}

## Projects

| Project | Content | Dependencies |
|---|---|---|
| `SegLcd.Core` (namespace `SegLcd`) | protocol, device, session (platform-neutral) | HidApi.Net |
| `SegLcd.Linux` | Linux sensors (`LinuxSensors`, `LinuxFrameSource`, `SensorSettings`, NVML, DRM, RAPL) | – |
| `SegLcd.Localization` | gettext localization ([details](../../localization/)) | Karambolo.PO.Compact, M.E.Localization.Abstractions, M.E.Logging.Console |
| `SegLcd.Tool` | CLI `seglcd` | DotMake.CommandLine |
| `SegLcd.Daemon` | service `seglcdd` with D-Bus | Microsoft.Extensions.Hosting(.Systemd), Tmds.DBus.Protocol/Generator, Tomlyn |

## Building blocks

| Type | Layer | Responsibility |
|---|---|---|
| `SegmentLcdFrame` | Codec | record with all values (`double?`), `Time`, `Unit` |
| `SegmentLcdProtocol` | Codec | `EncodeUpdate`, `EncodeOff`, `ToHidReport`, offset constants, field names |
| `KnownDevices` | Device | known VID/PID pairs with verification status |
| `SegmentLcdDevice` | Device | `Enumerate()`, `TryOpenFirst()`, `SendPacket()`, `Send(frame)`, `SendOff()`, `ReadInputReport()` |
| `SegmentLcdSession` | Session | keep-alive timer, reconnect (max. 1×/s), `Update(frame)`, `UpdateRaw(bytes)`, `Interval` changeable at runtime, `DevicePath`/`Model`/`LastPacket`, off packet on `DisposeAsync` |

## Example

```csharp
using SegLcd;

await using var session = new SegmentLcdSession();      // finds the first known device
session.ConnectionChanged += path => Console.WriteLine(path ?? "disconnected");
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
// DisposeAsync: stop timer, send 0F, close device
```

Using only the codec (e.g. with your own HID backend):

```csharp
byte[] packet = SegmentLcdProtocol.EncodeUpdate(frame);       // 64 bytes (wire)
byte[] report = SegmentLcdProtocol.ToHidReport(packet);       // 65 bytes for hidraw/hidapi/WriteFile
```

## hidapi quirks

| Property | Linux (hidraw backend) | Windows |
|---|---|---|
| `DeviceInfo.Path` | `/dev/hidraw5` | interface path `\\?\hid#vid_5131&pid_200e…` |
| `Hid.Enumerate(vid, pid)` | reads sysfs, **does not open** → works without access rights | SetupAPI |
| `ManufacturerString`/`ProductString`/`SerialNumber` | empty (device has no strings) | empty |
| `UsagePage`/`Usage` | `0xFF00` / `0x0001` (from the report descriptor) | same |
| `Device.Write` | 65 bytes incl. report ID `00` | 65 bytes |
| `Device.ReadTimeout` | 64 bytes, report ID 0 **stripped** | 64 bytes |
| `Device.GetReportDescriptor` | 34 bytes (hidapi ≥ 0.14) | reconstructed |
| Opening without permissions | `HidException` → `TryOpen` returns `null` | – |

`Hid.Exit()` releases the hidapi resources; the CLI calls it at program exit.

## Native AOT

- Library and CLI are built with `IsAotCompatible`/`PublishAot`; `dotnet publish -r linux-x64` produces
  a ~5 MB self-contained binary (only `libhidapi-hidraw.so.0` is loaded at runtime).
- Thanks to `UseArtifactsOutput` (`Directory.Build.props`), build outputs are collected under `artifacts/`:
  `artifacts/bin/<project>/<configuration>`, `artifacts/obj/…`, `artifacts/publish/<project>/release_linux-x64/`.
- The AOT publish is **warning-free**. Originally HidSharp 2.6.4 was used; its
  `netstandard2.0` build produces a trim warning (`IL2104`/`IL2075`, reflection on `Mono.Unix` in
  `NativeMethods.uname`). The code path is dead under .NET, but the warning cannot be cleanly suppressed –
  hence the switch to HidApi.Net. Tested in the AOT binary: enumeration, opening, reading the descriptor, sending.
- The unit tests (TUnit) avoid reflection-based assertions (`IsEquivalentTo`) and are therefore also
  AOT-compatible.

## Building & testing

```bash
dotnet build
dotnet test                                   # TUnit via Microsoft.Testing.Platform (global.json)
dotnet publish src/SegLcd.Tool -c Release -r linux-x64
# -> artifacts/publish/SegLcd.Tool/release_linux-x64/seglcd
```
