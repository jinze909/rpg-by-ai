#!/usr/bin/env python3
"""Register authored anatomy, fixed foot anchors and reusable head pixels.

Only technical isolation/padding/nearest sampling; no illustration is painted.
Public import entries all pass through this complete, staged actor batch.
"""
from pathlib import Path
import hashlib,json
import numpy as np
from scipy import ndimage
from PIL import Image
import pack_visual_atlases as visual
import pack_walk_atlases as walking

ROOT=Path(__file__).resolve().parents[1]
ART=ROOT/'Assets/Resources/Frontier/Art'
STAGE=ROOT/'.starfall-checks/frame-registration'

def head_box(cell,name):
    rgba=np.asarray(cell).astype(float);r,g,b,a=[rgba[:,:,i] for i in range(4)]
    if name.startswith('Player') or name in ('WalkVanguard','WalkRanger','WalkArcanist'):
        mask=(a>128)&(r>170)&(g>140)&(b>75)&(r>g+5)&(g>b+20)
    elif 'Mira' in name:
        mask=(a>128)&(r>110)&(r>g*1.5)&(r>b*1.8)&(g>22);mask[:70,:]=False;mask[139:,:]=False
    elif 'Ivo' in name:
        mask=(a>128)&(r>45)&(r<170)&(g<100)&(r>g*1.3)&(g>b*1.25);mask[:75,:]=False;mask[129:,:]=False
    elif 'Sen' in name:
        mask=(a>128)&(r>155)&(g>135)&(b>110);mask[:78,:]=False;mask[136:,:]=False
    else:raise ValueError('Unknown head profile '+name)
    labels,n=ndimage.label(mask,np.ones((3,3)))
    if not n:raise ValueError('Missing head '+name)
    counts=np.bincount(labels.ravel());counts[0]=0;best=int(np.argmax(counts));ys,xs=np.where(labels==best)
    top=int(ys.min());bottom=int(ys.max())+1
    if any(w in name for w in ('Mira','Ivo','Sen')):
        _,bx=np.where(labels[top:min(bottom,top+28)]==best)
        left,right=int(bx.min()),int(bx.max())+1;bottom=top+(48 if 'Sen' in name else 40)
    else:left,right=int(xs.min()),int(xs.max())+1;bottom+=8
    return max(0,left-3),max(0,top-4),min(cell.width,right+3),min(cell.height,bottom)

def regenerate():
    raw={};records=[];rawdir=STAGE/'raw';rawdir.mkdir(parents=True,exist_ok=True)
    def collect(image,name):
        raw[name]=image.copy().convert('RGBA');path=rawdir/(name+'.stage.png');raw[name].save(path)
        with Image.open(path) as im:im.load()
        path.replace(rawdir/(name+'.png'))
    visual.save_atlas=collect;walking.save_atlas=collect;walking.ART=rawdir
    specs={'PlayerVanguard':(4,3,None,False),'PlayerRanger':(4,3,None,False),'PlayerArcanist':(4,3,None,False),
        'PlayerRear':(4,3,None,False),'PlayerSouth':(4,3,[116]*3,False),
        'Monsters':(6,3,[64,94,102],False),'EnemyArcherVertical':(3,2,[94]*2,False),'Bosses':(4,8,[255]*8,False),
        'CompanionMira':(3,4,[94]*4,False),'CompanionIvo':(3,4,[94]*4,False),'CompanionSen':(3,4,[104]*4,False),
        'PlayerReactionsRear':(4,3,None,False),'PlayerReactionsSouth':(4,3,None,False)}
    for name,args in specs.items():records.append(visual.pack(name,*args))
    walks=[('WalkVanguard',3,'PlayerVanguard',4,3,None,True,False),('WalkRanger',3,'PlayerRanger',4,3,None,True,False),
        ('WalkArcanist',3,'PlayerArcanist',4,3,None,True,False),('WalkMira',3,'CompanionMira',3,4,None,False,True),
        ('WalkIvo',3,'CompanionIvo',3,4,None,False,True),('WalkSen',3,'CompanionSen',3,4,None,False,True),
        ('WalkMonsters',3,'Monsters',6,3,None,False,False),('WalkBossA',4,'Bosses',4,8,[0,1,2,3],False,False),
        ('WalkBossB',4,'Bosses',4,8,[4,5,6,7],False,False)]
    for args in walks:print('Raw gait import',args[0],flush=True);records.append(walking.pack(*args))
    return raw,records

def main():
    STAGE.mkdir(parents=True,exist_ok=True);raw,records=regenerate();heads={};profiles=[];pending={}
    for spec in records:
        name=spec['name'];image=raw[name];old=spec['cellSize'];cols=spec['columns'];rows=spec['rows']
        human=name.startswith(('Player','Companion')) or name in ('WalkVanguard','WalkRanger','WalkArcanist','WalkMira','WalkIvo','WalkSen')
        if not human:pending[name]=image;continue
        size=256;foot=240;atlas=Image.new('RGBA',(cols*size,rows*size));rects=[];frames=[]
        for idx in range(cols*rows):
            cell=image.crop((idx%cols*old,idx//cols*old,(idx%cols+1)*old,(idx//cols+1)*old));box=head_box(cell,name)
            death=(name in ('PlayerVanguard','PlayerRanger','PlayerArcanist') and idx==11) or (name.startswith('PlayerReactions') and idx%cols==3)
            factor=36/max(1,box[3]-box[1]-12) if name.startswith('Player') and not death else 1
            factor=max(.74,min(1.18,factor));moving=name.startswith('Walk');cx=(box[0]+box[2])/2
            dx=round(128-(cx if moving else old/2)*factor);dy=round(foot-old*120/128*factor)
            atlas.alpha_composite(cell.resize((round(old*factor),round(old*factor)),Image.Resampling.NEAREST),(idx%cols*size+dx,idx//cols*size+dy))
            rect=[round(box[0]*factor+dx),round(box[1]*factor+dy),round((box[2]-box[0])*factor),round((box[3]-box[1])*factor)]
            assert min(rect)>0 and rect[0]+rect[2]<size and rect[1]+rect[3]<size,(name,idx,rect)
            bounds=atlas.crop((idx%cols*size,idx//cols*size,(idx%cols+1)*size,(idx//cols+1)*size)).getchannel('A').point(lambda a:255 if a>=32 else 0).getbbox()
            assert bounds and min(bounds)>0 and bounds[2]<size and bounds[3]<size,(name,idx,bounds)
            rects.append(rect);frames.append({**spec['frames'][idx],'registrationScale':factor,'registrationOffset':[dx,dy],'headRect':rect,'foot':foot})
        spec['cellSize']=size;spec['frames']=frames;spec['registration']='fixed anatomy module, foot and torso axis; authored head layer'
        heads[name]=rects;pending[name]=atlas;profiles.append({'name':name,'cellSize':size,'frames':len(rects),'headRects':rects})
    for name,image in pending.items():
        path=STAGE/(name+'.png');image.save(path)
        with Image.open(path) as im:im.verify()
    for name in pending:(STAGE/(name+'.png')).replace(ART/(name+'.png'))
    manifest=ROOT/'docs/ATLAS-IMPORT.json';existing=json.loads(manifest.read_text());names=set(pending)
    manifest.write_text(json.dumps([r for r in existing if r['name'] not in names]+records,ensure_ascii=False,indent=2)+'\n')
    lines=['using System.Collections.Generic;','using UnityEngine;','namespace Starfall {','    // Generated from accepted authored pixels by register_actor_frames.py.','    internal static class ActorFrameRegistration {','        internal static readonly Dictionary<string, Rect[]> Heads = new Dictionary<string, Rect[]> {']
    for name,rects in heads.items():lines.append('            { "'+name+'", new [] { '+', '.join('new Rect('+','.join(str(n) for n in rect)+')' for rect in rects)+' } },')
    lines+=['        };','        internal static bool TryStableHead(Texture2D sheet, int row, out Rect rect) {',
        '            string name=sheet.name; int slash=name.LastIndexOf(\'/\'); if(slash>=0) name=name.Substring(slash+1);',
        '            if(name=="WalkVanguard" || name=="WalkRanger" || name=="WalkArcanist") { rect=new Rect(100,140,56,56); return true; }',
        '            Rect[] all; if(Heads.TryGetValue(name,out all) && name.StartsWith("Walk") && row>=0 && row*4+3<all.Length) {',
        '                float x=256,y=256,right=0,bottom=0; for(int i=row*4;i<row*4+4;i++) { Rect h=all[i]; x=System.Math.Min(x,h.x); y=System.Math.Min(y,h.y); right=System.Math.Max(right,h.x+h.width); bottom=System.Math.Max(bottom,h.y+h.height); }',
        '                rect=new Rect(x,y,right-x,bottom-y); return true;',
        '            } rect=new Rect(); return false;','        }','        internal static bool TryHead(Texture2D sheet, int frame, out Rect rect) {',
        '            string name=sheet.name; int slash=name.LastIndexOf(\'/\'); if(slash>=0) name=name.Substring(slash+1);',
        '            Rect[] all; if(Heads.TryGetValue(name,out all) && frame>=0 && frame<all.Length) { rect=all[frame]; return true; }',
        '            rect=new Rect(); return false;','        }','    }','}']
    (ROOT/'Assets/Scripts/Frontier/Runtime/ActorFrameRegistration.cs').write_text('\n'.join(lines)+'\n')
    (ROOT/'docs/ACTOR-REGISTRATION.json').write_text(json.dumps({'method':'technical import and runtime authored head layer; no repainting','profiles':profiles,'atlases':[{'name':n,'sha256':hashlib.sha256((ART/(n+'.png')).read_bytes()).hexdigest()} for n in pending]},indent=2)+'\n')
    print('Registered',len(pending),'actor atlases;',sum(len(v) for v in heads.values()),'human heads; fixed 256 canvas/240 foot.',flush=True)
    import bake_actor_masks
    bake_actor_masks.main()
    import bake_actor_bounds
    bake_actor_bounds.main()

if __name__=='__main__':main()
