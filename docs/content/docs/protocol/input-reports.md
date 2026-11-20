---
title: Input reports
weight: 3
---

According to the HID descriptor, the device can send 64-byte **input reports** via the interrupt IN endpoint `0x81`.
The original software (LA series) **never reads them**; their meaning is unknown.

Possible contents (speculation, to be checked): firmware version, acknowledgment (ACK) of received packets,
buttons/touch (the LB360-S display has no known controls).

## Investigation

```bash
# listen for 30 s without sending anything
seglcd listen --duration 30

# listen while update packets are being sent (in case the device only responds then)
seglcd listen --keep-alive --duration 30
```

On Linux, `read()` on hidraw returns the **64 bytes without report ID** for unnumbered reports;
hidapi (`hid_read`, HidApi.Net `ReadTimeout`) also returns 64 bytes without report ID; HidSharp, in contrast, returns 65 bytes with a leading `0x00`.

{{< callout type="info" >}}
A library does **not** need to read input reports. The kernel buffers up to 64 reports per open hidraw file descriptor
and then discards the oldest ones – an overflow therefore does no harm.
{{< /callout >}}
