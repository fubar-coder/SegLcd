---
title: hidraw in detail
weight: 2
---

This page describes how to access the display **without a HID library** directly via the Linux hidraw interface
– relevant for implementations in C, Rust, Go, Python, etc.

## Finding the device

The number `N` in `/dev/hidrawN` is not stable. The device is found via sysfs (readable without root):

```bash
for d in /sys/class/hidraw/hidraw*; do
  grep -q '^HID_ID=0003:00005131:0000200E$' "$d/device/uevent" && echo "/dev/$(basename "$d")"
done
```

| File | Content (example) |
|---|---|
| `/sys/class/hidraw/hidraw5/device/uevent` | `HID_ID=0003:00005131:0000200E` (bus 3 = USB, VID, PID – hex, uppercase), `HID_NAME=HID 5131:200e`, `HID_PHYS=usb-0000:02:00.0-10/input0` |
| `/sys/class/hidraw/hidraw5/device/report_descriptor` | 34-byte report descriptor (binary) |
| `/dev/input/by-id/usb-5131_200e-hidraw` | stable symlink (created by systemd-udev) |

Alternatively with libudev: `subsystem = "hidraw"`, parent device `usb`/`usb_device` with
`idVendor == "5131"` and `idProduct == "200e"`. For **hotplug**, subscribe a udev monitor to `hidraw`.

## Opening

```c
int fd = open("/dev/hidraw5", O_RDWR | O_NONBLOCK | O_CLOEXEC);
```

Multiple processes can open the node at the same time – the kernel serializes the writes, but the
packets of both processes reach the display interleaved. A library should therefore use the device
**exclusively** (e.g. via a lock file or a single service).

## Writing

```c
uint8_t buf[65] = {0};   // buf[0] = report ID 0 (device has no report IDs)
buf[1 + 1] = 0x01;       // packet offset 1: command
buf[1 + 2] = 0x02;       // packet offset 2: subcommand
buf[1 + 3] = 42;         // CPU temperature
ssize_t n = write(fd, buf, sizeof buf);   // n == 65 on success
```

What happens in the kernel (`drivers/hid/hidraw.c` → `usbhid_output_report`):

1. hidraw passes the buffer as an **output report** to `hid_hw_output_report()`.
2. `usbhid` detects `buf[0] == 0` and **strips the report ID byte**.
3. The remaining 64 bytes go to endpoint `0x01` via interrupt transfer.
4. `write()` blocks until the transfer completes (timeout in the kernel) and returns 65.

| Error (`errno`) | Meaning | Reaction |
|---|---|---|
| `ENODEV` | device unplugged | close, wait for hotplug |
| `EPIPE` | endpoint stall | close, reopen |
| `ETIMEDOUT` | device accepts nothing | retry, reopen if needed |
| `EACCES` (on `open`) | no permissions | [udev rule](../udev/) |
| `EINTR` | signal | retry |

{{< callout type="warning" >}}
Always write **exactly 65 bytes**. Shorter writes produce short USB packets whose handling by the
firmware is undefined.
{{< /callout >}}

## Reading (input reports)

```c
uint8_t in[64];
ssize_t n = read(fd, in, sizeof in);   // 64 bytes, WITHOUT report ID byte; EAGAIN if nothing available
```

Wait with `poll()` on `POLLIN`. The kernel buffers up to 64 reports per file descriptor.

## Useful ioctls (`<linux/hidraw.h>`)

| ioctl | Number (x86-64) | Purpose |
|---|---|---|
| `HIDIOCGRDESCSIZE` | `0x80044801` | Length of the report descriptor (34) |
| `HIDIOCGRDESC` | `0x90044802` | Read the report descriptor (`struct hidraw_report_descriptor`) |
| `HIDIOCGRAWINFO` | `0x80084803` | `bustype` (3), `vendor` (0x5131), `product` (0x200e) |
| `HIDIOCGRAWNAME(len)` | `0x80004804 \| len<<16` | Name: `HID 5131:200e` |
| `HIDIOCGRAWPHYS(len)` | `0x80004805 \| len<<16` | Physical path: `usb-0000:02:00.0-10/input0` |

`HIDIOCSFEATURE`/`HIDIOCGFEATURE` are pointless – the device has no feature reports.

## Minimal example in the shell

For a quick test without any software (root or udev rule required). Sends "42 °C" for 20 s:

```bash
f=$(mktemp)
{ printf '\x00\x00\x01\x02\x2a'; head -c 60 /dev/zero; } > "$f"   # 65 bytes
for i in $(seq 80); do
  dd if="$f" of=/dev/hidraw5 bs=65 count=1 status=none
  sleep 0.25
done
rm "$f"
```

`dd bs=65` ensures that the 65 bytes (`00` report ID + `00 01 02 2a` + 60 × `00`) reach the kernel in **a single**
`write()`. Redirecting several `printf`/`head` calls directly to the device would
instead produce *multiple* short reports.
