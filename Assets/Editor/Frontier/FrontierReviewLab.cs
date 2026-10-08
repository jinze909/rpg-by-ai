using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Starfall.Editor
{
    public sealed class FrontierReviewLab : EditorWindow
    {
        private int profession, room;
        private bool encounter, pending;
        private readonly string[] classes = { "先锋 / Vanguard", "游侠 / Ranger", "星术师 / Arcanist" };
        private readonly string[] regions = { "余烬镇", "荆棘密林", "潮汐遗迹", "星晶矿场", "星陨堡垒", "雾港", "琉璃沙海", "霜钟高原", "地脉 · 第六层", "炉火旅店" };
        [MenuItem("Starfall/Visual QA/Open Review Lab")]
        public static void Open() { GetWindow<FrontierReviewLab>("Starfall Visual QA").Show(); }
        private void OnEnable() { EditorApplication.update += Poll; }
        private void OnDisable() { EditorApplication.update -= Poll; }
        private void Poll()
        {
            if (!pending || !EditorApplication.isPlaying) return;
            var app = UnityEngine.Object.FindObjectOfType<FrontierRuntime>();
            if (app == null || app.Game == null) return;
            pending = false; app.BeginVisualReview(profession, room, encounter); Repaint();
        }
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("在真实游戏渲染中检查角色、地形和战斗。测试场独立运行，自动和手动存档均被拦截；退出后恢复原旅程。Native review; saves are protected.", MessageType.Info);
            profession = EditorGUILayout.Popup("主角装备 / Class", profession, classes);
            room = EditorGUILayout.Popup("地区 / Region", room, regions);
            encounter = EditorGUILayout.Toggle("触发地区 Boss", encounter);
            if (GUILayout.Button("进入 / 重建测试场"))
            {
                if (!EditorApplication.isPlaying)
                {
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                    EditorSceneManager.OpenScene("Assets/Scenes/Frontier.unity"); pending = true; EditorApplication.isPlaying = true;
                }
                else
                {
                    var current = UnityEngine.Object.FindObjectOfType<FrontierRuntime>();
                    if (current != null) current.BeginVisualReview(profession, room, encounter);
                    else EditorGUILayout.HelpBox("请先运行 Frontier 场景。", MessageType.Warning);
                }
            }
            var app = UnityEngine.Object.FindObjectOfType<FrontierRuntime>();
            if (app == null || !app.VisualReviewActive) return;
            app.VisualReviewPaused = EditorGUILayout.Toggle("冻结时间 / Freeze", app.VisualReviewPaused);
            if (GUILayout.Button("步进 1/60 秒")) app.AdvanceVisualReview(1f / 60, new Point());
            if (GUILayout.Button("步进 0.1 秒")) for (int i = 0; i < 6; i++) app.AdvanceVisualReview(1f / 60, new Point());
            if (GUILayout.Button("普通攻击 / Basic attack")) app.Game.Attack(new Point(1, 0));
            if (GUILayout.Button("职业战技 / Class technique")) app.Game.Skill();
            if (GUILayout.Button("截图（Game View）"))
            {
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/native-captures")); Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "region-" + room + "-class-" + profession + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".png");
                EditorApplication.ExecuteMenuItem("Window/General/Game"); app.StartCoroutine(Capture(path));
            }
            if (GUILayout.Button("退出测试场，恢复原旅程")) app.EndVisualReview();
            EditorGUILayout.HelpBox("取消冻结后，在 Game View 使用原操作游玩。区域布局、伤害数值与 Boss AI 使用正式代码；测试场只提前解锁路线。截图写入 docs/native-captures。", MessageType.None);
        }
        private static IEnumerator Capture(string path)
        { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(path); Debug.Log("Native game screenshot: " + path); }
    }
}
