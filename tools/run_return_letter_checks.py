#!/usr/bin/env python3
"""Exercise real first-return-letter input/OnGUI/save paths with offline declarations.

--capture-dir records disposable layout evidence. --source-root selects isolated
production code for semantic RED checks; new return-letter APIs are reflected by tests.
Native Unity import/render/hardware input/play feel remain NATIVE_UNVERIFIED.
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
    parser.add_argument('--source-root', type=Path, default=ROOT)
    args = parser.parse_args()
    mono = os.environ.get('STARFALL_MONO', shutil.which('mono'))
    mcs = os.environ.get('STARFALL_MCS', shutil.which('mcs'))
    if not mono or not mcs:
        raise SystemExit('Install Mono, or set STARFALL_MONO and STARFALL_MCS.')
    production = args.source_root.resolve()
    sources = sorted((production / 'Assets/Scripts/Frontier').rglob('*.cs'))
    sources += [ROOT / 'tests' / name for name in (
        'UnityLayoutStubs.cs', 'OfflineJson.cs', 'RuntimePolishChecks.cs',
        'VisualRuntimeChecks.cs', 'RenderStateChecks.cs', 'RuntimeContinuityChecks.cs',
        'ActorRegistrationChecks.cs', 'RuntimeReturnLetterChecks.cs')]
    campaign_path = production / 'Assets/Resources/Frontier/Campaign.json'
    watched = sources + [campaign_path, Path(__file__).resolve(), ROOT / 'tools/run_core_tests.py']

    def manifest():
        return {str(path): hashlib.sha256(path.read_bytes()).hexdigest() for path in watched}

    before = manifest()
    compile_code, exit_code = None, None
    try:
        with tempfile.TemporaryDirectory(prefix='starfall-signal-return-letter-') as folder:
            temp = Path(folder)
            campaign = temp / 'CampaignFixture.cs'
            campaign.write_text(fixture(json.loads(campaign_path.read_text())))
            compiler = [mono, mcs] if mcs.endswith('.exe') else [mcs]
            exe = temp / 'signal-return-letter.exe'
            compiled = subprocess.run(compiler + ['-main:RuntimeReturnLetterChecks', '-out:' + str(exe)]
                                      + [str(path) for path in sources] + [str(campaign)], cwd=ROOT)
            compile_code = compiled.returncode
            if compile_code == 0:
                command = [mono, str(exe)]
                if args.capture_dir is not None:
                    command.append(str(args.capture_dir.resolve()))
                exit_code = subprocess.run(command, cwd=ROOT).returncode
            else:
                exit_code = compile_code
    finally:
        after = manifest()
        unchanged = before == after
        if args.report is not None:
            args.report.parent.mkdir(parents=True, exist_ok=True)
            args.report.write_text(json.dumps({'compile_exit_code': compile_code, 'exit_code': exit_code,
                'source_root': str(production), 'source_unchanged': unchanged,
                'before': before, 'after': after, 'native': 'NATIVE_UNVERIFIED'}, indent=2) + '\n')
    if not unchanged:
        raise SystemExit('Production/test source changed during the check; rerun against frozen sources.')
    raise SystemExit(exit_code)


if __name__ == '__main__':
    main()
