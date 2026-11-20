---
title: Reverse Engineering
weight: 5
---

To resolve the remaining open questions about the LB360-S (timeout, off packet, possible further commands), the USB traffic of the **original Windows software**
is captured. This does not require Windows on real hardware: the display is passed through to
a Windows VM via USB passthrough, and the traffic is recorded **on the Linux host** with `usbmon`.

```mermaid
flowchart LR
    LCD["LB360-S Display<br/>5131:200e"] -- USB --> Host["Linux host<br/>(usbmon captures)"]
    Host -- "libusb / SPICE-usbredir" --> QEMU["QEMU (qemu:///session)"]
    QEMU --> Win["Windows VM<br/>TT SEGMENT LCD Software"]
```

{{< cards >}}
  {{< card link="vm-passthrough" title="USB passthrough into the VM" icon="desktop-computer" >}}
  {{< card link="usb-capture" title="USB capture & analysis" icon="search" >}}
{{< /cards >}}
