#!/usr/bin/env python3
"""Bake production flash/silhouette render data from exact authored alpha.

No actor artwork is painted here. Avoid first-hit CPU atlas scanning in OnGUI.
"""
from pathlib import Path
import hashlib,json
import numpy as np
from PIL import Image
ROOT=Path(__file__).resolve().parents[1]
ART=ROOT/'Assets/Resources/Frontier/Art'

def mask_images(image):
    alpha=np.asarray(image.convert('RGBA'))[:,:,3]
    flash=np.full((*alpha.shape,4),255,dtype=np.uint8);flash[:,:,3]=alpha
    a=alpha.astype(np.int16);h,w=a.shape;edge=np.zeros_like(a)
    center=a[2:h-2,2:w-2]
    for dy,dx in ((0,-2),(0,2),(-2,0),(2,0),(-1,-1),(-1,1),(1,-1),(1,1)):
        edge[2:h-2,2:w-2]=np.maximum(edge[2:h-2,2:w-2],a[2+dy:h-2+dy,2+dx:w-2+dx]-center)
    outline=np.full((*alpha.shape,4),255,dtype=np.uint8)
    outline[:,:,3]=np.where((a<=153)&(edge>102),230,0)
    return {'flash':Image.fromarray(flash),'outline':Image.fromarray(outline)}

def main():
    folder=ART/'ActorMasks';folder.mkdir(exist_ok=True);records=[]
    for spec in json.loads((ROOT/'docs/ACTOR-REGISTRATION.json').read_text())['atlases']:
        name=spec['name'];source=ART/(name+'.png');image=Image.open(source).convert('RGBA')
        for kind,mask in mask_images(image).items():
            path=folder/(kind+'_'+name+'.png');temp=path.with_suffix('.stage.png');mask.save(temp)
            with Image.open(temp) as check:check.verify()
            temp.replace(path)
            records.append({'source':name,'kind':kind,'dimensions':list(mask.size),'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
    (ROOT/'docs/ACTOR-MASKS.json').write_text(json.dumps(records,indent=2)+'\n')
    print(f'Baked {len(records)} alpha masks; no production first-hit pixel loops.')

if __name__=='__main__':main()
