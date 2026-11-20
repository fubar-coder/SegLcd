---
title: USB-Mitschnitt & Auswertung
weight: 2
---

## Mitschneiden auf dem Linux-Host (usbmon)

QEMU spricht über libusb bzw. usbredir mit dem echten Gerät – der Verkehr läuft also durch den USB-Stack des
Hosts und ist dort mit **usbmon** sichtbar. In Windows muss nichts installiert werden.

{{% steps %}}

### Werkzeuge

```bash
sudo pacman -S wireshark-cli      # dumpcap + tshark; für die GUI: wireshark-qt
```

### Bus und Adresse ermitteln

```bash
lsusb -d 5131:200e
# Bus 001 Device 003: ID 5131:200e MSR   -> usbmon1, Adresse 3
```

### Aufzeichnen

{{< tabs >}}
  {{< tab name="fish" >}}
```fish
sudo tools/capture-usbmon.sh captures/(date +%Y%m%d-%H%M)-lb360s-start.pcapng
```
  {{< /tab >}}
  {{< tab name="bash" >}}
```bash
sudo tools/capture-usbmon.sh captures/$(date +%Y%m%d-%H%M)-lb360s-start.pcapng
```
  {{< /tab >}}
{{< /tabs >}}

Das Skript lädt `usbmon` und schneidet den **ganzen Bus** mit (usbmon kann nicht nach Adresse filtern).
Jetzt in der VM das Szenario durchspielen (siehe unten), danach Strg+C.

Ohne Skript: `sudo modprobe usbmon && sudo dumpcap -q -i usbmon1 -w - > datei.pcapng`.

{{< callout type="info" >}}
`dumpcap -w datei` scheitert unter `sudo` mit *Permission denied*: dumpcap gibt nach dem Öffnen des Interfaces
seine Rechte ab und darf die Datei dann nicht mehr anlegen. Deshalb schreibt es nach stdout (`-w -`), und die
Datei wird von der Shell geöffnet. Das Skript übergibt die Datei zusätzlich an den aufrufenden Benutzer.
{{< /callout >}}

{{% /steps %}}

{{< callout type="info" >}}
Beim Anhängen an die VM setzt QEMU das Gerät zurück; die **Adresse kann sich ändern**. Daher erst
umleiten, dann `lsusb` prüfen, dann aufzeichnen – oder beim Auswerten ohne Adressfilter arbeiten
und nach 64-Byte-Interrupt-Paketen filtern.
{{< /callout >}}

### Alternative: USBPcap in Windows

Wireshark für Windows bringt optional **USBPcap** mit. Dann in der VM das Interface `USBPcap1…n` wählen,
auf dem das Gerät hängt. Die Auswertung unten funktioniert gleich (die Felder heißen identisch).

## Auswerten

Die Host→Gerät-Pakete sind **Interrupt-OUT-Transfers auf Endpoint `0x01`** mit 64 Byte Nutzlast.

| Wireshark-Anzeigefilter | Zweck |
|---|---|
| `usb.device_address == 3` | nur das Display |
| `usb.transfer_type == 0x01` | nur Interrupt-Transfers |
| `usb.endpoint_address == 0x01 && usb.data_len == 64` | Output-Reports (Host → Display) |
| `usb.endpoint_address == 0x81 && usb.data_len > 0` | Input-Reports (Display → Host) |
| `usb.transfer_type == 0x02` | Control-Transfers (Enumeration, ggf. `SET_REPORT`/`SET_IDLE`) |

Nutzlast extrahieren und dekodieren:

```bash
tools/extract-packets.sh captures/…-lb360s-start.pcapng 3 > captures/…-lb360s-start.txt
seglcd decode captures/…-lb360s-start.txt | less          # alle Felder je Paket
seglcd decode --diff captures/…-lb360s-start.txt | less   # nur geänderte Bytes
```

`extract-packets.sh` gibt pro Paket `<Zeit>\t<Hex>` aus (Feld `usb.capdata` bzw. bei neueren
Wireshark-Versionen `usbhid.data`). `seglcd decode` versteht genau dieses Format; bei 65-Byte-Daten mit
Report-ID hilft `--strip-report-id`.

Das Intervall zwischen den Paketen lässt sich direkt aus der Zeitspalte ablesen:

```bash
tools/extract-packets.sh datei.pcapng 3 | awk -F'\t' 'NR>1{printf "%.3f\n", $1-p} {p=$1}' | sort | uniq -c
```

## Szenarien

Jedes Szenario als eigene Datei aufzeichnen, damit sich Änderungen eindeutig zuordnen lassen.

| # | Szenario | Erwartung / Frage |
|---|---|---|
| 1 | Software starten, 30 s laufen lassen, beenden | Gibt es Initialisierungs-Pakete? Intervall? Aus-Paket `0F` am Ende? |
| 2 | Einheit °C ↔ °F umschalten | Bytes 5/15 wechseln auf `1`? Werte umgerechnet? |
| 3 | Anzeigemodus wechseln (falls vorhanden, z. B. CPU/GPU) | Neues Kommando oder nur andere Werte? |
| 4 | Last erzeugen (z. B. Prime95 in der VM) | Bytes 3, 6, 7–10 ändern sich |
| 5 | Software-Einstellungen wie Helligkeit, Display aus/an | **neue, unbekannte Kommandos** – besonders interessant |
| 6 | Gerät während laufender Software ab-/anstecken | Verhalten beim Reconnect |
| 7 | Windows-Herunterfahren mit laufender Software | Wird `0F` gesendet? |

Ergebnisse in [Verifikation](../../protocol/verification/) eintragen; neue Kommandos als eigene Seite
unter *Protokoll* dokumentieren.

## Kontrolle ohne Windows

Für die Funktionsprüfung des eigenen Codes kann man auch die Pakete von `seglcd` selbst mitschneiden
und mit `--verbose` vergleichen – so lässt sich z. B. prüfen, dass wirklich 64 Byte ohne Report-ID auf der
Leitung landen:

```bash
sudo dumpcap -i usbmon1 -a duration:5 -w /tmp/self.pcapng & seglcd send --cpu-temp 42 --duration 4 --verbose
tools/extract-packets.sh /tmp/self.pcapng | seglcd decode | head -20
```
