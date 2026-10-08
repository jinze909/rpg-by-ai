#!/usr/bin/env python3
"""Re-run the PUBLIC import entry in isolation; reject animation/mask drift."""
from pathlib import Path
import hashlib,json,shutil,subprocess,tempfile
ROOT=Path(__file__).resolve().parents[1]
def main():
    names=[v['name'] for v in json.loads((ROOT/'docs/ACTOR-REGISTRATION.json').read_text())['atlases']]
    with tempfile.TemporaryDirectory(prefix='actor-reimport-',dir=ROOT/'.starfall-checks') as temp:
        copy=Path(temp)
        for p in ('tools','docs','Assets/ArtSource/Frontier','Assets/Resources/Frontier/Art','Assets/Scripts/Frontier/Runtime'):(copy/p).mkdir(parents=True,exist_ok=True)
        for name in ('pack_visual_atlases.py','pack_walk_atlases.py','register_actor_frames.py','bake_actor_masks.py','bake_actor_bounds.py'):shutil.copy2(ROOT/'tools'/name,copy/'tools'/name)
        for name in ('ATLAS-IMPORT.json','POSE-REPAIRS.json','FRAME-ART-REPAIRS.json'):shutil.copy2(ROOT/'docs'/name,copy/'docs'/name)
        for name in names:
            for folder in ('Assets/ArtSource/Frontier','Assets/Resources/Frontier/Art'):shutil.copy2(ROOT/folder/(name+'.png'),copy/folder/(name+'.png'))
        for entries in json.loads((ROOT/'docs/POSE-REPAIRS.json').read_text()).values():
            for entry in entries.values():
                name=entry['source']+'.png';shutil.copy2(ROOT/'Assets/ArtSource/Frontier'/name,copy/'Assets/ArtSource/Frontier'/name)
        subprocess.run(['python3',str(copy/'tools/pack_walk_atlases.py')],check=True,cwd=copy,stdout=subprocess.DEVNULL)
        files=['Assets/Resources/Frontier/Art/'+name+'.png' for name in names]
        files+=['Assets/Resources/Frontier/Art/ActorMasks/'+kind+'_'+name+'.png' for name in names for kind in ('flash','outline')]
        files+=['docs/ACTOR-REGISTRATION.json','docs/ACTOR-MASKS.json','docs/ACTOR-VISIBILITY.json','Assets/Scripts/Frontier/Runtime/ActorFrameRegistration.cs','Assets/Scripts/Frontier/Runtime/ActorVisibilityBounds.cs']
        for file in files:
            if (ROOT/file).read_bytes()!=(copy/file).read_bytes():raise ValueError('Public importer changes registered pixels: '+file)
        print(f'PASS Public reimport: {len(names)} actor atlases, {len(names)*2} masks, anatomy and visibility registries are byte-identical.')
if __name__=='__main__':main()
