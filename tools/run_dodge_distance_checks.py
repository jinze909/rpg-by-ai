#!/usr/bin/env python3
"""Exercise shipped frame-rate-independent dodge Update input with offline Unity declarations.

--baseline-ref or --source-root compiles immutable older production with this harness for a semantic
RED. No native Unity Player, device or graphics validation is implied.
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
import tempfile
import tarfile

from run_core_tests import fixture

ROOT = Path(__file__).resolve().parents[1]


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
    report = {'native': 'NATIVE_UNVERIFIED', 'baseline_ref': args.baseline_ref,
              'groups_expected': 4, 'scenarios_expected_when_all_pass': 210, 'compile_exit_code': None,
              'execution_exit_code': None, 'exit_code': None,
              'groups_passed': 0, 'groups_failed': 4,
              'compile_output': '', 'runtime_output': ''}
    with tempfile.TemporaryDirectory(prefix='starfall-dodge-distance-') as folder:
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
            'ActorRegistrationChecks.cs', 'RuntimeDodgeDistanceChecks.cs')]
        campaign_path = production / 'Assets/Resources/Frontier/Campaign.json'
        watched = sources + [campaign_path, Path(__file__).resolve(), ROOT / 'tools/run_core_tests.py']

        def manifest():
            return {str(path): hashlib.sha256(path.read_bytes()).hexdigest() for path in watched}

        before = manifest()
        report.update(source_root=str(production), hashes=before)
        try:
            campaign = temp / 'CampaignFixture.cs'
            campaign.write_text(fixture(json.loads(campaign_path.read_text())))
            compiler = [mono, mcs] if mcs.endswith('.exe') else [mcs]
            exe = temp / 'dodge-distance.exe'
            compiled = subprocess.run(compiler + ['-main:RuntimeDodgeDistanceChecks', '-out:' + str(exe)]
                                      + [str(path) for path in sources] + [str(campaign)], cwd=ROOT,
                                      text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
            report.update(compile_exit_code=compiled.returncode, compile_output=compiled.stdout)
            print(compiled.stdout, end='')
            if compiled.returncode == 0:
                run = subprocess.run([mono, str(exe)], cwd=ROOT, text=True,
                                     stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
                summaries = re.findall(r'^Runtime dodge distance: ([0-4])/4 groups passed; ([0-9]+) scenarios; NATIVE_UNVERIFIED$', run.stdout, re.MULTILINE)
                passed = int(summaries[0][0]) if len(summaries) == 1 else 0
                scenarios = int(summaries[0][1]) if len(summaries) == 1 else 0
                pass_lines = len(re.findall(r'^PASS .*; NATIVE_UNVERIFIED$', run.stdout, re.MULTILINE))
                fail_lines = len(re.findall(r'^FAIL ', run.stdout, re.MULTILINE))
                report.update(execution_exit_code=run.returncode, exit_code=run.returncode,
                              runtime_output=run.stdout, groups_passed=passed, groups_failed=4-passed,
                              scenarios=scenarios, summary_count=len(summaries),
                              pass_lines=pass_lines, fail_lines=fail_lines)
                if len(summaries) != 1 or pass_lines != passed or fail_lines != 4-passed or (run.returncode == 0) != (passed == 4) or passed == 4 and scenarios != 210:
                    report['exit_code'] = 1
                    print('Runtime group/summary counts or exit status disagree; refusing incomplete evidence.')
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
