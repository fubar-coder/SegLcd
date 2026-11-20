---
title: Einstellungs-App seglcd-gtk
weight: 8
---

`seglcd-gtk` ist eine GTK-4-/libadwaita-App für [`seglcdd`](../service/): Sie zeigt den Zustand des Displays und die
aktuellen Werte und stellt die Intervalle und den Sensor jedes Wertes ein. Sie spricht nur über D-Bus mit dem Dienst.

| Seite | Inhalt |
|---|---|
| Status | Gerät, *Display versorgen* (`Enabled`), alle Werte (die LB360-S zeigt CPU- und GPU-Temperatur), Intervalle, Dienst und Konfigurationsdatei |
| Sensoren | pro Wert: *Automatisch* oder ein Sensor passender Art (aus `ListSensors`, mit aktuellem Wert); darunter der verwendete Sensor |

Änderungen wirken sofort. **Speichern** macht sie dauerhaft (`SaveConfig`; Systemdienst: `/var/lib/seglcd/config.toml`),
die Schaltfläche zum Neuladen verwirft nicht gespeicherte Änderungen.

## Installation

- **Pakete:** `seglcd-gtk` (AUR, RPM-Unterpaket, DEB) – braucht GTK 4 und libadwaita ≥ 1.6.
- **Pro Benutzer:** `packaging/install-user.sh` installiert sie mit dem Benutzerdienst (`--no-gui` lässt sie weg).

## Verbindung

Die App verwendet den Systemdienst, wenn er läuft (oder per D-Bus gestartet werden kann), sonst den Benutzerdienst,
und verbindet sich neu, wenn der Dienst neu startet. `--system` oder `--session` beschränkt sie auf einen Bus.

Zum Steuern des Systemdienstes ist die Gruppe `seglcd` nötig (`sudo usermod -aG seglcd $USER`, danach neu anmelden);
sonst kann die App nur Werte anzeigen und meldet „Nicht erlaubt“.
