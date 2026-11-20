---
title: Sensors & configuration
weight: 8
---

`seglcd live` and `seglcdd` read the sensors with the same code (`src/SegLcd.Linux`). The LB360-S only **shows CPU and
GPU temperature** (integer °C, max. 99); the other values are sent along like the original software does.

Sensors are read every `sample_interval_ms` (1000); the last values are sent every `update_interval_ms` (250, max. 400 –
otherwise the display turns off).

## Sources

| Field | Source | Default (first match) |
|---|---|---|
| CPU temperature | hwmon | `k10temp:Tctl`, `zenpower:Tdie`, `zenpower:Tctl`, `coretemp:Package id 0` |
| CPU usage | `/proc/stat` | fixed |
| CPU power | RAPL or hwmon | `rapl`, `zenpower:SVI2_P_Core+zenpower:SVI2_P_SoC` |
| CPU clock | cpufreq `scaling_cur_freq` of all cores | average (`cpu_clock_mode`) |
| CPU voltage | hwmon | `zenpower:SVI2_Core` |
| GPU temperature | hwmon or NVML | `amdgpu:edge`, `nvml:temperature`, `xe:pkg` |
| GPU usage | DRM or NVML | `drm:busy`, `nvml:usage` |
| GPU power | hwmon, NVML or RAPL | `amdgpu:PPT`, `nvml:power`, `rapl:uncore` |
| GPU clock | hwmon, NVML or DRM | `amdgpu:sclk`, `nvml:clock`, `drm:freq` |
| fan, pump | hwmon | **none** – depends on the mainboard header |
| RAM usage | `/proc/meminfo` | fixed |

Missing or unreadable values are sent as 0.

Notes:

- **CPU power:** RAPL is the exact package power (as shown by btop), but readable by root only
  ([PLATYPUS](https://platypusattack.com/)). The optional [udev rule](../linux/udev/#optional-rapl-for-cpu-power) opens
  it for the group `rapl`. Without RAPL, zenpower's Core + SoC is an approximation; k10temp has no power sensor.
- **Intel:** `coretemp` for the temperature, RAPL for the power; integrated GPUs: clock via `drm:freq`, power via
  `rapl:uncore`, no temperature.
- **NVIDIA:** via NVML (`libnvidia-ml.so.1`, loaded at runtime); fan only in percent.
- **Hybrid graphics:** configure the GPU explicitly (`nvml1:…`, `drm0:freq`).

{{< callout type="warning" >}}
NVIDIA and Intel support is implemented from the driver documentation but **not tested on real hardware** yet.
{{< /callout >}}

## Sensor specs

The same syntax works for CLI options, `config.toml` and D-Bus `SetSensor`:

| Spec | Example | Meaning |
|---|---|---|
| `chip:label` | `k10temp:Tctl`, `it8686_31050607:fan5` | hwmon sensor – **stable, prefer this** |
| `hwmonN/name` | `hwmon5/fan1` | hwmon path (the number may change on reboot) |
| `a+b` | `zenpower:SVI2_P_Core+zenpower:SVI2_P_SoC` | sum |
| `rapl[:domain]` | `rapl`, `rapl:uncore` | RAPL power: `package` (default), `core`, `uncore`, `dram`, `psys` |
| `nvml[N]:metric` | `nvml:temperature` | NVIDIA GPU N: `temperature`, `power`, `clock`, `memclock`, `usage`, `fan` |
| `drm[N]:metric` | `drm:busy` | DRM card N: `busy`, `freq` |
| empty | `""` | automatic (defaults above) |

`seglcd sensors` lists every available source with its spec and current value; "(permissions)" marks an unreadable
RAPL counter.

**Fan and pump:** the mainboard manual or BIOS names the headers (`CPU_FAN`, `AIO_PUMP`, …). The LB360-S pump runs
constantly at about 3000 RPM; radiator fans follow the CPU load.

## Configuration

| Field | `seglcd live` | `config.toml` / D-Bus `SetSensor` |
|---|---|---|
| CPU temperature / power / voltage | `--cpu-temp-sensor`, `--cpu-power-sensor`, `--cpu-voltage-sensor` | `cpu_temperature`, `cpu_power`, `cpu_voltage` |
| GPU temperature / usage / power / clock | `--gpu-temp-sensor`, `--gpu-usage-sensor`, `--gpu-power-sensor`, `--gpu-clock-sensor` | `gpu_temperature`, `gpu_usage`, `gpu_power`, `gpu_clock` |
| fan / pump | `--fan-sensor`, `--pump-sensor` | `fan`, `pump` |
| CPU clock mode | `--cpu-clock-mode` | `cpu_clock_mode` (TOML only) |

Recommended: find the specs with `seglcd sensors`, try them with `seglcd live --show --fan-sensor … --pump-sensor …`,
then put them into `config.toml` and run `systemctl --user reload seglcdd`.

### config.toml

User service: `~/.config/seglcd/config.toml`, system service: `/etc/seglcd/config.toml` (also `seglcdd --config`).
A missing file or key means the default; invalid values are logged and replaced by defaults. Complete default file:

{{< include-file path="packaging/config.example.toml" lang="toml" >}}
