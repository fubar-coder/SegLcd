---
title: Dienst seglcdd
weight: 7
---

`seglcdd` (Projekt `src/SegLcd.Daemon`) versorgt das Display dauerhaft mit Sensorwerten. Es läuft als
**systemd-Benutzerdienst**, braucht kein root (die [udev-Regel](../linux/udev/) vergibt die Rechte per `uaccess`)
und bietet auf dem **Session-Bus** eine D-Bus-Schnittstelle zum Abfragen und Steuern.

| | |
|---|---|
| Binary | `~/.local/bin/seglcdd` (Native AOT, ~6 MB, braucht zur Laufzeit `libhidapi-hidraw.so.0`) |
| Unit | `~/.config/systemd/user/seglcdd.service` (`Type=notify`) |
| Konfiguration | `~/.config/seglcd/config.toml` – siehe [Sensoren & Konfiguration](../sensoren/#configtoml) |
| D-Bus | Session-Bus, Name `io.github.fubar_coder.SegLcd1`, Objekt `/io/github/fubar_coder/SegLcd1` |
| Log | journald: `journalctl --user -u seglcdd` |
| Man-Pages | `seglcdd(1)`, `seglcdd.toml(5)` (Englisch und Deutsch, Quelle `man/`) |
| Bibliotheken | Microsoft.Extensions.Hosting(.Systemd), Tmds.DBus.Protocol + Generator, Tomlyn – alle AOT-tauglich, Publish ohne Warnungen |

## Installation

Voraussetzungen: .NET-SDK, `hidapi` (`sudo pacman -S hidapi`), udev-Regel `70-seglcd.rules`.

{{< tabs >}}
  {{< tab name="Skript" >}}
```fish
packaging/install-user.sh
```

Das Skript baut `seglcdd` und `seglcd` (Native AOT), installiert Binaries, Unit und D-Bus-Activation-Datei und
startet den Dienst (bzw. startet ihn neu). Eine **vorhandene `config.toml` bleibt unverändert**; nur wenn noch keine
existiert, wird die Standardkonfiguration angelegt.

| Option | Wirkung |
|---|---|
| `--reset-config` | vorhandene `config.toml` durch die Standardkonfiguration ersetzen (alte Datei → `config.toml.bak`) |
| `--no-start` | nur installieren, Dienst nicht aktivieren/starten |
| `-h`, `--help` | Hilfe anzeigen |
  {{< /tab >}}
  {{< tab name="Manuell" >}}
```fish
dotnet publish src/SegLcd.Daemon -c Release -r linux-x64
install -Dm755 artifacts/publish/SegLcd.Daemon/release_linux-x64/seglcdd ~/.local/bin/seglcdd
install -Dm644 packaging/systemd/seglcdd.service ~/.config/systemd/user/seglcdd.service
install -Dm644 packaging/dbus/io.github.fubar_coder.SegLcd1.service ~/.local/share/dbus-1/services/io.github.fubar_coder.SegLcd1.service
test -e ~/.config/seglcd/config.toml; or install -Dm644 packaging/config.example.toml ~/.config/seglcd/config.toml
systemctl --user daemon-reload
systemctl --user enable --now seglcdd
```
  {{< /tab >}}
{{< /tabs >}}

Danach die Konfiguration anpassen (mindestens `fan`/`pump`, siehe [Sensoren](../sensoren/#lüfter-und-pumpe-zuordnen))
und `systemctl --user reload seglcdd` ausführen.

### Unit-Datei

```ini {filename="packaging/systemd/seglcdd.service"}
[Service]
Type=notify
ExecStart=%h/.local/bin/seglcdd
ExecReload=/usr/bin/busctl --user call io.github.fubar_coder.SegLcd1 /io/github/fubar_coder/SegLcd1 io.github.fubar_coder.SegLcd1 ReloadConfig
Restart=on-failure
RestartSec=3
NoNewPrivileges=yes
PrivateTmp=yes

[Install]
WantedBy=default.target
```

- `Type=notify`: `seglcdd` meldet über `sd_notify`, wenn es bereit ist (Microsoft.Extensions.Hosting.Systemd).
- `ExecReload` ruft die D-Bus-Methode `ReloadConfig` auf – `systemctl --user reload seglcdd` lädt also die
  Konfiguration neu, ohne dass das Display kurz ausgeht.
- `WantedBy=default.target`: startet beim Login. Ohne Login (z. B. Login-Bildschirm) läuft kein Benutzerdienst;
  wer das will, kann `loginctl enable-linger` setzen – das Display funktioniert dann aber nur, wenn die
  udev-Regel zusätzlich eine Gruppe statt `uaccess` verwendet (`uaccess` gilt nur für die aktive Sitzung).

### D-Bus-Activation

`packaging/dbus/io.github.fubar_coder.SegLcd1.service` verknüpft den Bus-Namen mit der Unit
(`SystemdService=seglcdd.service`): Greift ein Programm auf den Namen zu, während der Dienst nicht läuft,
startet systemd ihn automatisch.

## Betrieb

```fish
systemctl --user status seglcdd
journalctl --user -u seglcdd -f
systemctl --user reload seglcdd      # Konfiguration neu laden
systemctl --user restart seglcdd
```

Beim Start protokolliert der Dienst die Konfigurationsdatei, für jedes Feld die gewählte Quelle und den
Verbindungsstatus:

```text
Konfiguration geladen: /home/…/.config/seglcd/config.toml
Quelle cpu_temperature: hwmon4/temp1 (k10temp:Tctl)
Quelle fan: hwmon5/fan1 (it8686_31050607:fan1)
D-Bus bereit: io.github.fubar_coder.SegLcd1 /io/github/fubar_coder/SegLcd1
Display verbunden: /dev/hidraw5 (Thermaltake LB360-S ARGB Sync)
```

| Situation | Verhalten |
|---|---|
| Display nicht angeschlossen / an VM durchgereicht | wartet, sucht jede Sekunde neu, verbindet sich automatisch |
| Display wird getrennt | „Display getrennt“, Reconnect wie oben (verifiziert per VM-Umleitung) |
| Dienst wird gestoppt | Aus-Paket `0F`, Gerät wird freigegeben |
| Konfiguration fehlerhaft | Fehler im Journal, Start mit Standardwerten |
| Kein Session-Bus | Warnung, Display läuft trotzdem (ohne D-Bus) |
| Name schon vergeben (zweite Instanz) | Fehler im Journal, die zweite Instanz bedient das Display trotzdem – nicht doppelt starten |

### Zusammenspiel mit `seglcd`

Dienst und `seglcd` dürfen nicht gleichzeitig senden, sonst wechseln sich ihre Pakete ab. Vor Experimenten mit
`seglcd send`/`probe`/`raw` den Dienst **pausieren**:

```fish
busctl --user set-property io.github.fubar_coder.SegLcd1 /io/github/fubar_coder/SegLcd1 io.github.fubar_coder.SegLcd1 Enabled b false
# … seglcd-Experimente …
busctl --user set-property io.github.fubar_coder.SegLcd1 /io/github/fubar_coder/SegLcd1 io.github.fubar_coder.SegLcd1 Enabled b true
```

`Enabled = false` sendet das Aus-Paket und gibt das Gerät frei; der Dienst läuft weiter. Alternativ
`systemctl --user stop seglcdd`. `seglcd list`, `sensors` und `decode` sind unkritisch.

## D-Bus-Schnittstelle

Definition: `src/SegLcd.Daemon/DBus/io.github.fubar_coder.SegLcd1.xml`
(daraus erzeugt Tmds.DBus.Generator den Server-Code).

| | |
|---|---|
| Bus | Session |
| Name | `io.github.fubar_coder.SegLcd1` |
| Objekt | `/io/github/fubar_coder/SegLcd1` |
| Interface | `io.github.fubar_coder.SegLcd1` (+ `org.freedesktop.DBus.Properties`, `…Introspectable`) |

### Properties

| Name | Typ | Zugriff | Bedeutung |
|---|---|---|---|
| `Enabled` | `b` | lesen/schreiben | Display versorgen; `false` = Aus-Paket, Gerät freigegeben |
| `UpdateIntervalMs` | `u` | lesen/schreiben | Sendeintervall, 50–400 ms; wirkt sofort, ohne Flackern |
| `SampleIntervalMs` | `u` | lesen/schreiben | Einleseintervall, 100–10000 ms |
| `Connected` | `b` | lesen | Display geöffnet |
| `DevicePath` | `s` | lesen | z. B. `/dev/hidraw5`, leer wenn getrennt |
| `Model` | `s` | lesen | z. B. `Thermaltake LB360-S ARGB Sync` |
| `ConfigPath` | `s` | lesen | Pfad der Konfigurationsdatei |
| `Sources` | `a{ss}` | lesen | Feld → aufgelöste Quelle (leer = keine) |
| `CurrentValues` | `a{sd}` | lesen | zuletzt eingelesene Werte, nur vorhandene (`cpu_temperature`, `cpu_usage`, `cpu_power`, `cpu_clock`, `cpu_voltage`, `gpu_temperature`, `gpu_usage`, `gpu_power`, `gpu_clock`, `fan`, `pump`, `ram_usage`) |
| `LastPacket` | `ay` | lesen | zuletzt gesendetes 64-Byte-Paket |

`PropertiesChanged` wird gesendet für `Enabled`, `UpdateIntervalMs`, `SampleIntervalMs`, `Connected`,
`DevicePath`, `Model` und `Sources`. **Nicht** für `CurrentValues` und `LastPacket` (ändern sich laufend – bei
Bedarf abfragen). Ungültige Werte beim Schreiben werden mit `org.freedesktop.DBus.Error.InvalidArgs` abgelehnt.

### Methoden

| Methode | Signatur | Bedeutung |
|---|---|---|
| `SetSensor(field, spec) → source` | `ss` → `s` | Quelle eines Feldes setzen ([Spec-Syntax](../sensoren/#sensor-angaben-spec-syntax), leer = automatisch); liefert die aufgelöste Quelle. Unbekanntes Feld oder nicht gefundener Sensor → `InvalidArgs`. **Nicht persistent** bis `SaveConfig`. |
| `ReloadConfig()` | – | `config.toml` neu lesen und anwenden; bei Fehler bleibt die alte Konfiguration aktiv |
| `SaveConfig()` | – | aktuelle Laufzeit-Einstellungen nach `config.toml` schreiben (Kommentare der Datei gehen dabei verloren) |

### Beispiele

```fish
set N io.github.fubar_coder.SegLcd1
set P /io/github/fubar_coder/SegLcd1

busctl --user introspect $N $P $N
busctl --user get-property $N $P $N Connected DevicePath Model
busctl --user get-property $N $P $N CurrentValues
busctl --user set-property $N $P $N UpdateIntervalMs u 200
busctl --user call $N $P $N SetSensor ss pump it8686_31050607:fan5
busctl --user call $N $P $N SaveConfig
busctl --user monitor $N            # Signale beobachten
```

Mit `gdbus`:

```fish
gdbus call --session --dest io.github.fubar_coder.SegLcd1 --object-path /io/github/fubar_coder/SegLcd1 \
  --method org.freedesktop.DBus.Properties.GetAll io.github.fubar_coder.SegLcd1
```

{{< callout type="info" >}}
Der Bus-Name folgt der D-Bus-Konvention „umgekehrte Domain“: `io.github.fubar_coder` entspricht dem
GitHub-Namensraum `github.com/fubar-coder` (Bindestriche sind in D-Bus-Namen nicht erlaubt und werden zu `_`).
{{< /callout >}}

## Aufbau

| Datei | Aufgabe |
|---|---|
| `Program.cs` | Generic Host, `AddSystemd()`, Dienste registrieren, `--config` |
| `DaemonConfig.cs` | Konfigurationsmodell, TOML laden/speichern (Tomlyn-Source-Generator, snake_case) |
| `DisplayController.cs` | Sensoren einlesen, `SegmentLcdSession` verwalten, Laufzeitänderungen, Logging |
| `DBus/DBusService.cs` | Verbindung zum Session-Bus, Name anfordern |
| `DBus/SegmentLcdObject.cs` | Implementierung der Properties/Methoden, `PropertiesChanged` |
| `DBus/io.github.fubar_coder.SegLcd1.xml` | Interface-Definition (Quelle für den Generator) |
