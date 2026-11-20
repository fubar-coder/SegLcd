---
title: Sensoren & Konfiguration
weight: 9
---

`seglcd live` und `seglcdd` lesen die Sensoren mit demselben Code (`src/SegLcd.Linux`). Die LB360-S **zeigt nur CPU-
und GPU-Temperatur** (ganzzahlig in °C, max. 99); die übrigen Werte werden wie bei der Original-Software mitgesendet.

Die Sensoren werden alle `sample_interval_ms` (1000) gelesen; gesendet werden die letzten Werte alle
`update_interval_ms` (250, max. 400 – sonst schaltet das Display ab).

## Quellen

| Feld | Quelle | Standard (erster Treffer) |
|---|---|---|
| CPU-Temperatur | hwmon | `k10temp:Tctl`, `zenpower:Tdie`, `zenpower:Tctl`, `coretemp:Package id 0` |
| CPU-Auslastung | `/proc/stat` | fest |
| CPU-Leistung | RAPL oder hwmon | `rapl`, `zenpower:SVI2_P_Core+zenpower:SVI2_P_SoC` |
| CPU-Takt | cpufreq `scaling_cur_freq` aller Kerne | Durchschnitt (`cpu_clock_mode`) |
| CPU-Spannung | hwmon | `zenpower:SVI2_Core` |
| GPU-Temperatur | hwmon oder NVML | `amdgpu:edge`, `nvml:temperature`, `xe:pkg` |
| GPU-Auslastung | DRM oder NVML | `drm:busy`, `nvml:usage` |
| GPU-Leistung | hwmon, NVML oder RAPL | `amdgpu:PPT`, `nvml:power`, `rapl:uncore` |
| GPU-Takt | hwmon, NVML oder DRM | `amdgpu:sclk`, `nvml:clock`, `drm:freq` |
| Lüfter, Pumpe | hwmon | **keiner** – hängt vom Mainboard-Anschluss ab |
| RAM-Auslastung | `/proc/meminfo` | fest |

Fehlende oder nicht lesbare Werte werden als 0 gesendet.

Hinweise:

- **CPU-Leistung:** RAPL ist die genaue Paketleistung (wie in btop), aber nur für root lesbar
  ([PLATYPUS](https://platypusattack.com/)). Die optionale [udev-Regel](../linux/udev/#optional-rapl-für-die-cpu-leistung)
  öffnet sie für die Gruppe `rapl`. Ohne RAPL ist zenpowers Core + SoC eine Näherung; k10temp hat keinen Leistungssensor.
- **Intel:** `coretemp` für die Temperatur, RAPL für die Leistung; integrierte GPUs: Takt über `drm:freq`, Leistung
  über `rapl:uncore`, keine Temperatur.
- **NVIDIA:** über NVML (`libnvidia-ml.so.1`, zur Laufzeit geladen); Lüfter nur in Prozent.
- **Hybrid-Grafik:** die GPU explizit konfigurieren (`nvml1:…`, `drm0:freq`).

{{< callout type="warning" >}}
NVIDIA- und Intel-Unterstützung sind nach der Treiberdokumentation umgesetzt, aber **noch nicht auf echter Hardware
getestet**.
{{< /callout >}}

## Sensor-Angaben

Dieselbe Syntax gilt für CLI-Optionen, `config.toml` und D-Bus `SetSensor`:

| Angabe | Beispiel | Bedeutung |
|---|---|---|
| `chip:label` | `k10temp:Tctl`, `it8686_31050607:fan5` | hwmon-Sensor – **stabil, bevorzugen** |
| `hwmonN/name` | `hwmon5/fan1` | hwmon-Pfad (die Nummer kann sich beim Neustart ändern) |
| `a+b` | `zenpower:SVI2_P_Core+zenpower:SVI2_P_SoC` | Summe |
| `rapl[:domäne]` | `rapl`, `rapl:uncore` | RAPL-Leistung: `package` (Standard), `core`, `uncore`, `dram`, `psys` |
| `nvml[N]:wert` | `nvml:temperature` | NVIDIA-GPU N: `temperature`, `power`, `clock`, `memclock`, `usage`, `fan` |
| `drm[N]:wert` | `drm:busy` | DRM-Karte N: `busy`, `freq` |
| leer | `""` | automatisch (Standards oben) |

`seglcd sensors` listet alle verfügbaren Quellen mit Angabe und aktuellem Wert; „(Rechte)“ kennzeichnet einen nicht
lesbaren RAPL-Zähler.

**Lüfter und Pumpe:** Handbuch oder BIOS des Mainboards benennen die Anschlüsse (`CPU_FAN`, `AIO_PUMP`, …). Die Pumpe
der LB360-S läuft konstant mit etwa 3000 RPM; die Radiatorlüfter folgen der CPU-Last.

## Konfiguration

| Feld | `seglcd live` | `config.toml` / D-Bus `SetSensor` |
|---|---|---|
| CPU-Temperatur / -Leistung / -Spannung | `--cpu-temp-sensor`, `--cpu-power-sensor`, `--cpu-voltage-sensor` | `cpu_temperature`, `cpu_power`, `cpu_voltage` |
| GPU-Temperatur / -Auslastung / -Leistung / -Takt | `--gpu-temp-sensor`, `--gpu-usage-sensor`, `--gpu-power-sensor`, `--gpu-clock-sensor` | `gpu_temperature`, `gpu_usage`, `gpu_power`, `gpu_clock` |
| Lüfter / Pumpe | `--fan-sensor`, `--pump-sensor` | `fan`, `pump` |
| CPU-Takt-Modus | `--cpu-clock-mode` | `cpu_clock_mode` (nur TOML) |

Empfohlen: Angaben mit `seglcd sensors` ermitteln, mit `seglcd live --show --fan-sensor … --pump-sensor …` testen,
dann in `config.toml` eintragen und `systemctl --user reload seglcdd` ausführen.

### config.toml

Benutzerdienst: `~/.config/seglcd/config.toml`, Systemdienst: `/etc/seglcd/config.toml` (auch `seglcdd --config`);
über D-Bus gespeicherte Werte liegen in `/var/lib/seglcd/config.toml` und haben Vorrang – löschen, um nur `/etc` zu verwenden.
Eine fehlende Datei oder ein fehlender Schlüssel bedeutet den Standardwert; ungültige Werte werden protokolliert und
durch Standardwerte ersetzt. Vollständige Standarddatei:

{{< include-file path="packaging/config.example.toml" lang="toml" >}}
