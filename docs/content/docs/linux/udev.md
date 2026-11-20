---
title: udev-Regel
weight: 1
---

Standardmäßig gehört `/dev/hidrawN` `root:root` mit Modus `0600` – ein normaler Benutzer kann das
Display nicht ansprechen. Die Regel `udev/70-seglcd.rules` gibt dem **lokal
angemeldeten Benutzer** (systemd-logind, `uaccess`) Zugriff auf

1. den **hidraw-Knoten** – für das Test-Werkzeug und jede Library, und
2. den **rohen USB-Knoten** `/dev/bus/usb/BBB/DDD` – für USB-Passthrough in eine VM unter
   `qemu:///session` (siehe [VM-Passthrough](../../reverse-engineering/vm-passthrough/)).

```udev {filename="udev/70-seglcd.rules"}
# --- LB360-S ARGB Sync (5131:200e) ---
SUBSYSTEM=="hidraw", ATTRS{idVendor}=="5131", ATTRS{idProduct}=="200e", MODE="0660", TAG+="uaccess"
SUBSYSTEM=="usb", ENV{DEVTYPE}=="usb_device", ATTR{idVendor}=="5131", ATTR{idProduct}=="200e", MODE="0660", TAG+="uaccess"

# --- LA-Serie (5131:2007) ---
SUBSYSTEM=="hidraw", ATTRS{idVendor}=="5131", ATTRS{idProduct}=="2007", MODE="0660", TAG+="uaccess"
SUBSYSTEM=="usb", ENV{DEVTYPE}=="usb_device", ATTR{idVendor}=="5131", ATTR{idProduct}=="2007", MODE="0660", TAG+="uaccess"
```

{{< callout type="warning" >}}
**Dateiname &lt; 73!** `TAG+="uaccess"` wird von `73-seat-late.rules` ausgewertet. Regeln mit höherer
Nummer (z. B. `99-…`) setzen das Tag zu spät – die ACL wird dann nicht vergeben.
{{< /callout >}}

## Installation

{{% steps %}}

### Regel kopieren

```bash
sudo install -m 644 udev/70-seglcd.rules /etc/udev/rules.d/
```

### udev neu laden und auf vorhandene Geräte anwenden

```bash
sudo udevadm control --reload
sudo udevadm trigger --action=change --subsystem-match=hidraw --subsystem-match=usb
```

Alternativ das USB-Kabel ab- und wieder anstecken bzw. neu starten.

### Prüfen

```bash
getfacl /dev/hidraw5                  # Nummer ggf. per 'seglcd list' ermitteln
# -> user:<dein-benutzer>:rw-

udevadm info -q property -n /dev/hidraw5 | grep TAGS
# -> CURRENT_TAGS=:seat:uaccess:

seglcd list
# -> Öffnen  OK
```

{{% /steps %}}

## Varianten

| Ziel | Regel |
|---|---|
| Nur angemeldeter Desktop-Benutzer (Standard) | `MODE="0660", TAG+="uaccess"` |
| Systemdienst unter eigenem Benutzer (z. B. `seglcd`) | `MODE="0660", GROUP="seglcd"` – zusätzlich oder statt `uaccess` |
| Jeder lokale Benutzer | `MODE="0666"` (nicht empfohlen) |

Ein Dienst, der als **root** läuft, braucht keine Regel.

## Optional: RAPL für die CPU-Leistung

Die genaue CPU-**Paketleistung** stammt aus dem RAPL-Energiezähler `/sys/class/powercap/intel-rapl:*/energy_uj`
(auch auf AMD). Er ist seit Linux 5.10 nur für root lesbar, weil feinaufgelöste Energiewerte Rückschlüsse auf
laufende Berechnungen erlauben ([PLATYPUS, CVE-2020-8694/8695](https://platypusattack.com/)).
`udev/72-rapl-energy-read.rules` macht den Zähler **nur für die Gruppe `rapl`** lesbar – deutlich enger als die
Capability `cap_dac_read_search`, mit der z. B. btop alle Dateien lesen darf.

```udev {filename="udev/72-rapl-energy-read.rules"}
SUBSYSTEM=="powercap", KERNEL=="intel-rapl:*", ACTION=="add|change", RUN+="/usr/bin/chgrp rapl /sys%p/energy_uj", RUN+="/usr/bin/chmod 0440 /sys%p/energy_uj"
```

```fish
sudo install -m 644 udev/rapl.sysusers.conf /etc/sysusers.d/rapl.conf
sudo systemd-sysusers                       # legt die Gruppe "rapl" an
sudo usermod -aG rapl $USER                 # danach ab- und wieder anmelden
sudo install -m 644 udev/72-rapl-energy-read.rules /etc/udev/rules.d/
sudo udevadm control --reload
sudo udevadm trigger --action=add --subsystem-match=powercap
ls -l /sys/class/powercap/intel-rapl:0/energy_uj   # -r--r----- root rapl
```

Danach nutzen `seglcd live` und `seglcdd` automatisch `rapl` als CPU-Leistungsquelle – auch mit k10temp, das selbst
keine Leistung liefert. Ohne die Regel bleibt die Leistung mit k10temp leer, mit zenpower wird Core + SoC genähert.

## Deinstallation

```bash
sudo rm /etc/udev/rules.d/70-seglcd.rules
sudo udevadm control --reload
sudo udevadm trigger --action=change --subsystem-match=hidraw --subsystem-match=usb
```
