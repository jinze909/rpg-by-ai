#!/usr/bin/env python3
"""Enforce A–E hero regressions offline, without claiming native Unity acceptance.

A assets/provenance; B whole-body production rendering; C shared simulation;
D production native integration source contracts; E isolated negative controls.
D is static integration evidence only. Licensed Frontier PlayMode/GPU remains
NATIVE_UNVERIFIED until actually executed; a passing stub is never native proof.
"""
from pathlib import Path
import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess
import tempfile
from PIL import Image
from run_core_tests import fixture

ROOT=Path(__file__).resolve().parents[1]
ART=Path('Assets/Resources/Frontier/Art')
ACTORS=Path('Assets/Scripts/Frontier/Runtime/FrontierActors.cs')
RUNTIME=Path('Assets/Scripts/Frontier/Runtime/FrontierRuntime.cs')
SELECTOR=Path('Assets/Scripts/Frontier/Core/HeroAnimationState.cs')
PRESENTER=Path('Assets/Scripts/Frontier/Runtime/FrontierHeroPresenter.cs')
LIBRARY=Path('Assets/Scripts/Frontier/Runtime/HeroAnimationLibrary.cs')
GENERATOR=Path('Assets/Editor/Frontier/HeroAnimationAssets.cs')
STRAIGHT_ALPHA_SHADER=Path('Assets/Resources/Frontier/HeroStraightAlpha.shader')
ATLASES=('PlayerVanguard','PlayerRanger','PlayerArcanist','WalkVanguard','WalkRanger','WalkArcanist','PlayerRear','PlayerSouth','PlayerReactionsRear','PlayerReactionsSouth')
CANONICAL_SHA='0d8b43d47fdc8a2f83909b41faccaf1fb87eaf83e553da42b7666a6b5e066976'
SCENE_GUID='3278a3359c364785a3b92b9896aff216'
PRESERVED_GUIDS=dict(zip(ATLASES,('caca668589295734aae0318fae7507da','d5df5c98508a533aa88f0c2c2661ca18','31e9100d99cc58038c5c2e0a74149f46','1ac126955a6c53588ccc9133a335e3f8','ce13fc67b80254cc8a0ba9014e7e184b','3724259f1f14569b9ab1ea88255197ec','7da2c895e62a5e67ba06190d6695e31d','c0c1851e25d15ce9b0a5c1ca760bf973','7679c77097125b8999314660f9702413','df301452bbf05e7684f2e7d06992917a')))


class GateFailure(RuntimeError):
    pass


def require(condition,category,message):
    if not condition:
        raise GateFailure(category+': '+message)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def code(path):
    """Remove comments, not strings; a commented-out hook cannot pass D."""
    text=path.read_text()
    return re.sub(r'//[^\n]*|/\*[\s\S]*?\*/','',text)


def method(text,name):
    match=re.search(r'\b'+re.escape(name)+r'\s*\([^;{}]*\)\s*\{',text)
    require(match is not None,'B','Missing production method '+name)
    start=match.end();depth=1
    for index in range(start,len(text)):
        if text[index]=='{':depth+=1
        elif text[index]=='}':
            depth-=1
            if depth==0:return text[start:index]
    raise GateFailure('B: Unclosed production method '+name)


def decoded(path,category='A'):
    require(path.exists(),category,'Missing asset '+str(path.relative_to(ROOT) if path.is_relative_to(ROOT) else path))
    require(not path.read_bytes().startswith(b'version https://git-lfs.'),category,'Unexpanded LFS asset '+path.name)
    try:
        with Image.open(path) as image:image.verify()
        with Image.open(path) as image:return image.convert('RGBA')
    except (OSError,ValueError,SyntaxError) as error:
        raise GateFailure(category+': Invalid PNG '+path.name) from error


def assets():
    require(digest(ROOT/'Assets/Resources/Frontier/ViolaMoves.png')==CANONICAL_SHA,'A','Original blond protagonist identity source changed')
    registry=json.loads((ROOT/'docs/ACTOR-REGISTRATION.json').read_text())
    accepted={entry['name']:entry['sha256'] for entry in registry['atlases']}
    masks={(entry['source'],entry['kind']):entry for entry in json.loads((ROOT/'docs/ACTOR-MASKS.json').read_text())}
    visibility={entry['name']:entry for entry in json.loads((ROOT/'docs/ACTOR-VISIBILITY.json').read_text())}
    cells=0
    for name in ATLASES:
        path=ROOT/ART/(name+'.png');image=decoded(path)
        require(image.size==(1024,768),'A','Incorrect whole-body 4x3 atlas '+name)
        require(name in accepted and digest(path)==accepted[name],'A','Accepted repaired atlas was changed without provenance: '+name)
        meta=path.with_suffix('.png.meta').read_text()
        require(re.search(r'^guid: '+PRESERVED_GUIDS[name]+r'$',meta,re.M) is not None,'A','Existing hero GUID changed: '+name)
        require(all(value in meta for value in ('filterMode: 0','enableMipMap: 0','textureCompression: 0')),'A','Pixel import filtering/compression regression: '+name)
        require(name in visibility and visibility[name]['sha256']==accepted[name],'A','Stale full-body visibility bounds: '+name)
        for index in range(12):
            cell=image.crop((index%4*256,index//4*256,(index%4+1)*256,(index//4+1)*256))
            bound=cell.getchannel('A').getbbox()
            require(bound is not None and min(bound)>0 and bound[2]<256 and bound[3]<256,'A','Empty/clipped full-body frame '+name+':'+str(index))
            require([bound[0],bound[1],bound[2]-bound[0],bound[3]-bound[1]]==visibility[name]['bounds'][index],'A','Stale frame visibility: '+name+':'+str(index))
            cells+=1
        for kind in ('outline','flash'):
            mask_path=ROOT/ART/'ActorMasks'/(kind+'_'+name+'.png');mask=decoded(mask_path)
            entry=masks.get((name,kind))
            require(entry is not None and mask.size==image.size and digest(mask_path)==entry['sha256'] and entry['source_sha256']==accepted[name],'A','Stale or mismatched alpha mask '+kind+'_'+name)
    repairs=json.loads((ROOT/'docs/POSE-REPAIRS.json').read_text())
    for atlas,entries in repairs.items():
        if atlas not in ATLASES:continue
        for repair in entries.values():decoded(ROOT/'Assets/ArtSource/Frontier'/(repair['source']+'.png'))
    print('PASS A: 120 accepted complete hero cells, 20 decoded masks, repaired-source provenance, original identity and 10 existing GUIDs',flush=True)
    return cells


def whole_body_source(source_root):
    text=code(source_root/ACTORS);draw=method(text,'DrawPlayerActor');whole=method(text,'DrawWholeHero');preview=method(text,'DrawPlayerPreview')
    require('HeroAnimationState.Select(Game)' in draw and 'DrawWholeHero(selected, p, tint)' in draw,'B','Formal player must use shared full-body selection and production whole-body renderer')
    require(not re.search(r'\b(?:HumanActor|ActorPart)\s*\(',draw+whole+preview),'B','Disconnected head/body fragment rendering reintroduced for protagonist')
    require('Actor(sheet, 4, 3, selected.frame, foot, 192, tint, selected.flip)' in whole,'B','Offline fallback must draw one complete selected 256-pixel cell at unchanged density')
    require('Actor(art.FlashMask(sheet), 4, 3, selected.frame, foot, 192,' in whole,'B','Hit flash must use the same complete frame')


def rendered_cells(capture_dir):
    files=sorted(capture_dir.glob('hero-*.json'))
    require(bool(files),'B','No fresh production hero captures were executed')
    seen=set();total=0
    accepted={entry['name']:entry['sha256'] for entry in json.loads((ROOT/'docs/ACTOR-REGISTRATION.json').read_text())['atlases']}
    for path in files:
        commands=json.loads(path.read_text())
        bodies=[entry for entry in commands if entry.get('k')=='texture' and entry.get('name','').startswith('resource:Frontier/Art/') and entry['name'].split('/')[-1] in ATLASES]
        require(len(bodies)==1,'B','Actual draw contains detached pieces or no full body: '+path.name)
        body=bodies[0];name=body['name'].split('/')[-1];u,v,w,h=body['uv'];x,y,width,height=body['r']
        require(abs(abs(w)-.25)<.00001 and abs(h-1/3)<.00001 and abs(width-192)<.001 and abs(height-192)<.001,'B','Actual body is cropped/scaled by pose: '+path.name)
        column=round(u*4)-(1 if w<0 else 0);row=2-round(v*3)
        require(column in range(4) and row in range(3),'B','Actual source cell escapes atlas')
        source=decoded(ROOT/ART/(name+'.png'),'B')
        expected=source.crop((column*256,row*256,(column+1)*256,(row+1)*256))
        # Reconstruct the actual submitted UV region from fresh production PNGs.
        # Identical whole-frame submission preserves authored head-to-feet pixels;
        # this is not a claim about the native GPU output or anatomical acceptance.
        submitted=source.crop((round(min(u,u+w)*source.width),round((1-v-h)*source.height),round(max(u,u+w)*source.width),round((1-v)*source.height)))
        if w<0:
            submitted=submitted.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
            expected=expected.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        require(submitted.size==(256,256) and submitted.tobytes()==expected.tobytes(),'B','Production UV no longer preserves accepted full-body pixels: '+path.name)
        require(digest(ROOT/ART/(name+'.png'))==accepted[name],'B','Capture uses an unregistered atlas')
        seen.add(name);total+=1
    require(set(ATLASES)<=seen,'B','Production input captures omit hero sources: '+','.join(sorted(set(ATLASES)-seen)))
    print('PASS B: '+str(total)+' actual production full-body submissions, complete UV/pixels/density/root and all 10 sources; anatomy/native GPU not inferred',flush=True)
    return total


def compiler():
    mono=os.environ.get('STARFALL_MONO',shutil.which('mono'));mcs=os.environ.get('STARFALL_MCS',shutil.which('mcs'))
    require(bool(mono and mcs),'C','Install mono-mcs, or set STARFALL_MONO and STARFALL_MCS')
    return mono,([mono,mcs] if mcs.endswith('.exe') else [mcs])


def simulation(source_root,quiet=False):
    mono,compile_command=compiler()
    with tempfile.TemporaryDirectory(prefix='hero-core-contract-') as directory:
        temp=Path(directory);campaign=temp/'CampaignFixture.cs';campaign.write_text(fixture(json.loads((ROOT/'Assets/Resources/Frontier/Campaign.json').read_text())))
        harness=temp/'Run.cs';harness.write_text('''using System;using Starfall.Tests;
class HeroCoreHarness{static int Main(){int count=0,fail=0;foreach(var m in typeof(CoreChecks).GetMethods()){if(!m.Name.StartsWith("HeroFullBody"))continue;count++;try{m.Invoke(null,new object[]{CampaignFixture.Create()});Console.WriteLine("PASS "+m.Name);}catch(Exception ex){fail++;Console.WriteLine("FAIL "+m.Name+": "+(ex.InnerException??ex).Message);}}if(count!=4){Console.WriteLine("FAIL exactly four hero Core groups must execute");return 1;}return fail==0?0:1;}}''')
        sources=sorted((ROOT/'Assets/Scripts/Frontier/Core').glob('*.cs'))
        sources=[source_root/SELECTOR if path.name==SELECTOR.name else path for path in sources]
        sources+=sorted((ROOT/'Assets/Tests/Frontier').glob('*Checks.cs'))+[campaign,harness]
        exe=temp/'hero-core.exe'
        compiled=subprocess.run(compile_command+['-out:'+str(exe)]+[str(path) for path in sources],text=True,capture_output=True,cwd=ROOT)
        require(compiled.returncode==0,'C','Hero Core compilation failed:\n'+compiled.stdout+compiled.stderr)
        result=subprocess.run([mono,str(exe)],text=True,capture_output=True,cwd=ROOT)
        require(result.returncode==0,'C','Hero shared contact/gear/clock checks failed:\n'+result.stdout+result.stderr)
        if not quiet:print(result.stdout,end='',flush=True)
    if not quiet:print('PASS C: 4 real Core phase/contact/equipment/dodge/reaction/pause/hitstop contracts; renderer selection is read-only',flush=True)


def native_integration_source(source_root,quiet=False):
    runtime=code(source_root/RUNTIME);actors=code(source_root/ACTORS);presenter=code(source_root/PRESENTER);library=code(source_root/LIBRARY);generator=code(source_root/GENERATOR)
    require('scene.name != "Frontier"' in runtime and 'AddComponent<FrontierRuntime>()' in runtime,'D','Formal Frontier scene boot route is missing')
    require('nativeHero = new FrontierHeroPresenter(gameObject);' in method(runtime,'Awake'),'D','Production Awake must instantiate the native whole-body presenter')
    whole=method(actors,'DrawWholeHero')
    require('nativeHero.Render(selected, drawingOutline ? new Color(1, .56f, .35f, .9f) : tint, drawingOutline,' in whole and 'GUI.DrawTexture(new Rect(foot.x - 96, foot.y - 180, 192, 192), target)' in whole,'D','Native rendered body must occupy the real sorted actor slot behind HUD, not an invisible background camera')
    on_gui=method(runtime,'OnGUI')
    require('catch (Exception ex)' in whole and 'throw new HeroPresentationException(ex);' in whole,'D','Late whole-body failures must enter the typed fatal presentation boundary with their original cause')
    require('catch (HeroPresentationException ex)' in on_gui and 'bootError = ex.ToString();' in on_gui and 'Debug.LogError(bootError);' in on_gui and 'Box(0, 0, Width, Height, ink); DrawGUI();' in on_gui,'D','A late hero failure must log its full cause and visibly replace the partial world in the same GUI pass')
    require('catch (Exception' not in on_gui,'D','The hero failure boundary must not swallow unrelated world or UI errors')
    require('if (bootError != null || Game == null' in method(runtime,'Update') and 'if (bootError != null) return false;' in method(runtime,'Save'),'D','Fatal presentation must stop hidden simulation and all callers of the real save path')
    required=('AddComponent<SpriteRenderer>()','AddComponent<Animator>()','animator.runtimeAnimatorController = library.Controller','animator.speed = 0','animator.Play(','animator.Update(0)','renderCamera.Render()','renderCamera.backgroundColor = Color.clear','RenderTextureFormat.ARGB32','body.sprite != set.body[frame.frame]')
    require(all(token in presenter for token in required),'D','Native renderer/controller/sample/transparent-composite contract is missing')
    require('body.sprite =' not in presenter.replace('body.sprite !=',''),'D','Manual body.sprite assignment cannot bypass the actual Animator clip binding')
    require('new Vector2(128, 16)' in library and 'new Rect(i % 4 * CanvasSize, (2 - i / 4) * CanvasSize, CanvasSize, CanvasSize)' in library and 'sprite.rect != expected' in library,'D','Runtime library must reject partial sprites and wrong collision-feet pivots')
    required=('Sprite.Create(texture, rect, new Vector2(.5f, .0625f), 1','typeof(SpriteRenderer)','propertyName = "m_Sprite"','AnimationUtility.SetObjectReferenceCurve','AnimationUtility.GetObjectReferenceCurveBindings','IPreprocessBuildWithReport','HeroAnimationAssets.Ensure()','PlayModeStateChange.ExitingEditMode')
    require(all(token in generator for token in required),'D','Editor generation/build validation/whole-body SpriteRenderer binding is missing')
    require((source_root/STRAIGHT_ALPHA_SHADER).exists(),'D','Native straight-alpha conversion shader must ship as a real Resources asset')
    shader=code(source_root/STRAIGHT_ALPHA_SHADER)
    constructor=method(presenter,'FrontierHeroPresenter');render=method(presenter,'Render')
    conversion='Graphics.Blit(premultipliedTarget, target, straightAlphaMaterial);'
    require('Shader.Find("Sprites/Default")' in constructor and 'spriteMaterial = new Material(shader)' in constructor and 'body.sharedMaterial = spriteMaterial' in constructor and 'renderer.sharedMaterial = spriteMaterial' in method(presenter,'ChildRenderer'),'D','Native body and masks must use the SpriteFrag premultiplied sprite material')
    require('Resources.Load<Shader>("Frontier/HeroStraightAlpha")' in constructor and 'straightAlphaMaterial = new Material(conversionShader)' in constructor,'D','Native presenter must load and bind its shipped straight-alpha conversion shader')
    require('premultipliedTarget = new RenderTexture(256, 256, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)' in constructor and 'new RenderTexture(256, 256, 0, RenderTextureFormat.ARGB32)' in method(presenter,'Target'),'D','Native conversion must read the actual high precision linear scratch target and write the display target')
    require('RenderTexture target = outline ? outlineTarget : normalTarget;' in render,'D','Native conversion must write the selected returned body/outline render target')
    require('renderCamera.targetTexture = premultipliedTarget;' in render and conversion in render,'D','Camera premultiplied output must pass through straight-alpha conversion before IMGUI compositing')
    require(render.index('renderCamera.targetTexture = premultipliedTarget;')<render.index('renderCamera.Render()')<render.index(conversion)<render.rfind('return target;'),'D','Native conversion must be inside the actual camera-to-returned-output chain')
    require('Graphics.CopyTexture(' not in render,'D','Native premultiplied camera output cannot bypass straight-alpha conversion with CopyTexture')
    require('GL.sRGBWrite = target.sRGB;' in render and render.index('GL.sRGBWrite = target.sRGB;')<render.index(conversion),'D','Conversion must use the destination color-space setting before writing the display target')
    require('Shader "Hidden/Starfall/HeroStraightAlpha"' in shader and re.search(r'\bBlend\s+Off\b',shader),'D','Conversion shader must overwrite straight RGBA without a second blend')
    # Checked against the shipped shader below: actual source must divide RGB
    # by coverage, retaining alpha and guarding transparent pixels. Native GPU
    # color/alpha acceptance still requires the supplied PlayMode assertions.
    fragment=re.search(r'\bfloat4\s+frag\s*\(\s*v2f_img\s+input\s*\)\s*:\s*SV_Target\s*\{([^{}]*)\}',shader)
    require(fragment is not None and re.search(r'#pragma\s+fragment\s+frag\b',shader),'D','Conversion shader must execute its straight-alpha fragment')
    fragment=re.sub(r'\s+','',fragment.group(1))
    require('float4color=tex2D(_MainTex,input.uv);' in fragment and 'returncolor.a>0?float4(color.rgb/max(color.a,0.000001),color.a):float4(0,0,0,0);' in fragment,'D','Straight-alpha fragment must undo SpriteFrag premultiplication while preserving coverage and zeroing transparent pixels')
    settings=(ROOT/'ProjectSettings/EditorBuildSettings.asset').read_text();scene_meta=(ROOT/'Assets/Scenes/Frontier.unity.meta').read_text()
    require('path: Assets/Scenes/Frontier.unity' in settings and 'guid: '+SCENE_GUID in settings and 'guid: '+SCENE_GUID in scene_meta,'D','Original Frontier scene/build GUID must remain authoritative')
    if not quiet:print('PASS D (STATIC ONLY): Frontier boot → native SpriteRenderer/Animator → transparent sorted-slot composite, explicit straight-alpha conversion and generated clip/build wiring. NATIVE_UNVERIFIED',flush=True)


def negative_controls():
    originals={relative:digest(ROOT/relative) for relative in (ACTORS,RUNTIME,SELECTOR,PRESENTER,LIBRARY,GENERATOR,STRAIGHT_ALPHA_SHADER)}
    controls=[]
    with tempfile.TemporaryDirectory(prefix='hero-regression-mutations-') as directory:
        temp=Path(directory)
        for relative in originals:
            target=temp/relative;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(ROOT/relative,target)
        cases=[
            ('B detached-head composition',ACTORS,'DrawWholeHero(selected, p, tint);','HumanActor(null,4,3,0,p,.75f,tint,false,null,0,false);',whole_body_source,'B:'),
            ('C contact pose replaced by idle',SELECTOR,'pose = game.PlayerPose;','pose = 0;',lambda root:simulation(root,True),'C:'),
            ('D disabled native production route',RUNTIME,'nativeHero = new FrontierHeroPresenter(gameObject);','nativeHero = null;',lambda root:native_integration_source(root,True),'D:'),
            ('D bypassed straight-alpha conversion',PRESENTER,'Graphics.Blit(premultipliedTarget, target, straightAlphaMaterial);','Graphics.CopyTexture(premultipliedTarget, target);',lambda root:native_integration_source(root,True),'D:'),
            ('D bypassed late hero failure boundary',ACTORS,'throw new HeroPresentationException(ex);','throw;',lambda root:native_integration_source(root,True),'D:'),
        ]
        for name,relative,before,after,check,category in cases:
            path=temp/relative;original=path.read_text();require(original.count(before)==1,'E','Mutation insertion point changed; update negative control rather than skip it: '+name)
            path.write_text(original.replace(before,after))
            try:
                try:check(temp)
                except GateFailure as error:
                    require(str(error).startswith(category),'E','Mutation failed for wrong reason: '+name+' → '+str(error))
                    if category=='C:':
                        require('Full-body selection expected ' in str(error),'E','Wrong-pose mutation must fail actual behavioral assertions, not compilation or zero-test discovery')
                    controls.append(name);print('PASS E negative control: '+name+' correctly rejected by '+category[0],flush=True)
                else:raise GateFailure('E: Target regression was not detected: '+name)
            finally:path.write_text(original)
        for label,payload in (('LFS actor pointer',b'version https://git-lfs.github.com/spec/v1\n'),('truncated actor PNG',(ROOT/ART/'PlayerRanger.png').read_bytes()[:24])):
            path=temp/('invalid-'+label.split()[0]+'.png');path.write_bytes(payload)
            try:decoded(path)
            except GateFailure as error:
                require(str(error).startswith('A:'),'E','Asset negative control failed for wrong category: '+label)
                controls.append('A '+label);print('PASS E negative control: A '+label+' correctly rejected by A',flush=True)
            else:raise GateFailure('E: Invalid actor asset was not rejected: '+label)
    changed=[str(path) for path,expected in originals.items() if digest(ROOT/path)!=expected]
    require(not changed,'E','Production source changed during negative controls (rerun with source frozen): '+', '.join(changed))
    require(len(controls)==7,'E','Exactly seven regression controls must reject their isolated temporary mutations')
    return controls


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--report',type=Path,help='Optional disposable JSON report, outside Assets.')
    args=parser.parse_args()
    if args.report is not None:
        destination=args.report.resolve();require(not destination.is_relative_to(ROOT/'Assets'),'E','Disposable reports must stay outside Assets')
        if destination.exists():destination.unlink()
    try:
        count=assets();whole_body_source(ROOT)
        with tempfile.TemporaryDirectory(prefix='hero-production-captures-') as directory:
            capture_dir=Path(directory)
            subprocess.run([os.environ.get('PYTHON','python3'),str(ROOT/'tools/run_hero_animation_checks.py'),'--capture-dir',str(capture_dir)],check=True,cwd=ROOT)
            captures=rendered_cells(capture_dir)
        simulation(ROOT);native_integration_source(ROOT);mutations=negative_controls()
        report={'A':{'status':'passed','accepted_full_body_cells':count},'B':{'status':'passed_offline','actual_input_draw_captures':captures,'anatomical_acceptance':'requires visual review'},'C':{'status':'passed','core_groups':4,'runtime_groups':4},'D':{'status':'static_integration_passed','native_Unity':'NATIVE_UNVERIFIED','scope':'Source wiring only; no Unity lifecycle/Animator/GPU assertion executed'},'E':{'status':'passed','rejected_source_mutations':mutations},'native_acceptance':'NATIVE_UNVERIFIED'}
        if args.report is not None:
            destination=args.report.resolve();require(not destination.is_relative_to(ROOT/'Assets'),'E','Disposable reports must stay outside Assets');destination.parent.mkdir(parents=True,exist_ok=True);destination.write_text(json.dumps(report,indent=2)+'\n')
        print('PASS A–E offline regression gates. Frontier native import/PlayMode/GPU acceptance remains NATIVE_UNVERIFIED.',flush=True)
    except (GateFailure,subprocess.CalledProcessError) as error:
        raise SystemExit('FAIL hero animation gate: '+str(error)) from error


if __name__=='__main__':main()
