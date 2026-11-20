#!/bin/sh
# Regenerates the message template po/seglcd.pot (seglcd and seglcdd) and merges it into every po/<language>.po.
#
# Message ids come from two places:
#   - T(...) and Tn(...) calls in src/ (xgettext; comments starting with "translators:" are passed on),
#   - [LoggerMessage] templates, which xgettext cannot see (po/ExtractLogTemplates.cs, by reflection).
#
# Needs the gettext tools (xgettext, msgcat, msgmerge, msgattrib) and the .NET SDK. Run from anywhere:
#   po/extract.sh
set -eu

cd "$(dirname "$0")/.."
template=po/seglcd.pot
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

find src -name '*.cs' -not -path '*/bin/*' -not -path '*/obj/*' | LC_ALL=C sort > "$work/files"
xgettext --language=C# --from-code=UTF-8 -k -kT -kTn:1,2 --add-comments=translators: \
    --package-name=seglcd --no-wrap \
    --files-from="$work/files" --output="$work/code.pot"

{
    # Same header as xgettext writes, so msgcat can combine both parts.
    sed -n '1,/^$/p' "$work/code.pot"
    dotnet run --file po/ExtractLogTemplates.cs
} > "$work/log.pot"

# No creation date: the template must not change unless a message changes.
msgcat --use-first --no-wrap --sort-output "$work/code.pot" "$work/log.pot" \
    | sed -e '/^"POT-Creation-Date: /d' -e 's/charset=CHARSET/charset=UTF-8/' > "$template"

for catalog in po/*.po; do
    msgmerge --quiet --update --backup=none --no-fuzzy-matching --no-wrap "$catalog" "$template"
    msgattrib --no-obsolete --no-wrap --output-file="$catalog" "$catalog"
done
