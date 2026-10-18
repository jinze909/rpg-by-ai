#!/usr/bin/env python3
"""Run the seven production Core focus groups with the shipped campaign and Mono.

These checks exercise real actions and world transitions, not Unity rendering or
physical input. UnityCoreTests wraps the same checks for native EditMode runs.
"""
from pathlib import Path
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
        raise SystemExit('Install Mono (mono-mcs), or set STARFALL_MONO and STARFALL_MCS.')
    with tempfile.TemporaryDirectory(prefix='starfall-combat-focus-core-') as folder:
        temp = Path(folder)
        campaign = temp / 'CampaignFixture.cs'
        campaign.write_text(fixture(json.loads((ROOT / 'Assets/Resources/Frontier/Campaign.json').read_text())))
        harness = temp / 'Harness.cs'
        harness.write_text('''using System; using System.Reflection; using Starfall.Tests;
class Harness { static int Main() {
    var data = CampaignFixture.Create(); int count = 0, failed = 0;
    foreach (var method in typeof(CoreChecks).GetMethods(BindingFlags.Public | BindingFlags.Static)) {
        if (!method.Name.StartsWith("CombatFocus", StringComparison.Ordinal)) continue;
        count++;
        try { method.Invoke(null, new object[] { data }); Console.WriteLine("PASS " + method.Name); }
        catch (Exception error) { failed++; Console.WriteLine("FAIL " + method.Name + ": " + (error.InnerException ?? error).Message); }
    }
    Console.WriteLine("Combat focus Core groups: " + count + ", failures: " + failed);
    return count == 7 && failed == 0 ? 0 : 1;
} }
''')
        sources = sorted((ROOT / 'Assets/Scripts/Frontier/Core').glob('*.cs'))
        sources += sorted((ROOT / 'Assets/Tests/Frontier').glob('*Checks.cs'))
        compiler = [mono, mcs] if mcs.endswith('.exe') else [mcs]
        exe = temp / 'combat-focus-core.exe'
        subprocess.run(compiler + ['-out:' + str(exe)] + [str(path) for path in sources]
                       + [str(campaign), str(harness)], check=True)
        subprocess.run([mono, str(exe)], check=True, cwd=ROOT)


if __name__ == '__main__':
    main()
