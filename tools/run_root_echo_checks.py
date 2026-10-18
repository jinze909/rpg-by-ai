#!/usr/bin/env python3
"""Exercise Root Echo through production input, hazards, draws and SaveStore offline.

Compares all six seeded floors with a checked-in immutable 3df9f32 fixture.
--verify-baseline recompiles that old source and checks the fixture provenance.
--capture-dir writes disposable OnGUI evidence, not native Unity playtest evidence.
"""
from pathlib import Path
import argparse
import io
import json
import os
import shutil
import subprocess
import tarfile
import tempfile

from run_core_tests import fixture
from run_fieldcraft_checks import require_actor_masks

ROOT = Path(__file__).resolve().parents[1]
BASELINE = '3df9f32c0b41f96b5d94cabf514a8b56301ccb57'
TOPOLOGY = r'''using System;using System.Text;using System.Globalization;using Starfall;
public static class AbyssTopologyBaseline {
static string N(float value){return value.ToString("R",CultureInfo.InvariantCulture);}
public static string Describe(GameCore g){var b=new StringBuilder();b.Append(g.save.abyssFloor+"|"+g.save.abyssSeed+"|"+String.Join("/",g.Map.rows));
foreach(var o in g.Map.objects)if(o.kind!="abyssEcho")b.Append("|O:"+o.id+":"+o.kind+":"+N(o.x)+":"+N(o.y));
foreach(var d in g.Map.decor)b.Append("|D:"+d.kind+":"+N(d.x)+":"+N(d.y));
foreach(var e in g.enemies)b.Append("|E:"+e.id+":"+e.kind+":"+e.boss+":"+N(e.x)+":"+N(e.y)+":"+N(e.hp)+":"+N(e.maxHp));return b.ToString();}
public static int Main(){var g=new GameCore(CampaignFixture.Create());g.NewGame(0);g.Close();g.SkipTutorial();g.save.frontierEnding=1;g.StartAbyss(2);
for(int floor=1;floor<=6;floor++){Console.WriteLine(Describe(g));if(floor<6){g.enemies.Clear();g.Talk(Array.Find(g.Map.objects,o=>o.kind=="abyssGate"));g.Choose(0);}}return 0;}}
'''


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--capture-dir', type=Path)
    parser.add_argument('--verify-baseline', action='store_true', help='Also compile immutable pre-feature source and verify the checked-in topology fixture; requires its Git object locally.')
    args = parser.parse_args()
    mono, mcs = os.environ.get('STARFALL_MONO', shutil.which('mono')), os.environ.get('STARFALL_MCS', shutil.which('mcs'))
    if not mono or not mcs:
        raise SystemExit('Install Mono (mono-mcs), or set STARFALL_MONO and STARFALL_MCS.')
    require_actor_masks()
    compiler = [mono, mcs] if mcs.endswith('.exe') else [mcs]
    preserved = json.loads((ROOT / 'tests/fixtures/root_echo_topology.json').read_text())
    if preserved.get('source_ref') != BASELINE or len(preserved.get('floors', [])) != 6:
        raise SystemExit('Invalid immutable Root Echo topology fixture.')
    with tempfile.TemporaryDirectory(prefix='starfall-root-echo-') as folder:
        temp = Path(folder)
        campaign = temp / 'CampaignFixture.cs'
        campaign.write_text(fixture(json.loads((ROOT / 'Assets/Resources/Frontier/Campaign.json').read_text())))
        topology = temp / 'AbyssTopologyBaseline.cs'
        topology.write_text(TOPOLOGY)
        topology_file = temp / 'topology.txt'
        topology_file.write_text('\n'.join(preserved['floors']) + '\n')
        if args.verify_baseline:
            baseline = temp / 'baseline'
            archive = subprocess.run(['git', 'archive', BASELINE, 'Assets/Scripts/Frontier/Core', 'Assets/Resources/Frontier/Campaign.json'], cwd=ROOT, check=True, stdout=subprocess.PIPE).stdout
            with tarfile.open(fileobj=io.BytesIO(archive)) as source:
                for member in source.getmembers():
                    target = (baseline / member.name).resolve()
                    if not target.is_relative_to(baseline.resolve()) or member.issym() or member.islnk():
                        raise SystemExit('Unsafe baseline member: ' + member.name)
                source.extractall(baseline, filter='data')
            baseline_campaign = temp / 'BaselineCampaignFixture.cs'
            baseline_campaign.write_text(fixture(json.loads((baseline / 'Assets/Resources/Frontier/Campaign.json').read_text())))
            baseline_exe = temp / 'baseline.exe'
            subprocess.run(compiler + ['-main:AbyssTopologyBaseline', '-out:' + str(baseline_exe)] + [str(p) for p in sorted((baseline / 'Assets/Scripts/Frontier/Core').glob('*.cs'))] + [str(baseline_campaign), str(topology)], check=True)
            result = subprocess.run([mono, str(baseline_exe)], check=True, stdout=subprocess.PIPE, text=True)
            if result.stdout.splitlines() != preserved['floors']:
                raise SystemExit('Immutable pre-feature source no longer matches the checked-in topology fixture.')
            print('PASS immutable old-source compilation matches all six checked-in topology fixtures.', flush=True)
        print('Immutable six-floor fixture: ' + BASELINE, flush=True)
        sources = sorted((ROOT / 'Assets/Scripts/Frontier').rglob('*.cs'))
        sources += [ROOT / 'tests' / name for name in ('UnityLayoutStubs.cs', 'OfflineJson.cs', 'RuntimePolishChecks.cs', 'VisualRuntimeChecks.cs', 'RenderStateChecks.cs', 'RuntimeContinuityChecks.cs', 'ActorRegistrationChecks.cs', 'RuntimeRootEchoChecks.cs')]
        exe = temp / 'root-echo.exe'
        subprocess.run(compiler + ['-main:RuntimeRootEchoChecks', '-out:' + str(exe)] + [str(p) for p in sources] + [str(campaign), str(topology)], check=True)
        command = [mono, str(exe), str(args.capture_dir.resolve()) if args.capture_dir else '-', str(topology_file)]
        raise SystemExit(subprocess.run(command, cwd=ROOT).returncode)


if __name__ == '__main__':
    main()
