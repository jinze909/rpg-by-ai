#!/usr/bin/env python3
"""Exercise batch supply quotes, exact transactions and real host conversations.

Uses shipped Core and campaign data; --source-root permits an isolated baseline
RED check. Offline Core evidence does not certify native Unity playback.
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
    parser.add_argument('--report', type=Path)
    args = parser.parse_args()
    mono = os.environ.get('STARFALL_MONO', shutil.which('mono'))
    mcs = os.environ.get('STARFALL_MCS', shutil.which('mcs'))
    if not mono or not mcs:
        raise SystemExit('Install Mono, or set STARFALL_MONO and STARFALL_MCS.')
    production = args.source_root.resolve()
    campaign_path = production / 'Assets/Resources/Frontier/Campaign.json'
    sources = sorted((production / 'Assets/Scripts/Frontier/Core').glob('*.cs'))
    sources += sorted((ROOT / 'Assets/Tests/Frontier').glob('*Checks.cs'))
    watched = sources + [campaign_path, Path(__file__).resolve(), ROOT / 'tools/run_core_tests.py']

    def manifest():
        return {str(path): hashlib.sha256(path.read_bytes()).hexdigest() for path in watched}

    before = manifest()
    compile_code, exit_code = None, None
    try:
        with tempfile.TemporaryDirectory(prefix='starfall-supply-preparation-') as folder:
            temp = Path(folder)
            campaign = temp / 'CampaignFixture.cs'
            campaign.write_text(fixture(json.loads(campaign_path.read_text())))
            harness = temp / 'Harness.cs'
            harness.write_text('''using System; using System.Reflection; using Starfall.Tests;
class Harness { static int Main() {
    var data = CampaignFixture.Create(); int count = 0, failed = 0;
    foreach (var method in typeof(CoreChecks).GetMethods(BindingFlags.Public | BindingFlags.Static)) {
        if (!method.Name.StartsWith("SupplyPreparation", StringComparison.Ordinal)) continue;
        count++;
        try { method.Invoke(null, new object[] { data }); Console.WriteLine("PASS " + method.Name); }
        catch (Exception error) { failed++; Console.WriteLine("FAIL " + method.Name + ": " + (error.InnerException ?? error)); }
    }
    Console.WriteLine("Supply preparation Core groups: " + count + ", failures: " + failed);
    return count == 5 && failed == 0 ? 0 : 1;
} }
''')
            compiler = [mono, mcs] if mcs.endswith('.exe') else [mcs]
            exe = temp / 'supply-preparation-core.exe'
            compiled = subprocess.run(compiler + ['-out:' + str(exe)] + [str(path) for path in sources]
                                      + [str(campaign), str(harness)], cwd=ROOT)
            compile_code = compiled.returncode
            exit_code = subprocess.run([mono, str(exe)], cwd=ROOT).returncode if compile_code == 0 else compile_code
    finally:
        unchanged = manifest() == before
        if args.report is not None:
            args.report.parent.mkdir(parents=True, exist_ok=True)
            args.report.write_text(json.dumps({'compile_exit_code': compile_code, 'exit_code': exit_code,
                'source_root': str(production), 'source_unchanged': unchanged, 'groups_required': 5,
                'native': 'NATIVE_UNVERIFIED'}, indent=2) + '\n')
    if not unchanged:
        raise SystemExit('Production/test sources changed during checks; rerun against frozen sources.')
    raise SystemExit(exit_code)


if __name__ == '__main__':
    main()
