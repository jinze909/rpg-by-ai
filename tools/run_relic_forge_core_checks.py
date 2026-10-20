#!/usr/bin/env python3
"""Check immutable relic-forge prices and real production refinement transactions.

Exercises exact costs, stale choices, full shortfalls, legacy saves and equipped
damage. This offline Core harness is not native Unity playtest evidence.
"""
from pathlib import Path
import hashlib
import json
import os
import shutil
import subprocess
import tempfile

from run_core_tests import fixture

ROOT = Path(__file__).resolve().parents[1]


def main():
    mono = os.environ.get('STARFALL_MONO', shutil.which('mono'))
    mcs = os.environ.get('STARFALL_MCS', shutil.which('mcs'))
    if not mono or not mcs:
        raise SystemExit('Install Mono, or set STARFALL_MONO and STARFALL_MCS.')
    campaign_path = ROOT / 'Assets/Resources/Frontier/Campaign.json'
    sources = sorted((ROOT / 'Assets/Scripts/Frontier/Core').glob('*.cs'))
    sources += sorted((ROOT / 'Assets/Tests/Frontier').glob('*Checks.cs'))
    watched = sources + [campaign_path, Path(__file__).resolve(), ROOT / 'tools/run_core_tests.py']
    before = {str(path): hashlib.sha256(path.read_bytes()).hexdigest() for path in watched}
    with tempfile.TemporaryDirectory(prefix='starfall-relic-forge-core-') as folder:
        temp = Path(folder)
        campaign = temp / 'CampaignFixture.cs'
        campaign.write_text(fixture(json.loads(campaign_path.read_text())))
        harness = temp / 'Harness.cs'
        harness.write_text('''using System; using System.Reflection; using Starfall.Tests;
class Harness { static int Main() {
    var data = CampaignFixture.Create(); int count = 0, failed = 0;
    foreach (var method in typeof(CoreChecks).GetMethods(BindingFlags.Public | BindingFlags.Static)) {
        if (!method.Name.StartsWith("Forge", StringComparison.Ordinal)) continue;
        count++;
        try { method.Invoke(null, new object[] { data }); Console.WriteLine("PASS " + method.Name); }
        catch (Exception error) { failed++; Console.WriteLine("FAIL " + method.Name + ": " + (error.InnerException ?? error)); }
    }
    Console.WriteLine("Relic forge Core groups: " + count + ", failures: " + failed);
    return count == 5 && failed == 0 ? 0 : 1;
} }
''')
        compiler = [mono, mcs] if mcs.endswith('.exe') else [mcs]
        exe = temp / 'relic-forge-core.exe'
        subprocess.run(compiler + ['-out:' + str(exe)] + [str(path) for path in sources]
                       + [str(campaign), str(harness)], check=True)
        result = subprocess.run([mono, str(exe)], cwd=ROOT)
    after = {str(path): hashlib.sha256(path.read_bytes()).hexdigest() for path in watched}
    if before != after:
        raise SystemExit('Production/test sources changed during checks; rerun with frozen sources.')
    raise SystemExit(result.returncode)


if __name__ == '__main__':
    main()
