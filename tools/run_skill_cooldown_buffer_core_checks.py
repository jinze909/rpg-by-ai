#!/usr/bin/env python3
"""Run only the five cooldown-tail Q Core groups with Mono.

--source-root selects production Core and campaign inputs independently of this
checkout's current regression checks, allowing an archived baseline RED run.
--report retains compile/run logs and before/after hashes of every input. This
offline harness provides neither native Unity compilation nor physical-input evidence.
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
    'SkillCooldownThresholdsAndOverlappingReadiness',
    'SkillCooldownRetainsReleasedVolleyAndExecutionValues',
    'SkillCooldownRejectsInvalidRequestsAndRechecksCosts',
    'SkillCooldownHonorsDiscretePriorityAndHitStop',
    'SkillCooldownClearsAtLifecycleProfilesAndTrainingReview',
)


def manifest(paths):
    return {str(path): hashlib.sha256(path.read_bytes()).hexdigest() for path in paths}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', type=Path, default=ROOT)
    parser.add_argument('--report', type=Path, help='Write JSON evidence and sibling compile/run logs.')
    args = parser.parse_args()
    source_root = args.source_root.resolve()
    mono = os.environ.get('STARFALL_MONO') or shutil.which('mono')
    mcs = os.environ.get('STARFALL_MCS') or shutil.which('mcs')
    if not mono or not mcs:
        raise SystemExit('Install Mono, or set STARFALL_MONO and STARFALL_MCS (PATH is also supported).')
    campaign_path = source_root / 'Assets/Resources/Frontier/Campaign.json'
    production = sorted((source_root / 'Assets/Scripts/Frontier/Core').glob('*.cs'))
    checks = sorted((ROOT / 'Assets/Tests/Frontier').glob('*Checks.cs'))
    if not production or not campaign_path.is_file():
        raise SystemExit('--source-root must contain the production Core and shipped Campaign.json.')
    watched = sorted(set(production + checks + [campaign_path, Path(__file__).resolve(), ROOT / 'tools/run_core_tests.py']))
    before = manifest(watched)
    compile_exit, runtime_exit, compile_output, runtime_output = None, None, '', ''
    report = {
        'source_root': str(source_root), 'tests_root': str(ROOT), 'groups_expected': list(GROUPS),
        'mono': mono, 'mcs': mcs, 'input_sha256_before': before,
    }
    try:
        with tempfile.TemporaryDirectory(prefix='starfall-skill-cooldown-core-') as folder:
            temp = Path(folder)
            campaign = temp / 'CampaignFixture.cs'
            campaign.write_text(fixture(json.loads(campaign_path.read_text())))
            names = ', '.join('"' + name + '"' for name in GROUPS)
            harness = temp / 'Harness.cs'
            harness.write_text('''using System; using System.Reflection; using Starfall.Tests;
class Harness { static int Main() {
    var data = CampaignFixture.Create(); int count = 0, failed = 0;
    string[] expected = new string[] { ''' + names + ''' };
    foreach (var method in typeof(CoreChecks).GetMethods(BindingFlags.Public | BindingFlags.Static)) {
        if (method.Name.StartsWith("SkillCooldown", StringComparison.Ordinal)) count++;
    }
    foreach (string name in expected) {
        try {
            var method = typeof(CoreChecks).GetMethod(name, BindingFlags.Public | BindingFlags.Static);
            if (method == null) throw new Exception("Missing required Core group");
            method.Invoke(null, new object[] { data }); Console.WriteLine("PASS " + name);
        }
        catch (Exception error) { failed++; Console.WriteLine("FAIL " + name + ": " + (error.InnerException ?? error)); }
    }
    Console.WriteLine("Skill cooldown-buffer Core groups: " + count + ", failures: " + failed);
    return count == 5 && failed == 0 ? 0 : 1;
} }
''')
            exe = temp / 'skill-cooldown-core.exe'
            compiler = [mono, mcs] if mcs.endswith('.exe') else [mcs]
            compiled = subprocess.run(compiler + ['-out:' + str(exe)]
                                      + [str(path) for path in production + checks + [campaign, harness]],
                                      stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
            compile_exit, compile_output = compiled.returncode, compiled.stdout
            print(compile_output, end='')
            if compile_exit == 0:
                executed = subprocess.run([mono, str(exe)], cwd=source_root, stdout=subprocess.PIPE,
                                          stderr=subprocess.STDOUT, text=True)
                runtime_exit, runtime_output = executed.returncode, executed.stdout
                print(runtime_output, end='')
    except OSError as error:
        compile_output += str(error) + '\n'
        print(str(error))
    after = manifest(watched)
    changed = [path for path in before if before[path] != after.get(path)]
    passed = [line[5:] for line in runtime_output.splitlines() if line.startswith('PASS SkillCooldown')]
    failed = [line[5:].split(':', 1)[0] for line in runtime_output.splitlines() if line.startswith('FAIL SkillCooldown')]
    valid = compile_exit == 0 and runtime_exit == 0 and sorted(passed) == sorted(GROUPS) and not failed and not changed
    report.update({
        'compile_exit_code': compile_exit, 'runtime_exit_code': runtime_exit,
        'groups_passed': passed, 'groups_failed': failed, 'input_sha256_after': after,
        'inputs_unchanged': not changed, 'changed_inputs': changed, 'exit_code': 0 if valid else 1,
    })
    if args.report:
        report_path = args.report.resolve()
        report_path.parent.mkdir(parents=True, exist_ok=True)
        compile_log = report_path.with_suffix('.compile.log')
        runtime_log = report_path.with_suffix('.run.log')
        compile_log.write_text(compile_output)
        runtime_log.write_text(runtime_output)
        report.update({'compile_log': str(compile_log), 'runtime_log': str(runtime_log)})
        report_path.write_text(json.dumps(report, indent=2) + '\n')
        print('Evidence report: ' + str(report_path))
    if changed:
        print('Inputs changed during checks; rerun with frozen sources.')
    raise SystemExit(report['exit_code'])


if __name__ == '__main__':
    main()
