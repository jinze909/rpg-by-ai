#!/usr/bin/env python3
"""Exercise discovered gathering landmarks through real shipped Update and OnGUI paths.

--baseline-ref or --source-root runs the same harness against isolated production
for semantic RED verification. These are offline runtime checks only:
NATIVE_UNVERIFIED (no native import, GPU, hardware input or play acceptance).
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


def capture_layout(paths):
    from PIL import ImageFont
    from render_layout import wrap_text
    fonts = {}
    checked = 0
    overflows = []
    for path in paths:
        for item in json.loads(path.read_text()):
            if item.get('k') != 'text':
                continue
            x, y, width, height = item['r']
            new_field = (abs(x-278) < .01 and abs(y-106) < .01
                         or abs(x-279) < .01 and abs(y-178) < .01
                         or abs(x-548) < .01 and abs(y-178) < .01
                         or abs(x-291) < .01 and abs(y-670) < .01
                         or abs(x-305) < .01 and abs(y-261) < .01
                         or abs(x-279) < .01 and any(abs(y-row) < .01 for row in range(225, 546, 64))
                         or abs(x-860) < .01 and abs(y-193) < .01)
            if not new_field:
                continue
            left, right, top, bottom = item.get('padding', [0, 0, 0, 0])
            size = item['size']
            if size not in fonts:
                fonts[size] = ImageFont.truetype(str(ROOT/'Assets/Resources/Frontier/Fonts/NotoSansSC-Regular.otf'), size)
            lines = wrap_text(item['text'], fonts[size], max(1, width-left-right-4))
            step = round(size*1.35)
            checked += 1
            if len(lines)*step > height-top-bottom+step*.6:
                overflows.append(dict(capture=str(path), text=item['text'], required=len(lines)*step, available=height-top-bottom))
    return dict(native='NATIVE_UNVERIFIED', checked=checked, overflows=overflows)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    source = parser.add_mutually_exclusive_group()
    source.add_argument('--source-root', type=Path, default=ROOT)
    source.add_argument('--baseline-ref')
    parser.add_argument('--report', type=Path)
    parser.add_argument('--capture-dir', type=Path)
    parser.add_argument('--capture-prefix')
    args = parser.parse_args()
    mono = os.environ.get('STARFALL_MONO') or shutil.which('mono')
    mcs = os.environ.get('STARFALL_MCS') or shutil.which('mcs')
    if not mono or not mcs:
        raise SystemExit('Install Mono, or set STARFALL_MONO and STARFALL_MCS.')
    require_actor_masks()
    report = {'native': 'NATIVE_UNVERIFIED', 'baseline_ref': args.baseline_ref,
              'mono': mono, 'mcs': mcs, 'groups_total': GROUPS,
              'compile_exit_code': None, 'execution_exit_code': None, 'exit_code': None,
              'groups_passed': 0, 'groups_failed': None,
              'compile_output': '', 'runtime_output': ''}
    with tempfile.TemporaryDirectory(prefix='starfall-gathering-landmark-') as folder:
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
            'ActorRegistrationChecks.cs', 'RuntimeSceneryCoverChecks.cs',
            'RuntimeLandmarkChecks.cs', 'RuntimeGatheringLandmarkChecks.cs')]
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
            exe = temp / 'gathering-landmark.exe'
            compiled = subprocess.run(compiler + ['-main:RuntimeGatheringLandmarkChecks', '-out:' + str(exe)]
                                      + [str(path) for path in sources] + [str(campaign)], cwd=ROOT,
                                      text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
            report.update(compile_exit_code=compiled.returncode, compile_output=compiled.stdout)
            print(compiled.stdout, end='')
            if compiled.returncode == 0:
                command = [mono, str(exe)]
                command += [str(args.capture_dir.resolve()) if args.capture_dir else '-',
                            args.capture_prefix or ('qa-gathering-landmark-before' if args.baseline_ref else 'qa-gathering-landmark')]
                run = subprocess.run(command, cwd=ROOT, text=True,
                                     stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
                summaries = re.findall(r'^Runtime gathering landmarks: ([0-4])/4 groups passed; NATIVE_UNVERIFIED$', run.stdout, re.MULTILINE)
                passed = int(summaries[0]) if len(summaries) == 1 else 0
                pass_lines = len(re.findall(r'^PASS .*; NATIVE_UNVERIFIED$', run.stdout, re.MULTILINE))
                fail_lines = len(re.findall(r'^FAIL ', run.stdout, re.MULTILINE))
                report.update(execution_exit_code=run.returncode, exit_code=run.returncode,
                              runtime_output=run.stdout, groups_passed=passed, groups_failed=GROUPS-passed,
                              summary_count=len(summaries), pass_lines=pass_lines, fail_lines=fail_lines)
                if len(summaries) != 1 or pass_lines != passed or fail_lines != GROUPS-passed or (run.returncode == 0) != (passed == GROUPS):
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
            captures = {}
            if args.capture_dir is not None and args.capture_dir.exists():
                capture_prefix = args.capture_prefix or ('qa-gathering-landmark-before' if args.baseline_ref else 'qa-gathering-landmark')
                captures = {str(path): hashlib.sha256(path.read_bytes()).hexdigest()
                            for path in sorted(args.capture_dir.glob(capture_prefix + '-*.json'))}
            report.update(capture_count=len(captures), capture_hashes=captures)
            if captures:
                report['capture_layout'] = capture_layout([Path(path) for path in captures])
                if report['capture_layout']['overflows']:
                    report['exit_code'] = 1
                    print('New gathering-landmark text overflows: ' + json.dumps(report['capture_layout']['overflows'], ensure_ascii=False))
            if args.report is not None:
                args.report.parent.mkdir(parents=True, exist_ok=True)
                args.report.write_text(json.dumps(report, indent=2) + '\n')
        if not report['source_unchanged']:
            raise SystemExit('Production/test source changed during checks; rerun against frozen sources.')
    raise SystemExit(report['exit_code'])


if __name__ == '__main__':
    main()
