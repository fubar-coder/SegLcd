---
title: Settings app seglcd-gtk
weight: 8
---

`seglcd-gtk` is a GTK 4 / libadwaita app for [`seglcdd`](../service/): it shows the display state and the current
values and sets the intervals and the sensor of each value. It talks to the service only over D-Bus.

| Page | Contents |
|---|---|
| Status | device, *Feed display* (`Enabled`), all values (the LB360-S shows CPU and GPU temperature), intervals, service and configuration file |
| Sensors | per value: *Automatic* or a sensor of the matching kind (from `ListSensors`, with current value), below it the sensor in use; how the CPU clock is derived |

Every change applies and is saved at once (`SaveConfig`; system service: `/var/lib/seglcd/config.toml`). The menu
reloads the configuration file, e.g. after editing it by hand.

## Installation

- **Packages:** `seglcd-gtk` (AUR, RPM subpackage, DEB) – needs GTK 4 and libadwaita ≥ 1.6.
- **Per user:** `packaging/install-user.sh` installs it with the user service (`--no-gui` skips it).

## Connection

The app uses the system service if it runs (or can be started via D-Bus), otherwise the user service, and reconnects
when the service restarts. `--system` or `--session` restricts it to one bus.

Changing the system service is allowed for the active local user; otherwise (e.g. over SSH) polkit asks for
administrator authentication.
