#!/usr/bin/env python3
"""Assemble the recorded production GUI/Core timeline into an offline QA GIF.

This composites screenshots, not artwork, and is not a native Unity recording.
Run run_layout_checks.py first to generate the actual command/time records.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
from render_layout import ROOT, render
import sys,json
from concurrent.futures import ProcessPoolExecutor

def render_frames(keys):
    if '--reuse-recorded-frames' in sys.argv:
        for key in keys:
            image=ROOT/'.starfall-checks/qa-previews'/(key+'.png');record=ROOT/'.starfall-checks/layout'/(key+'.json')
            if not image.exists() or image.stat().st_mtime<record.stat().st_mtime:raise ValueError('Missing or stale captured frame: '+key)
        return
    # Independent captures only; preserve order when composing the timeline.
    with ProcessPoolExecutor(max_workers=4) as pool:
        for overflows in pool.map(render,keys):
            if overflows:raise ValueError('A motion capture contains clipped text')


def build(prefix, names, count, target_name, description, pattern='qa-motion-{prefix}-{vocation}-{frame:02d}'):
    font = ImageFont.truetype(str(ROOT/'Assets/Resources/Frontier/Fonts/NotoSansSC-Regular.otf'), 17)
    render_frames([pattern.format(prefix=prefix,vocation=vocation,frame=frame) for frame in range(count) for vocation in range(len(names))])
    frames = []
    for frame in range(count):
        canvas = Image.new('RGB', (1245, 318), '#101e2d')
        draw = ImageDraw.Draw(canvas)
        for vocation, name in enumerate(names):
            key = pattern.format(prefix=prefix,vocation=vocation,frame=frame)
            source = Image.open(ROOT/'.starfall-checks/qa-previews'/f'{key}.png').convert('RGB')
            canvas.paste(source.crop((440, 245, 855, 515)), (vocation*415, 24))
            draw.text((vocation*415+12, 2), name, font=font, fill='#f2ead7')
        draw.text((12, 296), 'OFFLINE QA / production Core + recorded GUI / ' + description, font=font, fill='#c2d9d2')
        frames.append(canvas)
    target = ROOT/'docs/previews'/target_name
    frames[0].save(target, save_all=True, append_images=frames[1:], duration=40, loop=0, disposal=2, optimize=True)
    print(f'{target.relative_to(ROOT)}: {count} frames, 40 ms each; offline QA only', flush=True)


def main():
    if '--continuity-only' not in sys.argv:
        build('class', ['Vanguard', 'Ranger', 'Arcanist'], 50, 'classes-motion.gif', 'basic attack, skill, dodge')
        build('companion', ['Mira', 'Ivo', 'Sen'], 40, 'companions-motion.gif', 'walk, prepare, command release')
    build('continuity', ['Vanguard', 'Ranger', 'Arcanist'], 64, 'continuity-motion.gif', 'walk, contact, recovery, North Q, walk', 'qa-continuity-motion-{vocation}-{frame:02d}')
    font=ImageFont.truetype(str(ROOT/'Assets/Resources/Frontier/Fonts/NotoSansSC-Regular.otf'),15)
    frames=[]
    keys=sorted((ROOT/'.starfall-checks/layout').glob('qa-continuity-bossfight-*.json'))
    render_frames([p.stem for p in keys])
    for path in keys:
        image=Image.open(ROOT/'.starfall-checks/qa-previews'/(path.stem+'.png')).convert('RGB').resize((800,500),Image.Resampling.NEAREST)
        draw=ImageDraw.Draw(image);draw.rectangle((0,474,800,500),fill='#101e2d');draw.text((12,477),'OFFLINE QA / production Core + recorded GUI / full forest Boss fight',font=font,fill='#e4e0cc');frames.append(image)
    if frames:
        # The living fight is sampled at 0.16s; the retained death at 0.04s.
        durations=json.loads((ROOT/'.starfall-checks/continuity-boss-timeline.json').read_text())
        if len(durations)!=len(frames):raise ValueError('Boss frames and real capture timing disagree')
        frames[0].save(ROOT/'docs/previews/continuity-boss-fight.gif',save_all=True,append_images=frames[1:],duration=durations,loop=0,disposal=2,optimize=True)
        print(f'docs/previews/continuity-boss-fight.gif: {len(frames)} offline recorded frames',flush=True)


if __name__ == '__main__':
    main()
