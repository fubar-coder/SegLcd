---
title: Lokalisierung
weight: 10
---

`seglcd` und `seglcdd` sind mit **GNU gettext**-Katalogen lokalisiert. Quellsprache ist **Englisch**; derzeit gibt es
eine deutsche Übersetzung (`po/de.po`). Das Verfahren entspricht dem Projekt *hcl-notes-link-handler*.

## Sprache wählen

| Variable | Wirkung |
|---|---|
| `LC_MESSAGES` | bestimmt die Sprache, z. B. `de_DE.UTF-8` → Deutsch |
| `LANG` | wird verwendet, wenn `LC_MESSAGES` nicht gesetzt oder leer ist |
| `LC_ALL`, `LANGUAGE` | werden bewusst **ignoriert** |

Verwendet wird der Sprachteil vor `_`, `.` oder `@`. Gibt es dafür keinen Katalog (oder ist der Wert `C`/`POSIX`),
bleibt alles Englisch.

```fish
LANG=de_DE.UTF-8 seglcd --help     # Deutsch
LANG=C seglcd --help               # Englisch
```

## Was übersetzt wird – und was nicht

| übersetzt | nicht übersetzt |
|---|---|
| Hilfe-Texte aller Befehle, Optionen und Argumente | Befehls- und Optionsnamen |
| Abschnittstitel der Hilfe, `[Optionen]`, `[erforderlich]`, `[Standard: …]` | Hex-Ausgaben, Pakete, `decode`-Zusammenfassung (`=> CPU …`) |
| Meldungen, Warnungen, Fehler von `seglcd` | Spec-Angaben (`k10temp:Tctl`, `rapl`, …) und Feldnamen (`cpu_temperature`) |
| D-Bus-Fehlermeldungen von `seglcdd` | D-Bus-Namen, -Properties, -Methoden |
| `seglcdd`-Log **in einem Terminal** | `seglcdd`-Log **im Journal** (bleibt Englisch) |
| Feldnamen in `probe`/`decode` | Zahlen- und Datumsformat (immer invariant) |

Parser-Fehler wie „`'sned' was not matched`“ stammen von System.CommandLine. Sie erscheinen teils englisch, weil
System.CommandLine sie selbst nicht übersetzt.

## Aufbau

| Teil | Ort |
|---|---|
| Kataloge | `po/seglcd.pot` (Vorlage), `po/de.po` (Deutsch) – als Ressource in `SegLcd.Localization` eingebettet |
| Parser | [Karambolo.PO.Compact](https://github.com/adams85/po) (AOT-tauglich) |
| Laufzeit | `PoStringLocalizer`/`…Factory` (`IStringLocalizer`), `DisplayLanguage`, `Texts.Current` |
| Aufruf | `T("…")`, `T("… {0}", x)`, `Tn("singular", "plural", n, …)` aus `LocalizerExtensions` |
| CLI-Beschreibungen | `CliTexts` (Attribute verweisen mit `Description = nameof(CliTexts.X)`) und `LocalizedHelpBuilder` |
| Log-Vorlagen | englische `[LoggerMessage]`-Texte sind die msgids; `LocalizingConsoleFormatter` übersetzt nur die Konsolenausgabe |

## Regeln für Entwickler

- Jeder für Menschen bestimmte Text läuft über `T`/`Tn` – immer mit **String-Literal** (kein `$"…"`, keine Konstante,
  nicht der Indexer), sonst findet `xgettext` ihn nicht.
- Kontext für Übersetzer als Kommentar direkt davor: `// translators: …`.
- Neue CLI-Beschreibung = neue Property in `CliTexts`; ein Literal in `Description` lässt die Tests fehlschlagen.
- Log-Meldungen: englische Vorlage mit benannten Platzhaltern (`{Path}`), eigene `EventId` (Bereiche:
  `DisplayController` 100–199, D-Bus 200–299). Keine zusammengesetzten Sätze über `{Message}` loggen.
- Maschinenlesbare Ausgaben nie übersetzen.

## Ablauf nach Textänderungen

```fish
po/extract.sh                                  # .pot neu erzeugen, in de.po mergen
$EDITOR po/de.po                               # neue/geänderte Einträge übersetzen
msgfmt --check --statistics -o /dev/null po/de.po
dotnet test                                    # CatalogTests prüfen Vollständigkeit
```

`po/extract.sh` sammelt die `T`/`Tn`-Aufrufe per `xgettext` und die `[LoggerMessage]`-Vorlagen per Reflection
(`po/ExtractLogTemplates.cs`), führt beides zusammen und aktualisiert alle `po/*.po` mit `msgmerge`.
Benötigt die gettext-Werkzeuge (`xgettext`, `msgcat`, `msgmerge`, `msgattrib`) und das .NET-SDK.

Übersetzungsstil Deutsch: neutral und unpersönlich (kein „du“), Platzhalter (`{0}`, `{Path}`) und technische
Angaben unverändert lassen.

### Neue Sprache

```fish
msginit -l fr -i po/seglcd.pot -o po/fr.po
# übersetzen, dann:
po/extract.sh; dotnet test
```

Die neue Datei wird automatisch eingebettet; `LANG=fr_FR.UTF-8` wählt sie aus.

## Man-Pages

Die Man-Pages liegen in `man/` (Englisch) und `man/de/` (Deutsch) und werden von Hand gepflegt (roff, keine
Zusatzwerkzeuge nötig). `man` wählt die Sprache über dieselben Variablen (`LANG`/`LC_MESSAGES`).
`tests/…/ManPageTests.cs` prüft, dass beide Sprachfassungen jeden Befehl und jede Option von `seglcd`, jeden
Schlüssel von `config.toml` sowie jede D-Bus-Methode und -Property von `seglcdd` erwähnen.

```fish
man -l man/de/man1/seglcd.1            # ohne Installation ansehen
groff -k -man -Tutf8 -ww -z man/de/man1/seglcd.1   # auf Warnungen prüfen
```

## Tests

`tests/…/CatalogTests.cs` stellt sicher, dass

- der eingebettete Katalog der Datei entspricht und beide Kataloge gültig sind,
- jeder Eintrag der Vorlage übersetzt und nicht `fuzzy` ist, und kein veralteter Eintrag übrig bleibt,
- alle Log-Vorlagen in der Vorlage stehen und übersetzt sind,
- die Platzhalter in Original und Übersetzung übereinstimmen und `msgfmt --check` den Katalog akzeptiert,
- `po/seglcd.pot` aktuell ist (erneuter `xgettext`-Lauf),
- jede CLI-Beschreibung auf `CliTexts` verweist und jede `CliTexts`-Property verwendet wird.
