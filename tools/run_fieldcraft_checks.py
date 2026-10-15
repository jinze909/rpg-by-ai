#!/usr/bin/env python3
"""Exercise production fieldcraft input, GUI, combat and save paths offline.

Optional --capture-dir writes disposable OnGUI evidence. This harness does not
constitute native Unity import, rendering, build, hardware or playtest evidence.
"""
from pathlib import Path
import argparse
import json
import os
import shutil
import struct
import subprocess
import tempfile

from run_core_tests import fixture

ROOT = Path(__file__).resolve().parents[1]


def require_actor_masks():
    """Use the shipped masks, never an ignored local pixel-cache substitute."""
    art = ROOT / 'Assets/Resources/Frontier/Art'
    remedy = ('Run git lfs pull '
              '--include="Assets/Resources/Frontier/Art/ActorMasks/*.png" --exclude="".')

    def dimensions(path):
        try:
            with path.open('rb') as asset:
                header = asset.read(24)
        except OSError as error:
            raise SystemExit(f'Missing actor texture: {path.relative_to(ROOT)}. {remedy}') from error
        if header.startswith(b'version https://git-lfs.'):
            raise SystemExit(f'Unexpanded LFS actor texture: {path.relative_to(ROOT)}. {remedy}')
        if len(header) != 24 or header[:8] != b'\x89PNG\r\n\x1a\n' or header[12:16] != b'IHDR':
            raise SystemExit(f'Invalid PNG actor texture: {path.relative_to(ROOT)}.')
        size = struct.unpack('>II', header[16:24])
        if 0 in size:
            raise SystemExit(f'Invalid PNG dimensions: {path.relative_to(ROOT)}.')
        return size

    atlases = json.loads((ROOT / 'docs/ACTOR-REGISTRATION.json').read_text())['atlases']
    for atlas in atlases:
        name = atlas['name']
        source_size = dimensions(art / (name + '.png'))
        for kind in ('flash', 'outline'):
            path = art / 'ActorMasks' / (kind + '_' + name + '.png')
            if dimensions(path) != source_size:
                raise SystemExit(f'Actor mask/source dimensions differ: {path.relative_to(ROOT)}.')
    print(f'PASS {len(atlases) * 2} production actor masks are materialized PNGs with matching dimensions.', flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--capture-dir', type=Path)
    args = parser.parse_args()
    mono = os.environ.get('STARFALL_MONO', shutil.which('mono'))
    mcs = os.environ.get('STARFALL_MCS', shutil.which('mcs'))
    if not mono or not mcs:
        raise SystemExit('Install Mono (mono-mcs), or set STARFALL_MONO and STARFALL_MCS.')
    require_actor_masks()
    with tempfile.TemporaryDirectory(prefix='starfall-fieldcrafts-') as folder:
        temp = Path(folder)
        campaign = temp / 'CampaignFixture.cs'
        campaign.write_text(fixture(json.loads((ROOT / 'Assets/Resources/Frontier/Campaign.json').read_text())))
        sources = sorted((ROOT / 'Assets/Scripts/Frontier').rglob('*.cs'))
        sources += [ROOT / 'tests' / name for name in (
            'UnityLayoutStubs.cs', 'OfflineJson.cs', 'RuntimePolishChecks.cs',
            'VisualRuntimeChecks.cs', 'RenderStateChecks.cs', 'RuntimeContinuityChecks.cs',
            'ActorRegistrationChecks.cs', 'RuntimeFieldcraftChecks.cs')]
        compiler = [mono, mcs] if mcs.endswith('.exe') else [mcs]
        exe = temp / 'fieldcrafts.exe'
        subprocess.run(compiler + ['-main:RuntimeFieldcraftChecks', '-out:' + str(exe)]
                       + [str(path) for path in sources] + [str(campaign)], check=True)
        command = [mono, str(exe)]
        if args.capture_dir is not None:
            command.append(str(args.capture_dir.resolve()))
        subprocess.run(command, check=True, cwd=ROOT)


if __name__ == '__main__':
    main()
