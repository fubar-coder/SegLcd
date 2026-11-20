---
title: Localization
weight: 10
---

`seglcd` and `seglcdd` are localized with **GNU gettext** catalogs. The source language is **English**; currently there is
a German translation (`po/de.po`). The approach matches the project *hcl-notes-link-handler*.

## Choosing the language

| Variable | Effect |
|---|---|
| `LC_MESSAGES` | determines the language, e.g. `de_DE.UTF-8` → German |
| `LANG` | used if `LC_MESSAGES` is not set or empty |
| `LC_ALL`, `LANGUAGE` | deliberately **ignored** |

The language part before `_`, `.` or `@` is used. If there is no catalog for it (or the value is `C`/`POSIX`),
everything stays English.

```fish
LANG=de_DE.UTF-8 seglcd --help     # German
LANG=C seglcd --help               # English
```

## What is translated – and what is not

| translated | not translated |
|---|---|
| help texts of all commands, options and arguments | command and option names |
| section titles of the help, `[options]`, `[required]`, `[default: …]` | hex output, packets, `decode` summary (`=> CPU …`) |
| messages, warnings, errors of `seglcd` | sensor specs (`k10temp:Tctl`, `rapl`, …) and field names (`cpu_temperature`) |
| D-Bus error messages of `seglcdd` | D-Bus names, properties, methods |
| `seglcdd` log **in a terminal** | `seglcdd` log **in the journal** (stays English) |
| field names in `probe`/`decode` | number and date format (always invariant) |

Parser errors such as "`'sned' was not matched`" come from System.CommandLine. Some of them appear in English because
System.CommandLine does not translate them itself.

## Structure

| Part | Location |
|---|---|
| Catalogs | `po/seglcd.pot` (template), `po/de.po` (German) – embedded as a resource in `SegLcd.Localization` |
| Parser | [Karambolo.PO.Compact](https://github.com/adams85/po) (AOT-compatible) |
| Runtime | `PoStringLocalizer`/`…Factory` (`IStringLocalizer`), `DisplayLanguage`, `Texts.Current` |
| Call | `T("…")`, `T("… {0}", x)`, `Tn("singular", "plural", n, …)` from `LocalizerExtensions` |
| CLI descriptions | `CliTexts` (attributes reference them with `Description = nameof(CliTexts.X)`) and `LocalizedHelpBuilder` |
| Log templates | the English `[LoggerMessage]` texts are the msgids; `LocalizingConsoleFormatter` translates only the console output |

## Rules for developers

- Every human-readable text goes through `T`/`Tn` – always with a **string literal** (no `$"…"`, no constant,
  not the indexer), otherwise `xgettext` does not find it.
- Context for translators as a comment directly before it: `// translators: …`.
- New CLI description = new property in `CliTexts`; a literal in `Description` makes the tests fail.
- Log messages: English template with named placeholders (`{Path}`), its own `EventId` (ranges:
  `DisplayController` 100–199, D-Bus 200–299). Do not log composed sentences via `{Message}`.
- Never translate machine-readable output.

## Workflow after text changes

```fish
po/extract.sh                                  # regenerate .pot, merge into de.po
$EDITOR po/de.po                               # translate new/changed entries
msgfmt --check --statistics -o /dev/null po/de.po
dotnet test                                    # CatalogTests check completeness
```

`po/extract.sh` collects the `T`/`Tn` calls via `xgettext` and the `[LoggerMessage]` templates via reflection
(`po/ExtractLogTemplates.cs`), merges both and updates all `po/*.po` with `msgmerge`.
Requires the gettext tools (`xgettext`, `msgcat`, `msgmerge`, `msgattrib`) and the .NET SDK.

German translation style: neutral and impersonal (no "du"), leave placeholders (`{0}`, `{Path}`) and technical
details unchanged.

### New language

```fish
msginit -l fr -i po/seglcd.pot -o po/fr.po
# translate, then:
po/extract.sh; dotnet test
```

The new file is embedded automatically; `LANG=fr_FR.UTF-8` selects it.

## Man pages

The man pages are in `man/` (English) and `man/de/` (German) and are maintained by hand (roff, no
additional tools needed). `man` selects the language via the same variables (`LANG`/`LC_MESSAGES`).
`tests/…/ManPageTests.cs` checks that both language versions mention every command and every option of `seglcd`, every
key of `config.toml` and every D-Bus method and property of `seglcdd`.

```fish
man -l man/de/man1/seglcd.1            # view without installing
groff -k -man -Tutf8 -ww -z man/de/man1/seglcd.1   # check for warnings
```

## Tests

`tests/…/CatalogTests.cs` ensures that

- the embedded catalog matches the file and both catalogs are valid,
- every entry of the template is translated and not `fuzzy`, and no obsolete entry remains,
- all log templates are in the template and translated,
- the placeholders in the original and the translation match and `msgfmt --check` accepts the catalog,
- `po/seglcd.pot` is up to date (re-run of `xgettext`),
- every CLI description references `CliTexts` and every `CliTexts` property is used.
