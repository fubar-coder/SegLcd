---
title: SegLcd
layout: hextra-home
---

{{< hextra/hero-headline >}}
  SegLcd: segment LCD of the Thermaltake&nbsp;LB360-S on&nbsp;Linux
{{< /hextra/hero-headline >}}

{{< hextra/hero-subtitle >}}
  USB protocol, tools and reference implementation for the pump display
  of the Thermaltake LB360-S ARGB Sync – precise enough to write your own library.
{{< /hextra/hero-subtitle >}}

<div class="hx:mt-6 hx:mb-6">
{{< hextra/hero-button text="Go to the documentation" link="docs" >}}
</div>

{{< hextra/feature-grid >}}
  {{< hextra/feature-card
    title="Protocol"
    subtitle="64-byte HID reports, described byte by byte, with test vectors."
    link="docs/protocol"
  >}}
  {{< hextra/feature-card
    title="Linux access"
    subtitle="udev rule, hidraw semantics and permissions."
    link="docs/linux"
  >}}
  {{< hextra/feature-card
    title="Reverse engineering"
    subtitle="USB passthrough into a Windows VM (qemu:///session) and capture with usbmon."
    link="docs/reverse-engineering"
  >}}
  {{< hextra/feature-card
    title="Library guide"
    subtitle="Architecture, states, error handling – plus .NET 11 reference (AOT)."
    link="docs/library"
  >}}
{{< /hextra/feature-grid >}}
