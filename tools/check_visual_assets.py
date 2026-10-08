#!/usr/bin/env python3
"""Validate actual visual files/imports and record provenance. Not GPU validation."""
from pathlib import Path
import hashlib, json, re, subprocess
from datetime import datetime, timezone
from PIL import Image
from fontTools.ttLib import TTFont

ROOT = Path(__file__).resolve().parents[1]
BASELINE = 'f32333dc9825119aa54d7268a69fe8544d316d74'
CANONICAL_SHA = '0d8b43d47fdc8a2f83909b41faccaf1fb87eaf83e553da42b7666a6b5e066976'


def check(value, message):
    if not value:
        raise ValueError(message)


def main():
    assets = ROOT/'Assets'
    art = assets/'Resources/Frontier/Art'
    canonical = assets/'Resources/Frontier/ViolaMoves.png'
    check(hashlib.sha256(canonical.read_bytes()).hexdigest() == CANONICAL_SHA, 'Original playable identity source changed')
    guids = {}
    for path in assets.rglob('*'):
        if path.suffix == '.meta':
            match = re.search(r'^guid: (\w+)', path.read_text(), re.M)
            check(match is not None, f'Missing GUID: {path}')
            check(match[1] not in guids, f'Duplicate GUID: {path}')
            guids[match[1]] = path
        else:
            check(path.with_name(path.name+'.meta').exists(), f'Missing import file: {path}')
    png_count = 0
    for path in assets.rglob('*.png'):
        with Image.open(path) as img:
            img.verify()
        png_count += 1
    atlases = json.loads((ROOT/'docs/ATLAS-IMPORT.json').read_text())
    files = []
    cells = 0
    for spec in atlases:
        path = art/(spec['name']+'.png')
        image = Image.open(path).convert('RGBA')
        size = spec['cellSize']
        check(image.size == (spec['columns']*size, spec['rows']*size), f'Atlas grid size: {path}')
        check(max(image.size) <= 4096, f'Texture exceeds importer limit: {path}')
        meta = path.with_name(path.name+'.meta').read_text()
        check('filterMode: 0' in meta and 'enableMipMap: 0' in meta and 'textureCompression: 0' in meta, f'Point/uncompressed/no-mipmap import: {path}')
        if spec['name'].startswith(('Player','Walk','Companion')) or spec['name'] in ('Monsters', 'Bosses', 'EnemyArcherVertical'):
            check('isReadable: 1' in meta, f'Hit flash needs readable alpha: {path}')
        for row in range(spec['rows']):
            for column in range(spec['columns']):
                cell = image.crop((column*size, row*size, (column+1)*size, (row+1)*size))
                bounds = cell.getchannel('A').point(lambda a: 255 if a >= 32 else 0).getbbox()
                check(bounds is not None, f'Empty authored pose: {path}, {row}, {column}')
                if size >= 128:
                    check(bounds[0] > 0 and bounds[1] > 0 and bounds[2] < size and bounds[3] < size, f'Clipped cell/gutter: {path}, {row}, {column}')
                cells += 1
        files.append({'path': path.relative_to(ROOT).as_posix(), 'dimensions': image.size, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()})
    recorded = {file['path'] for file in files}
    from bake_actor_masks import mask_images
    registrations=json.loads((ROOT/'docs/ACTOR-REGISTRATION.json').read_text())
    visibility=json.loads((ROOT/'docs/ACTOR-VISIBILITY.json').read_text())
    check(len(visibility)==len(registrations['atlases']),'Every actor needs baked visibility bounds')
    for entry in visibility:
        path=art/(entry['name']+'.png');image=Image.open(path).convert('RGBA');size=entry['cellSize'];columns=image.width//size
        check(hashlib.sha256(path.read_bytes()).hexdigest()==entry['sha256'],f'Stale visible bounds: {path}')
        for i,bound in enumerate(entry['bounds']):
            cell=image.crop((i%columns*size,i//columns*size,(i%columns+1)*size,(i//columns+1)*size));actual=cell.getchannel('A').getbbox()
            check([actual[0],actual[1],actual[2]-actual[0],actual[3]-actual[1]]==bound,f'Visible pixels extend outside HUD bounds: {path}, {i}')
    for entry in registrations['atlases']:
        path=art/(entry['name']+'.png')
        check(hashlib.sha256(path.read_bytes()).hexdigest()==entry['sha256'],f'Stale frame registry: {path}')
    mask_records=json.loads((ROOT/'docs/ACTOR-MASKS.json').read_text())
    check(len(mask_records)==len(registrations['atlases'])*2,'Every actor requires both precomputed masks')
    for entry in mask_records:
        path=art/'ActorMasks'/(entry['kind']+'_'+entry['source']+'.png')
        source=art/(entry['source']+'.png');actual=Image.open(path).convert('RGBA')
        check(hashlib.sha256(path.read_bytes()).hexdigest()==entry['sha256'] and hashlib.sha256(source.read_bytes()).hexdigest()==entry['source_sha256'],f'Stale baked mask: {path}')
        expected=mask_images(Image.open(source))[entry['kind']]
        check(actual.size==expected.size and actual.tobytes()==expected.tobytes(),f'Mask alpha differs from authored pixels: {path}')
        meta=path.with_name(path.name+'.meta').read_text()
        check('isReadable: 0' in meta and 'filterMode: 0' in meta and 'enableMipMap: 0' in meta,f'GPU-only mask import: {path}')
        files.append({'path':path.relative_to(ROOT).as_posix(),'dimensions':actual.size,'sha256':entry['sha256']})
    for path in sorted(art.glob('*.png')):
        if path.relative_to(ROOT).as_posix() not in recorded:
            files.append({'path': path.relative_to(ROOT).as_posix(), 'dimensions': Image.open(path).size, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()})
    for path in sorted((assets/'ArtSource/Frontier').glob('*.png')):
        files.append({'path': path.relative_to(ROOT).as_posix(), 'dimensions': Image.open(path).size, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()})
    # Validate displayed characters; the source scan includes editor/test-lab captions.
    font = TTFont(assets/'Resources/Frontier/Fonts/NotoSansSC-Regular.otf')
    cmap = font.getBestCmap()
    sources = list((assets/'Scripts').rglob('*.cs')) + list((assets/'Editor').rglob('*.cs')) + [assets/'Resources/Frontier/Campaign.json']
    required = {ord(char) for source in sources for char in source.read_text() if not char.isspace()}
    check(not required-set(cmap), f'Missing runtime font characters: {sorted(required-set(cmap))}')
    current = json.loads((assets/'Resources/Frontier/Campaign.json').read_text())
    preservation = {'baseline': BASELINE, 'canonical_sprite_sha256': CANONICAL_SHA, 'baseline_comparison': 'not rerun (git baseline unavailable)'}
    result = subprocess.run(['git', 'show', BASELINE+':Assets/Resources/Frontier/Campaign.json'], cwd=ROOT, text=True, capture_output=True)
    if result.returncode == 0:
        previous = json.loads(result.stdout)
        for old, new in zip(previous['maps'], current['maps']):
            for key in ['id', 'rows', 'decor', 'spawnX', 'spawnY']:
                check(old[key] == new[key], f'Original map changed: {old["id"]}, {key}')
            check(old['objects'] == new['objects'][:len(old['objects'])], f'Original map objects changed: {old["id"]}')
        check(previous['bosses'] == current['bosses'], 'Original Boss data changed')
        check(previous['items'] == current['items'], 'Original item data changed')
        check(previous['lore'] == current['lore'][:len(previous['lore'])], 'Original lore changed')
        preservation.update({'baseline_comparison': 'passed', 'unchanged_original_maps': len(previous['maps']), 'original_objects': 'unchanged prefix; town inn portal appended', 'bosses_items_original_lore': 'unchanged'})
    report = {'generated_utc': datetime.now(timezone.utc).isoformat(), 'png_decodes': png_count, 'unique_unity_guids': len(guids), 'validated_atlas_cells': cells, 'required_font_characters': len(required), 'preservation': preservation, 'files': files, 'native_unity_import': 'not executed in this environment'}
    (ROOT/'docs/VISUAL-ASSETS.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n')
    print(f'PASS Visual assets: {png_count} PNGs, {cells} atlas cells, {len(guids)} unique GUIDs, {len(required)} font characters; original sprite/data retained.')


if __name__ == '__main__':
    main()
