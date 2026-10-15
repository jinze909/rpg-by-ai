#if UNITY_5_3_OR_NEWER
using System;
using UnityEngine;

namespace Starfall
{
    // Generated in place by HeroAnimationAssets. This is visual data only: no
    // gameplay clock, attack event or saved character state belongs here.
    public sealed class HeroAnimationLibrary : ScriptableObject
    {
        public const string ResourcePath = "Frontier/HeroAnimationLibrary";
        public const int FramesPerAtlas = 12;
        public const int CanvasSize = 256;
        public const int FormatVersion = 1;

        [Serializable]
        public sealed class FrameSet
        {
            public string atlas;
            public Texture2D source, outlineSource, flashSource;
            public Sprite[] body, outline, flash;
            public AnimationClip clip;
        }

        [SerializeField] private int formatVersion;
        [SerializeField] private string sourceFingerprint;
        [SerializeField] private RuntimeAnimatorController controller;
        [SerializeField] private FrameSet[] sets;

        public int Version { get { return formatVersion; } }
        public string SourceFingerprint { get { return sourceFingerprint; } }
        public RuntimeAnimatorController Controller { get { return controller; } }
        public FrameSet[] Sets { get { return sets; } }

        public FrameSet Find(string atlas)
        {
            if (sets != null)
                foreach (FrameSet set in sets)
                    if (set != null && set.atlas == atlas) return set;
            throw new InvalidOperationException("Missing whole-body hero atlas in animation library: " + atlas);
        }

        public void Validate()
        {
            if (formatVersion != FormatVersion || controller == null || sets == null || sets.Length != HeroAnimationState.Atlases.Length)
                throw new InvalidOperationException("Hero animation library is missing or stale. Run Starfall/Animation/Rebuild hero animation library.");
            foreach (string atlas in HeroAnimationState.Atlases)
            {
                FrameSet set = Find(atlas);
                if (set.source == null || set.outlineSource == null || set.flashSource == null || set.clip == null)
                    throw new InvalidOperationException("Incomplete whole-body animation sources: " + atlas);
                ValidateFrames(set.body, set.source, atlas);
                ValidateFrames(set.outline, set.outlineSource, atlas + " outline");
                ValidateFrames(set.flash, set.flashSource, atlas + " flash");
            }
        }

        private static void ValidateFrames(Sprite[] frames, Texture2D source, string name)
        {
            if (source.width != CanvasSize * 4 || source.height != CanvasSize * 3 || frames == null || frames.Length != FramesPerAtlas)
                throw new InvalidOperationException("Invalid whole-body hero canvas: " + name);
            for (int i = 0; i < frames.Length; i++)
            {
                Sprite sprite = frames[i];
                Rect expected = new Rect(i % 4 * CanvasSize, (2 - i / 4) * CanvasSize, CanvasSize, CanvasSize);
                if (sprite == null || sprite.texture != source || sprite.rect != expected ||
                    sprite.pixelsPerUnit != 1 || sprite.pivot != new Vector2(128, 16))
                    throw new InvalidOperationException("Invalid whole-body hero sprite or foot anchor: " + name + " frame " + i);
            }
        }

#if UNITY_EDITOR
        public void SetGeneratedData(string fingerprint, RuntimeAnimatorController generatedController, FrameSet[] generatedSets)
        {
            formatVersion = FormatVersion;
            sourceFingerprint = fingerprint;
            controller = generatedController;
            sets = generatedSets;
        }
#endif
    }
}
#endif
