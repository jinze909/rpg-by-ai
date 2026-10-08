#!/usr/bin/env python3
"""Slice authored atlas cells and standardize technical import size/foot anchors.

Does not paint, invent frames, replace colors, or generate artwork. Source images
remain in Assets/ArtSource/Frontier; runtime atlases use uncompressed Point import.
"""
from pathlib import Path
import json, shutil
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT/'Assets/ArtSource/Frontier'
OUTPUT = ROOT/'Assets/Resources/Frontier/Art'

def save_atlas(image, name):
    """Publish a complete, decoded PNG; interrupted writes retain the old atlas."""
    stage=ROOT/'.starfall-checks/atlas-staging';stage.mkdir(parents=True,exist_ok=True)
    temporary=stage/(name+'.png');image.save(temporary)
    with Image.open(temporary) as checked:checked.verify()
    OUTPUT.mkdir(parents=True,exist_ok=True);temporary.replace(OUTPUT/(name+'.png'))

def isolated_actor_cells(source, columns, rows):
    """Separate overlapping atlas rectangles without repainting source pixels.

    Rectangular grid cuts cannot isolate a long spear that overlaps a neighbour's
    bounding box. Alpha watershed follows the transparent valleys instead. The
    seed's connected opaque body bounds exclude distant background speckles.
    """
    import numpy as np
    from scipy import ndimage
    rgba=np.asarray(source);alpha=rgba[:,:,3];height,width=alpha.shape
    distance=ndimage.distance_transform_edt(alpha>=96)
    markers=np.zeros(alpha.shape,dtype=np.int32);seeds=[]
    for row in range(rows):
        for col in range(columns):
            x0=round((col+.12)*width/columns);x1=round((col+.88)*width/columns)
            y0=round((row+.17)*height/rows);y1=round((row+.9)*height/rows)
            block=distance[y0:y1,x0:x1];dy,dx=np.unravel_index(np.argmax(block),block.shape)
            seed=(y0+dy,x0+dx);seeds.append(seed);markers[seed]=row*columns+col+1
    labels=ndimage.watershed_ift(np.ascontiguousarray(255-alpha),markers)
    cells=[];bounds=[];roots=[]
    for index,seed in enumerate(seeds):
        assigned=labels==index+1
        connected,_=ndimage.label(assigned & (alpha>=64),np.ones((3,3)))
        core=connected[seed]
        if not core: raise ValueError(f'Actor pose has no opaque body seed: {index}')
        ys,xs=np.where(connected==core)
        box=(max(0,int(xs.min())-8),max(0,int(ys.min())-8),min(width,int(xs.max())+9),min(height,int(ys.max())+9))
        pixels=np.array(rgba[box[1]:box[3],box[0]:box[2]])
        pixels[~assigned[box[1]:box[3],box[0]:box[2]]]=0
        cell=Image.fromarray(pixels)
        opaque=cell.getchannel('A').point(lambda a:255 if a>=32 else 0).getbbox()
        cell=cell.crop(opaque)
        box=(box[0]+opaque[0],box[1]+opaque[1],box[0]+opaque[2],box[1]+opaque[3])
        cells.append(cell);bounds.append(box)
        roots.append((int(seed[1])-box[0],cell.height))
    return [cells[row*columns:row*columns+columns] for row in range(rows)],bounds,roots

def humanoid_anchor(cell, prior_x=None):
    """Estimate boot contact from warm footwear; recorded for manual review."""
    import numpy as np
    rgba=np.asarray(cell);r,g,b,a=[rgba[:,:,i].astype(float) for i in range(4)]
    height,width=a.shape
    warm=(a>160)&(r>g*1.12)&(r>b*1.4)&(g>b*1.08)&(g<180)
    warm[:round(height*.68),:]=False
    prior=width/2 if prior_x is None else prior_x
    warm[:,:max(0,round(prior-width*.42))]=False
    warm[:,min(width,round(prior+width*.42)):]=False
    counts=warm.sum(axis=1);rows=np.flatnonzero(counts>=max(4,round(width*.035)))
    if not len(rows): return (width/2,height)
    foot=int(rows[-1])+1
    yy,xx=np.where(warm & (np.arange(height)[:,None]>=foot-max(8,round(height*.16))))
    if not len(xx): return (width/2,height)
    center=float((np.percentile(xx,10)+np.percentile(xx,90))/2)
    return center,foot

def hero_body_height(cell,foot):
    import numpy as np
    from scipy import ndimage
    rgba=np.asarray(cell);r,g,b,a=[rgba[:,:,i].astype(float) for i in range(4)]
    hair=(a>128)&(r>170)&(g>140)&(b>75)&(r>g+5)&(g>b+20)
    hair[round(cell.height*.57):,:]=False
    labels,n=ndimage.label(hair,np.ones((3,3)))
    if not n: return cell.height
    counts=np.bincount(labels.ravel());counts[0]=0;main=int(np.argmax(counts))
    ys,xs=np.where(labels==main)
    head=max(0,int(ys.min())-3)
    return max(1,foot[1]-head)

def grid_cells(source, columns, rows):
    # Generated sheets can have slightly uneven transparent gutters. Cut at
    # actual gutters so extended weapons do not get clipped on an assumed grid.
    alpha=source.getchannel('A').point(lambda a:255 if a>=32 else 0)
    def cuts(count,length,axis):
        projection=list(alpha.resize((length,1) if axis=='x' else (1,length),Image.Resampling.BOX).get_flattened_data())
        result=[0]
        for index in range(1,count):
            ideal=round(index*length/count);radius=round(length/count*.24)
            chosen=min(range(max(result[-1]+1,ideal-radius),min(length,ideal+radius+1)),key=lambda n:(projection[n],abs(n-ideal)))
            result.append(chosen)
        return result+[length]
    xs=cuts(columns,source.width,'x');ys=cuts(rows,source.height,'y')
    cells=[];source_bounds=[];anchors=[]
    for row in range(rows):
        line=[]
        for col in range(columns):
            box=(xs[col],ys[row],xs[col+1],ys[row+1])
            cell=source.crop(box)
            bounds=cell.getchannel('A').point(lambda a: 255 if a>=32 else 0).getbbox()
            if not bounds: raise ValueError(f'Empty authored grid cell: {row} {col}')
            line.append(cell.crop(bounds));source_bounds.append((box[0]+bounds[0],box[1]+bounds[1],box[0]+bounds[2],box[1]+bounds[3]))
        cells.append(line)
    return cells,source_bounds,xs,ys

def archer_body_height(cell, foot):
    import numpy as np
    rgba=np.asarray(cell);r,g,b,a=[rgba[:,:,i].astype(float) for i in range(4)]
    hood=(a>128)&(r>g*1.6)&(r>b*1.2)&(r>70)
    hood[round(cell.height*.65):,:]=False
    rows=np.flatnonzero(hood.sum(axis=1)>=max(4,round(cell.width*.035)))
    return max(1,foot[1]-int(rows[0])) if len(rows) else cell.height

def pack(name, columns, rows, heights=None, fit=False):
    source = Image.open(SOURCE/(name+'.png')).convert('RGBA')
    cells,source_bounds,xs,ys=grid_cells(source,columns,rows);anchors=[]
    companion=name.startswith('Companion')
    actor=name.startswith('Player') or companion or name in ('Bosses','Monsters','Npcs','EnemyArcherVertical')
    if actor:
        cells,source_bounds,roots=isolated_actor_cells(source,columns,rows)
        if name=='Bosses':anchors=roots
    frame_repairs=ROOT/'docs/FRAME-ART-REPAIRS.json'
    reactions=json.loads(frame_repairs.read_text()).get('reaction_import',{}) if frame_repairs.exists() else {}
    if name in reactions:
        rule=reactions[name];mirrors=rule.get('mirrorRows',[rule.get('mirror',False)]*rows)
        cells=[[cell.transpose(Image.Transpose.FLIP_LEFT_RIGHT) if mirrors[row] else cell for cell in line] for row,line in enumerate(cells)]
    original_first_heights=[line[0].height for line in cells]
    original_cell_heights=[cell.height for line in cells for cell in line]
    scale_reference=cells[0][0]
    repairs=ROOT/'docs/POSE-REPAIRS.json'
    repair_specs=json.loads(repairs.read_text()).get(name,{}) if repairs.exists() else {}
    overrides={}
    for index,spec in repair_specs.items():
        image=Image.open(SOURCE/(spec['source']+'.png')).convert('RGBA')
        repaired,boxes,_,_=grid_cells(image,spec['columns'],spec['rows'])
        if spec['source'].startswith('Companion'):
            repaired,boxes,_=isolated_actor_cells(image,spec['columns'],spec['rows'])
        col=spec['cell']%spec['columns'];row=spec['cell']//spec['columns']
        cell=repaired[row][col];cells[int(index)//columns][int(index)%columns]=cell
        if name=='Bosses':anchors[int(index)]=(cell.width/2,cell.height)
        overrides[int(index)]={**spec,'sourceBounds':boxes[spec['cell']]}
    hero=name.startswith('Player')
    vertical_archer=name=='EnemyArcherVertical'
    cell_size=320 if name=='Bosses' else 192 if hero or vertical_archer or companion else 128
    foot=round(cell_size*120/128)
    atlas=Image.new('RGBA',(columns*cell_size,rows*cell_size))
    report=[]
    if not anchors:
        for row,line in enumerate(cells):
            for col,cell in enumerate(line):
                death=hero and ((name not in ('PlayerRear','PlayerSouth') and row*columns+col==11) or (name.startswith('PlayerReactions') and col==3))
                anchors.append((cell.width/2,cell.height) if death or fit else humanoid_anchor(cell) if hero or vertical_archer or companion or name=='Npcs' else (cell.width/2,cell.height))
    for row,line in enumerate(cells):
        target=heights[row] if heights else 94
        first_row=row if name in ('PlayerRear','PlayerSouth') or name.startswith('PlayerReactions') else 0
        first_col=1 if name.startswith('PlayerReactions') else 0
        ratio=92/hero_body_height(cells[first_row][first_col],anchors[first_row*columns+first_col]) if hero else target/scale_reference.height if companion else 94/archer_body_height(line[0],anchors[row*columns]) if vertical_archer else target/original_first_heights[row]
        scales=[ratio]*columns
        for col,cell in enumerate(line):
            if row*columns+col in overrides:
                scales[col]=92/hero_body_height(cell,anchors[row*columns+col]) if hero else ratio*original_cell_heights[row*columns+col]/cell.height if name=='Bosses' else target/cell.height
                spec=overrides[row*columns+col]
                if 'reference_cell' in spec:
                    reference,_,_,_=grid_cells(Image.open(SOURCE/(spec['source']+'.png')).convert('RGBA'),spec['columns'],spec['rows'])
                    ref=reference[spec['reference_cell']//spec['columns']][spec['reference_cell']%spec['columns']]
                    scales[col]=94/archer_body_height(ref,humanoid_anchor(ref))
        if not fit:
            limits=[]
            for col,cell in enumerate(line):
                ax,ay=anchors[row*columns+col]
                limits.append(min((cell_size/2-4)/max(ax,cell.width-ax),(foot-4)/max(1,ay),(cell_size-foot-2)/max(1,cell.height-ay)))
            if not hero and not vertical_archer and not companion:
                common=min([ratio]+[limit for col,limit in enumerate(limits) if row*columns+col not in overrides])
                if name=='Bosses':
                    scales=[common*original_cell_heights[row*columns+col]/line[col].height if row*columns+col in overrides else common for col in range(columns)]
                    if any(scale>limit+1e-6 for scale,limit in zip(scales,limits)):raise ValueError('Boss repair cannot fit without changing anatomical density: '+name+':'+str(row))
                else:scales=[min(scales[col],limits[col]) if row*columns+col in overrides else common for col in range(columns)]
            else:scales=[min(scale,limits[col]) for col,scale in enumerate(scales)]
        for col,cell in enumerate(line):
            scale=min(116/cell.width,112/cell.height) if fit else scales[col]
            size=(max(1,round(cell.width*scale)),max(1,round(cell.height*scale)))
            cell=cell.resize(size,Image.Resampling.NEAREST)
            ax,ay=anchors[row*columns+col]
            x=col*cell_size+round(cell_size/2-ax*scale);y=row*cell_size+((cell_size-size[1])//2 if name in ("Effects","Icons") else round(foot-ay*scale))
            atlas.alpha_composite(cell,(x,y))
            report.append({'cell':row*columns+col,'width':size[0],'height':size[1],'foot':foot,'sourceFoot':anchors[row*columns+col],'scale':scale,'sourceBounds':source_bounds[row*columns+col],'repair':overrides.get(row*columns+col)})
    save_atlas(atlas,name)
    return {'name':name,'columns':columns,'rows':rows,'cellSize':cell_size,'sourceSize':source.size,'sourceColumnCuts':None if actor else xs,'sourceRowCuts':None if actor else ys,'segmentation':'alpha watershed / connected body bounds' if actor else 'transparent grid gutters','frames':report}

def main():
    portrait=SOURCE/'PlayerPortraits.png'
    if portrait.exists():save_atlas(Image.open(portrait),'PlayerPortraits')
    specs={'PlayerVanguard':(4,3,None,False),'PlayerRanger':(4,3,None,False),'PlayerArcanist':(4,3,None,False),'PlayerRear':(4,3,None,False),'PlayerSouth':(4,3,[116]*3,False),
           'Monsters':(6,3,[64,94,102],False),'EnemyArcherVertical':(3,2,[94]*2,False),'Bosses':(4,8,[255]*8,False),
           'CompanionMira':(3,4,[94]*4,False),'CompanionIvo':(3,4,[94]*4,False),'CompanionSen':(3,4,[104]*4,False),
           'Npcs':(4,4,[94]*4,False),'FieldProps':(4,4,None,True),'Details':(4,4,None,True),
           'Effects':(4,4,None,True),'Icons':(4,4,None,True)}
    records=[]
    for name,spec in specs.items():
        if (SOURCE/(name+'.png')).exists(): records.append(pack(name,*spec))
    for name,columns,rows,trim in [('Terrain',8,2,0),('TerrainVariants',4,4,2)]:
        terrain=SOURCE/(name+'.png')
        if not terrain.exists(): continue
        source=Image.open(terrain).convert('RGB');atlas=Image.new('RGB',(columns*48,rows*48))
        for row in range(rows):
            for col in range(columns):
                box=(round(col*source.width/columns)+trim,round(row*source.height/rows)+trim,round((col+1)*source.width/columns)-trim,round((row+1)*source.height/rows)-trim)
                cell=source.crop(box);atlas.paste(cell.resize((48,48),Image.Resampling.NEAREST),(col*48,row*48))
        save_atlas(atlas,name)
        records.append({'name':name,'columns':columns,'rows':rows,'cellSize':48,'sourceSize':source.size,'trim':trim})
    report=ROOT/'docs/ATLAS-IMPORT.json';temporary=report.with_suffix('.json.tmp')
    temporary.write_text(json.dumps(records,ensure_ascii=False,indent=2)+'\n');temporary.replace(report)
    print(f'Packed and decoded {len(records)} atlases; all outputs complete.')
    from register_actor_frames import main as register
    register()
if __name__=='__main__': main()
