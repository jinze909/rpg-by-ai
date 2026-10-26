#!/usr/bin/env python3
"""Run exactly four interaction priority Core groups with the selected Mono SDK.

The same final checks compile against archived production. They assert real
E-frame request retirement, paid-contact continuity, live target geometry and training/save ownership.
This offline harness does not verify native Unity import, rendering or playtests.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile

from run_core_tests import fixture

ROOT = Path(__file__).resolve().parents[1]
GROUPS = (
    'InteractionPriorityRetiresUnstartedPaidRequestsAtTheirReadiness',
    'InteractionPriorityPreservesPaidContactsAndWorldSimulation',
    'InteractionPriorityUsesPostMovementTargetAndDoesNotStealRejectedInputs',
    'InteractionPriorityRetainsTrainingHitStopAndLegacyOwnership',
)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', type=Path, default=ROOT)
    parser.add_argument('--report', type=Path, help='Retain JSON evidence and sibling compile/run logs.')
    args = parser.parse_args()
    source_root = args.source_root.resolve()
    mono = os.environ.get('STARFALL_MONO') or shutil.which('mono')
    mcs = os.environ.get('STARFALL_MCS') or shutil.which('mcs')
    if not mono or not mcs:
        raise SystemExit('Install Mono, or set STARFALL_MONO and STARFALL_MCS.')
    campaign_path = source_root / 'Assets/Resources/Frontier/Campaign.json'
    production = sorted((source_root / 'Assets/Scripts/Frontier/Core').glob('*.cs'))
    checks = sorted((ROOT / 'Assets/Tests/Frontier').glob('*Checks.cs'))
    if not production or not campaign_path.is_file():
        raise SystemExit('--source-root requires production Core and Campaign.json.')
    watched = production + checks + [campaign_path, Path(__file__).resolve(), ROOT / 'tools/run_core_tests.py']

    def hashes():
        return {str(path): hashlib.sha256(path.read_bytes()).hexdigest() for path in watched}

    before = hashes()
    report = {
        'source_root': str(source_root), 'groups_expected': list(GROUPS),
        'mono': mono, 'mcs': mcs, 'compiler_profile': 'Selected Mono compiler default', 'native': 'NATIVE_UNVERIFIED',
        'baseline_contract': 'Same final checks must compile and fail semantically on the same-frame paid-before-interaction baseline.',
        'input_sha256_before': before, 'compile_exit_code': None, 'runtime_exit_code': None,
    }
    compile_output, runtime_output = '', ''
    with tempfile.TemporaryDirectory(prefix='starfall-interaction-priority-core-') as folder:
        temp = Path(folder)
        campaign = temp / 'CampaignFixture.cs'
        campaign.write_text(fixture(json.loads(campaign_path.read_text())))
        names = ', '.join('"' + name + '"' for name in GROUPS)
        harness = temp / 'Harness.cs'
        harness.write_text('''using System; using System.Collections.Generic; using System.Reflection; using Starfall.Tests;
class Harness { static int Main() {
    var data = CampaignFixture.Create(); int failed = 0;
    string[] expected = new string[] { ''' + names + ''' };
    var discovered = new List<string>();
    foreach (var method in typeof(CoreChecks).GetMethods(BindingFlags.Public | BindingFlags.Static))
        if (method.Name.StartsWith("InteractionPriority", StringComparison.Ordinal)) discovered.Add(method.Name);
    var required = new List<string>(expected); required.Sort(StringComparer.Ordinal); discovered.Sort(StringComparer.Ordinal);
    bool exact = required.Count == discovered.Count;
    if (exact) for (int i = 0; i < required.Count; i++) if (required[i] != discovered[i]) exact = false;
    if (!exact) { Console.WriteLine("FAIL group discovery: expected exactly the four named InteractionPriority groups"); return 1; }
    foreach (string name in expected) {
        try { typeof(CoreChecks).GetMethod(name).Invoke(null, new object[] { data }); Console.WriteLine("PASS " + name); }
        catch (Exception error) { failed++; Console.WriteLine("FAIL " + name + ": " + (error.InnerException ?? error)); }
    }
    Console.WriteLine("Interaction-priority Core groups: " + discovered.Count + ", failures: " + failed);
    return failed == 0 ? 0 : 1;
} }
''')
        exe = temp / 'interaction-priority-core.exe'
        compiler = [mono, mcs] if mcs.endswith('.exe') else [mcs]
        command = compiler + ['-out:' + str(exe)]
        command += [str(path) for path in production + checks + [campaign, harness]]
        report['compile_command'] = command
        compiled = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
        report['compile_exit_code'], compile_output = compiled.returncode, compiled.stdout
        print(compile_output, end='')
        if compiled.returncode == 0:
            executed = subprocess.run([mono, str(exe)], cwd=source_root, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
            report['runtime_exit_code'], runtime_output = executed.returncode, executed.stdout
            print(runtime_output, end='')
    after = hashes()
    passed = [line[5:] for line in runtime_output.splitlines() if line.startswith('PASS InteractionPriority')]
    failed = [line[5:].split(':', 1)[0] for line in runtime_output.splitlines() if line.startswith('FAIL InteractionPriority')]
    valid = (report['compile_exit_code'] == 0 and report['runtime_exit_code'] == 0
             and sorted(passed) == sorted(GROUPS) and not failed and before == after)
    report.update(groups_passed=passed, groups_failed=failed, input_sha256_after=after,
                  inputs_unchanged=before == after, exit_code=0 if valid else 1)
    if args.report:
        path = args.report.resolve()
        path.parent.mkdir(parents=True, exist_ok=True)
        path.with_suffix('.compile.log').write_text(compile_output)
        path.with_suffix('.run.log').write_text(runtime_output)
        path.write_text(json.dumps(report, indent=2) + '\n')
        print('Evidence report: ' + str(path))
    if before != after:
        print('Inputs changed during checks; rerun against frozen sources.')
    raise SystemExit(report['exit_code'])


if __name__ == '__main__':
    main()
