#!/usr/bin/env python3
"""Check actual Reedwater Reach shoreline draw/state paths with offline Unity.

--capture-dir records disposable production OnGUI evidence. --baseline-ref reads
an immutable Git archive into a temporary directory, so the same harness can
compile the old renderer and demonstrate a semantic RED without a checkout.
Neither these declarations nor the captures constitute native Unity playback.
"""
from pathlib import Path
import argparse
import io
import json
import os
import shutil
import subprocess
import tarfile
import tempfile

from run_core_tests import fixture

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--capture-dir', type=Path)
    parser.add_argument('--capture-prefix', default=None)
    parser.add_argument('--capture-only', action='store_true')
    parser.add_argument('--baseline-ref', help='Read production C# and campaign from this Git revision.')
    args = parser.parse_args()
    if args.capture_only and args.capture_dir is None:
        parser.error('--capture-only requires --capture-dir')
    mono = os.environ.get('STARFALL_MONO', shutil.which('mono'))
    mcs = os.environ.get('STARFALL_MCS', shutil.which('mcs'))
    if not mono or not mcs:
        raise SystemExit('Install Mono (mono-mcs), or set STARFALL_MONO and STARFALL_MCS.')
    with tempfile.TemporaryDirectory(prefix='starfall-reach-shore-') as folder:
        temp = Path(folder)
        production = ROOT
        if args.baseline_ref:
            production = temp / 'baseline'
            archive = subprocess.run(
                ['git', 'archive', args.baseline_ref, 'Assets/Scripts/Frontier',
                 'Assets/Resources/Frontier/Campaign.json'], cwd=ROOT,
                check=True, stdout=subprocess.PIPE).stdout
            with tarfile.open(fileobj=io.BytesIO(archive)) as source:
                # Git tree members are expected to be relative repository paths.
                for member in source.getmembers():
                    target = (production / member.name).resolve()
                    if not target.is_relative_to(production.resolve()) or member.issym() or member.islnk():
                        raise SystemExit('Unsafe member in production source archive: ' + member.name)
                source.extractall(production, filter='data')
            print('Production source: ' + args.baseline_ref + ' (read-only archive)', flush=True)
        campaign = temp / 'CampaignFixture.cs'
        campaign.write_text(fixture(json.loads(
            (production / 'Assets/Resources/Frontier/Campaign.json').read_text())))
        sources = sorted((production / 'Assets/Scripts/Frontier').rglob('*.cs'))
        sources += [ROOT / 'tests' / name for name in (
            'UnityLayoutStubs.cs', 'OfflineJson.cs', 'RuntimePolishChecks.cs',
            'VisualRuntimeChecks.cs', 'RenderStateChecks.cs', 'RuntimeContinuityChecks.cs',
            'ActorRegistrationChecks.cs', 'RuntimeReachChecks.cs', 'RuntimeReachShoreChecks.cs')]
        compiler = [mono, mcs] if mcs.endswith('.exe') else [mcs]
        exe = temp / 'reach-shore.exe'
        subprocess.run(compiler + ['-main:RuntimeReachShoreChecks', '-out:' + str(exe)]
                       + [str(path) for path in sources] + [str(campaign)], check=True)
        command = [mono, str(exe), str(args.capture_dir.resolve()) if args.capture_dir else '-',
                   args.capture_prefix or ('qa-shore-before' if args.baseline_ref else 'qa-shore'),
                   'capture-only' if args.capture_only else 'check']
        result = subprocess.run(command, cwd=ROOT)
        raise SystemExit(result.returncode)


if __name__ == '__main__':
    main()
