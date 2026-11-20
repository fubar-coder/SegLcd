---
title: Update-Paket (01 02)
weight: 1
---

Das Update-Paket überträgt alle Werte, die ein Segment-LCD dieser Familie anzeigen kann.
Welche davon ein bestimmtes Modell tatsächlich darstellt, hängt von dessen Segment-Layout ab.

## Byte-Layout

Offsets beziehen sich auf das 64-Byte-Paket **ohne** Report-ID (siehe [Transport](../#transport)).

| Offset | Hex | Inhalt | Kodierung | Wertebereich |
|---:|---:|---|---|---|
| 0 | `0x00` | reserviert | immer `0x00` | – |
| 1 | `0x01` | **Kommando** | `0x01` | – |
| 2 | `0x02` | **Sub-Kommando** | `0x02` | – |
| 3 | `0x03` | CPU-Temperatur | ganzzahliger Teil | 0–255 |
| 4 | `0x04` | CPU-Temperatur | Nachkommastellen ×100 | 0–99 |
| 5 | `0x05` | CPU-Temperatur-Einheit | `0` = °C, `1` = °F ³ | 0–1 |
| 6 | `0x06` | CPU-Auslastung | Prozent, ganzzahliger Teil (Nachkomma: Offset 48) | 0–100 |
| 7 | `0x07` | CPU-Leistung | Watt **mod 100** (Zehner + Einer) | 0–99 |
| 8 | `0x08` | CPU-Leistung | Nachkommastellen ×100 | 0–99 |
| 9 | `0x09` | CPU-Takt | MHz **÷ 100** (ganzzahlig) | 0–99 |
| 10 | `0x0A` | CPU-Takt | MHz **mod 100** | 0–99 |
| 11 | `0x0B` | CPU-Spannung | ganzzahliger Teil (V) | 0–255 |
| 12 | `0x0C` | CPU-Spannung | Nachkommastellen ×100 ¹ | 0–99 |
| 13 | `0x0D` | GPU-Temperatur | ganzzahliger Teil | 0–255 |
| 14 | `0x0E` | GPU-Temperatur | Nachkommastellen ×100 | 0–99 |
| 15 | `0x0F` | GPU-Temperatur-Einheit | `0` = °C, `1` = °F ³ | 0–1 |
| 16 | `0x10` | GPU-Auslastung | Prozent, ganzzahlig | 0–100 |
| 17 | `0x11` | GPU-Leistung | Watt **mod 100** | 0–99 |
| 18 | `0x12` | GPU-Leistung | Nachkommastellen ×100 | 0–99 |
| 19 | `0x13` | GPU-Takt | MHz ÷ 100 | 0–99 |
| 20 | `0x14` | GPU-Takt | MHz mod 100 | 0–99 |
| 21 | `0x15` | Lüfterdrehzahl | RPM ÷ 100 | 0–99 |
| 22 | `0x16` | Lüfterdrehzahl | RPM mod 100 | 0–99 |
| 23 | `0x17` | Pumpendrehzahl | RPM ÷ 100 | 0–99 |
| 24 | `0x18` | Pumpendrehzahl | RPM mod 100 | 0–99 |
| 25 | `0x19` | Jahr | Jahrhundert (`20` für 2026) | 0–99 |
| 26 | `0x1A` | Jahr | Jahr mod 100 (`26` für 2026) | 0–99 |
| 27 | `0x1B` | Monat | 1–12 | 1–12 |
| 28 | `0x1C` | Tag | 1–31 | 1–31 |
| 29 | `0x1D` | Stunde | 0–23 | 0–23 |
| 30 | `0x1E` | Minute | 0–59 | 0–59 |
| 31 | `0x1F` | Sekunde | 0–59 | 0–59 |
| 32 | `0x20` | Wochentag | `0` = Sonntag … `6` = Samstag | 0–6 |
| 33 | `0x21` | RAM-Auslastung | Prozent, ganzzahliger Teil (Nachkomma: Offset 50) | 0–100 |
| 34 | `0x22` | CPU-Leistung | Watt **÷ 100** (Hunderter) | 0–99 |
| 35 | `0x23` | GPU-Leistung | Watt ÷ 100 | 0–99 |
| 36 | `0x24` | unbenutzt | `0x00` | – |
| 37 | `0x25` | **unbekannt** ² | Original-Software (LB360-S): immer `5` | ? |
| 38–47 | | unbenutzt | `0x00` | – |
| 48 | `0x30` | CPU-Auslastung ² | Nachkommastellen ×100 (bei 100 % immer `0`) | 0–99 |
| 49 | `0x31` | unbenutzt | `0x00` | – |
| 50 | `0x32` | RAM-Auslastung ² | Nachkommastellen ×100 | 0–99 |
| 51–63 | | unbenutzt | `0x00` | – |

¹ Die Original-Software (LA-Serie, Version 2025-03) sendet hier `floor(V) × 100` mod 256, also
für jede 1,x-V-Kernspannung den Wert `100` (`0x64`) – vermutlich ein Bug. Da kein bekanntes Display
die Spannung anzeigt, ist das folgenlos. Die Referenz-Implementierung sendet die echten zwei Nachkommastellen.

² Diese Bytes kennt die LA-Analyse nicht; sie stammen aus den [Mitschnitten der TT SEGMENT LCD Software](../verification/#mitschnitt-der-original-software-vm)
für die LB360-S. 48 und 50 sind eindeutig zugeordnet: Bei 100 % CPU-Auslastung ist Byte 48 in allen Paketen `0`,
und Byte 33 + Byte 50/100 ergibt einen lückenlosen RAM-Verlauf (z. B. 27,98 → 28,03). Byte 37 ist immer `5`,
Bedeutung unbekannt; die Referenz-Implementierung sendet dort `0` – das Display funktioniert damit.

³ Die LB360-S rechnet nicht um und zeigt immer das °C-Symbol; mit `1` werden Werte ≥ 100 nur zweistellig
(mod 100) dargestellt statt auf 99 begrenzt. Für dieses Modell **immer `0`** senden – Details in der
[Verifikation](../verification/#lb360-s-einheiten-flag).

### Kodierungsregeln

Alle Werte sind **einzelne vorzeichenlose Bytes**. Mehrstellige Größen werden in **Basis-100-Ziffern**
zerlegt (nicht binär!): 1234 RPM → `12, 34` → `0x0C 0x22`, *nicht* `0x04 0xD2`.

| Größe | Formel | Beispiel |
|---|---|---|
| Fixkomma (Temperatur, Spannung) | `c = round(v × 100)`; `whole = c div 100`; `frac = c mod 100` | 42,5 °C → `42, 50` |
| Basis 100 (Takt, Drehzahl) | `v = min(floor(v), 9999)`; `hi = v div 100`; `lo = v mod 100` | 4567 MHz → `45, 67` |
| Leistung (3 Bytes) | `c = round(v × 100)`; `W = c div 100`; `[34/35] = W div 100`; `[7/17] = W mod 100`; `[8/18] = c mod 100` | 123,45 W → `[34]=1, [7]=23, [8]=45` |
| Prozent (CPU, RAM) | Fixkomma wie Temperatur: `[6]/[48]` bzw. `[33]/[50]` | 37,25 % → `37, 25` |
| Prozent (GPU) | `min(floor(v), 255)`, keine Nachkommastellen bekannt | 37,8 % → `37` |
| Fahrenheit | Host rechnet um: `F = C × 9/5 + 32`, Einheiten-Byte `1` | 50 °C → `122`, Flag `1` |

Empfohlene Behandlung von Sonderfällen (so implementiert in der Referenz-Library):

| Eingabe | Verhalten |
|---|---|
| Wert unbekannt / nicht vorhanden | `0` senden |
| `NaN`, ±∞, negativ | `0` senden |
| Überlauf | auf das Maximum des Feldes begrenzen (Temperatur `255.99`, Basis-100 `9999`, Leistung `9999.99`) |
| Rundung bei Fixkomma | **erst** auf 1/100 runden, **dann** zerlegen – sonst wird 42,999 zu `42, 100` statt `43, 00` |

{{< callout type="info" >}}
Die LB360-S zeigt Temperaturen **zweistellig**: Werte ≥ 100 erscheinen als **99** (geprüft bis 255).
Ob andere Modelle Werte über 99 in den „÷ 100“-Bytes oder Nachkommastellen über 99 sinnvoll darstellen,
ist unbekannt – nur Werte im angegebenen Bereich senden.
{{< /callout >}}

### Uhrzeit

Die Felder 25–32 enthalten die **lokale** Uhrzeit des Hosts. Das LA-Display zeigt sie nicht an;
ob das runde LB360-S-Display eine Uhr hat, ist offen. Die Original-Software füllt die Felder immer,
eine Library sollte das ebenfalls tun (kostet nichts, und Geräte mit Uhr funktionieren dann).

## Testvektoren

Alle Vektoren mit fester Zeit **Freitag, 2026-11-20 13:45:30** (Bytes 25–32 = `14 1a 0b 14 0d 2d 1e 05`).
Erzeugt mit `seglcd send … --time 2026-11-20T13:45:30 --dry-run`, abgesichert durch die Unit-Tests.

### Vektor 1 – Beispiel aus der LA-Dokumentation

CPU 42,5 °C, 37 %, 123,45 W, 4567 MHz; Lüfter 1234 RPM; Pumpe 2345 RPM.

```text
00 01 02 2a 32 00 25 17 2d 2d 43 00 00 00 00 00 00 00 00 00 00 0c 22 17 2d 14 1a 0b 14 0d 2d 1e
05 00 01 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
```

### Vektor 2 – alle Felder belegt

CPU 65,37 °C, 12 %, 88,2 W, 4450 MHz, 1,2 V; GPU 47 °C, 3 %, 18,5 W, 1714 MHz;
Lüfter 1566 RPM; Pumpe 2947 RPM; RAM 41 %.

```text
00 01 02 41 25 00 0c 58 14 2c 32 01 14 2f 00 00 03 12 32 11 0e 0f 42 1d 2f 14 1a 0b 14 0d 2d 1e
05 29 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
```

### Vektor 3 – Fahrenheit

CPU 50 °C, GPU 100 °C, Einheit °F → 122 °F / 212 °F, Flags an Offset 5 und 15.

```text
00 01 02 7a 00 01 00 00 00 00 00 00 00 d4 00 01 00 00 00 00 00 00 00 00 00 14 1a 0b 14 0d 2d 1e
05 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
```

### Vektor 4 – Rundung und Begrenzung

CPU 42,999 °C (→ 43,00), CPU 1234,567 W (→ 12 | 34 | 57), Lüfter 20000 RPM (→ 9999).

```text
00 01 02 2b 00 00 00 22 39 00 00 00 00 00 00 00 00 00 00 00 00 63 63 00 00 14 1a 0b 14 0d 2d 1e
05 00 0c 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
```

### Vektor 5 – Paket der Original-Software

Aus dem zweiten Mitschnitt der Original-Software vom 2026-11-20 (t = 0,161 s): CPU-Auslastung 6,20 %, „Spannung“ 1,55 V,
RAM 27,64 %, Freitag 2026-11-20 03:14:53, Byte 37 = `5`. Die Referenz-Implementierung erzeugt es mit diesen Werten
byte-identisch (Byte 37 von Hand gesetzt) – siehe Unit-Test `Reproduces_packet_of_original_software_except_byte_37`.

```text
00 01 02 00 00 00 06 00 00 00 00 01 37 00 00 00 00 00 00 00 00 00 00 00 00 14 1a 0b 14 03 0e 35
05 1b 00 00 00 05 00 00 00 00 00 00 00 00 00 00 14 00 40 00 00 00 00 00 00 00 00 00 00 00 00 00
```

### Als hidraw-Write

Vektor 1 so, wie er an `write(fd, buf, 65)` übergeben wird – mit Report-ID `00` vorne:

```text
00 | 00 01 02 2a 32 00 25 17 2d 2d 43 00 00 … (insgesamt 65 Byte)
```

## Referenz-Pseudocode

```text
function encode_update(f, now) -> byte[64]:
    p = zeros(64)
    p[1] = 0x01; p[2] = 0x02
    fixed2(p, 3, 4,   to_unit(f.cpu_temp, f.unit), max_whole=255)
    p[5]  = f.unit                      # 0 = °C, 1 = °F
    fixed2(p, 6, 48,  f.cpu_usage, max_whole=255)
    power(p, 34, 7, 8, f.cpu_power)
    base100(p, 9, 10, f.cpu_clock)
    fixed2(p, 11, 12, f.cpu_voltage, max_whole=255)
    fixed2(p, 13, 14, to_unit(f.gpu_temp, f.unit), max_whole=255)
    p[15] = f.unit
    p[16] = int_clamp(f.gpu_usage, 255)
    power(p, 35, 17, 18, f.gpu_power)
    base100(p, 19, 20, f.gpu_clock)
    base100(p, 21, 22, f.fan_rpm)
    base100(p, 23, 24, f.pump_rpm)
    p[25] = now.year div 100;  p[26] = now.year mod 100
    p[27] = now.month; p[28] = now.day
    p[29] = now.hour;  p[30] = now.minute; p[31] = now.second
    p[32] = now.weekday                 # Sonntag = 0
    fixed2(p, 33, 50, f.ram_usage, max_whole=255)
    return p

sanitize(v)        = (v is finite and v > 0) ? v : 0
int_clamp(v, m)    = min(floor(sanitize(v)), m)
fixed2(p,a,b,v,m): c = min(round_half_away(sanitize(v)*100), m*100+99); p[a]=c div 100; p[b]=c mod 100
base100(p,a,b,v):  n = min(floor(sanitize(v)), 9999);                 p[a]=n div 100; p[b]=n mod 100
power(p,h,l,f,v):  c = min(round_half_away(sanitize(v)*100), 999999); w=c div 100
                   p[h]=w div 100; p[l]=w mod 100; p[f]=c mod 100
```
