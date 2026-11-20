---
title: Test-Werkzeug seglcd
weight: 6
---

`seglcd` sendet an das Display und analysiert es. Vollständige Optionen: `seglcd <befehl> --help` und `seglcd(1)`.
Voraussetzungen: `hidapi` und die [udev-Regel](../linux/udev/); installiert durch die Pakete oder
`packaging/install-user.sh`. Aus dem Quellbaum: `dotnet run --project src/SegLcd.Tool -- <befehl>`.

{{< callout type="warning" >}}
Einen laufenden [`seglcdd`](../service/#betrieb) vor sendenden Befehlen pausieren, sonst senden beide abwechselnd.
{{< /callout >}}

| Befehl | Zweck |
|---|---|
| `list [--all]` | bekannte Displays (oder alle HID-Geräte): IDs, Deskriptor, ob sich das Gerät öffnen lässt |
| `send` | feste Werte senden: `--cpu-temp`, `--gpu-temp`, `--fan`, `--pump`, `--ram`, … (fehlende = 0) |
| `live` | aktuelle Sensorwerte senden; Quellen wie unter [Sensoren & Konfiguration](../sensors/), `--show` gibt sie aus |
| `probe` | jeweils ein Byte setzen, um herauszufinden, was das Display anzeigt |
| `sweep <offset>` | ein Byte hochzählen (`--from`, `--to`, `--step`, `--step-ms`) |
| `raw <hex…>` | bis zu 64 Byte (ohne Report-ID) senden, periodisch oder `--once` |
| `off` | das Aus-Paket senden |
| `hold` | `--send` s senden, dann aufhören, das Gerät aber `--hold` s geöffnet halten (Timeout-Test) |
| `listen` | Input-Reports ausgeben (die LB360-S sendet keine) |
| `sensors` | alle Sensorquellen mit Angabe und aktuellem Wert auflisten |
| `decode [datei]` | Hex-Pakete dekodieren, z. B. aus `tools/extract-packets.sh`; `--diff` zeigt nur Änderungen |

Sendende Befehle wiederholen ihr Paket alle `--interval` ms (250; Warnung über 400), bis `--duration` abläuft oder
Strg+C gedrückt wird. Gemeinsame Optionen: `--device /dev/hidrawN`, `--dry-run` (nur das Paket ausgeben), `--verbose`.
Rückgabewerte: 0 Erfolg, 1 nichts gefunden / ungültige Eingabe, 2 Gerät fehlt oder ist nicht zugreifbar.

`--fahrenheit` eignet sich nicht für die LB360-S: Werte ≥ 100 erscheinen zweistellig (122 → „22“).

## Beispiele

```fish
seglcd list
seglcd send --cpu-temp 42 --gpu-temp 57 --duration 20
seglcd send --cpu-temp 42.5 --fan 1234 --dry-run | seglcd decode
seglcd live --show --fan-sensor it8686_31050607:fan1 --pump-sensor it8686_31050607:fan5
seglcd raw 00 01 02 2a --duration 5
```

## probe

Schreibt `--value` (88) – oder mit `--values 0,1,50,99,128,255` eine Folge mit je `--value-seconds` (1) – in ein Byte,
alle anderen bleiben 0. `--offsets 3,13,36-63` begrenzt die Bytes (Standard 3–63), `--auto <s>` schaltet automatisch
weiter. Tasten: Enter nächstes Byte, `b` zurück, eine Zahl ändert den Wert, `o<offset>` springt, `q` beendet.

```fish
seglcd probe --values 0,1,50,99,128,255 --auto 0 --offsets 3-63
```
