#!/usr/bin/env python3
"""Check real journal pagination, input, tracking and SaveStore with offline declarations.

Native Unity font rendering, controller hardware and play feel remain NATIVE_UNVERIFIED.
--source-root permits a semantic RED check against an immutable prior checkout.
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
        raise SystemExit('Install Mono (mono-mcs), or set STARFALL_MONO and STARFALL_MCS.')
    production = args.source_root.resolve()
    sources = sorted((production / 'Assets/Scripts/Frontier').rglob('*.cs'))
    sources += [ROOT / 'tests' / name for name in (
        'UnityLayoutStubs.cs', 'OfflineJson.cs', 'RuntimePolishChecks.cs',
        'VisualRuntimeChecks.cs', 'RenderStateChecks.cs', 'RuntimeContinuityChecks.cs',
        'ActorRegistrationChecks.cs', 'RuntimeJournalPaginationChecks.cs')]
    campaign_path = production / 'Assets/Resources/Frontier/Campaign.json'
    watched = sources + [campaign_path, Path(__file__).resolve(), ROOT / 'tools/run_core_tests.py']

    def manifest():
        return {str(path): hashlib.sha256(path.read_bytes()).hexdigest() for path in watched}

    before = manifest()
    report = {'source_root': str(production), 'compile_exit_code': None,
              'execution_exit_code': None, 'native': 'NATIVE_UNVERIFIED'}
    try:
        with tempfile.TemporaryDirectory(prefix='starfall-journal-pagination-') as folder:
            temp = Path(folder)
            campaign = temp / 'CampaignFixture.cs'
            campaign.write_text(fixture(json.loads(campaign_path.read_text())))
            compiler = [mono, mcs] if mcs.endswith('.exe') else [mcs]
            exe = temp / 'journal-pagination.exe'
            compiled = subprocess.run(compiler + ['-main:RuntimeJournalPaginationChecks', '-out:' + str(exe)]
                                      + [str(path) for path in sources] + [str(campaign)])
            report['compile_exit_code'] = compiled.returncode
            if compiled.returncode == 0:
                command = [mono, str(exe)]
                if args.capture_dir is not None:
                    command.append(str(args.capture_dir.resolve()))
                report['execution_exit_code'] = subprocess.run(command, cwd=ROOT).returncode
    finally:
        after = manifest()
        report.update(before=before, after=after, source_unchanged=before == after)
        report['exit_code'] = (0 if report['compile_exit_code'] == 0 and report['execution_exit_code'] == 0
                               else report['compile_exit_code'] or report['execution_exit_code'] or 1)
        if not report['source_unchanged']:
            report['exit_code'] = 1
        if args.report is not None:
            args.report.parent.mkdir(parents=True, exist_ok=True)
            args.report.write_text(json.dumps(report, indent=2) + '\n')
    if not report['source_unchanged']:
        raise SystemExit('Production/test source changed during the check; rerun against frozen sources.')
    raise SystemExit(report['exit_code'])


if __name__ == '__main__':
    main()
