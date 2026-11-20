---
title: Verifikation & offene Fragen
weight: 4
---

## Status

Legende: ✅ verifiziert · ❌ geprüft, kein sichtbarer Effekt · 🟡 angenommen (von LA-Serie übernommen) · ❓ unbekannt

| Aspekt | LA-Serie `5131:2007` | LB360-S `5131:200e` |
|---|---|---|
| HID-Deskriptor (64/64 Byte, keine Report-IDs) | ✅ | ✅ (byte-identisch, selbst ausgelesen) |
| Update-Paket `01 02`, Layout | ✅ | ✅ (2026-11-20, `seglcd send`/`probe`) |
| Aus-Paket `0F` | ✅ | 🟡 Wirkung nicht vom Timeout (~450 ms) unterscheidbar; Original-Software der LB360-S sendet es nicht |
| Sendeintervall 250 ms | ✅ | ✅ funktioniert |
| Display geht beim Programmende aus | ✅ | ✅ **unmittelbar** beim Ende von `seglcd send` (ohne `--off-at-end`, also ohne `0F`); während der Laufzeit (z. B. 30 s) bleibt es an |
| Ursache: Timeout nach letztem Paket **oder** Schließen des Geräts | ❓ | ✅ **Timeout** – mit `seglcd hold --send 5 --hold 15` ging das Display beim Ende des Sendens aus, obwohl das Gerät noch offen war |
| Timeout nach letztem Paket | ❓ („kurz“) | ✅ **ca. 400–500 ms** (Intervall 400 ms stabil, 500 ms blinkt) |
| Angezeigte Felder | CPU-Temp. (ganzzahlig), CPU-Auslastung + Balken, Lüfter-RPM + Balken | CPU- und GPU-Temperatur (ganzzahlig, °C) – [Details](#lb360-s-feldzuordnung) |
| Endwert der Balkenanzeigen | ❓ (1234 RPM ≈ halber Lüfterbalken) | ❓ (CPU-Auslastung zeigt keinen Effekt) |
| °F-Darstellung | ❓ | ❌ Symbol bleibt °C; mit Flag `1` werden Werte ≥ 100 nur zweistellig (mod 100) angezeigt – **nicht verwenden** |
| Bedeutung der Input-Reports | ❓ | ❓ |
| Wiederverbinden nach Trennen (VM-Umleitung) | – | ✅ `seglcd live` verbindet sich nach Rückgabe des Geräts selbst wieder |
| String-Deskriptoren 4/5 | – | ❓ |

## LB360-S: Feldzuordnung

Ergebnis von `seglcd send` und `seglcd probe` auf der LB360-S am 2026-11-20:

| Offset | Feld | Ergebnis |
|---:|---|---|
| 3 | CPU-Temperatur, ganzzahlig | ✅ wird angezeigt |
| 4 | CPU-Temperatur, Nachkomma | ❌ kein Effekt – das Display hat keine Nachkommastelle |
| 6 | CPU-Auslastung | ❌ kein sichtbarer Effekt |
| 13 | GPU-Temperatur, ganzzahlig | ✅ wird angezeigt |
| 14 | GPU-Temperatur, Nachkomma | ❌ kein Effekt |
| 5 / 15 | CPU-/GPU-Temperatur-Einheit | ⚠️ Symbol bleibt °C, aber die **Darstellung ändert sich**: Flag `0` begrenzt auf 99, Flag `1` zeigt nur die **letzten zwei Stellen** (122 → „22“) und bei Wert 0 „99“ – siehe [Einheiten-Flag](#lb360-s-einheiten-flag) |
| übrige (7–12, 16–35) | Leistung, Takt, Spannung, Drehzahlen, Uhrzeit, RAM | ❓ noch nicht einzeln bestätigt |
| 36–49 | erweiterter Bereich, u. a. 37 (unbekannt), 48 (CPU-Auslastung Nachkomma) | ❌ kein sichtbarer Effekt (`seglcd probe --offsets 36-63`, bis 49 geprüft) |
| 50–63 | u. a. 50 (RAM-Auslastung Nachkomma) | ❌ kein sichtbarer Effekt (`seglcd probe --offsets 50-63`) |

### LB360-S: Einheiten-Flag

Test mit `seglcd raw` (2026-11-20), jeweils nur Temperatur-Byte und Flag gesetzt:

| Wert (Byte 3 bzw. 13) | Flag (Byte 5 bzw. 15) | Anzeige |
|---:|:---:|---:|
| 0 | 0 | 0 |
| ≥ 100 (100, 105, 123, 199, 255) | 0 | 99 |
| 0 | 1 | 99 |
| 31 | 1 | 31 |
| 32 | 1 | 32 |
| 50 | 1 | 50 |
| 122 | 1 | 22 (CPU und GPU gleich) |

Das Display rechnet **nicht** von °F nach °C um. Mit Flag `1` entfällt die Begrenzung auf 99, es werden nur die
letzten zwei Stellen gezeigt – vermutlich Firmware für dreistellige °F-Werte, ohne Hunderterstelle auf diesem
LCD. Warum 0 mit Flag `1` als 99 erscheint, ist offen. Ein Symbolwechsel auf °F findet nicht statt.

Folgerungen für Implementierungen:

- Für die LB360-S genügt es, **Offset 3 (CPU) und 13 (GPU)** zu füllen. Die übrigen Felder trotzdem
  wie spezifiziert senden: Das kostet nichts und hält die Pakete mit der Original-Software kompatibel.
- **Immer in °C mit Flag `0` senden.** Mit Flag `1` (°F) erscheinen Werte ≥ 100 nur zweistellig: 50 °C =
  122 °F würde als „22“ angezeigt, und das Symbol bleibt °C. Eine Library sollte die Fahrenheit-Option für dieses
  Modell ignorieren oder davor warnen (`seglcd` warnt).
- Temperaturen werden abgeschnitten, nicht gerundet (Byte 3 = `floor`). Wer lieber rundet, muss den Wert
  vor dem Kodieren selbst runden (z. B. 42,7 → 43).
- Werte **≥ 100** zeigt das Display als **99** (geprüft mit 99, 100, 105, 123, 199, 255). Eine Library muss
  nicht begrenzen, sollte aber wissen, dass z. B. 105 °C als „99“ erscheinen.

## Mitschnitt der Original-Software (VM)

Aufgezeichnet am 2026-11-20 mit `tools/capture-usbmon.sh`: TT SEGMENT LCD Software in der Windows-VM
(`qemu:///session`, SPICE-USB-Umleitung, 8 vCPUs). Die Rohdaten sind nicht im Repository; die Auswertung steht hier vollständig.
In der VM sind die meisten Sensorwerte 0. Die Software zeigt die Kacheln *CPU Temp*, *CPU Usage*, *CPU Fan Speed*,
*CPU Frequency*, *CPU Power* und *GPU Fan Speed / Temp*; einstellbar ist nur die Quelle für *CPU Usage*
(*Total CPU Usage*, *Core 0…7 Usage*, *Max CPU/Thread Usage*, *Core 0…7 Utility*, *Total CPU Utility*).
Während des Mitschnitts wurde einmal von *Total CPU Usage* auf eine andere Quelle umgeschaltet.

| Befund | Detail |
|---|---|
| Kommandos | ausschließlich `00 01 02 …` (567 Pakete in ~117 s) – **keine Initialisierung**, keine weiteren Kommandos |
| Aus-Paket `0F` | **wird nicht gesendet** – Software regulär beendet: letztes Update bei 141,16 s, ~1 s später schließt sie das Gerät (offene IN-Anfragen werden abgebrochen), danach kein Verkehr mehr. Die Software verlässt sich auf den Timeout. |
| Sendeintervall | **200–220 ms** (Median 0,20 s) |
| Aktualisierung der Werte | ca. alle **2 s**; dazwischen wird das letzte Paket unverändert wiederholt |
| Erste Pakete | ca. 1 s lang nur Uhrzeit, RAM, Byte 37 und Byte 50 belegt, alle Messwerte 0 |
| Byte 6 (CPU-Auslastung) | 0–58 %, plausibel für die VM ✅ |
| Bytes 11/12 (CPU-Spannung) | konstant `01 37` → „1,55 V“ (die LA-Software sendete hier `floor(V) × 100`) |
| Bytes 25–32 | lokale Uhrzeit ✅, Wochentag 5 = Freitag ✅ |
| Byte 33 (RAM) | 28–30 % ✅ |
| **Byte 37** | **neu**, konstant `5` – Bedeutung unbekannt (nicht die vCPU-Anzahl: 8) |
| **Byte 48** | **neu**, 0–99, springt stark, wechselt im 2-s-Takt → später als **CPU-Auslastung, Nachkomma** identifiziert |
| **Byte 50** | **neu**, 11–99, ändert sich langsamer, wechselt im 2-s-Takt → später als **RAM-Auslastung, Nachkomma** identifiziert |
| Control-Transfers | nur `GET_DESCRIPTOR` (Gerät, Konfiguration) beim Umleiten und zwei `SET_IDLE` (HID-Klasse, `bmRequestType 0x21`) von Windows |
| Input-Reports | **keine** – Windows hält IN-Anfragen offen, das Gerät antwortet nie |
| String-Deskriptoren | werden von Windows nicht abgefragt |

### Zweiter Mitschnitt: alle Auslastungsquellen

Zweiter Mitschnitt – 866 Pakete in ~176 s; nacheinander alle Quellen für
*CPU Usage* (Total, Core 0–7 Usage, Max CPU/Thread Usage, Core 0–7 Utility) im ~10-s-Takt.

| Befund | Detail |
|---|---|
| Kommandos | wieder ausschließlich `01 02` – **die Auswahl erzeugt kein eigenes Kommando** |
| Byte 6 | 4–30 %, in 10-s-Fenstern ohne erkennbare Stufen – bei gleichmäßig wenig belasteter VM sind Gesamt- und Einzelkernwerte nicht unterscheidbar |
| Byte 37 | wieder konstant `5` |
| Byte 48 | 1–98, springt bei jeder Aktualisierung (wirkt wie Zufall), Korrelation zu Byte 6: −0,06 |
| Byte 50 | 3–98, driftet langsam (Zufallsbewegung), Korrelation zu Byte 6: −0,01 |
| Bytes 11/12 | wieder konstant `01 37` |

Folgerung: Bei gleichmäßig wenig belasteter VM lässt sich die Quelle nicht erkennen – das klärt der dritte Mitschnitt.

### Dritter Mitschnitt: ein Kern unter Volllast

Dritter Mitschnitt – in der VM lief `while ($true) {}` mit Affinität auf Kern 5;
in der Software nacheinander *Core 5 Usage* → *Total CPU Usage* → *Core 7 Usage*.

| Abschnitt | Byte 6 | Byte 48 |
|---|---|---|
| Core 5 Usage | **100** (konstant, ~12 s) | **0** in allen 58 Paketen |
| Total CPU Usage | 23–29 (≈ 100/8 + Grundlast) | 40–86 |
| Core 7 Usage | 8–18 | 3–85 |

Folgerungen:

- Die in der Software gewählte **Auslastungsquelle landet in Byte 6** (kein eigenes Kommando, kein eigenes Feld).
- **Byte 48 = Nachkommastellen der CPU-Auslastung** (bei 100,00 % exakt 0, sonst gleichverteilt 0–99).
- **Byte 50 = Nachkommastellen der RAM-Auslastung:** Byte 33 + Byte 50/100 verläuft in allen drei Mitschnitten
  glatt (max. Sprung 0,2 Prozentpunkte zwischen zwei Aktualisierungen, z. B. 27,94 → 27,97 → 27,98 → 28,03).

Offene Punkte aus den Mitschnitten:

- **Byte 37:** Blieb beim Umschalten der Auslastungsquelle konstant `5` → **nicht** die Kernauswahl.
- ✅ **Bytes 48/50:** CPU- bzw. RAM-Auslastung, Nachkommastellen (dritter Mitschnitt).
- **Byte 37** auf dem Display testen: `seglcd probe --offsets 37 --value 88`.
- ✅ **Auslastungsquelle:** landet in Byte 6 (dritter Mitschnitt).

## Testplan für die LB360-S

Voraussetzung: [udev-Regel](../../linux/udev/) installiert, `seglcd list` zeigt „Öffnen OK“.

1. ✅ **Grundfunktion** – zeigt das Display überhaupt etwas?
   ```bash
   seglcd send --cpu-temp 42 --gpu-temp 57 --duration 20
   ```
2. ✅ (teilweise) **Feldzuordnung** – jedes Byte einzeln auf einen auffälligen Wert setzen:
   ```bash
   seglcd probe --value 88            # interaktiv, Enter = nächstes Byte
   seglcd probe --value 88 --auto 3   # automatisch, 3 s pro Byte
   ```
   Für jedes Byte notieren, ob und wo sich die Anzeige ändert.
3. ✅ **Nachkommastellen / Einheit** – keine Nachkommastellen, kein °F (siehe oben).
   ```bash
   seglcd send --cpu-temp 42.75 --gpu-temp 57.25 --duration 10
   seglcd send --cpu-temp 42 --gpu-temp 57 --fahrenheit --duration 10
   ```
4. **Balken/Skalen** – Endwerte bestimmen:
   ```bash
   seglcd sweep 6 --from 0 --to 100 --step 5 --step-ms 700     # CPU-Auslastung
   seglcd sweep 21 --from 0 --to 50 --step-ms 700              # Lüfter (×100 RPM)
   ```
5. ✅ **Timeout oder Schließen?** Ergebnis: **Timeout** (Display aus, sobald keine Pakete mehr kommen). Beim Programmende passieren zwei Dinge gleichzeitig: Es kommen keine Pakete mehr,
   **und** der Prozess schließt `/dev/hidrawN` (dann stoppt `usbhid` auch das Abfragen des Interrupt-IN-Endpoints).
   `seglcd hold` trennt beides: erst senden, dann aufhören, das Gerät aber offen lassen.
   ```bash
   seglcd hold --send 5 --hold 15
   ```
   - Display geht **beim Hinweis „Letztes Paket gesendet“** (oder kurz danach) aus → echter Timeout; Zeit notieren.
   - Display geht erst **bei „Gerät wird geschlossen“** aus → das Schließen (Ende des IN-Pollings) löst es aus;
     dann ist ein Keep-Alive im Sinne von „Pakete wiederholen“ eventuell gar nicht nötig – zu prüfen mit
     `seglcd hold --send 1 --hold 60`.
   - Display bleibt **auch nach dem Schließen** an → nur das Aus-Paket schaltet ab.

   ✅ Timeout eingegrenzt: **400 ms** Intervall stabil, **500 ms** blinkt. Messung:
   ```bash
   for i in 300 400 500 750 1000; do echo "Intervall $i ms"; seglcd send --cpu-temp 42 --gpu-temp 57 --interval $i --duration 6; done
   ```

6. **Abgleich mit dem Original** – USB-Mitschnitt der Windows-Software ([Anleitung](../../reverse-engineering/usb-capture/)),
   mit `seglcd decode --diff` auswerten und mit diesem Dokument vergleichen.

Ergebnisse bitte in dieser Tabelle nachtragen. Mitschnitte gehören in den lokalen Ordner `captures/`; er ist per `.gitignore` ausgeschlossen, weil usbmon den ganzen Bus aufzeichnet (auch andere Geräte).
