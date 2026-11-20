---
title: Overview
weight: 1
---

This documentation collects everything known about the **segment LCD on the pump block of the
Thermaltake LB360-S ARGB Sync** (model numbers CL-W499-PL12SW-A, Snow Edition CL-W500-PL12SW-A)
and how it is driven over USB. The goal is a description precise enough
to develop a library for driving the display without further research.

{{< callout type="info" >}}
SegLcd is an independent project and not affiliated with Thermaltake. "Thermaltake" is a trademark of Thermaltake Technology Co., Ltd.; the name is used only to identify the supported hardware.
{{< /callout >}}

## Summary

| Property | Value |
|---|---|
| USB ID | `5131:200e` (vendor ID 0x5131 is registered in usb.ids as "MSR", **not** Thermaltake's `264a`) |
| Device class | HID, vendor usage page `0xFF00`, 64-byte input and output reports, **no report IDs** |
| Linux driver | `usbhid` / `hid-generic`, access via `/dev/hidrawN` |
| Control | Host sends a 64-byte update packet (`00 01 02 …`) with readings every **250 ms** |
| Turning off | Packet `00 0F 00 …` – or simply stop sending (the display turns off after a timeout) |
| ARGB lighting | **not** via USB, but via the motherboard's 5 V ARGB header |
| Official software | "TT SEGMENT LCD Software" (Windows only; according to Thermaltake supports CL-W499/CL-W500) |

{{< callout type="info" >}}
**Verification status (2026-11-20):** The protocol comes from the project
[ThermalTakeWaKue](https://github.com/AL-193/ThermalTakeWaKue) for the **LA series** (`5131:2007`).
On the **LB360-S** (`5131:200e`) the update packet is now **confirmed on real hardware**: the display shows
the **CPU and GPU temperature as an integer in °C**. Decimals, the °F flag and CPU usage
have no visible effect. Details: [Verification & open questions](protocol/verification/).
{{< /callout >}}

## Contents

{{< cards >}}
  {{< card link="hardware" title="Hardware & USB descriptors" icon="chip" >}}
  {{< card link="protocol" title="Protocol" icon="document-text" >}}
  {{< card link="linux" title="Linux: udev & hidraw" icon="terminal" >}}
  {{< card link="reverse-engineering" title="Reverse engineering" icon="beaker" >}}
  {{< card link="library" title="Library guide" icon="code" >}}
  {{< card link="tool" title="Test tool seglcd" icon="adjustments" >}}
  {{< card link="service" title="Service seglcdd (systemd, D-Bus)" icon="server" >}}
  {{< card link="gui" title="Settings app seglcd-gtk" icon="desktop-computer" >}}
  {{< card link="sensors" title="Sensors & configuration" icon="chart-bar" >}}
  {{< card link="localization" title="Localization" icon="translate" >}}
  {{< card link="sources" title="Sources" icon="link" >}}
{{< /cards >}}

## Repository layout

{{< filetree/container >}}
  {{< filetree/folder name="SegLcd" >}}
    {{< filetree/folder name="docs" state="closed" >}}
      {{< filetree/file name="hugo.yaml" >}}
      {{< filetree/folder name="content" state="closed" >}}{{< /filetree/folder >}}
      {{< filetree/folder name="themes/hextra (submodule)" state="closed" >}}{{< /filetree/folder >}}
    {{< /filetree/folder >}}
    {{< filetree/folder name="src" >}}
      {{< filetree/folder name="SegLcd.Core" state="closed" >}}{{< /filetree/folder >}}
      {{< filetree/folder name="SegLcd.Linux" state="closed" >}}{{< /filetree/folder >}}
      {{< filetree/folder name="SegLcd.Localization" state="closed" >}}{{< /filetree/folder >}}
      {{< filetree/folder name="SegLcd.Tool" state="closed" >}}{{< /filetree/folder >}}
      {{< filetree/folder name="SegLcd.Daemon" state="closed" >}}{{< /filetree/folder >}}
      {{< filetree/folder name="SegLcd.Client" state="closed" >}}{{< /filetree/folder >}}
      {{< filetree/folder name="SegLcd.Gtk" state="closed" >}}{{< /filetree/folder >}}
    {{< /filetree/folder >}}
    {{< filetree/folder name="tests/SegLcd.Tests" state="closed" >}}{{< /filetree/folder >}}
    {{< filetree/folder name="udev" >}}
      {{< filetree/file name="70-seglcd.rules" >}}
      {{< filetree/file name="72-rapl-energy-read.rules" >}}
    {{< /filetree/folder >}}
    {{< filetree/folder name="packaging" >}}
      {{< filetree/file name="install-user.sh" >}}
      {{< filetree/file name="config.example.toml" >}}
      {{< filetree/folder name="systemd" state="closed" >}}{{< /filetree/folder >}}
      {{< filetree/folder name="dbus" state="closed" >}}{{< /filetree/folder >}}
    {{< /filetree/folder >}}
    {{< filetree/folder name="vm" >}}
      {{< filetree/file name="lb360s-usb-hostdev.xml" >}}
    {{< /filetree/folder >}}
    {{< filetree/folder name="tools" >}}
      {{< filetree/file name="capture-usbmon.sh" >}}
      {{< filetree/file name="extract-packets.sh" >}}
    {{< /filetree/folder >}}
    {{< filetree/folder name="po" >}}
      {{< filetree/file name="seglcd.pot" >}}
      {{< filetree/file name="de.po" >}}
      {{< filetree/file name="extract.sh" >}}
    {{< /filetree/folder >}}
    {{< filetree/folder name="captures" state="closed" >}}{{< /filetree/folder >}}
    {{< filetree/file name="SegLcd.slnx" >}}
  {{< /filetree/folder >}}
{{< /filetree/container >}}
