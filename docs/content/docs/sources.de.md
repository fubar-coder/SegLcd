---
title: Quellen
weight: 11
---

## Protokoll

- **ThermalTakeWaKue / ttlcd** (AL-193) – Open-Source-Treiber für das Segment-LCD der LA-Serie, Windows und Linux,
  .NET, GPL-3.0. Quelle der Protokollbeschreibung (Mitschnitt der Software *TT SEGMENT LCD LA SERIES*, v2025-03).
  <https://github.com/AL-193/ThermalTakeWaKue> – insbesondere `docs/PROTOCOL.md`,
  `src/TtLcd/Protocol/LcdPacket.cs`, `src/TtLcd/Device/LcdDevice.cs`, `packaging/linux/99-ttlcd.rules`.

  {{< callout type="info" >}}
  Die Library in diesem Repository ist eine **eigenständige Implementierung** auf Basis der Protokollbeschreibung;
  es wurde kein GPL-Code übernommen.
  {{< /callout >}}

## Hersteller

- Produktseite LB360-S ARGB Sync: <https://www.thermaltake.com/lb360-s-argb-sync-aio-liquid-cooler.html>
- Snow Edition: <https://www.thermaltake.com/lb360-s-argb-sync-aio-liquid-cooler-snow-edition.html>
- US-Produktseite (CL-W499): <https://thermaltakeusa.com/products/lb360-s-argb-sync-aio-liquid-cooler-cl-w499-pl12sw-a>
- Handbuch: <https://file.thermaltake.com/file/qig/LB360-S_ARGB_Sync_manual.pdf>
- Software-Downloads (TT SEGMENT LCD Software, unterstützt CL-W499/CL-W500): <https://thermaltake.com/pages/downloads>

## Verwandte Projekte (anderes Protokoll/Gerät)

- liquidctl, Anfrage Thermaltake TOUGHLIQUID Ultra (LCD): <https://github.com/liquidctl/liquidctl/issues/626>
- linux-thermaltake-rgb (Riing/Floe-Lüfter, kein Display): <https://pypi.org/project/linux-thermaltake-rgb/>
- ttlcd für das Thermaltake LCD Panel Kit (Tower 200, Pixel-Display): <https://github.com/bekindpleaserewind/ttlcd>

## USB-IDs

- Vendor 0x5131 („MSR“): <https://devicehunt.com/view/type/usb/vendor/5131>

## Technik

- Linux hidraw: <https://docs.kernel.org/hid/hidraw.html>
- usbmon: <https://docs.kernel.org/usb/usbmon.html>
- Wireshark USB-Capture: <https://wiki.wireshark.org/CaptureSetup/USB>
- HidApi.Net: <https://github.com/badcel/HidApi.Net>
- hidapi: <https://github.com/libusb/hidapi>
- HidSharp (zuerst verwendet, wegen AOT-Trim-Warnung ersetzt): <https://www.zer7.com/software/hidsharp>
- DotMake.CommandLine: <https://dotmake.build/command-line/>
- TUnit: <https://tunit.dev/>
- Hextra: <https://imfing.github.io/hextra/>

## Referenzsystem

| | |
|---|---|
| Datum der Aufnahme | 2026-11-20 |
| Kernel | Linux 7.3.4-1-cachyos |
| CPU / GPU | AMD Ryzen 7 5800X3D / AMD Radeon Navi 44 (RX 9050 / 9060 XT) |
| Display am Port | Bus 1, Port 10 (`usb-0000:02:00.0-10`), `/dev/hidraw5` |
