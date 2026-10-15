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
        common = [str(mono), str(mcs), '-target:library', '-define:UNITY_EDITOR,UNITY_INCLUDE_TESTS,UNITY_5_3_OR_NEWER,UNITY_2022_3']
        engine = references + [facade]
        outputs = {}
        for stage, dependencies in [('Core', []), ('Runtime', ['Core']), ('Editor', ['Core', 'Runtime']), ('Tests', ['Core']), ('VisualTests', ['Core', 'Runtime'])]:
            output = temp / ('Starfall.' + stage + '.dll')
            refs = engine + [outputs[name] for name in dependencies]
            if stage in ('Tests', 'VisualTests'):
                refs += [nunit, test_runners[0]]
            command = common + ['-out:' + str(output)] + ['-r:' + str(path) for path in refs] + [str(path) for path in sources[stage]]
            result = subprocess.run(command, cwd=ROOT, capture_output=True, text=True)
            diagnostic = result.stdout + result.stderr
            results.append({'stage': stage, 'exit': result.returncode, 'sources': len(sources[stage]), 'diagnostic': diagnostic})
            print(diagnostic, end='')
            print(('PASS ' if result.returncode == 0 else 'FAIL ') + stage + ': actual Unity DLL binding compilation', flush=True)
            if result.returncode:
                break
            outputs[stage] = output
    changed = [path for path, digest in hashes.items() if hashlib.sha256((ROOT / path).read_bytes()).hexdigest() != digest]
    report = {'editor': str(args.editor.resolve()), 'checks': results, 'source_drift': changed, 'native_editor_executed': False, 'native_acceptance': 'NATIVE_UNVERIFIED'}
    if report_path:
        report_path.write_text(json.dumps(report, indent=2) + '\n')
    if changed or len(results) != 5 or any(item['exit'] for item in results):
        raise SystemExit('Unity DLL binding compilation failed or sources changed during verification.')
    print('PASS all five actual-DLL compilation stages. No native Editor/import/PlayMode/shader/GPU/player build was executed. NATIVE_UNVERIFIED.')


if __name__ == '__main__':
    main()
