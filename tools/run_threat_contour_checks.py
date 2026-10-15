#!/usr/bin/env python3
"""Exercise foreground threat boundaries through production OnGUI and GameCore.

Optional --capture-dir .starfall-checks/layout writes disposable qa-threat-*
draw records. These offline declarations do not replace native Unity GPU QA.
"""
from pathlib import Path
import argparse
import json
import os
import shutil
import subprocess
import tempfile

from run_core_tests import fixture
from run_fieldcraft_checks import require_actor_masks

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--capture-dir', type=Path)
    args = parser.parse_args()
    mono = os.environ.get('STARFALL_MONO', shutil.which('mono'))
    mcs = os.environ.get('STARFALL_MCS', shutil.which('mcs'))
    if not mono or not mcs:
        raise SystemExit('Install Mono (mono-mcs), or set STARFALL_MONO and STARFALL_MCS.')
    require_actor_masks()
    with tempfile.TemporaryDirectory(prefix='starfall-threat-contours-') as folder:
        temp = Path(folder)
        campaign = temp / 'CampaignFixture.cs'
        campaign.write_text(fixture(json.loads((ROOT / 'Assets/Resources/Frontier/Campaign.json').read_text())))
        sources = sorted((ROOT / 'Assets/Scripts/Frontier').rglob('*.cs'))
        sources += [ROOT / 'tests' / name for name in (
            'UnityLayoutStubs.cs', 'OfflineJson.cs', 'RuntimePolishChecks.cs',
            'VisualRuntimeChecks.cs', 'RenderStateChecks.cs', 'RuntimeContinuityChecks.cs',
            'ActorRegistrationChecks.cs', 'RuntimeThreatContourChecks.cs')]
        compiler = [mono, mcs] if mcs.endswith('.exe') else [mcs]
        exe = temp / 'threat-contours.exe'
        subprocess.run(compiler + ['-main:RuntimeThreatContourChecks', '-out:' + str(exe)]
                       + [str(path) for path in sources] + [str(campaign)], check=True)
        command = [mono, str(exe)]
        if args.capture_dir is not None:
            command.append(str(args.capture_dir.resolve()))
        subprocess.run(command, check=True, cwd=ROOT)


if __name__ == '__main__':
    main()
