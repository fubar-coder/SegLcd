---
title: Überblick
weight: 1
---

Diese Dokumentation sammelt alles, was über das **Segment-LCD auf dem Pumpenblock der
Thermaltake LB360-S ARGB Sync** (Modellnummern CL-W499-PL12SW-A, Snow Edition CL-W500-PL12SW-A)
und seine USB-Ansteuerung bekannt ist. Ziel ist eine Beschreibung, die genau genug ist,
um ohne weitere Recherche eine Library zur Ansteuerung des Displays zu entwickeln.

{{< callout type="info" >}}
SegLcd ist ein unabhängiges Projekt und nicht mit Thermaltake verbunden. „Thermaltake“ ist eine Marke der Thermaltake Technology Co., Ltd.; der Name wird nur verwendet, um die unterstützte Hardware zu bezeichnen.
{{< /callout >}}

## Kurzfassung

| Eigenschaft | Wert |
|---|---|
| USB-ID | `5131:200e` (Vendor-ID 0x5131 ist bei usb.ids als „MSR“ registriert, **nicht** Thermaltakes `264a`) |
| Geräteklasse | HID, Vendor-Usage-Page `0xFF00`, 64-Byte-Input- und -Output-Reports, **keine Report-IDs** |
| Linux-Treiber | `usbhid` / `hid-generic`, Zugriff über `/dev/hidrawN` |
| Ansteuerung | Host sendet alle **250 ms** ein 64-Byte-Update-Paket (`00 01 02 …`) mit Messwerten |
| Ausschalten | Paket `00 0F 00 …` – oder einfach nicht mehr senden (Display geht nach Timeout aus) |
| ARGB-Beleuchtung | **nicht** über USB, sondern über den 5-V-ARGB-Header des Mainboards |
| Offizielle Software | „TT SEGMENT LCD Software“ (nur Windows, unterstützt laut Thermaltake CL-W499/CL-W500) |

{{< callout type="info" >}}
**Verifikationsstand (2026-11-20):** Das Protokoll stammt vom Projekt
[ThermalTakeWaKue](https://github.com/AL-193/ThermalTakeWaKue) für die **LA-Serie** (`5131:2007`).
Auf der **LB360-S** (`5131:200e`) ist das Update-Paket jetzt **auf echter Hardware bestätigt**: Das Display zeigt
die **CPU- und die GPU-Temperatur als ganze Zahl in °C**. Nachkommastellen, das °F-Flag und die CPU-Auslastung
haben keinen sichtbaren Effekt. Details: [Verifikation & offene Fragen](protokoll/verifikation/).
{{< /callout >}}

## Inhalt

{{< cards >}}
  {{< card link="hardware" title="Hardware & USB-Deskriptoren" icon="chip" >}}
  {{< card link="protokoll" title="Protokoll" icon="document-text" >}}
  {{< card link="linux" title="Linux: udev & hidraw" icon="terminal" >}}
  {{< card link="reverse-engineering" title="Reverse Engineering" icon="beaker" >}}
  {{< card link="library" title="Library-Leitfaden" icon="code" >}}
  {{< card link="werkzeug" title="Test-Werkzeug seglcd" icon="adjustments" >}}
  {{< card link="dienst" title="Dienst seglcdd (systemd, D-Bus)" icon="server" >}}
  {{< card link="sensoren" title="Sensoren & Konfiguration" icon="chart-bar" >}}
  {{< card link="lokalisierung" title="Lokalisierung" icon="translate" >}}
  {{< card link="quellen" title="Quellen" icon="link" >}}
{{< /cards >}}

## Repository-Aufbau

{{< filetree/container >}}
  {{< filetree/folder name="SegLcd" >}}
    {{< filetree/folder name="docs" state="closed" >}}
      {{< filetree/file name="hugo.yaml" >}}
      {{< filetree/folder name="content" state="closed" >}}{{< /filetree/folder >}}
      {{< filetree/folder name="themes/hextra (Submodule)" state="closed" >}}{{< /filetree/folder >}}
    {{< /filetree/folder >}}
    {{< filetree/folder name="src" >}}
      {{< filetree/folder name="SegLcd.Core" state="closed" >}}{{< /filetree/folder >}}
      {{< filetree/folder name="SegLcd.Linux" state="closed" >}}{{< /filetree/folder >}}
      {{< filetree/folder name="SegLcd.Localization" state="closed" >}}{{< /filetree/folder >}}
      {{< filetree/folder name="SegLcd.Tool" state="closed" >}}{{< /filetree/folder >}}
      {{< filetree/folder name="SegLcd.Daemon" state="closed" >}}{{< /filetree/folder >}}
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
