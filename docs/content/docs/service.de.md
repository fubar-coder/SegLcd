---
title: Dienst seglcdd
weight: 7
---

`seglcdd` versorgt das Display mit Sensorwerten und bietet eine D-Bus-Schnittstelle. Nur **eine** der beiden Varianten
verwenden – zwei Instanzen würden abwechselnd senden.

| | Systemdienst (RPM/DEB/AUR) | Benutzerdienst (`install-user.sh`) |
|---|---|---|
| Start | automatisch, sobald das Display vorhanden ist (udev); verhindern mit `systemctl mask seglcdd` | beim Login: `systemctl --user enable --now seglcdd` |
| Läuft als | Benutzer `seglcd` (`seglcdd --system`) | angemeldeter Benutzer |
| Konfiguration | `/etc/seglcd/config.toml` | `~/.config/seglcd/config.toml` |
| D-Bus | System-Bus – lesen: alle, steuern: root + Gruppe `seglcd` | Session-Bus |
| Log | `journalctl -u seglcdd` | `journalctl --user -u seglcdd` |

Konfiguration: [Sensoren & Konfiguration](../sensors/) und `seglcdd.toml(5)`. Referenz: `seglcdd(1)`.

## Installation

**Pakete:** siehe [`packaging/README.md`](https://github.com/fubar-coder/SegLcd/blob/main/packaging/README.md).
Zum Steuern des Dienstes: `sudo usermod -aG seglcd $USER` (danach neu anmelden).

**Pro Benutzer** ohne root (braucht das .NET-SDK, `hidapi` und die [udev-Regel](../linux/udev/)):

```fish
packaging/install-user.sh      # --reset-config ersetzt config.toml (Sicherung .bak), --no-start, --help
packaging/uninstall-user.sh    # --purge entfernt auch ~/.config/seglcd
```

## Betrieb

```fish
systemctl --user reload seglcdd      # Konfiguration neu laden, ohne dass das Display ausgeht
journalctl --user -u seglcdd -f      # Systemdienst: ohne --user
```

| Situation | Verhalten |
|---|---|
| Display fehlt oder ist an eine VM durchgereicht | sucht jede Sekunde neu, verbindet sich selbst |
| Dienst gestoppt | sendet das Aus-Paket, gibt das Gerät frei |
| Konfiguration fehlerhaft | Fehler im Journal, Standardwerte |
| kein D-Bus | Warnung, das Display läuft weiter |

**Vor Experimenten mit `seglcd`** (`send`, `probe`, `raw`, …) den Dienst pausieren; das sendet das Aus-Paket und gibt
das Gerät frei:

```fish
set N io.github.fubar_coder.SegLcd1; set P /io/github/fubar_coder/SegLcd1
busctl --user set-property $N $P $N Enabled b false     # … danach b true
```

## D-Bus-Schnittstelle

Name und Interface `io.github.fubar_coder.SegLcd1`, Objekt `/io/github/fubar_coder/SegLcd1`, definiert in
`src/SegLcd.Daemon/DBus/io.github.fubar_coder.SegLcd1.xml`. Beim Systemdienst `busctl --system` verwenden.

| Property | Typ | Zugriff | Bedeutung |
|---|---|---|---|
| `Enabled` | `b` | rw | Display versorgen; `false` = Aus-Paket, Gerät freigegeben |
| `UpdateIntervalMs` | `u` | rw | 50–400 ms |
| `SampleIntervalMs` | `u` | rw | 100–10000 ms |
| `Connected`, `DevicePath`, `Model`, `ConfigPath` | `b`, `s` | r | Zustand und Pfade |
| `Sources` | `a{ss}` | r | Feld → aufgelöster Sensor (leer = keiner) |
| `CurrentValues` | `a{sd}` | r | letzte Werte (`cpu_temperature`, `gpu_temperature`, `fan`, …) |
| `LastPacket` | `ay` | r | letztes 64-Byte-Paket |

| Methode | Bedeutung |
|---|---|
| `SetSensor(field, spec) → source` | Sensor setzen ([Spec-Syntax](../sensors/#sensor-angaben)); erst mit `SaveConfig` gespeichert |
| `ReloadConfig()` | `config.toml` neu lesen; bei Fehlern bleibt die bisherige Einstellung aktiv |
| `SaveConfig()` | aktuelle Einstellungen schreiben (Kommentare in der Datei gehen verloren) |

`PropertiesChanged` wird für alle Properties außer `CurrentValues` und `LastPacket` gesendet. Ungültige Werte werden
mit `InvalidArgs` abgelehnt.

```fish
busctl --user get-property $N $P $N Connected CurrentValues
busctl --user set-property $N $P $N UpdateIntervalMs u 200
busctl --user call $N $P $N SetSensor ss pump it8686_31050607:fan5
busctl --user call $N $P $N SaveConfig
```
