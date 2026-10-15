#if UNITY_EDITOR && UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Starfall.Editor
{
    // Existing atlas importers and GUIDs are untouched. Generated sprites are
    // whole-frame subassets, not new sliced imports or disconnected body parts.
    [InitializeOnLoad]
    public static class HeroAnimationAssets
    {
        public const string LibraryPath = "Assets/Resources/Frontier/HeroAnimationLibrary.asset";
        public const string ControllerPath = "Assets/Resources/Frontier/HeroWholeBody.controller";
        private const string ArtRoot = "Assets/Resources/Frontier/Art/";
        private static bool queued, generating;

        static HeroAnimationAssets()
        {
            QueueEnsure();
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        internal static void QueueEnsure()
        {
            if (queued || generating) return;
            queued = true;
            EditorApplication.delayCall += EnsureWhenReady;
        }

        private static void EnsureWhenReady()
        {
            queued = false;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { QueueEnsure(); return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { Ensure(); }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;
            try { Ensure(); }
            catch (Exception ex) { EditorApplication.isPlaying = false; Debug.LogException(ex); }
        }

        [MenuItem("Starfall/Animation/Rebuild hero animation library")]
        public static void Rebuild() { Generate(Fingerprint()); }

        public static void Ensure()
        {
            if (generating) throw new InvalidOperationException("Recursive hero animation asset generation.");
            string fingerprint = Fingerprint();
            HeroAnimationLibrary library = AssetDatabase.LoadAssetAtPath<HeroAnimationLibrary>(LibraryPath);
            if (library == null || library.SourceFingerprint != fingerprint || library.Version != HeroAnimationLibrary.FormatVersion)
                Generate(fingerprint);
            ValidateGenerated(AssetDatabase.LoadAssetAtPath<HeroAnimationLibrary>(LibraryPath));
        }

        private static string Fingerprint()
        {
            StringBuilder text = new StringBuilder("whole-body-hero-" + HeroAnimationLibrary.FormatVersion);
            foreach (string atlas in HeroAnimationState.Atlases)
                foreach (string path in Paths(atlas))
                {
                    if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null) throw new InvalidOperationException("Missing imported hero atlas or alpha mask: " + path);
                    text.Append('|').Append(path).Append(':').Append(AssetDatabase.GetAssetDependencyHash(path));
                }
            return Hash128.Compute(text.ToString()).ToString();
        }

        private static string[] Paths(string atlas)
        { return new[] { ArtRoot + atlas + ".png", ArtRoot + "ActorMasks/outline_" + atlas + ".png", ArtRoot + "ActorMasks/flash_" + atlas + ".png" }; }

        private static void Generate(string fingerprint)
        {
            if (generating) throw new InvalidOperationException("Recursive hero animation asset generation.");
            generating = true;
            try
            {
                HeroAnimationLibrary library = AssetDatabase.LoadAssetAtPath<HeroAnimationLibrary>(LibraryPath);
                if (library == null)
                {
                    library = ScriptableObject.CreateInstance<HeroAnimationLibrary>(); library.name = "Viola whole-body animation library";
                    AssetDatabase.CreateAsset(library, LibraryPath);
                }
                AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
                if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
                ClearGeneratedController(controller);
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(LibraryPath))
                    if (asset != null && asset != library) UnityEngine.Object.DestroyImmediate(asset, true);

                controller.AddLayer("Base Layer");
                AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
                List<HeroAnimationLibrary.FrameSet> sets = new List<HeroAnimationLibrary.FrameSet>();
                foreach (string atlas in HeroAnimationState.Atlases)
                {
                    string[] paths = Paths(atlas);
                    HeroAnimationLibrary.FrameSet set = new HeroAnimationLibrary.FrameSet {
                        atlas = atlas, source = AssetDatabase.LoadAssetAtPath<Texture2D>(paths[0]),
                        outlineSource = AssetDatabase.LoadAssetAtPath<Texture2D>(paths[1]), flashSource = AssetDatabase.LoadAssetAtPath<Texture2D>(paths[2])
                    };
                    set.body = Frames(library, set.source, atlas);
                    set.outline = Frames(library, set.outlineSource, atlas + "_outline");
                    set.flash = Frames(library, set.flashSource, atlas + "_flash");
                    set.clip = Clip(library, atlas, set.body);
                    AnimatorState state = stateMachine.AddState(atlas); state.motion = set.clip; state.writeDefaultValues = false;
                    sets.Add(set);
                }
                stateMachine.defaultState = stateMachine.states[0].state;
                library.SetGeneratedData(fingerprint, controller, sets.ToArray());
                EditorUtility.SetDirty(library); EditorUtility.SetDirty(controller); EditorUtility.SetDirty(stateMachine);
                AssetDatabase.SaveAssets();
                ValidateGenerated(library);
                Debug.Log("Generated Viola's whole-body native animation library: " + sets.Count + " atlases, " + sets.Count * 12 + " complete body sprites; original asset GUIDs retained.");
            }
            finally { generating = false; }
        }

        private static void ClearGeneratedController(AnimatorController controller)
        {
            // Preserve the controller's .meta GUID, including when regenerating
            // after an accepted atlas repair. No mature controller is edited.
            controller.layers = new AnimatorControllerLayer[0];
            controller.parameters = new AnimatorControllerParameter[0];
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
                if (asset != null && asset != controller) UnityEngine.Object.DestroyImmediate(asset, true);
        }

        private static Sprite[] Frames(HeroAnimationLibrary library, Texture2D texture, string name)
        {
            if (texture == null || texture.width != 1024 || texture.height != 768)
                throw new InvalidOperationException("Whole-body hero texture must be 4x3 complete 256-pixel frames: " + name);
            Sprite[] sprites = new Sprite[12];
            for (int frame = 0; frame < sprites.Length; frame++)
            {
                Rect rect = new Rect(frame % 4 * 256, texture.height - (frame / 4 + 1) * 256, 256, 256);
                sprites[frame] = Sprite.Create(texture, rect, new Vector2(.5f, .0625f), 1, 0, SpriteMeshType.FullRect, Vector4.zero, false);
                sprites[frame].name = name + "_" + frame.ToString("D2");
                AssetDatabase.AddObjectToAsset(sprites[frame], library);
            }
            return sprites;
        }

        private static AnimationClip Clip(HeroAnimationLibrary library, string name, Sprite[] frames)
        {
            AnimationClip clip = new AnimationClip { name = name, frameRate = 12 };
            ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[13];
            for (int frame = 0; frame < 13; frame++) keys[frame] = new ObjectReferenceKeyframe { time = frame / 12f, value = frames[Math.Min(frame, 11)] };
            AnimationUtility.SetObjectReferenceCurve(clip, new EditorCurveBinding { path = "", type = typeof(SpriteRenderer), propertyName = "m_Sprite" }, keys);
            AnimationUtility.SetAnimationEvents(clip, new AnimationEvent[0]);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = false; AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.AddObjectToAsset(clip, library);
            return clip;
        }

        public static void ValidateGenerated(HeroAnimationLibrary library)
        {
            if (library == null) throw new InvalidOperationException("The whole-body hero animation library has not been generated.");
            library.Validate();
            AnimatorController controller = library.Controller as AnimatorController;
            if (controller == null || controller.layers.Length != 1 || controller.parameters.Length != 0)
                throw new InvalidOperationException("Hero animation controller must have one visual-only layer and no gameplay parameters.");
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            if (machine.states.Length != HeroAnimationState.Atlases.Length || machine.anyStateTransitions.Length != 0 || machine.stateMachines.Length != 0)
                throw new InvalidOperationException("Hero animation controller has missing states or autonomous transitions.");
            foreach (HeroAnimationLibrary.FrameSet set in library.Sets)
            {
                AnimatorState state = null;
                foreach (ChildAnimatorState child in machine.states) if (child.state.name == set.atlas) state = child.state;
                if (state == null || state.motion != set.clip || state.transitions.Length != 0 || state.behaviours.Length != 0 || set.clip.events.Length != 0 || set.clip.hasRootCurves)
                    throw new InvalidOperationException("Hero whole-body animation may not drive gameplay, transitions or root motion: " + set.atlas);
                EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(set.clip);
                if (bindings.Length != 1 || bindings[0].path != "" || bindings[0].type != typeof(SpriteRenderer) || bindings[0].propertyName != "m_Sprite" || AnimationUtility.GetCurveBindings(set.clip).Length != 0)
                    throw new InvalidOperationException("Hero animation must bind exactly one complete SpriteRenderer sprite: " + set.atlas);
                ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(set.clip, bindings[0]);
                if (keys.Length != 13) throw new InvalidOperationException("Hero sprite clip has incomplete frames: " + set.atlas);
                for (int i = 0; i < keys.Length; i++)
                    if (keys[i].value != set.body[Math.Min(i, 11)] || Math.Abs(keys[i].time - i / 12f) > .00001f)
                        throw new InvalidOperationException("Hero sprite clip is not a deterministic whole-frame sequence: " + set.atlas);
            }
        }
    }

    public sealed class HeroAnimationBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder { get { return -1000; } }
        public void OnPreprocessBuild(BuildReport report) { HeroAnimationAssets.Ensure(); }
    }

    public sealed class HeroAnimationSourceImports : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string[] paths in new[] { imported, deleted, moved, movedFrom })
                foreach (string path in paths)
                    foreach (string atlas in HeroAnimationState.Atlases)
                        if (path == "Assets/Resources/Frontier/Art/" + atlas + ".png" || path == "Assets/Resources/Frontier/Art/ActorMasks/outline_" + atlas + ".png" || path == "Assets/Resources/Frontier/Art/ActorMasks/flash_" + atlas + ".png")
                        { HeroAnimationAssets.QueueEnsure(); return; }
        }
    }
}
#endif
