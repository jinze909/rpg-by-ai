#!/usr/bin/env python3
"""Extend the existing display font from the licensed, complete Noto SC font.

Usage: python3 tools/build_font_subset.py /path/to/NotoSansSC-Regular.otf
Keep existing glyphs and include every runtime/editor/campaign character.
"""
from pathlib import Path
import sys
from fontTools import subset
from fontTools.ttLib import TTFont
ROOT=Path(__file__).resolve().parents[1]
def main():
    source=Path(sys.argv[1]);target=ROOT/'Assets/Resources/Frontier/Fonts/NotoSansSC-Regular.otf'
    previous=TTFont(target);required=set(previous.getBestCmap());previous.close()
    sources=list((ROOT/'Assets/Scripts').rglob('*.cs'))+list((ROOT/'Assets/Editor').rglob('*.cs'))+[ROOT/'Assets/Resources/Frontier/Campaign.json']
    required.update(ord(c) for p in sources for c in p.read_text() if not c.isspace())
    font=TTFont(source);missing=required-set(font.getBestCmap())
    if missing:raise SystemExit(f'Source font lacks required glyphs: {sorted(missing)}')
    options=subset.Options();options.name_IDs=['*'];options.name_legacy=True;options.name_languages=['*']
    builder=subset.Subsetter(options=options);builder.populate(unicodes=required);builder.subset(font);font.save(target);font.close()
    print(f'Extended licensed Noto SC subset: {len(required)} codepoints retained.')
if __name__=='__main__':main()
