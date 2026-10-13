#!/usr/bin/env python3
"""Check asset/meta pairs and GUID syntax; this is not a native Unity import."""
from pathlib import Path
import argparse
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
GUID_LINE = re.compile(r"^guid:[ \t]*(.*)$", re.MULTILINE)
GUID_TOKEN = re.compile(r"[0-9a-fA-F]{32}")


def validate_assets(assets):
    """Return explicit errors without changing metadata or generating GUIDs."""
    assets = Path(assets)
    if not assets.is_dir():
        return [f"Assets directory missing: {assets}"]
    errors = []
    guids = {}
    paths = sorted(assets.rglob("*"))
    for path in paths:
        if path.suffix != ".meta":
            if not path.with_name(path.name + ".meta").is_file():
                errors.append(f"Missing metadata: {path}")
            continue
        if not path.is_file():
            errors.append(f"Metadata is not a file: {path}")
            continue
        if not path.with_suffix("").exists():
            errors.append(f"Orphan metadata: {path}")
        try:
            text = path.read_text(encoding="utf-8")
        except (OSError, UnicodeError) as ex:
            errors.append(f"Unreadable metadata: {path}: {ex}")
            continue
        matches = GUID_LINE.findall(text)
        if len(matches) != 1:
            errors.append(f"Expected one top-level GUID, found {len(matches)}: {path}")
            continue
        guid = matches[0].strip()
        if not GUID_TOKEN.fullmatch(guid) or guid == "0" * 32:
            errors.append(f"Invalid GUID (expected nonzero 32 hex characters): {path}: {guid!r}")
            continue
        normalized = guid.lower()
        if normalized in guids:
            errors.append(f"Duplicate GUID: {path} and {guids[normalized]}: {guid}")
        else:
            guids[normalized] = path
    return errors


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--assets", type=Path, default=ROOT / "Assets")
    args = parser.parse_args(argv)
    errors = validate_assets(args.assets)
    if errors:
        for error in errors:
            print(error, file=sys.stderr)
        return 1
    count = len(list(args.assets.rglob("*.meta")))
    print(f"PASS {count} asset/meta pairs; nonzero 32-hex GUIDs are unique. Native import not executed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
