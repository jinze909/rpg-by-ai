#!/usr/bin/env python3
"""Exercise combat-contract guidance, unchanged accounting and navigation ownership through shipped Update/OnGUI offline.

--baseline-ref compiles older immutable production with this same harness for
semantic RED evidence. Captures are actual OnGUI command streams, never native
Unity/GPU or player-feel evidence. NATIVE_UNVERIFIED.
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
              'compile_exit_code': None, 'exit_code': None,
              'groups_passed': 0, 'groups_failed': None,
              'compile_output': '', 'runtime_output': '', 'captures': [],
              'layout_overflows': [], 'capture_count': 0, 'rendering': 'offline approximate font metrics; NATIVE_UNVERIFIED'}
    with tempfile.TemporaryDirectory(prefix='starfall-contract-hunt-') as folder:
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
            'ActorRegistrationChecks.cs', 'RuntimeContractHuntChecks.cs')]
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
            exe = temp / 'contract-hunt.exe'
            compiled = subprocess.run(compiler + ['-main:RuntimeContractHuntChecks', '-out:' + str(exe)]
                                      + [str(path) for path in sources] + [str(campaign)], cwd=ROOT,
                                      text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
            report.update(compile_exit_code=compiled.returncode, compile_output=compiled.stdout)
            print(compiled.stdout, end='')
            if compiled.returncode == 0:
                if args.capture_dir:
                    args.capture_dir.mkdir(parents=True, exist_ok=True)
                    prefix = args.capture_prefix or ('qa-contract-hunt-before' if args.baseline_ref else 'qa-contract-hunt')
                    for stale in args.capture_dir.glob(prefix + '-*.json'):
                        stale.unlink()
                    for stale in (args.capture_dir / 'previews').glob(prefix + '-*.png'):
                        stale.unlink()
                command = [mono, str(exe), str(args.capture_dir.resolve()) if args.capture_dir else '-',
                           args.capture_prefix or ('qa-contract-hunt-before' if args.baseline_ref else 'qa-contract-hunt')]
                run = subprocess.run(command, cwd=ROOT, text=True,
                                     stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
                summary = re.search(r'Runtime contract hunt: ([0-4])/4 groups passed;', run.stdout)
                passed = int(summary.group(1)) if summary else 0
                report.update(exit_code=run.returncode, runtime_output=run.stdout,
                              groups_passed=passed, groups_failed=4-passed)
                if run.returncode == 0 and passed != 4:
                    report['exit_code'] = 1
                    print('Missing complete 4/4 Runtime group summary; refusing an incomplete success.')
                print(run.stdout, end='')
                if args.capture_dir:
                    # Render only this run's real OnGUI streams. A private view
                    # of the asset root keeps other suites' captures untouched.
                    import render_layout
                    capture_dir = args.capture_dir.resolve()
                    names = sorted(capture_dir.glob((args.capture_prefix or ('qa-contract-hunt-before' if args.baseline_ref else 'qa-contract-hunt')) + '-*.json'))
                    view = temp / 'render-view'
                    (view / '.starfall-checks').mkdir(parents=True)
                    (view / 'Assets').symlink_to(ROOT / 'Assets', target_is_directory=True)
                    (view / '.starfall-checks/layout').symlink_to(capture_dir, target_is_directory=True)
                    original_root = render_layout.ROOT
                    try:
                        render_layout.ROOT = view
                        previews = capture_dir / 'previews'
                        previews.mkdir(exist_ok=True)
                        for path in names:
                            overflow = render_layout.render(path.stem)
                            target = previews / (path.stem + '.png')
                            shutil.copyfile(view / '.starfall-checks/qa-previews' / target.name, target)
                            commands = json.loads(path.read_text())
                            report['captures'].append({'commands': str(path), 'preview': str(target),
                                                       'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
                                                       'preview_sha256': hashlib.sha256(target.read_bytes()).hexdigest(),
                                                       'viewport': commands[0].get('screen') if commands else None})
                            report['layout_overflows'].extend({'capture': path.stem, 'text': o[0], 'estimated_height': o[1], 'available_height': o[2]} for o in overflow)
                    except Exception:
                        report['exit_code'] = 1
                        raise
                    finally:
                        render_layout.ROOT = original_root
                    report['capture_count'] = len(names)
                    print('Offline rendered captures: %d; potential text overflows: %d; NATIVE_UNVERIFIED' % (len(names), len(report['layout_overflows'])))
            else:
                report['exit_code'] = compiled.returncode
        finally:
            report['source_unchanged'] = before == manifest()
            if args.report is not None:
                args.report.parent.mkdir(parents=True, exist_ok=True)
                args.report.write_text(json.dumps(report, indent=2) + '\n')
        if not report['source_unchanged']:
            raise SystemExit('Production/test source changed during checks; rerun against frozen sources.')
    raise SystemExit(report['exit_code'])


if __name__ == '__main__':
    main()
