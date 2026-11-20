---
title: USB-Passthrough (qemu:///session)
weight: 1
---

Bei `qemu:///session` läuft QEMU **als normaler Benutzer**. Damit QEMU (bzw. der SPICE-Client) das
USB-Gerät öffnen darf, braucht der Benutzer Schreibrechte auf `/dev/bus/usb/BBB/DDD` – genau dafür
enthält die [udev-Regel](../../linux/udev/) die zweite Zeile (`SUBSYSTEM=="usb"`, `uaccess`).

{{< callout type="info" >}}
Während das Gerät an die VM durchgereicht ist, löst der Host `usbhid` vom Gerät; `/dev/hidrawN`
verschwindet. `seglcd` auf dem Host funktioniert in dieser Zeit nicht. Nach dem Trennen bindet der Kernel
das Gerät wieder (ggf. mit neuer hidraw-Nummer).
{{< /callout >}}

## Voraussetzungen

- udev-Regel installiert (`getfacl /dev/bus/usb/001/003` zeigt den eigenen Benutzer mit `rw`).
  Bus/Adresse liefert `lsusb -d 5131:200e`.
- Die VM (hier `Win11Pro`) hat einen USB-Controller (`qemu-xhci` ist vorhanden) und – für Variante A –
  SPICE-Grafik mit `redirdev`-Kanälen (ebenfalls vorhanden: zwei `spicevmc`-Kanäle).

## Variante A: SPICE-USB-Umleitung (empfohlen)

Am einfachsten und jederzeit umkehrbar – keine Änderung an der VM-Konfiguration nötig.

{{% steps %}}

### VM starten und Konsole öffnen

```bash
virt-manager --connect qemu:///session --show-domain-console Win11Pro
```

### Gerät umleiten

Im Konsolenfenster: **Virtuelle Maschine → USB-Gerät umleiten** (bzw. *Redirect USB device*) und
`5131:200e` auswählen. Der Eintrag heißt mangels Produktstring nur nach der ID.

### Zurückgeben

Im selben Dialog den Haken entfernen oder das Konsolenfenster schließen.

{{% /steps %}}

Falls die Umleitung mit „Permission denied“ scheitert, ist die udev-Regel nicht aktiv (oder das Gerät seit der
Installation nicht neu getriggert). spice-gtk kann ersatzweise über `spice-client-glib-usb-acl-helper` (polkit)
Rechte anfordern; das fragt dann nach dem Passwort.

## Variante B: libvirt-Hostdev

Dauerhafter, unabhängig vom offenen Konsolenfenster. Die Datei `vm/lb360s-usb-hostdev.xml`
wählt das Gerät über VID/PID aus (unabhängig von Bus/Adresse):

```xml {filename="vm/lb360s-usb-hostdev.xml"}
<hostdev mode='subsystem' type='usb'>
  <source startupPolicy='optional'>
    <vendor id='0x5131'/>
    <product id='0x200e'/>
  </source>
</hostdev>
```

```bash
# an laufende VM anhängen / wieder abhängen
virsh -c qemu:///session attach-device Win11Pro vm/lb360s-usb-hostdev.xml --live
virsh -c qemu:///session detach-device Win11Pro vm/lb360s-usb-hostdev.xml --live

# dauerhaft in die Konfiguration (VM startet dank startupPolicy='optional' auch ohne Gerät)
virsh -c qemu:///session attach-device Win11Pro vm/lb360s-usb-hostdev.xml --config
```

Typische Fehler:

| Meldung | Ursache |
|---|---|
| `cannot open /dev/bus/usb/001/003: Permission denied` | udev-Regel fehlt / nicht getriggert |
| `Did not find USB device 5131:200e` | Gerät nicht angesteckt oder gerade an anderen Client umgeleitet |
| `device is in use by …` | gleichzeitig per SPICE umgeleitet – nur eine Variante verwenden |

## In Windows

1. Die **TT SEGMENT LCD Software** von der
   [Thermaltake-Download-Seite](https://thermaltake.com/pages/downloads) installieren
   (aktuelle Liste: unterstützt CL-W499/CL-W500 = LB360-S).

   {{< callout type="warning" >}}
   **Nicht TT RGB Plus!** TT RGB Plus ist auf der Download-Seite prominenter, findet das Display aber nicht.
   Die TT SEGMENT LCD Software ist etwas versteckt – nach „SEGMENT LCD“ suchen.
   {{< /callout >}}
2. Im Geräte-Manager erscheint unter *Eingabegeräte (HID)* ein „HID-konformes, vom Hersteller definiertes Gerät“
   mit `VID_5131&PID_200E`.
3. Software starten – das Gerät erscheint dort als **„LB-S ARGB AIO“**.
4. In der VM sind praktisch alle Messwerte **0** (virtuelle CPU ohne Sensoren, keine echte GPU, keine Lüfter).
   Für die Protokollanalyse ist das kaum ein Nachteil: Die Feldzuordnung ist bereits mit `seglcd probe` bekannt.
   Interessant am Mitschnitt sind Start-/Ende-Sequenz, Intervall und vor allem **Einstellungen** der Software
   (Helligkeit, Anzeigemodus, Einheit, Display an/aus), die neue Kommandos erzeugen könnten.

{{< callout type="warning" >}}
Falls die Software ein **Firmware-Update** anbietet: vorerst ablehnen. Ein Update über USB-Passthrough
kann abbrechen (Re-Enumeration, geänderte Adresse) und das Gerät unbrauchbar machen. Außerdem würde es
die Vergleichbarkeit mit der hier dokumentierten Firmware zerstören.
{{< /callout >}}

Weiter mit dem [USB-Mitschnitt](../usb-capture/).
