#!/usr/bin/env python3
"""Offline text-fit checks, using approximate font metrics rather than Unity native layout."""
from pathlib import Path
import json
from PIL import ImageFont
from render_layout import wrap_text
ROOT=Path(__file__).resolve().parents[1]
def check():
    fonts={};failures=[];files=sorted((ROOT/'.starfall-checks/layout').glob('*.json'))
    for path in files:
        for c in json.loads(path.read_text()):
            if c['k']!='text' or not c['text']:continue
            size=c['size'] or 16
            if size not in fonts:fonts[size]=ImageFont.truetype(str(ROOT/'Assets/Resources/Frontier/Fonts/NotoSansSC-Regular.otf'),size)
            left,right,top,bottom=c.get('padding',[0,0,0,0]);w=max(1,round(c['r'][2])-left-right);h=max(1,round(c['r'][3])-top-bottom)
            lines=wrap_text(c['text'],fonts[size],w-4);height=len(lines)*round(size*1.35)
            if height>h+round(size*.6):failures.append({'layout':path.stem,'text':c['text'],'estimated_height':height,'available_height':h})
    report={'kind':'offline-approximation','states':len(files),'text_overflows':failures}
    (ROOT/'.starfall-checks/layout-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
    print('Offline layout states:',len(files),'potential text overflows:',len(failures))
    for f in failures[:30]:print(f['layout'],f['estimated_height'],f['available_height'],f['text'][:80])
    return report
if __name__=='__main__':
    result=check();raise SystemExit(1 if result['text_overflows'] else 0)
