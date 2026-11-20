---
title: SegLcd
layout: hextra-home
---

{{< hextra/hero-headline >}}
  SegLcd: Segment-LCD der Thermaltake&nbsp;LB360-S unter&nbsp;Linux
{{< /hextra/hero-headline >}}

{{< hextra/hero-subtitle >}}
  USB-Protokoll, Werkzeuge und Referenz-Implementierung für das Pumpen-Display
  der Thermaltake LB360-S ARGB Sync – genau genug, um eine eigene Library zu schreiben.
{{< /hextra/hero-subtitle >}}

<div class="hx:mt-6 hx:mb-6">
{{< hextra/hero-button text="Zur Dokumentation" link="docs" >}}
</div>

{{< hextra/feature-grid >}}
  {{< hextra/feature-card
    title="Protokoll"
    subtitle="64-Byte-HID-Reports, Byte für Byte beschrieben, mit Testvektoren."
    link="docs/protocol"
  >}}
  {{< hextra/feature-card
    title="Linux-Zugriff"
    subtitle="udev-Regel, hidraw-Semantik und Berechtigungen."
    link="docs/linux"
  >}}
  {{< hextra/feature-card
    title="Reverse Engineering"
    subtitle="USB-Passthrough in eine Windows-VM (qemu:///session) und Mitschnitt mit usbmon."
    link="docs/reverse-engineering"
  >}}
  {{< hextra/feature-card
    title="Library-Leitfaden"
    subtitle="Architektur, Zustände, Fehlerbehandlung – plus .NET-11-Referenz (AOT)."
    link="docs/library"
  >}}
{{< /hextra/feature-grid >}}
