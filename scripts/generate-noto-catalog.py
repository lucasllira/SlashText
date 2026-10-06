"""Generate the checked-in offline manifest from reviewed upstream snapshots.

Usage: python scripts/generate-noto-catalog.py TREE ORDER UNICODE PT PT_DERIVED LICENSE FLAGS_LICENSE SOURCE_CHECKOUT
Asset bytes are acquired and verified by restore-noto-emoji.ps1 at build time.
"""
import hashlib
import json
from pathlib import Path
import re
import sys

tree_path, order_path, unicode_path, pt_path, derived_path, license_path, flags_license_path, source_checkout = map(Path, sys.argv[1:])
tree = json.loads(tree_path.read_text())
tree_index = {item["path"]: item for item in tree["tree"]}
assets = {}
for item in tree["tree"]:
    path = item["path"]
    if path.startswith("third_party/region-flags/png/") and path.endswith(".png"):
        code = Path(path).stem
        if len(code) == 2 and code.isupper():
            assets[tuple(ord(c) - ord('A') + 0x1f1e6 for c in code)] = item
        elif code in ("GB-ENG", "GB-SCT", "GB-WLS"):
            assets[(0x1f3f4, *(ord(c) + 0xe0000 for c in code.replace('-', '').lower()), 0xe007f)] = item
        continue
    if not path.startswith("2D/png/128/") or not path.endswith(".png"):
        continue
    name = Path(path).name
    codes = tuple(int(c, 16) for c in re.findall(r"[0-9a-fA-F]+", name.removeprefix("emoji_").replace(".png", "")) if int(c, 16) != 0xfe0f)
    # Prefer standard filenames over legacy aliases of the same sequence.
    if codes not in assets or name.startswith("emoji_"):
        assets[codes] = item

names = {}
group = "Outros"
for line in unicode_path.read_text().splitlines():
    if line.startswith("# group: "):
        group = line[9:]
    match = re.match(r"([0-9A-F ]+);\s*(fully-qualified|component)\s*#\s*(\S+)\s+E[0-9.]+\s+(.+)", line)
    if match:
        key = tuple(int(c, 16) for c in match[1].split() if int(c, 16) != 0xfe0f)
        names[key] = (match[3], match[4], group)

pt = {}
for file in (pt_path, derived_path):
    doc = json.loads(file.read_text())
    body = doc.get("annotations", doc.get("annotationsDerived"))
    for text, annotations in body["annotations"].items():
        key = tuple(ord(c) for c in text if ord(c) != 0xfe0f)
        pt[key] = annotations

categories = {"Smileys and emotions": "Rostos e emoções", "People": "Pessoas e gestos",
              "Animals and nature": "Animais e natureza", "Food and drink": "Comidas e bebidas",
              "Travel and places": "Viagens e lugares", "Activities and events": "Atividades",
              "Objects": "Objetos", "Symbols": "Símbolos", "Flags": "Bandeiras"}
unicode_categories = {"Smileys & Emotion": "Rostos e emoções", "People & Body": "Pessoas e gestos",
                      "Animals & Nature": "Animais e natureza", "Food & Drink": "Comidas e bebidas",
                      "Travel & Places": "Viagens e lugares", "Activities": "Atividades",
                      "Objects": "Objetos", "Symbols": "Símbolos", "Flags": "Bandeiras", "Component": "Pessoas e gestos"}
items = []
seen = set()

def add(codes, category, keywords=()):
    key = tuple(c for c in codes if c != 0xfe0f)
    if key in seen or key not in assets:
        return
    asset = assets[key]
    source = asset
    while source.get("mode") == "120000":
        link = source_checkout / source["path"]
        target = str(link.readlink()) if link.is_symlink() else link.read_text().strip()
        link_bytes = target.encode()
        assert hashlib.sha1(b"blob " + str(len(link_bytes)).encode() + b"\0" + link_bytes).hexdigest() == source["sha"]
        # Noto's flag aliases are relative sibling links. Resolve at generation,
        # since Windows Git may checkout a symlink as a text file rather than PNG.
        assert "/" not in target and ".." not in target
        source = tree_index[str(Path(source["path"]).parent / target)]
    localized = pt.get(key, {})
    unicode_entry = names.get(key)
    name = localized.get("tts", [unicode_entry[1] if unicode_entry else "Emoji " + " ".join(f"U+{c:X}" for c in key)])[0]
    value = unicode_entry[0] if unicode_entry else "".join(chr(c) for c in codes)
    items.append({"Value": value, "Name": name, "AssetName": Path(asset["path"]).name,
                  "Category": category, "Keywords": " ".join([*localized.get("default", []), *keywords,
                         unicode_entry[1] if unicode_entry else "", " ".join(f"U+{c:X}" for c in key)]),
                  "SourcePath": source["path"], "BlobSha": source["sha"]})
    seen.add(key)

for section in json.loads(order_path.read_text()):
    for item in section["emoji"]:
        for codes in [item["base"], *item.get("alternates", [])]:
            add(codes, categories[section["group"]], item.get("shortcodes", []) + item.get("emoticons", []))
for key, (value, name, group) in names.items():
    add([ord(c) for c in value], unicode_categories.get(group, "Outros"))
# Preserve older family sequences supported by the pinned assets/CLDR too.
for key in sorted(assets):
    if key in pt and len(key) > 1:
        add(key, "Pessoas e gestos")

assert len(items) > 3600
assert len({x["Value"] for x in items}) == len(items)
root = Path(__file__).resolve().parents[1]
folder = root / "src/SlashText/Assets/NotoEmoji"
manifest = {"Commit": tree["sha"], "AssetPaths": ["2D/png/128", "third_party/region-flags/png"], "Count": len(items),
            "MetadataInputs": {p.name: hashlib.sha256(p.read_bytes()).hexdigest()
                               for p in (order_path, unicode_path, pt_path, derived_path)}, "Items": items}
(folder / "catalog.json").write_text(json.dumps(manifest, ensure_ascii=False, separators=(",", ":")) + "\n")
(folder / "UNICODE-LICENSE.txt").write_bytes(license_path.read_bytes())
(folder / "FLAGS-LICENSE.txt").write_bytes(flags_license_path.read_bytes())
print(f"Generated {len(items)} unique supported Noto emoji, {len(categories)} categories, pinned {tree['sha']}")
