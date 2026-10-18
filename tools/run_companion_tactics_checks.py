#!/usr/bin/env python3
"""Exercise companion notes and command receipts through real Update/OnGUI.

These offline declarations verify shipped input and simulation paths, not native
Unity import, GPU rendering, hardware input or play feel (NATIVE_UNVERIFIED).
"""
from pathlib import Path
import argparse
import hashlib
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
    parser.add_argument('--report', type=Path)
    parser.add_argument('--source-root', type=Path, default=ROOT,
                        help='Optional isolated production checkout for semantic RED verification.')
    args = parser.parse_args()
    mono = os.environ.get('STARFALL_MONO', shutil.which('mono'))
    mcs = os.environ.get('STARFALL_MCS', shutil.which('mcs'))
    if not mono or not mcs:
        raise SystemExit('Install Mono (mono-mcs), or set STARFALL_MONO and STARFALL_MCS.')
    production = args.source_root.resolve()
    sources = sorted((production / 'Assets/Scripts/Frontier').rglob('*.cs'))
    sources += [ROOT / 'tests' / name for name in (
        'UnityLayoutStubs.cs', 'OfflineJson.cs', 'RuntimePolishChecks.cs',
        'VisualRuntimeChecks.cs', 'RenderStateChecks.cs', 'RuntimeContinuityChecks.cs',
        'ActorRegistrationChecks.cs', 'RuntimeCompanionTacticsChecks.cs')]
    campaign_path = production / 'Assets/Resources/Frontier/Campaign.json'
    watched = sources + [campaign_path, Path(__file__).resolve()]

    def manifest():
        return {str(path): hashlib.sha256(path.read_bytes()).hexdigest() for path in watched}

    before = manifest()
    with tempfile.TemporaryDirectory(prefix='starfall-companion-tactics-') as folder:
        temp = Path(folder)
        campaign = temp / 'CampaignFixture.cs'
        campaign.write_text(fixture(json.loads(campaign_path.read_text())))
        compiler = [mono, mcs] if mcs.endswith('.exe') else [mcs]
        exe = temp / 'companion-tactics.exe'
        subprocess.run(compiler + ['-main:RuntimeCompanionTacticsChecks', '-out:' + str(exe)]
                       + [str(path) for path in sources] + [str(campaign)], check=True)
        command = [mono, str(exe)]
        if args.capture_dir is not None:
            command.append(str(args.capture_dir.resolve()))
        result = subprocess.run(command, cwd=ROOT)
    after = manifest()
    unchanged = before == after
    if args.report is not None:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps({'exit_code': result.returncode,
            'source_unchanged': unchanged, 'before': before, 'after': after,
            'native': 'NATIVE_UNVERIFIED'}, indent=2) + '\n')
    if not unchanged:
        raise SystemExit('Production/test source changed during the check; rerun against frozen sources.')
    raise SystemExit(result.returncode)


if __name__ == '__main__':
    main()
