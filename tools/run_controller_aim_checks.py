#!/usr/bin/env python3
"""Exercise production twin-stick input, settings, saves and aim rendering offline.

Optional --capture-dir records disposable OnGUI evidence. Offline declarations
cannot certify Unity compilation, GPU output or physical controller feel.
"""
from pathlib import Path
import argparse
import json
import os
import re
import shutil
import subprocess
import tempfile

from run_core_tests import fixture

ROOT = Path(__file__).resolve().parents[1]


def check_axis_configuration():
    """The legacy InputManager must provide every production profile axis."""
    source = (ROOT / 'ProjectSettings/InputManager.asset').read_text()
    axes = []
    for block in re.split(r'^  - serializedVersion:', source, flags=re.M)[1:]:
        entry = {}
        for line in block.splitlines():
            match = re.match(r'^    ([A-Za-z_]+):\s*(.*?)\s*$', line)
            if match:
                entry[match.group(1)] = match.group(2)
        axes.append(entry)
    expected = {
        'Aim Horizontal': (3, 0), 'Aim Vertical': (4, 1),
        'Alt Aim Horizontal': (2, 0), 'Alt Aim Vertical': (3, 1),
    }
    for name, (axis, invert) in expected.items():
        matching = [entry for entry in axes if entry.get('m_Name') == name]
        if len(matching) != 1:
            raise SystemExit(f'{name}: expected one unique InputManager declaration, found {len(matching)}')
        entry = matching[0]
        for key, value in {'type': 2, 'axis': axis, 'invert': invert, 'joyNum': 0, 'dead': 0}.items():
            if float(entry.get(key, 'nan')) != value:
                raise SystemExit(f'{name}: {key} must be {value}, got {entry.get(key)}')
        for key in ('negativeButton', 'positiveButton', 'altNegativeButton', 'altPositiveButton'):
            if entry.get(key) != '':
                raise SystemExit(f'{name}: unexpected keyboard button {key}={entry.get(key)}')
    print('PASS four unique production right-stick InputManager axes: joystick profiles, vertical convention, zero engine deadzone and no keyboard bindings', flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--capture-dir', type=Path)
    args = parser.parse_args()
    check_axis_configuration()
    mono = os.environ.get('STARFALL_MONO', shutil.which('mono'))
    mcs = os.environ.get('STARFALL_MCS', shutil.which('mcs'))
    if not mono or not mcs:
        raise SystemExit('Install Mono (mono-mcs), or set STARFALL_MONO and STARFALL_MCS.')
    with tempfile.TemporaryDirectory(prefix='starfall-controller-aim-') as folder:
        temp = Path(folder)
        campaign = temp / 'CampaignFixture.cs'
        campaign.write_text(fixture(json.loads((ROOT / 'Assets/Resources/Frontier/Campaign.json').read_text())))
        sources = sorted((ROOT / 'Assets/Scripts/Frontier').rglob('*.cs'))
        sources += [ROOT / 'tests' / name for name in (
            'UnityLayoutStubs.cs', 'OfflineJson.cs', 'RuntimePolishChecks.cs',
            'VisualRuntimeChecks.cs', 'RenderStateChecks.cs', 'RuntimeContinuityChecks.cs',
            'ActorRegistrationChecks.cs', 'RuntimeControllerAimChecks.cs')]
        compiler = [mono, mcs] if mcs.endswith('.exe') else [mcs]
        exe = temp / 'controller-aim.exe'
        subprocess.run(compiler + ['-main:RuntimeControllerAimChecks', '-out:' + str(exe)]
                       + [str(path) for path in sources] + [str(campaign)], check=True)
        command = [mono, str(exe)]
        if args.capture_dir is not None:
            command.append(str(args.capture_dir.resolve()))
        subprocess.run(command, check=True, cwd=ROOT)


if __name__ == '__main__':
    main()
