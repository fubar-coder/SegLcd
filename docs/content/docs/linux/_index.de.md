---
title: Linux
weight: 4
---

Unter Linux bindet der Kernel-Treiber `usbhid`/`hid-generic` das Display automatisch und stellt es
als `/dev/hidrawN` bereit. Es wird **kein eigener Kernel-Treiber** benötigt; der Zugriff erfolgt
komplett im Userspace.

{{< cards >}}
  {{< card link="udev" title="udev-Regel & Berechtigungen" icon="lock-open" >}}
  {{< card link="hidraw" title="hidraw-Schnittstelle im Detail" icon="terminal" >}}
{{< /cards >}}
