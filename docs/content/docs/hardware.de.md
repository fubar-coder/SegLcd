---
title: Hardware & USB-Deskriptoren
weight: 2
---

## Produkt

| | |
|---|---|
| Produkt | Thermaltake LB360-S ARGB Sync AIO Liquid Cooler |
| Modellnummern | CL-W499-PL12SW-A (schwarz), CL-W500-PL12SW-A (Snow Edition) |
| Display | rundes **Segment-LCD** (feste Ziffern-/Symbolsegmente, kein Pixel-Display) für CPU-/GPU-Temperatur |
| Display-Anschluss | USB Type-C am Pumpenblock → interner USB-2.0-Header (9-Pin) des Mainboards |
| Beleuchtung | ARGB über 5-V-Header des Mainboards (3-Pin), **unabhängig** vom USB-Kabel |
| Offizielle Software | *TT SEGMENT LCD Software* (Windows 10/11), aktuelle Version V1.0.6 vom 2026-06-08 laut Download-Seite |

{{< callout type="info" >}}
Das USB-Kabel dient ausschließlich dem Display. Lüfter- und Pumpendrehzahl werden über
die Mainboard-Header geregelt bzw. gemessen; die Beleuchtung über ARGB-Sync. Eine Library
für das Display muss also weder PWM noch RGB beherrschen.
{{< /callout >}}

## USB-Identifikation

Ermittelt auf dem Referenzsystem (AMD Ryzen 7 5800X3D, Mainboard mit ITE IT8686E/IT8792E,
Linux 7.3, CachyOS) mit `lsusb -v -d 5131:200e`:

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

Wichtige Punkte für eine Implementierung:

- **Keine Hersteller-, Produkt- oder Seriennummer-Strings** (`iManufacturer = iProduct = iSerial = 0`).
  HID-APIs liefern dafür Fehler bzw. leere Strings – das ist normal. Zur Erkennung **nur VID/PID** verwenden.
  Die LA-Serie (`5131:2007`) meldet laut ThermalTakeWaKue den Produktstring `FBB`.
- Die String-Indizes 4 (Konfiguration) und 5 (Interface) existieren; ihr Inhalt ist noch nicht ausgelesen
  (erfordert Root: `sudo lsusb -v -d 5131:200e`).
- **Keine Seriennummer** ⇒ mehrere identische Geräte lassen sich nur über den physischen Pfad
  (`HID_PHYS`, USB-Port) unterscheiden.
- Full-Speed-Gerät, je ein **Interrupt-IN (0x81)** und **Interrupt-OUT (0x01)** Endpoint mit 64 Byte,
  Polling-Intervall 1 ms.
- `bcdDevice = 0.00` – eine Firmwareversion ist darüber nicht ablesbar.

### Vendor-ID 0x5131

`0x5131` ist in der USB-ID-Datenbank als „MSR“ eingetragen (dort einziges bekanntes Produkt:
`5131:2007` „MSR-101U Mini HID magnetic card reader“). Tatsächlich verwendet Thermaltake diese
ID offenbar für den Mikrocontroller seiner Segment-LCDs – `5131:2007` ist das LA-Serien-Display.
Eine Library sollte sich daher **nicht** auf den Vendor-Namen verlassen und nur
**explizit bekannte VID/PID-Paare** ansprechen, um keine fremden Geräte (z. B. Kartenleser) zu beschreiben.

| VID:PID | Gerät | Protokoll |
|---|---|---|
| `5131:200e` | Thermaltake LB360-S ARGB Sync (CL-W499/CL-W500) | Update-Paket verifiziert (2026-11-20) |
| `5131:2007` | Thermaltake LA-Serie Wasserblock | verifiziert (ThermalTakeWaKue) |

## HID-Report-Deskriptor

34 Bytes, gelesen aus `/sys/bus/usb/devices/1-10:1.0/0003:5131:200E.0006/report_descriptor`
(ohne Root lesbar):

```text
06 00 ff 09 01 a1 01 09 02 15 00 26 00 ff 75 08 95 40 81 06
09 02 15 00 26 00 ff 75 08 95 40 91 06 c0
```

Dekodiert:

| Bytes | Item | Bedeutung |
|---|---|---|
| `06 00 ff` | Usage Page | Vendor Defined `0xFF00` |
| `09 01` | Usage | `0x01` |
| `a1 01` | Collection | Application |
| `09 02` | Usage | `0x02` |
| `15 00` | Logical Minimum | 0 |
| `26 00 ff` | Logical Maximum | `0xFF00` (s. Hinweis) |
| `75 08` | Report Size | 8 Bit |
| `95 40` | Report Count | 64 |
| `81 06` | **Input** | Data, Variable, Relative → 64 Byte |
| `09 02` | Usage | `0x02` |
| `15 00` | Logical Minimum | 0 |
| `26 00 ff` | Logical Maximum | `0xFF00` |
| `75 08` | Report Size | 8 Bit |
| `95 40` | Report Count | 64 |
| `91 06` | **Output** | Data, Variable, Relative → 64 Byte |
| `c0` | End Collection | |

Folgerungen:

- Es gibt **kein `Report ID`-Item** ⇒ das Gerät nutzt *unnummerierte* Reports. Auf der Leitung
  sind Reports genau 64 Byte lang; HID-APIs erwarten beim Schreiben trotzdem ein führendes
  Report-ID-Byte `0x00` (also 65 Byte), siehe [Transport](../protocol/#transport).
- Es gibt **keine Feature-Reports** – `HIDIOCSFEATURE`/`HidD_SetFeature` sind nicht nutzbar.
- *Logical Maximum* ist als `26 00 ff` kodiert, also little-endian `0xFF00` (vorzeichenbehaftet −256).
  Gemeint war sicher 255 (`26 ff 00`). Das ist ein harmloser Firmware-Fehler; Linux und Windows
  ignorieren ihn für Vendor-Reports. Eine Library darf den Deskriptor nicht strikt validieren.
- Der Deskriptor ist **byte-identisch** mit dem der LA-Serie (`5131:2007`).

## Darstellung unter Linux

```text
/dev/hidraw5
  HID_ID=0003:00005131:0000200E
  HID_NAME=HID 5131:200e
  HID_PHYS=usb-0000:02:00.0-10/input0
  DRIVER=hid-generic
  Symlink: /dev/input/by-id/usb-5131_200e-hidraw
```

Es entsteht **kein** Input-Device (Tastatur/Maus o. ä.), nur der hidraw-Knoten.
Die Nummer `N` in `/dev/hidrawN` ist nicht stabil – stattdessen über sysfs/udev nach VID/PID suchen
oder den Symlink `/dev/input/by-id/usb-5131_200e-hidraw` verwenden.
