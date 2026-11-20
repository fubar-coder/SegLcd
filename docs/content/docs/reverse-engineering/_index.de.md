---
title: Reverse Engineering
weight: 5
---

Um die restlichen offenen Fragen zur LB360-S zu klären (Timeout, Aus-Paket, mögliche weitere Kommandos), wird der USB-Verkehr der **originalen Windows-Software**
mitgeschnitten. Dafür muss kein Windows auf echter Hardware laufen: das Display wird per USB-Passthrough an
eine Windows-VM durchgereicht und der Verkehr **auf dem Linux-Host** mit `usbmon` aufgezeichnet.

```mermaid
flowchart LR
    LCD["LB360-S Display<br/>5131:200e"] -- USB --> Host["Linux-Host<br/>(usbmon schneidet mit)"]
    Host -- "libusb / SPICE-usbredir" --> QEMU["QEMU (qemu:///session)"]
    QEMU --> Win["Windows-VM<br/>TT SEGMENT LCD Software"]
```

{{< cards >}}
  {{< card link="vm-passthrough" title="USB-Passthrough in die VM" icon="desktop-computer" >}}
  {{< card link="usb-capture" title="USB-Mitschnitt & Auswertung" icon="search" >}}
{{< /cards >}}
