#!/usr/bin/env python3
"""Render recorded OnGUI calls for offline layout QA; does not emulate Unity gameplay or text metrics."""
from pathlib import Path
import json,sys,math
from PIL import Image,ImageDraw,ImageFont,ImageChops

ROOT=Path(__file__).resolve().parents[1]
def wrap_text(text,f,width):
    result=[]
    for para in text.split('\n'):
        current=''
        for char in para:
            if current and f.getlength(current+char)>width:
                split=current.rfind(' ')
                if split>0:
                    result.append(current[:split].rstrip());current=current[split+1:]+char
                else:
                    result.append(current.rstrip());current=char.lstrip()
            else:current+=char
        result.append(current)
    return result

def render(name):
    folder=ROOT/'.starfall-checks/layout'
    commands=json.loads((folder/(name+'.json')).read_text())
    viewport=tuple(commands[0].get('screen',[1280,800])) if commands else (1280,800)
    canvas=Image.new('RGBA',viewport,(14,26,32,255));textures={};fonts={};overflows=[]
    def font(size):
        if size not in fonts:fonts[size]=ImageFont.truetype(str(ROOT/'Assets/Resources/Frontier/Fonts/NotoSansSC-Regular.otf'),size or 16)
        return fonts[size]
    def composite(im,rect,m):
        # Apply the actual recorded canvas and local rotation. The production
        # renderer uses uniform 2D scale, rotation and translation; reject shear
        # rather than silently manufacturing a misleading preview.
        a,b,tx,c,d,ty=m;x,y,w,h=rect
        sx=math.hypot(a,c);sy=math.hypot(b,d)
        if abs(a*b+c*d)>1e-4 or a*d-b*c<=0:raise ValueError('Unsupported GUI shear or reflection')
        im=im.resize((max(1,round(w*sx)),max(1,round(h*sy))),Image.Resampling.NEAREST)
        angle=math.degrees(math.atan2(c,a))
        if abs(angle)>1e-5:im=im.rotate(-angle,resample=Image.Resampling.NEAREST,expand=True)
        cx=a*(x+w/2)+b*(y+h/2)+tx;cy=c*(x+w/2)+d*(y+h/2)+ty
        canvas.alpha_composite(im,(round(cx-im.width/2),round(cy-im.height/2)))
    for c in commands:
        x,y,logical_w,logical_h=c['r'];w=max(1,round(logical_w));h=max(1,round(logical_h))
        matrix=c.get('m',[1,0,0,0,1,0])
        color=tuple(int(max(0,min(1,a))*255) for a in c['color'])
        if c['k']=='texture':
            if c['name']=='white':im=Image.new('RGBA',(w,h),color)
            else:
                key=c['name']
                if key not in textures:
                    if key.startswith('resource:'):textures[key]=Image.open(ROOT/('Assets/Resources/'+key.split(':',1)[1]+'.png')).convert('RGBA')
                    elif key=='hero':textures[key]=Image.open(ROOT/'Assets/Resources/Frontier/ViolaMoves.png').convert('RGBA')
                    elif key=='titlevista':textures[key]=Image.open(ROOT/'Assets/Resources/Frontier/Art/TitleVista.png').convert('RGBA')
                    elif key=='worldprops':textures[key]=Image.open(ROOT/'Assets/Resources/Frontier/Art/WorldProps.png').convert('RGBA')
                    elif key=='portraits':textures[key]=Image.open(ROOT/'Assets/Resources/Frontier/Art/Portraits.png').convert('RGBA')
                    else:
                        size=tuple(map(int,(folder/(key+'.size')).read_text().split(',')))
                        textures[key]=Image.frombytes('RGBA',size,(folder/(key+'.rgba')).read_bytes()).transpose(Image.Transpose.FLIP_TOP_BOTTOM)
                source=textures[key];u,v,uw,vh=c['uv']
                box=(round(u*source.width),round((1-v-vh)*source.height),round((u+uw)*source.width),round((1-v)*source.height))
                if uw<0:
                    box=(box[2],box[1],box[0],box[3])
                im=source.crop(box).resize((w,h),Image.Resampling.NEAREST)
                if uw<0:im=im.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
                if color!=(255,255,255,255):im=ImageChops.multiply(im,Image.new('RGBA',im.size,color))
            composite(im,c['r'],matrix)
        else:
            left,right,top,bottom=c.get('padding',[0,0,0,0]);w=max(1,w-left-right);h=max(1,h-top-bottom)
            f=font(c['size']);wrapped=wrap_text(c['text'],f,w-4);step=round((c['size'] or 16)*1.35)
            if len(wrapped)*step>h+step*.6:overflows.append((c['text'][:45],len(wrapped)*step,h))
            layer=Image.new('RGBA',(w,h));draw=ImageDraw.Draw(layer)
            yy=max(0,round((h-len(wrapped)*step)/2)) if c['center'] else 0
            for line in wrapped:
                xx=(w-f.getlength(line))/2 if c['center'] else 0
                draw.text((xx,yy),line,font=f,fill=color,anchor='lt');yy+=step
            composite(layer,[x+left,y+top,max(1,logical_w-left-right),max(1,logical_h-top-bottom)],matrix)
    out=ROOT/('.starfall-checks/qa-previews' if name.startswith('qa-') else 'docs/previews');out.mkdir(parents=True,exist_ok=True)
    target=out/(name+'.png');temporary=out/(name+'.tmp.png');canvas.convert('RGB').save(temporary);temporary.replace(target)
    print(name,'layout overflows:',overflows)
    return overflows
if __name__=='__main__':
    for name in (sys.argv[1:] or ['title','town','forest','inventory','journal','talents','map','dialogue']):render(name)
