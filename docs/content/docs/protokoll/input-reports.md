---
title: Input-Reports
weight: 3
---

Laut HID-Deskriptor kann das Gerät 64-Byte-**Input-Reports** über den Interrupt-IN-Endpoint `0x81`
senden. Die Original-Software (LA-Serie) **liest sie nie**; ihre Bedeutung ist unbekannt.

Mögliche Inhalte (Spekulation, zu prüfen): Firmwareversion, Bestätigung (ACK) empfangener Pakete,
Tasten/Touch (das LB360-S-Display hat keine bekannten Bedienelemente).

## Untersuchung

```bash
# 30 s lauschen, ohne selbst zu senden
seglcd listen --duration 30

# lauschen, während Update-Pakete gesendet werden (falls das Gerät nur dann antwortet)
seglcd listen --keep-alive --duration 30
```

Unter Linux liefert `read()` auf hidraw bei unnummerierten Reports die **64 Byte ohne Report-ID**;
hidapi (`hid_read`, HidApi.Net `ReadTimeout`) liefert ebenfalls 64 Byte ohne Report-ID; HidSharp dagegen 65 Byte mit führendem `0x00`.

{{< callout type="info" >}}
Eine Library muss Input-Reports **nicht** lesen. Der Kernel puffert pro geöffnetem hidraw-Dateideskriptor
bis zu 64 Reports und verwirft danach die ältesten – ein Überlauf schadet also nicht.
{{< /callout >}}
