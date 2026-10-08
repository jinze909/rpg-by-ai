#!/usr/bin/env python3
"""Import authored gait extensions; only isolate, scale and anchor source pixels."""
from pathlib import Path
import json
from statistics import median
from PIL import Image
from pack_visual_atlases import isolated_actor_cells, humanoid_anchor, hero_body_height, save_atlas

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT/'Assets/Resources/Frontier/Art'
SOURCE = ROOT/'Assets/ArtSource/Frontier'

def bounds(cell):
    return cell.getchannel('A').point(lambda a: 255 if a >= 96 else 0).getbbox()

def pack(name, rows, reference, reference_cols, reference_rows, first_rows=None, hero=False, companion=False):
    image=Image.open(SOURCE/(name+'.png')).convert('RGBA')
    cells,boxes,roots=isolated_actor_cells(image,4,rows)
    original=Image.open(ART/(reference+'.png')).convert('RGBA')
    size=384 if 'Boss' in name else 192
    foot=round(size*120/128)
    atlas=Image.new('RGBA',(4*size,rows*size));records=[]
    for row,line in enumerate(cells):
        actor_row=(first_rows or list(range(rows)))[row]
        reference_index=0 if hero or companion else actor_row*reference_cols
        w,h=original.width//reference_cols,original.height//reference_rows
        idle=original.crop((reference_index%reference_cols*w,reference_index//reference_cols*h,(reference_index%reference_cols+1)*w,(reference_index//reference_cols+1)*h))
        target=bounds(idle)[3]-bounds(idle)[1]
        heights=[bounds(cell)[3]-bounds(cell)[1] for cell in line]
        common=target/median(heights)
        for col,cell in enumerate(line):
            anchor=humanoid_anchor(cell) if hero or companion else roots[row*4+col]
            # A constant canonical body height removes generator framing drift.
            # This is import-time normalization; runtime never scales poses.
            ratio=92/hero_body_height(cell,anchor) if hero else common
            limit=min((size/2-3)/max(anchor[0],cell.width-anchor[0]),(foot-3)/max(1,anchor[1]),(size-foot-2)/max(1,cell.height-anchor[1]))
            if ratio>limit+1e-6: raise ValueError(f'{name}:{row*4+col} weapon/foot cannot fit the fixed canvas')
            resized=cell.resize((max(1,round(cell.width*ratio)),max(1,round(cell.height*ratio))),Image.Resampling.NEAREST)
            x=col*size+round(size/2-anchor[0]*ratio); y=row*size+round(foot-anchor[1]*ratio)
            atlas.alpha_composite(resized,(x,y))
            records.append({'cell':row*4+col,'foot':foot,'scale':ratio,'sourceFoot':anchor,'sourceBounds':boxes[row*4+col],'width':resized.width,'height':resized.height})
    save_atlas(atlas,name)
    return {'name':name,'columns':4,'rows':rows,'cellSize':size,'sourceSize':image.size,'segmentation':'alpha watershed / connected body bounds','frames':records,'usage':'distance-driven locomotion; no runtime scale animation'}

def main():
    from register_actor_frames import main as register
    register()

if __name__=='__main__': main()
