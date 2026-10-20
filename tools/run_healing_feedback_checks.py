#!/usr/bin/env python3
"""Exercise actual combat healing via stew, companion contacts, relics, Update and OnGUI.

--source-root can point to immutable production code for semantic RED evidence.
--capture-dir writes disposable OnGUI captures; --report records source hashes.
Native Unity import, GPU rendering and feel remain NATIVE_UNVERIFIED.
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
        'ActorRegistrationChecks.cs', 'RuntimeHealingFeedbackChecks.cs')]
    campaign_path = production / 'Assets/Resources/Frontier/Campaign.json'
    watched = sources + [campaign_path, Path(__file__).resolve(), ROOT / 'tools/run_core_tests.py']

    def manifest():
        return {str(path): hashlib.sha256(path.read_bytes()).hexdigest() for path in watched}

    before = manifest()
    compile_code, exit_code = None, None
    try:
        with tempfile.TemporaryDirectory(prefix='starfall-healing-feedback-') as folder:
            temp = Path(folder)
            campaign = temp / 'CampaignFixture.cs'
            campaign.write_text(fixture(json.loads(campaign_path.read_text())))
            compiler = [mono, mcs] if mcs.endswith('.exe') else [mcs]
            exe = temp / 'healing-feedback.exe'
            compiled = subprocess.run(compiler + ['-main:RuntimeHealingFeedbackChecks', '-out:' + str(exe)]
                                      + [str(path) for path in sources] + [str(campaign)], cwd=ROOT)
            compile_code = compiled.returncode
            if compile_code == 0:
                command = [mono, str(exe), str(args.capture_dir.resolve()) if args.capture_dir else '-']
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
