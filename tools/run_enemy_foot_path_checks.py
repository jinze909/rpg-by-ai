#!/usr/bin/env python3
"""Exercise authored enemy exact-foot pursuit through real Runtime.Update.

The same harness can use --baseline-ref or --source-root for semantic RED/GREEN
evidence. Offline Unity declarations cannot establish native import, GPU output,
hardware input, or gameplay acceptance: NATIVE_UNVERIFIED.
"""
from pathlib import Path
import argparse
import hashlib
import io
import json
import os
import re
import shutil
import subprocess
import tarfile
import tempfile

from run_core_tests import fixture
from run_fieldcraft_checks import require_actor_masks

ROOT = Path(__file__).resolve().parents[1]
GROUPS = 4


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    source = parser.add_mutually_exclusive_group()
    source.add_argument('--source-root', type=Path, default=ROOT)
    source.add_argument('--baseline-ref')
    parser.add_argument('--report', type=Path)
    args = parser.parse_args()
    mono = os.environ.get('STARFALL_MONO') or shutil.which('mono')
    mcs = os.environ.get('STARFALL_MCS') or shutil.which('mcs')
    if not mono or not mcs:
        raise SystemExit('Install Mono, or set STARFALL_MONO and STARFALL_MCS.')
    require_actor_masks()
    report = {'native': 'NATIVE_UNVERIFIED', 'baseline_ref': args.baseline_ref,
              'compile_exit_code': None, 'execution_exit_code': None, 'exit_code': None,
              'groups_passed': 0, 'groups_failed': None,
              'compile_output': '', 'runtime_output': ''}
    with tempfile.TemporaryDirectory(prefix='starfall-enemy-foot-path-') as folder:
        temp = Path(folder)
        production = args.source_root.resolve()
        if args.baseline_ref:
            production = temp / 'baseline'
            archive = subprocess.run(
                ['git', 'archive', args.baseline_ref, 'Assets/Scripts/Frontier',
                 'Assets/Resources/Frontier/Campaign.json'], cwd=ROOT,
                check=True, stdout=subprocess.PIPE).stdout
            with tarfile.open(fileobj=io.BytesIO(archive)) as source_archive:
                for member in source_archive.getmembers():
                    target = (production / member.name).resolve()
                    if not target.is_relative_to(production.resolve()) or member.issym() or member.islnk():
                        raise SystemExit('Unsafe production archive member: ' + member.name)
                source_archive.extractall(production, filter='data')
            print('Production source: ' + args.baseline_ref + ' (read-only archive)', flush=True)
        sources = sorted((production / 'Assets/Scripts/Frontier').rglob('*.cs'))
        if not sources:
            raise SystemExit('No production Frontier C# sources under --source-root.')
        sources += [ROOT / 'tests' / name for name in (
            'UnityLayoutStubs.cs', 'OfflineJson.cs', 'RuntimePolishChecks.cs',
            'VisualRuntimeChecks.cs', 'RenderStateChecks.cs', 'RuntimeContinuityChecks.cs',
            'ActorRegistrationChecks.cs', 'RuntimeEnemyFootPathChecks.cs')]
        campaign_path = production / 'Assets/Resources/Frontier/Campaign.json'
        watched = sources + [campaign_path, Path(__file__).resolve(), ROOT / 'tools/run_core_tests.py',
                             ROOT / 'tools/run_fieldcraft_checks.py']

        def manifest():
            return {str(path): hashlib.sha256(path.read_bytes()).hexdigest() for path in watched}

        before = manifest()
        report.update(source_root=str(production), hashes=before)
        try:
            campaign = temp / 'CampaignFixture.cs'
            campaign.write_text(fixture(json.loads(campaign_path.read_text())))
            compiler = [mono, mcs] if mcs.endswith('.exe') else [mcs]
            exe = temp / 'enemy-foot-path.exe'
            compiled = subprocess.run(compiler + ['-main:RuntimeEnemyFootPathChecks', '-out:' + str(exe)]
                                      + [str(path) for path in sources] + [str(campaign)], cwd=ROOT,
                                      text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
            report.update(compile_exit_code=compiled.returncode, compile_output=compiled.stdout)
            print(compiled.stdout, end='')
            if compiled.returncode == 0:
                run = subprocess.run([mono, str(exe)], cwd=ROOT, text=True,
                                     stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
                summary = re.search(r'Runtime enemy foot path: ([0-4])/4 groups passed;', run.stdout)
                passed = int(summary.group(1)) if summary else 0
                report.update(execution_exit_code=run.returncode, exit_code=run.returncode,
                              runtime_output=run.stdout, groups_passed=passed, groups_failed=GROUPS-passed)
                if run.returncode == 0 and passed != GROUPS:
                    report['exit_code'] = 1
                    print('Missing complete 4/4 Runtime group summary; refusing an incomplete success.')
                print(run.stdout, end='')
            else:
                report['exit_code'] = compiled.returncode
        finally:
            after = manifest()
            report.update(after_hashes=after, source_unchanged=before == after)
            if not report['source_unchanged']:
                report['exit_code'] = 1
            if args.report is not None:
                args.report.parent.mkdir(parents=True, exist_ok=True)
                args.report.write_text(json.dumps(report, indent=2) + '\n')
        if not report['source_unchanged']:
            raise SystemExit('Production/test source changed during checks; rerun against frozen sources.')
    raise SystemExit(report['exit_code'])


if __name__ == '__main__':
    main()
