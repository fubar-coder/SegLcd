# Documentation (Hugo + Hextra)

Published at https://fubar-coder.github.io/SegLcd/ (German: `/de/`) – automatically by the GitHub Action
`.github/workflows/docs.yml` on every push to `main` that touches `docs/`.

Requirement: Hugo **extended** ≥ 0.146 (`sudo pacman -S hugo`).

```bash
git submodule update --init          # Hextra theme into docs/themes/hextra
cd docs
hugo server                          # http://localhost:1313/
hugo --minify                        # static site into docs/public/
```

## Languages

English is the primary language, German a translation (Hugo "translation by filename"):

| File | Language | URL |
|---|---|---|
| `content/docs/page.md` | English | `/docs/page/` |
| `content/docs/page.de.md` | German | `/de/docs/page/` |

When changing a page, update both files. German pages show a banner noting that the translation may lag behind.
