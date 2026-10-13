#!/usr/bin/env python3
"""Add stable import metadata only for new project assets; preserve existing GUIDs."""
from pathlib import Path
import re, uuid
from check_unity_metadata import validate_assets
ROOT=Path(__file__).resolve().parents[1]
NAMESPACE=uuid.UUID('bd41a38c-5c2c-4b20-91fd-24a6cac2db2b')
def main():
    new=[]
    for path in sorted((ROOT/'Assets').rglob('*')):
        if path.suffix=='.meta': continue
        meta=path.with_name(path.name+'.meta')
        if meta.exists(): continue
        relative=path.relative_to(ROOT).as_posix();guid=uuid.uuid5(NAMESPACE,relative).hex
        data=f'fileFormatVersion: 2\nguid: {guid}\n'
        if path.is_dir(): data+='folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n'
        elif path.suffix=='.cs': data+='MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {fileID: 0}\n'
        elif path.suffix=='.asmdef': data+='AssemblyDefinitionImporter:\n  externalObjects: {}\n'
        elif path.suffix=='.png':
            readable=relative.startswith('Assets/Resources/Frontier/Art/') and (path.stem.startswith(('Player','Walk','Companion')) and path.stem!='PlayerPortraits' or path.stem in ('Monsters','Bosses','EnemyArcherVertical'))
            data+='TextureImporter:\n  externalObjects: {}\n  serializedVersion: 12\n  mipmaps:\n    enableMipMap: 0\n  isReadable: '+str(int(readable))+'\n  maxTextureSize: 4096\n  textureSettings:\n    serializedVersion: 2\n    filterMode: 0\n    wrapU: 1\n    wrapV: 1\n    wrapW: 1\n  nPOTScale: 0\n  alphaIsTransparency: 1\n  textureType: 0\n  textureCompression: 0\n'
        else: data+='DefaultImporter:\n  externalObjects: {}\n'
        data+='  userData:\n  assetBundleName:\n  assetBundleVariant:\n';meta.write_text(data);new.append(relative)
    ids={}
    for path in (ROOT/'Assets').rglob('*.meta'):
        match=re.search(r'^guid: (\w+)',path.read_text(),re.M)
        if match:
            if match[1] in ids: raise ValueError(f'Duplicate GUID: {path}, {ids[match[1]]}')
            ids[match[1]]=path
    errors=validate_assets(ROOT/'Assets')
    if errors: raise ValueError('\n'.join(errors))
    print(f'{len(new)} new import files; {len(ids)} unique GUIDs; existing metadata retained.')
if __name__=='__main__': main()
