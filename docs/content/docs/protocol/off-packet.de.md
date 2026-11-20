---
title: Aus-Paket (0F)
weight: 2
---

Die Original-Software sendet beim Beenden einmalig dieses Paket; das Display wird daraufhin dunkel.

| Offset | Wert | Inhalt |
|---:|---|---|
| 0 | `0x00` | reserviert |
| 1 | `0x0F` | Kommando „Aus“ |
| 2–63 | `0x00` | – |

```text
00 0f 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
```

Hinweise für Implementierungen:

- Das Aus-Paket ist **optional**: Bei der LB360-S geht das Display auch ohne `0F` etwa 400–500 ms später aus, sobald
  keine Update-Pakete mehr kommen (Timeout, verifiziert mit `seglcd hold`). Das Paket macht das Abschalten
  nur explizit.
- Nach einem Aus-Paket genügt ein neues Update-Paket, um das Display wieder einzuschalten
  (bei der LA-Serie beobachtet).
- Die **TT SEGMENT LCD Software für die LB360-S sendet `0F` beim Beenden nicht** (Mitschnitt 2026-11-20) –
  sie verlässt sich auf den Timeout. Die Angabe „Original sendet `0F`“ stammt von der LA-Software.
- Empfehlung trotzdem: beim geordneten Beenden (Dienst-Stopp, `SIGTERM`, Strg+C) senden; schadet nicht und
  schaltet ohne Verzögerung ab. Bei einem Absturz übernimmt der Timeout.
