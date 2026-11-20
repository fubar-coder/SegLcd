---
title: udev rule
weight: 1
---

By default, `/dev/hidrawN` is owned by `root:root` with mode `0600` – a regular user cannot access the
display. The rule `udev/70-seglcd.rules` grants the **locally
logged-in user** (systemd-logind, `uaccess`) access to

1. the **hidraw node** – for the test tool and any library, and
2. the **raw USB node** `/dev/bus/usb/BBB/DDD` – for USB passthrough into a VM under
   `qemu:///session` (see [VM passthrough](../../reverse-engineering/vm-passthrough/)).

```udev {filename="udev/70-seglcd.rules"}
# --- LB360-S ARGB Sync (5131:200e) ---
SUBSYSTEM=="hidraw", ATTRS{idVendor}=="5131", ATTRS{idProduct}=="200e", MODE="0660", TAG+="uaccess"
SUBSYSTEM=="usb", ENV{DEVTYPE}=="usb_device", ATTR{idVendor}=="5131", ATTR{idProduct}=="200e", MODE="0660", TAG+="uaccess"

# --- LA series (5131:2007) ---
SUBSYSTEM=="hidraw", ATTRS{idVendor}=="5131", ATTRS{idProduct}=="2007", MODE="0660", TAG+="uaccess"
SUBSYSTEM=="usb", ENV{DEVTYPE}=="usb_device", ATTR{idVendor}=="5131", ATTR{idProduct}=="2007", MODE="0660", TAG+="uaccess"
```

{{< callout type="warning" >}}
**File name &lt; 73!** `TAG+="uaccess"` is evaluated by `73-seat-late.rules`. Rules with a higher
number (e.g. `99-…`) set the tag too late – the ACL is then not granted.
{{< /callout >}}

## Installation

{{% steps %}}

### Copy the rule

```bash
sudo install -m 644 udev/70-seglcd.rules /etc/udev/rules.d/
```

### Reload udev and apply to existing devices

```bash
sudo udevadm control --reload
sudo udevadm trigger --action=change --subsystem-match=hidraw --subsystem-match=usb
```

Alternatively, unplug and replug the USB cable, or reboot.

### Verify

```bash
getfacl /dev/hidraw5                  # determine the number via 'seglcd list' if needed
# -> user:<your-user>:rw-

udevadm info -q property -n /dev/hidraw5 | grep TAGS
# -> CURRENT_TAGS=:seat:uaccess:

seglcd list
# -> Open  OK
```

{{% /steps %}}

## Variants

| Goal | Rule |
|---|---|
| Logged-in desktop user only (default) | `MODE="0660", TAG+="uaccess"` |
| System service under its own user (e.g. `seglcd`) | `MODE="0660", GROUP="seglcd"` – in addition to or instead of `uaccess` |
| Every local user | `MODE="0666"` (not recommended) |

A service running as **root** does not need a rule.

## Optional: RAPL for CPU power

The exact CPU **package power** comes from the RAPL energy counter `/sys/class/powercap/intel-rapl:*/energy_uj`
(also on AMD). Since Linux 5.10 it is readable by root only, because fine-grained energy values allow inferences about
running computations ([PLATYPUS, CVE-2020-8694/8695](https://platypusattack.com/)).
`udev/72-rapl-energy-read.rules` makes the counter readable **only for the group `rapl`** – much narrower than the
capability `cap_dac_read_search`, which lets e.g. btop read all files.

```udev {filename="udev/72-rapl-energy-read.rules"}
SUBSYSTEM=="powercap", KERNEL=="intel-rapl:*", ACTION=="add|change", RUN+="/usr/bin/chgrp rapl /sys%p/energy_uj", RUN+="/usr/bin/chmod 0440 /sys%p/energy_uj"
```

```fish
sudo install -m 644 udev/rapl.sysusers.conf /etc/sysusers.d/rapl.conf
sudo systemd-sysusers                       # creates the group "rapl"
sudo usermod -aG rapl $USER                 # then log out and back in
sudo install -m 644 udev/72-rapl-energy-read.rules /etc/udev/rules.d/
sudo udevadm control --reload
sudo udevadm trigger --action=add --subsystem-match=powercap
ls -l /sys/class/powercap/intel-rapl:0/energy_uj   # -r--r----- root rapl
```

Afterwards, `seglcd live` and `seglcdd` automatically use `rapl` as the CPU power source – even with k10temp, which itself
provides no power reading. Without the rule, power stays empty with k10temp; with zenpower, it is approximated as Core + SoC.

## Uninstallation

```bash
sudo rm /etc/udev/rules.d/70-seglcd.rules
sudo udevadm control --reload
sudo udevadm trigger --action=change --subsystem-match=hidraw --subsystem-match=usb
```
