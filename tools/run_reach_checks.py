#!/usr/bin/env python3
"""Exercise the production Reedwater Reach input, GUI and save paths offline.

Pass --capture-dir .starfall-checks/layout for disposable OnGUI evidence.
Unity declarations used here do not constitute native Editor/Player playback.
"""
from pathlib import Path
import argparse
import json
import os
import shutil
import subprocess
import tempfile

from run_core_tests import fixture

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--capture-dir', type=Path)
    args = parser.parse_args()
    mono = os.environ.get('STARFALL_MONO', shutil.which('mono'))
    mcs = os.environ.get('STARFALL_MCS', shutil.which('mcs'))
    if not mono or not mcs:
        raise SystemExit('Install Mono (mono-mcs), or set STARFALL_MONO and STARFALL_MCS.')
    with tempfile.TemporaryDirectory(prefix='starfall-reach-') as folder:
        temp = Path(folder)
        campaign = temp / 'CampaignFixture.cs'
        campaign.write_text(fixture(json.loads((ROOT / 'Assets/Resources/Frontier/Campaign.json').read_text())))
        sources = sorted((ROOT / 'Assets/Scripts/Frontier').rglob('*.cs'))
        sources += [ROOT / 'tests' / name for name in (
            'UnityLayoutStubs.cs', 'OfflineJson.cs', 'RuntimePolishChecks.cs',
            'VisualRuntimeChecks.cs', 'RenderStateChecks.cs', 'RuntimeContinuityChecks.cs',
            'ActorRegistrationChecks.cs', 'RuntimeReachChecks.cs')]
        compiler = [mono, mcs] if mcs.endswith('.exe') else [mcs]
        exe = temp / 'reach.exe'
        subprocess.run(compiler + ['-main:RuntimeReachChecks', '-out:' + str(exe)]
                       + [str(path) for path in sources] + [str(campaign)], check=True)
        command = [mono, str(exe)]
        if args.capture_dir is not None:
            command.append(str(args.capture_dir.resolve()))
        subprocess.run(command, check=True, cwd=ROOT)


if __name__ == '__main__':
    main()
