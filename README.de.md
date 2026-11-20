# SegLcd – Segment-LCD der Thermaltake LB360-S ARGB Sync unter Linux

[English](README.md) · **Deutsch**

> Hinweis: Die englische Fassung ist maßgeblich; diese Übersetzung kann hinter ihr zurückliegen.

> SegLcd ist ein unabhängiges Projekt und nicht mit Thermaltake verbunden. „Thermaltake“ ist eine Marke der Thermaltake Technology Co., Ltd.; der Name wird nur verwendet, um die unterstützte Hardware zu bezeichnen.

Protokoll-Dokumentation, Library und Werkzeuge, um das Segment-LCD auf dem Pumpenblock der
**Thermaltake LB360-S ARGB Sync** (USB `5131:200e`) unter Linux anzusteuern – vom Reverse Engineering des
USB-HID-Protokolls bis zum systemd-Dienst, der CPU- und GPU-Temperatur live aufs Display bringt.

📖 **Dokumentation:** https://fubar-coder.github.io/SegLcd/de/ ([English](https://fubar-coder.github.io/SegLcd/))

- `seglcd` – CLI zum Testen, Senden und Analysieren (`list`, `send`, `live`, `probe`, `decode`, …)
- `seglcdd` – systemd-Benutzerdienst mit D-Bus-Schnittstelle und TOML-Konfiguration
- Sensoren für AMD, Intel und NVIDIA (hwmon, RAPL, NVML, DRM), Native AOT, Deutsch/Englisch

| Pfad | Inhalt |
|---|---|
| `docs/` | Hugo/Hextra-Dokumentation (`cd docs && hugo server`) |
| `udev/70-seglcd.rules` | Zugriff ohne Root + USB-Passthrough unter `qemu:///session` |
| `udev/72-rapl-energy-read.rules` | optional: CPU-Paketleistung (RAPL) für Gruppe `rapl` |
| `vm/lb360s-usb-hostdev.xml` | libvirt-Hostdev für die Windows-VM |
| `tools/` | usbmon-Mitschnitt und Paket-Extraktion |
| `src/SegLcd.Core` | Library (.NET 11, AOT, HidApi.Net → native hidapi) |
| `src/SegLcd.Linux` | Linux-Sensoren (hwmon, procfs, cpufreq, RAPL, NVML, DRM) |
| `src/SegLcd.Localization` | gettext-Lokalisierung (`po/`: Englisch → Deutsch) |
| `src/SegLcd.Tool` | CLI `seglcd` (DotMake.CommandLine, Native AOT) |
| `src/SegLcd.Daemon` | Dienst `seglcdd` (systemd --user, D-Bus, TOML-Konfiguration) |
| `man/` | Man-Pages `seglcd(1)`, `seglcdd(1)`, `seglcdd.toml(5)` – Englisch und Deutsch (`man/de/`) |
| `packaging/` | Pakete (AUR, RPM, DEB), System-/Benutzer-Units, D-Bus-Policy, Beispielkonfiguration, `install-user.sh` / `uninstall-user.sh` |
| `tests/SegLcd.Tests` | TUnit-Tests mit den Testvektoren der Doku |

```bash
git submodule update --init
sudo pacman -S hidapi                # native Abhängigkeit (libhidapi-hidraw.so.0)
sudo install -m 644 udev/70-seglcd.rules /etc/udev/rules.d/
sudo udevadm control --reload
sudo udevadm trigger --action=change --subsystem-match=hidraw --subsystem-match=usb

dotnet test
dotnet publish src/SegLcd.Tool -c Release -r linux-x64
# -> artifacts/publish/SegLcd.Tool/release_linux-x64/seglcd
artifacts/publish/SegLcd.Tool/release_linux-x64/seglcd list
artifacts/publish/SegLcd.Tool/release_linux-x64/seglcd send --cpu-temp 42 --gpu-temp 57 --duration 20
```

Dienst installieren:

- **systemweit als Paket** (Systemdienst ab dem Booten, Benutzer `seglcd`, System-Bus): Arch (AUR), Fedora/openSUSE
  (RPM), Debian/Ubuntu (DEB) – siehe [packaging/README.md](packaging/README.md); der Dienst startet automatisch, sobald das Display vorhanden ist;
- **pro Benutzer ohne root** (systemd-Benutzerdienst, Session-Bus):

```bash
packaging/install-user.sh          # --help, --reset-config, --no-start
journalctl --user -u seglcdd -f
packaging/uninstall-user.sh        # --purge entfernt auch ~/.config/seglcd
```

## Lizenz

GPL-3.0-or-later – siehe [LICENSE](LICENSE).
