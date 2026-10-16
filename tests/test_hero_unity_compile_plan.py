"""Check real compiler argument isolation without pretending to execute Unity."""
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'tools'))
from check_hero_unity_bindings import compile_plan


class HeroUnityCompilePlanChecks(unittest.TestCase):
    def setUp(self):
        # Unity ships editor DLLs alongside engine DLLs in this real folder.
        # Adversarial editor/test entries must never leak into Player commands.
        self.engine = [Path('/sdk/UnityEngine.dll'), Path('/sdk/UnityEngine.CoreModule.dll'),
                       Path('/sdk/UnityEngine.AnimationModule.dll'), Path('/sdk/UnityEditor.dll'),
                       Path('/sdk/UnityEditor.CoreModule.dll'), Path('/sdk/Unity.Cecil.dll'),
                       Path('/sdk/UnityEngine.TestRunner.dll'), Path('/sdk/nunit.framework.dll')]
        self.facade = Path('/sdk/Facades/netstandard.dll')
        self.sources = {group: [Path('/project/' + group + '.cs')]
                        for group in ('Core', 'Runtime', 'Editor', 'Tests', 'VisualTests')}
        self.plan = compile_plan(Path('/sdk/mono'), Path('/sdk/mcs.exe'), self.engine, self.facade,
                                 Path('/tests/nunit.framework.dll'), Path('/tests/UnityEngine.TestRunner.dll'),
                                 self.sources, Path('/output'))
        self.by_name = {item['stage']: item for item in self.plan}

    def test_all_seven_stages_execute_in_dependency_order(self):
        self.assertEqual(['Core', 'Runtime', 'Editor', 'Tests', 'VisualTests', 'PlayerCore', 'PlayerRuntime'],
                         [item['stage'] for item in self.plan])
        self.assertEqual(7, len(self.by_name))
        for item in self.plan:
            self.assertEqual(['/sdk/mono', '/sdk/mcs.exe', '-target:library'], item['command'][:3])
            self.assertIn('-out:/output/Starfall.' + item['stage'] + '.dll', item['command'])

    def test_player_compiles_native_branch_without_editor_or_test_symbols(self):
        for name in ('PlayerCore', 'PlayerRuntime'):
            item = self.by_name[name]
            self.assertEqual(['UNITY_5_3_OR_NEWER', 'UNITY_2022_3'], item['defines'])
            self.assertEqual(['-define:UNITY_5_3_OR_NEWER,UNITY_2022_3'],
                             [arg for arg in item['command'] if arg.startswith('-define:')])

    def test_player_rejects_editor_and_test_assemblies_from_mixed_sdk_directory(self):
        allowed = {str(path) for path in self.engine[:3]} | {str(self.facade)}
        for name in ('PlayerCore', 'PlayerRuntime'):
            refs = set(self.by_name[name]['references'])
            expected = allowed | ({'/output/Starfall.PlayerCore.dll'} if name == 'PlayerRuntime' else set())
            self.assertEqual(expected, refs)
            self.assertEqual(refs, {arg[3:] for arg in self.by_name[name]['command'] if arg.startswith('-r:')})

    def test_player_runtime_depends_only_on_player_core_not_editor_core(self):
        for name, expected in [('PlayerCore', []), ('PlayerRuntime', ['/output/Starfall.PlayerCore.dll'])]:
            self.assertEqual(expected, [path for path in self.by_name[name]['references'] if path.startswith('/output/')])

    def test_player_sources_are_actual_core_and_runtime_without_editor_or_test_sources(self):
        for name, group in [('PlayerCore', 'Core'), ('PlayerRuntime', 'Runtime')]:
            item = self.by_name[name]
            self.assertEqual(1, item['sources'])
            self.assertEqual([str(self.sources[group][0])], [arg for arg in item['command'] if arg.endswith('.cs')])

    def test_original_five_stages_keep_editor_symbols_references_and_test_bindings(self):
        dependencies = {'Core': [], 'Runtime': ['Core'], 'Editor': ['Core', 'Runtime'],
                        'Tests': ['Core'], 'VisualTests': ['Core', 'Runtime']}
        for name, parents in dependencies.items():
            item = self.by_name[name]
            self.assertEqual(['UNITY_EDITOR', 'UNITY_INCLUDE_TESTS', 'UNITY_5_3_OR_NEWER', 'UNITY_2022_3'], item['defines'])
            expected = [str(path) for path in self.engine] + [str(self.facade)]
            expected += ['/output/Starfall.' + parent + '.dll' for parent in parents]
            if name in ('Tests', 'VisualTests'):
                expected += ['/tests/nunit.framework.dll', '/tests/UnityEngine.TestRunner.dll']
            self.assertEqual(expected, item['references'])
            self.assertEqual([str(self.sources[name][0])], [arg for arg in item['command'] if arg.endswith('.cs')])


if __name__ == '__main__':
    unittest.main()
