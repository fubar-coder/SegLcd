---
title: Hardware & USB descriptors
weight: 2
---

## Product

| | |
|---|---|
| Product | Thermaltake LB360-S ARGB Sync AIO Liquid Cooler |
| Model numbers | CL-W499-PL12SW-A (black), CL-W500-PL12SW-A (Snow Edition) |
| Display | round **segment LCD** (fixed digit/symbol segments, not a pixel display) for CPU/GPU temperature |
| Display connection | USB Type-C on the pump block → internal USB 2.0 header (9-pin) of the motherboard |
| Lighting | ARGB via the motherboard's 5 V header (3-pin), **independent** of the USB cable |
| Official software | *TT SEGMENT LCD Software* (Windows 10/11), current version V1.0.6 from 2026-06-08 according to the download page |

{{< callout type="info" >}}
The USB cable serves the display only. Fan and pump speeds are controlled and measured via
the motherboard headers; the lighting via ARGB Sync. A library
for the display therefore needs to handle neither PWM nor RGB.
{{< /callout >}}

## USB identification

Determined on the reference system (AMD Ryzen 7 5800X3D, motherboard with ITE IT8686E/IT8792E,
Linux 7.3, CachyOS) with `lsusb -v -d 5131:200e`:

```text
Bus 001 Device 003: ID 5131:200e MSR
Negotiated speed: Full Speed (12Mbps)
Device Descriptor:
  bLength                18
  bDescriptorType         1
  bcdUSB               1.10
  bDeviceClass            0 [unknown]
  bDeviceSubClass         0 [unknown]
  bDeviceProtocol         0
  bMaxPacketSize0        64
  idVendor           0x5131 MSR
  idProduct          0x200e
  bcdDevice            0.00
  iManufacturer           0
  iProduct                0
  iSerial                 0
  bNumConfigurations      1
  Configuration Descriptor:
    bLength                 9
    bDescriptorType         2
    wTotalLength       0x0029
    bNumInterfaces          1
    bConfigurationValue     1
    iConfiguration          4
    bmAttributes         0xa0
      (Bus Powered)
      Remote Wakeup
    MaxPower               70mA
    Interface Descriptor:
      bLength                 9
      bDescriptorType         4
      bInterfaceNumber        0
      bAlternateSetting       0
      bNumEndpoints           2
      bInterfaceClass         3 Human Interface Device
      bInterfaceSubClass      0 [unknown]
      bInterfaceProtocol      0
      iInterface              5
        HID Device Descriptor:
          bLength                 9
          bDescriptorType        33
          bcdHID               1.00
          bCountryCode            0 Not supported
          bNumDescriptors         1
          bDescriptorType        34 Report
          wDescriptorLength      34
      Endpoint Descriptor:
        bEndpointAddress     0x81  EP 1 IN
        bmAttributes            3
          Transfer Type            Interrupt
        wMaxPacketSize     0x0040  1x 64 bytes
        bInterval               1
      Endpoint Descriptor:
        bEndpointAddress     0x01  EP 1 OUT
        bmAttributes            3
          Transfer Type            Interrupt
        wMaxPacketSize     0x0040  1x 64 bytes
        bInterval               1
```

Key points for an implementation:

- **No manufacturer, product or serial number strings** (`iManufacturer = iProduct = iSerial = 0`).
  HID APIs return errors or empty strings for them – this is normal. Use **VID/PID only** for detection.
  According to ThermalTakeWaKue, the LA series (`5131:2007`) reports the product string `FBB`.
- String indices 4 (configuration) and 5 (interface) exist; their contents have not been read yet
  (requires root: `sudo lsusb -v -d 5131:200e`).
- **No serial number** ⇒ multiple identical devices can only be distinguished by their physical path
  (`HID_PHYS`, USB port).
- Full-speed device, one **interrupt IN (0x81)** and one **interrupt OUT (0x01)** endpoint of 64 bytes each,
  polling interval 1 ms.
- `bcdDevice = 0.00` – no firmware version can be read from it.

### Vendor ID 0x5131

`0x5131` is listed in the USB ID database as "MSR" (the only known product there:
`5131:2007` "MSR-101U Mini HID magnetic card reader"). In practice, Thermaltake apparently uses this
ID for the microcontroller of its segment LCDs – `5131:2007` is the LA series display.
A library should therefore **not** rely on the vendor name and should only address
**explicitly known VID/PID pairs**, so as not to write to unrelated devices (e.g. card readers).

| VID:PID | Device | Protocol |
|---|---|---|
| `5131:200e` | Thermaltake LB360-S ARGB Sync (CL-W499/CL-W500) | update packet verified (2026-11-20) |
| `5131:2007` | Thermaltake LA series water block | verified (ThermalTakeWaKue) |

## HID report descriptor

34 bytes, read from `/sys/bus/usb/devices/1-10:1.0/0003:5131:200E.0006/report_descriptor`
(readable without root):

```text
06 00 ff 09 01 a1 01 09 02 15 00 26 00 ff 75 08 95 40 81 06
09 02 15 00 26 00 ff 75 08 95 40 91 06 c0
```

Decoded:

| Bytes | Item | Meaning |
|---|---|---|
| `06 00 ff` | Usage Page | Vendor Defined `0xFF00` |
| `09 01` | Usage | `0x01` |
| `a1 01` | Collection | Application |
| `09 02` | Usage | `0x02` |
| `15 00` | Logical Minimum | 0 |
| `26 00 ff` | Logical Maximum | `0xFF00` (see note) |
| `75 08` | Report Size | 8 bits |
| `95 40` | Report Count | 64 |
| `81 06` | **Input** | Data, Variable, Relative → 64 bytes |
| `09 02` | Usage | `0x02` |
| `15 00` | Logical Minimum | 0 |
| `26 00 ff` | Logical Maximum | `0xFF00` |
| `75 08` | Report Size | 8 bits |
| `95 40` | Report Count | 64 |
| `91 06` | **Output** | Data, Variable, Relative → 64 bytes |
| `c0` | End Collection | |

Conclusions:

- There is **no `Report ID` item** ⇒ the device uses *unnumbered* reports. On the wire,
  reports are exactly 64 bytes long; HID APIs nevertheless expect a leading
  report ID byte `0x00` when writing (i.e. 65 bytes), see [Transport](../protocol/#transport).
- There are **no feature reports** – `HIDIOCSFEATURE`/`HidD_SetFeature` cannot be used.
- *Logical Maximum* is encoded as `26 00 ff`, i.e. little-endian `0xFF00` (signed −256).
  255 (`26 ff 00`) was surely intended. This is a harmless firmware bug; Linux and Windows
  ignore it for vendor reports. A library must not validate the descriptor strictly.
- The descriptor is **byte-identical** to that of the LA series (`5131:2007`).

## Representation on Linux

```text
/dev/hidraw5
  HID_ID=0003:00005131:0000200E
  HID_NAME=HID 5131:200e
  HID_PHYS=usb-0000:02:00.0-10/input0
  DRIVER=hid-generic
  Symlink: /dev/input/by-id/usb-5131_200e-hidraw
```

**No** input device (keyboard/mouse or similar) is created, only the hidraw node.
The number `N` in `/dev/hidrawN` is not stable – instead, search by VID/PID via sysfs/udev
or use the symlink `/dev/input/by-id/usb-5131_200e-hidraw`.
