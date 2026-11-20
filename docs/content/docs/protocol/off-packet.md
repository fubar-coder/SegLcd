---
title: Off packet (0F)
weight: 2
---

The original software sends this packet once on exit; the display then goes dark.

| Offset | Value | Content |
|---:|---|---|
| 0 | `0x00` | reserved |
| 1 | `0x0F` | command "off" |
| 2–63 | `0x00` | – |

```text
00 0f 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
```

Notes for implementations:

- The off packet is **optional**: on the LB360-S the display also turns off about 400–500 ms later without `0F`, as soon as
  no more update packets arrive (timeout, verified with `seglcd hold`). The packet merely makes turning off
  explicit.
- After an off packet, a new update packet is enough to turn the display back on
  (observed on the LA series).
- The **TT SEGMENT LCD Software for the LB360-S does not send `0F` on exit** (capture 2026-11-20) –
  it relies on the timeout. The statement "original sends `0F`" comes from the LA software.
- Recommendation nonetheless: send it on orderly shutdown (service stop, `SIGTERM`, Ctrl+C); it does no harm and
  turns the display off without delay. In case of a crash, the timeout takes over.
