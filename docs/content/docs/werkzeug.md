---
title: Test-Werkzeug seglcd
weight: 6
---

`seglcd` (Projekt `src/SegLcd.Tool`, .NET 11, DotMake.CommandLine, Native AOT) ist das Werkzeug
zum Ausprobieren, Verifizieren und Analysieren des Protokolls. Für den Dauerbetrieb gibt es den Dienst
[`seglcdd`](../dienst/); beide nutzen dieselbe Sensor-Library ([Sensoren & Konfiguration](../sensoren/)).

## Installation

Laufzeit-Voraussetzung: native **hidapi** (`sudo pacman -S hidapi`) und die [udev-Regel](../linux/udev/).

```fish
dotnet publish src/SegLcd.Tool -c Release -r linux-x64
install -Dm755 artifacts/publish/SegLcd.Tool/release_linux-x64/seglcd ~/.local/bin/seglcd
```

Die Man-Page `seglcd(1)` (Englisch und Deutsch, Quelle `man/`) installiert `packaging/install-user.sh` mit;
ansehen ohne Installation: `man -l man/de/man1/seglcd.1`.

Ohne Installation: `dotnet run --project src/SegLcd.Tool -- <befehl> …` oder direkt
`artifacts/bin/SegLcd.Tool/debug/seglcd` nach `dotnet build`. Hilfe: `seglcd --help`,
`seglcd <befehl> --help`.

{{< callout type="warning" >}}
Läuft der Dienst `seglcdd`, vor sendenden Befehlen (`send`, `live`, `probe`, `sweep`, `raw`, `off`, `hold`) dessen
`Enabled`-Property auf `false` setzen oder ihn stoppen – sonst senden beide abwechselnd
([Details](../dienst/#zusammenspiel-mit-seglcd)).
{{< /callout >}}

## Übersicht

| Befehl | Zweck | sendet |
|---|---|---|
| [`list`](#list) | Displays bzw. alle HID-Geräte mit Deskriptor und Öffnen-Test | nein |
| [`send`](#send) | feste Werte senden | ja |
| [`live`](#live) | echte Sensorwerte senden | ja |
| [`probe`](#probe) | Byte für Byte testen, welches Feld angezeigt wird | ja |
| [`sweep`](#sweep) | ein Byte hochzählen | ja |
| [`raw`](#raw) | beliebiges Paket senden | ja |
| [`off`](#off) | Aus-Paket `0F` senden | ja |
| [`hold`](#hold) | Timeout-Test: senden, aufhören, Gerät offen halten | ja |
| [`listen`](#listen) | Input-Reports mitlesen | optional |
| [`sensors`](#sensors) | verfügbare Sensoren mit aktuellen Werten | nein |
| [`decode`](#decode) | Hex-Pakete (z. B. aus Mitschnitten) dekodieren | nein |

### Gemeinsame Optionen der Gerätebefehle

| Option | Bedeutung |
|---|---|
| `--device <pfad>` | bestimmtes Gerät, z. B. `/dev/hidraw5`; Standard: erstes bekanntes Segment-LCD |
| `--dry-run` | nichts senden, nur das (erste) Paket als Hex (64 Byte) ausgeben |
| `--verbose` | jedes gesendete Paket mit Zeitstempel ausgeben |

Alle sendenden Befehle laufen bis zum Ende von `--duration` bzw. bis Strg+C und senden periodisch (Keep-Alive).
Ist `--interval` größer als 400 ms, warnt das Tool: das Display schaltet nach ca. 400–500 ms ohne Paket ab.

Rückgabewerte: `0` Erfolg, `1` nichts gefunden / ungültige Eingabe, `2` Gerät nicht gefunden oder nicht zu öffnen.

## Befehle

### list

```fish
seglcd list          # nur bekannte Segment-LCDs (5131:200e, 5131:2007)
seglcd list --all    # alle HID-Geräte
```

Zeigt Pfad, VID:PID mit Modell, Bus/Interface, Usage-Page, Strings, ob das Gerät geöffnet werden kann (Rechte!)
und den Report-Deskriptor. Erster Schritt bei Problemen.

### send

Feste Werte senden; alle Felder sind optional (fehlende = 0):

| Option | Feld | Option | Feld |
|---|---|---|---|
| `--cpu-temp` | CPU-Temperatur °C | `--gpu-temp` | GPU-Temperatur °C |
| `--cpu-usage` | CPU-Auslastung % | `--gpu-usage` | GPU-Auslastung % |
| `--cpu-power` | CPU-Leistung W | `--gpu-power` | GPU-Leistung W |
| `--cpu-clock` | CPU-Takt MHz | `--gpu-clock` | GPU-Takt MHz |
| `--cpu-voltage` | CPU-Spannung V | `--fan`, `--pump` | Drehzahlen RPM |
| `--ram` | RAM-Auslastung % | `--time` | Uhrzeit (ISO 8601), Standard: laufende Systemzeit |
| `--fahrenheit` | Temperaturen in °F – **nicht für die LB360-S** (Werte ≥ 100 erscheinen zweistellig, z. B. 122 → „22“) | `--off-at-end` | beim Beenden `0F` senden |
| `--duration` | Laufzeit in s | `--interval` | Sendeintervall in ms (250) |

```fish
seglcd send --cpu-temp 42 --gpu-temp 57 --duration 20
seglcd send --cpu-temp 42.5 --fan 1234 --dry-run | seglcd decode
```

### live

Echte Sensorwerte. Quellen, Standardwerte und Spec-Syntax: [Sensoren & Konfiguration](../sensoren/).

| Option | Standard |
|---|---|
| `--cpu-temp-sensor` | `k10temp:Tctl`, `zenpower:Tdie`, `zenpower:Tctl`, `coretemp:Package id 0` |
| `--cpu-power-sensor` | `rapl`, sonst `zenpower:SVI2_P_Core+zenpower:SVI2_P_SoC` |
| `--cpu-voltage-sensor` | `zenpower:SVI2_Core` |
| `--cpu-clock-mode Avg\|Max\|Min\|First` | `Avg` |
| `--gpu-temp-sensor` | `amdgpu:edge`, `nvml:temperature`, `xe:pkg` |
| `--gpu-usage-sensor` | `drm:busy`, `nvml:usage` |
| `--gpu-power-sensor` | `amdgpu:PPT`, `nvml:power`, `rapl:uncore` |
| `--gpu-clock-sensor` | `amdgpu:sclk`, `nvml:clock`, `drm:freq` |
| `--fan-sensor` / `--pump-sensor` | keiner |
| `--sample-interval` | 1000 ms (Sensoren einlesen) |
| `--interval` | 250 ms (senden) |
| `--show` | Werte fortlaufend ausgeben |
| `--fahrenheit`, `--duration` | wie bei `send` |

```fish
seglcd live --show --fan-sensor it8686_31050607:fan1 --pump-sensor it8686_31050607:fan5
```

Beim Start gibt `live` für jedes Feld die gewählte Quelle aus; beim Beenden (Strg+C) sendet es `0F`.
Trennen und Wiederverbinden des Geräts (z. B. per VM-Umleitung) wird erkannt.

### probe

Setzt nacheinander jeweils **ein** Byte auf einen auffälligen Wert, alle anderen bleiben 0 – zum Herausfinden,
welche Felder ein Display anzeigt.

| Option | Bedeutung |
|---|---|
| `--value` | geschriebener Wert (88) |
| `--values 0,1,50,99,128,255` | statt eines Wertes eine **Wertefolge** pro Byte durchlaufen |
| `--value-seconds` | Sekunden pro Wert der Folge (1) |
| `--offsets 3,13,36-63` | nur diese Offsets bzw. Bereiche (Standard 3–63) |
| `--auto <s>` | automatisch weiterschalten statt auf Enter zu warten |
| `--no-header` | Byte 1/2 nicht auf `01 02` setzen |

Interaktiv: Enter = nächstes Byte, `b` = zurück, Zahl = Wert ändern, `o<offset>` = springen, `q` = Ende.
Mit `--values` wiederholt sich die Folge, bis Enter gedrückt wird; mit `--auto` wird sie je Byte einmal
durchlaufen. Grenzwerte wie 0, 1, 99, 128 und 255 decken Flags, Zweistelligkeit und Vorzeichen-Effekte auf:

```fish
seglcd probe --values 0,1,50,99,128,255 --auto 0 --offsets 3-63
```

### sweep

Zählt ein Byte von `--from` bis `--to` (100) in Schritten von `--step` (1) alle `--step-ms` (500) hoch;
`--set 21=12,22=34` setzt weitere feste Bytes. Für Balkenanzeigen und Skalen.

```fish
seglcd sweep 6 --to 100 --step 5 --step-ms 700
```

### raw

Sendet bis zu 64 Byte **ohne** Report-ID (der Rest wird mit `00` aufgefüllt), periodisch oder mit `--once` einmal.

```fish
seglcd raw 00 01 02 2a --duration 5
seglcd raw --once 00 0f
```

### off

Sendet einmal das Aus-Paket `00 0F 00 …`.

### hold

Sendet `--send` Sekunden (5) lang Update-Pakete mit `--cpu-temp`/`--gpu-temp` (42/57), hört dann auf und hält das
Gerät noch `--hold` Sekunden (10) offen. Damit ließ sich zeigen, dass das Display wegen des **Timeouts** ausgeht,
nicht wegen des Schließens.

### listen

Liest Input-Reports (64 Byte) und gibt sie mit Zeitstempel aus; `--keep-alive` sendet parallel Nullwerte,
`--duration` begrenzt die Laufzeit. Die LB360-S sendet keine Input-Reports.

### sensors

Listet alle verfügbaren Quellen gruppiert nach Art – hwmon, RAPL-Domänen, NVIDIA (NVML), DRM sowie CPU-Takt und
RAM – jeweils mit der Spec-Angabe, die man direkt in Optionen oder `config.toml` verwenden kann, und dem aktuellen
Wert. Grundlage für die Konfiguration ([Beispielausgabe](../sensoren/#verfügbare-sensoren-finden)).

### decode

Dekodiert Hex-Pakete Feld für Feld – eine Zeile pro Paket, optional mit vorangestellter Zeit und Tabulator
(Ausgabeformat von `tools/extract-packets.sh`). Liest aus einer Datei oder von stdin.

| Option | Bedeutung |
|---|---|
| `--diff` | nur Bytes zeigen, die sich gegenüber dem vorigen Paket geändert haben |
| `--strip-report-id` | bei 65-Byte-Zeilen das führende Report-ID-Byte entfernen |

```fish
tools/extract-packets.sh captures/x.pcapng 3 | seglcd decode --diff
```

Jedes Update-Paket endet mit einer Zusammenfassung, z. B. `=> CPU 42.50°C 37.25% … | RAM 27.98%`.
