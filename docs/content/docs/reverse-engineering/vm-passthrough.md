---
title: USB passthrough (qemu:///session)
weight: 1
---

With `qemu:///session`, QEMU runs **as a regular user**. For QEMU (or the SPICE client) to be allowed to open the
USB device, the user needs write permission on `/dev/bus/usb/BBB/DDD` – this is exactly why the
[udev rule](../../linux/udev/) contains the second line (`SUBSYSTEM=="usb"`, `uaccess`).

{{< callout type="info" >}}
While the device is passed through to the VM, the host detaches `usbhid` from the device; `/dev/hidrawN`
disappears. `seglcd` on the host does not work during this time. After disconnecting, the kernel binds
the device again (possibly with a new hidraw number).
{{< /callout >}}

## Prerequisites

- udev rule installed (`getfacl /dev/bus/usb/001/003` shows your own user with `rw`).
  Bus/address are reported by `lsusb -d 5131:200e`.
- The VM (here `Win11Pro`) has a USB controller (`qemu-xhci` is present) and – for variant A –
  SPICE graphics with `redirdev` channels (also present: two `spicevmc` channels).

## Variant A: SPICE USB redirection (recommended)

Simplest and reversible at any time – no change to the VM configuration required.

{{% steps %}}

### Start the VM and open the console

```bash
virt-manager --connect qemu:///session --show-domain-console Win11Pro
```

### Redirect the device

In the console window: **Virtual Machine → Redirect USB device** and
select `5131:200e`. Lacking a product string, the entry is named only after the ID.

### Release

Uncheck the device in the same dialog or close the console window.

{{% /steps %}}

If the redirection fails with "Permission denied", the udev rule is not active (or the device has not been re-triggered
since installation). As a fallback, spice-gtk can request permissions via `spice-client-glib-usb-acl-helper` (polkit);
it then asks for the password.

## Variant B: libvirt hostdev

More permanent, independent of an open console window. The file `vm/lb360s-usb-hostdev.xml`
selects the device by VID/PID (independent of bus/address):

```xml {filename="vm/lb360s-usb-hostdev.xml"}
<hostdev mode='subsystem' type='usb'>
  <source startupPolicy='optional'>
    <vendor id='0x5131'/>
    <product id='0x200e'/>
  </source>
</hostdev>
```

```bash
# attach to / detach from a running VM
virsh -c qemu:///session attach-device Win11Pro vm/lb360s-usb-hostdev.xml --live
virsh -c qemu:///session detach-device Win11Pro vm/lb360s-usb-hostdev.xml --live

# permanently add to the configuration (thanks to startupPolicy='optional', the VM also starts without the device)
virsh -c qemu:///session attach-device Win11Pro vm/lb360s-usb-hostdev.xml --config
```

Typical errors:

| Message | Cause |
|---|---|
| `cannot open /dev/bus/usb/001/003: Permission denied` | udev rule missing / not triggered |
| `Did not find USB device 5131:200e` | device not plugged in or currently redirected to another client |
| `device is in use by …` | simultaneously redirected via SPICE – use only one variant |

## In Windows

1. Install the **TT SEGMENT LCD Software** from the
   [Thermaltake download page](https://thermaltake.com/pages/downloads)
   (current list: supports CL-W499/CL-W500 = LB360-S).

   {{< callout type="warning" >}}
   **Not TT RGB Plus!** TT RGB Plus is more prominent on the download page but does not find the display.
   The TT SEGMENT LCD Software is somewhat hidden – search for "SEGMENT LCD".
   {{< /callout >}}
2. In Device Manager, a "HID-compliant vendor-defined device" with `VID_5131&PID_200E`
   appears under *Human Interface Devices*.
3. Start the software – the device appears there as **"LB-S ARGB AIO"**.
4. In the VM, practically all readings are **0** (virtual CPU without sensors, no real GPU, no fans).
   For protocol analysis this is hardly a drawback: the field mapping is already known from `seglcd probe`.
   What is interesting in the capture are the start/end sequence, the interval, and above all the software's **settings**
   (brightness, display mode, unit, display on/off), which could produce new commands.

{{< callout type="warning" >}}
If the software offers a **firmware update**: decline for now. An update via USB passthrough
can abort (re-enumeration, changed address) and brick the device. It would also
destroy comparability with the firmware documented here.
{{< /callout >}}

Continue with the [USB capture](../usb-capture/).
