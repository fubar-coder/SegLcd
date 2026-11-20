# Dokumentation (Hugo + Hextra)

Voraussetzung: Hugo **extended** ≥ 0.146 (`sudo pacman -S hugo`).

```bash
git submodule update --init          # Hextra-Theme nach docs/themes/hextra
cd docs
hugo server                          # http://localhost:1313/
hugo --minify                        # statische Seite nach docs/public/
```
