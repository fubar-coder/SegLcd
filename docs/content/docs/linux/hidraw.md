---
title: hidraw im Detail
weight: 2
---

Diese Seite beschreibt, wie man das Display **ohne HID-Library** direkt über die Linux-hidraw-Schnittstelle
anspricht – relevant für Implementierungen in C, Rust, Go, Python usw.

## Gerät finden

Die Nummer `N` in `/dev/hidrawN` ist nicht stabil. Gefunden wird das Gerät über sysfs (ohne Root lesbar):

```bash
for d in /sys/class/hidraw/hidraw*; do
  grep -q '^HID_ID=0003:00005131:0000200E$' "$d/device/uevent" && echo "/dev/$(basename "$d")"
done
```

| Datei | Inhalt (Beispiel) |
|---|---|
| `/sys/class/hidraw/hidraw5/device/uevent` | `HID_ID=0003:00005131:0000200E` (Bus 3 = USB, VID, PID – Hex, großgeschrieben), `HID_NAME=HID 5131:200e`, `HID_PHYS=usb-0000:02:00.0-10/input0` |
| `/sys/class/hidraw/hidraw5/device/report_descriptor` | 34 Byte Report-Deskriptor (binär) |
| `/dev/input/by-id/usb-5131_200e-hidraw` | stabiler Symlink (von systemd-udev angelegt) |

Alternativ mit libudev: `subsystem = "hidraw"`, Eltern-Gerät `usb`/`usb_device` mit
`idVendor == "5131"` und `idProduct == "200e"`. Für **Hotplug** einen udev-Monitor auf `hidraw` abonnieren.

## Öffnen

```c
int fd = open("/dev/hidraw5", O_RDWR | O_NONBLOCK | O_CLOEXEC);
```

Mehrere Prozesse können den Knoten gleichzeitig öffnen – der Kernel serialisiert die Writes, aber die
Pakete beider Prozesse landen abwechselnd auf dem Display. Eine Library sollte das Gerät daher
**exklusiv** benutzen (z. B. über eine Lock-Datei oder einen einzigen Dienst).

## Schreiben

```c
uint8_t buf[65] = {0};   // buf[0] = Report-ID 0 (Gerät hat keine Report-IDs)
buf[1 + 1] = 0x01;       // Paket-Offset 1: Kommando
buf[1 + 2] = 0x02;       // Paket-Offset 2: Sub-Kommando
buf[1 + 3] = 42;         // CPU-Temperatur
ssize_t n = write(fd, buf, sizeof buf);   // n == 65 bei Erfolg
```

Was im Kernel passiert (`drivers/hid/hidraw.c` → `usbhid_output_report`):

1. hidraw übergibt den Puffer als **Output-Report** an `hid_hw_output_report()`.
2. `usbhid` erkennt `buf[0] == 0` und **entfernt das Report-ID-Byte**.
3. Die verbleibenden 64 Byte gehen per Interrupt-Transfer an Endpoint `0x01`.
4. `write()` blockiert, bis der Transfer abgeschlossen ist (Timeout im Kernel), und liefert 65 zurück.

| Fehler (`errno`) | Bedeutung | Reaktion |
|---|---|---|
| `ENODEV` | Gerät abgezogen | schließen, auf Hotplug warten |
| `EPIPE` | Endpoint-Stall | schließen, neu öffnen |
| `ETIMEDOUT` | Gerät nimmt nichts an | erneut versuchen, ggf. neu öffnen |
| `EACCES` (bei `open`) | keine Rechte | [udev-Regel](../udev/) |
| `EINTR` | Signal | wiederholen |

{{< callout type="warning" >}}
Immer **genau 65 Byte** schreiben. Kürzere Writes erzeugen kurze USB-Pakete, deren Behandlung durch die
Firmware undefiniert ist.
{{< /callout >}}

## Lesen (Input-Reports)

```c
uint8_t in[64];
ssize_t n = read(fd, in, sizeof in);   // 64 Byte, OHNE Report-ID-Byte; EAGAIN wenn nichts da
```

Mit `poll()` auf `POLLIN` warten. Der Kernel puffert pro Dateideskriptor bis zu 64 Reports.

## Nützliche ioctls (`<linux/hidraw.h>`)

| ioctl | Nummer (x86-64) | Zweck |
|---|---|---|
| `HIDIOCGRDESCSIZE` | `0x80044801` | Länge des Report-Deskriptors (34) |
| `HIDIOCGRDESC` | `0x90044802` | Report-Deskriptor lesen (`struct hidraw_report_descriptor`) |
| `HIDIOCGRAWINFO` | `0x80084803` | `bustype` (3), `vendor` (0x5131), `product` (0x200e) |
| `HIDIOCGRAWNAME(len)` | `0x80004804 \| len<<16` | Name: `HID 5131:200e` |
| `HIDIOCGRAWPHYS(len)` | `0x80004805 \| len<<16` | Physischer Pfad: `usb-0000:02:00.0-10/input0` |

`HIDIOCSFEATURE`/`HIDIOCGFEATURE` sind sinnlos – das Gerät hat keine Feature-Reports.

## Minimalbeispiel in der Shell

Zum schnellen Test ohne jede Software (Root oder udev-Regel nötig). Sendet 20 s lang „42 °C“:

```bash
f=$(mktemp)
{ printf '\x00\x00\x01\x02\x2a'; head -c 60 /dev/zero; } > "$f"   # 65 Byte
for i in $(seq 80); do
  dd if="$f" of=/dev/hidraw5 bs=65 count=1 status=none
  sleep 0.25
done
rm "$f"
```

`dd bs=65` stellt sicher, dass die 65 Byte (`00` Report-ID + `00 01 02 2a` + 60 × `00`) in **einem**
`write()` beim Kernel ankommen. Mehrere `printf`/`head` direkt auf das Gerät umzuleiten, würde
dagegen *mehrere* kurze Reports erzeugen.
