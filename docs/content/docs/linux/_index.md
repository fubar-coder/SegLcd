---
title: Linux
weight: 4
---

On Linux, the kernel driver `usbhid`/`hid-generic` binds the display automatically and exposes it
as `/dev/hidrawN`. **No custom kernel driver** is required; access happens
entirely in userspace.

{{< cards >}}
  {{< card link="udev" title="udev rule & permissions" icon="lock-open" >}}
  {{< card link="hidraw" title="hidraw interface in detail" icon="terminal" >}}
{{< /cards >}}
