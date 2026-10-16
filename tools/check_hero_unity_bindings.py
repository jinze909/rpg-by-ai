#!/usr/bin/env python3
"""Compile production/native test code against an installed Unity Editor's DLLs.

This does not launch Unity, import assets, execute tests, compile shaders or build
a player. Native Frontier/Animator/GPU acceptance remains NATIVE_UNVERIFIED.
No offline Unity declarations are referenced. No package installation is done.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import tarfile
import tempfile

ROOT = Path(__file__).resolve().parents[1]


def compile_plan(mono, mcs, engine, facade, nunit, test_runner, sources, output_dir):
    """Build actual compiler invocations, with isolated shipping-player branches.

    The installed UnityEngine directory also contains UnityEditor assemblies.
    A player compilation must not gain editor/test APIs through that directory.
    This pure plan is checked in CI even when no Unity SDK is installed there.
    """
    native_defines = ['UNITY_5_3_OR_NEWER', 'UNITY_2022_3']
    editor_defines = ['UNITY_EDITOR', 'UNITY_INCLUDE_TESTS'] + native_defines
    editor_refs = list(engine) + [facade]
    player_refs = [path for path in engine if path.name.startswith('UnityEngine.')
                   and path.name != 'UnityEngine.TestRunner.dll'] + [facade]
    stages = [
        ('Core', 'Core', [], False),
        ('Runtime', 'Runtime', ['Core'], False),
        ('Editor', 'Editor', ['Core', 'Runtime'], False),
        ('Tests', 'Tests', ['Core'], True),
        ('VisualTests', 'VisualTests', ['Core', 'Runtime'], True),
        ('PlayerCore', 'Core', [], False),
        ('PlayerRuntime', 'Runtime', ['PlayerCore'], False),
    ]
    outputs, plan = {}, []
    for stage, group, dependencies, tests in stages:
        player = stage.startswith('Player')
        defines = native_defines if player else editor_defines
        refs = list(player_refs if player else editor_refs) + [outputs[name] for name in dependencies]
        if tests:
            refs += [nunit, test_runner]
        output = output_dir / ('Starfall.' + stage + '.dll')
        command = [str(mono), str(mcs), '-target:library', '-define:' + ','.join(defines), '-out:' + str(output)]
        command += ['-r:' + str(path) for path in refs] + [str(path) for path in sources[group]]
        plan.append({'stage': stage, 'command': command, 'sources': len(sources[group]),
                     'defines': list(defines), 'references': [str(path) for path in refs]})
        outputs[stage] = output
    return plan


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--editor', required=True, type=Path, help='Installed Unity executable (2022.3.53f1 for this project).')
    parser.add_argument('--report', type=Path, help='Optional disposable JSON report outside Assets.')
    args = parser.parse_args()
    data = args.editor.resolve().parent / 'Data'
    mono = data / 'MonoBleedingEdge/bin-linux64/mono'
    mcs = data / 'MonoBleedingEdge/lib/mono/4.5/mcs.exe'
    facade = data / 'MonoBleedingEdge/lib/mono/4.5/Facades/netstandard.dll'
    references = sorted((data / 'Managed/UnityEngine').glob('*.dll'))
    nunit_archives = sorted((data / 'Resources/PackageManager/Editor').glob('com.unity.ext.nunit-*.tgz'))
    test_runners = sorted((data / 'Resources/PackageManager/ProjectTemplates/libcache').glob('*/ScriptAssemblies/UnityEngine.TestRunner.dll'))
    required = [args.editor, mono, mcs, facade]
    if not all(path.is_file() for path in required) or not references or not nunit_archives or not test_runners:
        raise SystemExit('An installed Linux Unity Editor with Mono, engine DLLs, bundled NUnit and TestRunner DLLs is required; no fallback declarations are allowed.')
    report_path = args.report.resolve() if args.report else None
    if report_path:
        if report_path.is_relative_to(ROOT / 'Assets'):
            raise SystemExit('Disposable reports must stay outside Assets.')
        report_path.parent.mkdir(parents=True, exist_ok=True)
        report_path.unlink(missing_ok=True)
    sources = {
        'Core': sorted((ROOT / 'Assets/Scripts/Frontier/Core').glob('*.cs')),
        'Runtime': sorted((ROOT / 'Assets/Scripts/Frontier/Runtime').glob('*.cs')),
        'Editor': sorted((ROOT / 'Assets/Editor/Frontier').glob('*.cs')),
        'Tests': sorted((ROOT / 'Assets/Tests/Frontier').glob('*.cs')),
        'VisualTests': sorted((ROOT / 'Assets/Tests/PlayMode').glob('*.cs')),
    }
    hashes = {str(path.relative_to(ROOT)): hashlib.sha256(path.read_bytes()).hexdigest() for paths in sources.values() for path in paths}
    results = []
    with tempfile.TemporaryDirectory(prefix='hero-unity-bindings-') as directory:
        temp = Path(directory)
        with tarfile.open(nunit_archives[0], 'r:gz') as archive:
            member = archive.getmember('package/net35/unity-custom/nunit.framework.dll')
            with archive.extractfile(member) as stream:
                nunit = temp / 'nunit.framework.dll'
                nunit.write_bytes(stream.read())
        plan = compile_plan(mono, mcs, references, facade, nunit, test_runners[0], sources, temp)
        for item in plan:
            stage = item['stage']
            result = subprocess.run(item['command'], cwd=ROOT, capture_output=True, text=True)
            diagnostic = result.stdout + result.stderr
            results.append({'stage': stage, 'exit': result.returncode, 'sources': item['sources'],
                            'defines': item['defines'], 'references': item['references'], 'diagnostic': diagnostic})
            print(diagnostic, end='')
            print(('PASS ' if result.returncode == 0 else 'FAIL ') + stage + ': actual Unity DLL binding compilation', flush=True)
            if result.returncode:
                break
    changed = [path for path, digest in hashes.items() if hashlib.sha256((ROOT / path).read_bytes()).hexdigest() != digest]
    report = {'editor': str(args.editor.resolve()), 'checks': results, 'source_drift': changed, 'native_editor_executed': False, 'native_acceptance': 'NATIVE_UNVERIFIED'}
    if report_path:
        report_path.write_text(json.dumps(report, indent=2) + '\n')
    if changed or len(results) != 7 or any(item['exit'] for item in results):
        raise SystemExit('Unity DLL binding compilation failed or sources changed during verification.')
    print('PASS all seven actual-DLL compilation stages, including isolated Player Core/Runtime. No native Editor/import/PlayMode/shader/GPU/player build was executed. NATIVE_UNVERIFIED.')


if __name__ == '__main__':
    main()
