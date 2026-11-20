---
title: Sources
weight: 10
---

## Protocol

- **ThermalTakeWaKue / ttlcd** (AL-193) – open-source driver for the segment LCD of the LA series, Windows and Linux,
  .NET, GPL-3.0. Source of the protocol description (capture of the software *TT SEGMENT LCD LA SERIES*, v2025-03).
  <https://github.com/AL-193/ThermalTakeWaKue> – in particular `docs/PROTOCOL.md`,
  `src/TtLcd/Protocol/LcdPacket.cs`, `src/TtLcd/Device/LcdDevice.cs`, `packaging/linux/99-ttlcd.rules`.

  {{< callout type="info" >}}
  The library in this repository is an **independent implementation** based on the protocol description;
  no GPL code was copied.
  {{< /callout >}}

## Manufacturer

- Product page LB360-S ARGB Sync: <https://www.thermaltake.com/lb360-s-argb-sync-aio-liquid-cooler.html>
- Snow Edition: <https://www.thermaltake.com/lb360-s-argb-sync-aio-liquid-cooler-snow-edition.html>
- US product page (CL-W499): <https://thermaltakeusa.com/products/lb360-s-argb-sync-aio-liquid-cooler-cl-w499-pl12sw-a>
- Manual: <https://file.thermaltake.com/file/qig/LB360-S_ARGB_Sync_manual.pdf>
- Software downloads (TT SEGMENT LCD Software, supports CL-W499/CL-W500): <https://thermaltake.com/pages/downloads>

## Related projects (different protocol/device)

- liquidctl, request for Thermaltake TOUGHLIQUID Ultra (LCD): <https://github.com/liquidctl/liquidctl/issues/626>
- linux-thermaltake-rgb (Riing/Floe fans, no display): <https://pypi.org/project/linux-thermaltake-rgb/>
- ttlcd for the Thermaltake LCD Panel Kit (Tower 200, pixel display): <https://github.com/bekindpleaserewind/ttlcd>

## USB IDs

- Vendor 0x5131 ("MSR"): <https://devicehunt.com/view/type/usb/vendor/5131>

## Technology

- Linux hidraw: <https://docs.kernel.org/hid/hidraw.html>
- usbmon: <https://docs.kernel.org/usb/usbmon.html>
- Wireshark USB capture: <https://wiki.wireshark.org/CaptureSetup/USB>
- HidApi.Net: <https://github.com/badcel/HidApi.Net>
- hidapi: <https://github.com/libusb/hidapi>
- HidSharp (used initially, replaced due to AOT trim warning): <https://www.zer7.com/software/hidsharp>
- DotMake.CommandLine: <https://dotmake.build/command-line/>
- TUnit: <https://tunit.dev/>
- Hextra: <https://imfing.github.io/hextra/>

## Reference system

| | |
|---|---|
| Date of recording | 2026-11-20 |
| Kernel | Linux 7.3.4-1-cachyos |
| CPU / GPU | AMD Ryzen 7 5800X3D / AMD Radeon Navi 44 (RX 9050 / 9060 XT) |
| Display on port | Bus 1, Port 10 (`usb-0000:02:00.0-10`), `/dev/hidraw5` |
