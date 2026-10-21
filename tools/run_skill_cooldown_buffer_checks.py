#!/usr/bin/env python3
"""Exercise real Update/OnGUI Q cooldown buffering with offline Unity declarations.

--source-root compiles immutable older production with the current harness for a
semantic RED. Captures are offline GUI command streams; NATIVE_UNVERIFIED.
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
    parser.add_argument('--source-root', type=Path, default=ROOT)
    parser.add_argument('--capture-dir', type=Path)
    parser.add_argument('--report', type=Path)
    args = parser.parse_args()
    mono = os.environ.get('STARFALL_MONO') or shutil.which('mono')
    mcs = os.environ.get('STARFALL_MCS') or shutil.which('mcs')
    if not mono or not mcs:
        raise SystemExit('Install Mono, or set STARFALL_MONO and STARFALL_MCS.')
    production = args.source_root.resolve()
    sources = sorted((production / 'Assets/Scripts/Frontier').rglob('*.cs'))
    if not sources:
        raise SystemExit('No production Frontier C# sources under --source-root.')
    sources += [ROOT / 'tests' / name for name in (
        'UnityLayoutStubs.cs', 'OfflineJson.cs', 'RuntimePolishChecks.cs',
        'VisualRuntimeChecks.cs', 'RenderStateChecks.cs', 'RuntimeContinuityChecks.cs',
        'ActorRegistrationChecks.cs', 'RuntimeSkillCooldownBufferChecks.cs')]
    campaign_path = production / 'Assets/Resources/Frontier/Campaign.json'
    watched = sources + [campaign_path, Path(__file__).resolve(), ROOT / 'tools/run_core_tests.py']

    def manifest():
        return {str(path): hashlib.sha256(path.read_bytes()).hexdigest() for path in watched}

    before = manifest()
    compile_code = exit_code = None
    compile_output = output = ''
    try:
        with tempfile.TemporaryDirectory(prefix='starfall-skill-cooldown-') as folder:
            temp = Path(folder)
            campaign = temp / 'CampaignFixture.cs'
            campaign.write_text(fixture(json.loads(campaign_path.read_text())))
            compiler = [mono, mcs] if mcs.endswith('.exe') else [mcs]
            exe = temp / 'skill-cooldown.exe'
            compiled = subprocess.run(compiler + ['-main:RuntimeSkillCooldownBufferChecks', '-out:' + str(exe)]
                                      + [str(path) for path in sources] + [str(campaign)], cwd=ROOT,
                                      text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
            compile_code, compile_output = compiled.returncode, compiled.stdout
            print(compile_output, end='')
            if compile_code == 0:
                run = subprocess.run([mono, str(exe), str(args.capture_dir.resolve()) if args.capture_dir else '-'],
                                     cwd=ROOT, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
                exit_code, output = run.returncode, run.stdout
                print(output, end='')
            else:
                exit_code = compile_code
    finally:
        after = manifest()
        unchanged = before == after
        capture_hashes = {}
        if args.capture_dir is not None and args.capture_dir.exists():
            capture_hashes = {str(path.resolve()): hashlib.sha256(path.read_bytes()).hexdigest()
                              for path in sorted(args.capture_dir.glob('qa-skill-cooldown-*.json'))}
        if args.report is not None:
            args.report.parent.mkdir(parents=True, exist_ok=True)
            args.report.write_text(json.dumps({
                'source_root': str(production), 'source_unchanged': unchanged,
                'compile_exit_code': compile_code, 'exit_code': exit_code,
                'hashes': before, 'hashes_after': after,
                'compile_output': compile_output, 'runtime_output': output,
                'capture_count': len(capture_hashes), 'capture_hashes': capture_hashes,
                'native': 'NATIVE_UNVERIFIED'}, indent=2) + '\n')
    if not unchanged:
        raise SystemExit('Production/test source changed during checks; rerun against frozen sources.')
    raise SystemExit(exit_code)


if __name__ == '__main__':
    main()
