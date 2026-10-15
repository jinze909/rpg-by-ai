#if UNITY_5_3_OR_NEWER
using System;
using UnityEngine;

namespace Starfall
{
    // Render the real Animator/SpriteRenderer into the existing sorted IMGUI
    // world slot. A screen camera would be hidden by the opaque IMGUI terrain.
    public sealed class FrontierHeroPresenter : IDisposable
    {
        private const int RenderLayer = 2; // Built-in Ignore Raycast; no project-layer mutation.
        private readonly HeroAnimationLibrary library;
        private readonly GameObject root;
        private readonly SpriteRenderer body, outlineRenderer, flashRenderer;
        private readonly Animator animator;
        private readonly Camera renderCamera;
        private readonly Material spriteMaterial, straightAlphaMaterial;
        private readonly RenderTexture normalTarget, outlineTarget, premultipliedTarget;
        private RenderKey normalKey, outlineKey;
        private HeroVisualFrame sampledFrame;
        private bool hasSampledFrame;
        private bool hasNormal, hasOutline, disposed;
        private int renderCount;

        private struct RenderKey
        {
            public HeroVisualFrame frame;
            public Color tint;
            public float flash;
            public bool Matches(RenderKey other)
            { return frame.atlas == other.frame.atlas && frame.frame == other.frame.frame && frame.flip == other.frame.flip && tint == other.tint && flash == other.flash; }
        }

        public HeroAnimationLibrary Library { get { return library; } }
        public SpriteRenderer BodyRenderer { get { return body; } }
        public Animator Animator { get { return animator; } }
        public Camera RenderCamera { get { return renderCamera; } }
        public int RenderCount { get { return renderCount; } }

        public FrontierHeroPresenter(GameObject owner)
        {
            if (owner == null) throw new ArgumentNullException("owner");
            library = Resources.Load<HeroAnimationLibrary>(HeroAnimationLibrary.ResourcePath);
            if (library == null) throw new InvalidOperationException("Missing native hero animation library. Open Frontier in the Editor to generate it before play/build.");
            library.Validate();
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) throw new InvalidOperationException("The native hero requires the Sprites/Default shader.");
            Shader conversionShader = Resources.Load<Shader>("Frontier/HeroStraightAlpha");
            if (conversionShader == null) throw new InvalidOperationException("The native hero requires its straight-alpha compositing shader.");
            try
            {
                root = new GameObject("Viola native whole-body presenter") { hideFlags = HideFlags.DontSave, layer = RenderLayer };
                root.transform.SetParent(owner.transform, false);
                // A disabled renderer outside our explicit Render call cannot
                // ghost into Main Camera. Isolation also excludes town actors.
                root.transform.position = new Vector3(50000, 50000, 0);
                spriteMaterial = new Material(shader) { name = "Viola whole-body sprite material", hideFlags = HideFlags.DontSave };
                straightAlphaMaterial = new Material(conversionShader) { name = "Viola straight-alpha compositing material", hideFlags = HideFlags.DontSave };
                body = root.AddComponent<SpriteRenderer>(); body.sharedMaterial = spriteMaterial; body.enabled = false;
                animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = library.Controller;
                animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.speed = 0;
                outlineRenderer = ChildRenderer("Whole-body alpha outline", 0);
                flashRenderer = ChildRenderer("Whole-body hit flash", 1);
                GameObject cameraObject = new GameObject("Viola transparent render camera") { hideFlags = HideFlags.DontSave, layer = RenderLayer };
                cameraObject.transform.SetParent(root.transform, false); cameraObject.transform.localPosition = new Vector3(0, 112, -10);
                renderCamera = cameraObject.AddComponent<Camera>();
                renderCamera.enabled = false; renderCamera.orthographic = true; renderCamera.orthographicSize = 128; renderCamera.aspect = 1;
                renderCamera.nearClipPlane = .01f; renderCamera.farClipPlane = 100;
                renderCamera.clearFlags = CameraClearFlags.SolidColor; renderCamera.backgroundColor = Color.clear;
                renderCamera.cullingMask = 1 << RenderLayer; renderCamera.allowHDR = false; renderCamera.allowMSAA = false;
                renderCamera.useOcclusionCulling = false;
                normalTarget = Target("Viola whole-body render"); outlineTarget = Target("Viola whole-body outline render");
                premultipliedTarget = new RenderTexture(256, 256, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
                { name = "Viola premultiplied sprite scratch", hideFlags = HideFlags.DontSave, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, antiAliasing = 1, useMipMap = false, autoGenerateMips = false };
                if (!premultipliedTarget.Create()) throw new InvalidOperationException("Unable to create the native hero compositing scratch target.");
            }
            catch { Dispose(); throw; }
        }

        private SpriteRenderer ChildRenderer(string name, int sortingOrder)
        {
            GameObject child = new GameObject(name) { hideFlags = HideFlags.DontSave, layer = RenderLayer };
            child.transform.SetParent(root.transform, false);
            SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = spriteMaterial; renderer.sortingOrder = sortingOrder; renderer.enabled = false;
            return renderer;
        }

        private static RenderTexture Target(string name)
        {
            RenderTexture target = new RenderTexture(256, 256, 0, RenderTextureFormat.ARGB32)
            { name = name, hideFlags = HideFlags.DontSave, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, antiAliasing = 1, useMipMap = false, autoGenerateMips = false };
            if (!target.Create()) { DestroyOwned(target); throw new InvalidOperationException("Unable to create the native hero render target."); }
            return target;
        }

        public RenderTexture Render(HeroVisualFrame frame, Color tint, bool outline, float flashAlpha)
        {
            if (disposed) throw new ObjectDisposedException("FrontierHeroPresenter");
            HeroAnimationLibrary.FrameSet set = library.Find(frame.atlas);
            if (frame.frame < 0 || frame.frame >= HeroAnimationLibrary.FramesPerAtlas) throw new ArgumentOutOfRangeException("frame");
            float flash = float.IsNaN(flashAlpha) ? 0 : Mathf.Clamp01(flashAlpha);
            RenderKey key = new RenderKey { frame = frame, tint = tint, flash = outline ? 0 : flash };
            RenderTexture target = outline ? outlineTarget : normalTarget;
            // This controller owns the complete displayed body frame. Core
            // chooses the sample; Animator never advances gameplay or events.
            // A cached target may follow an outline request for another frame;
            // keep the real Animator truthful even when no GPU work is needed.
            if (!hasSampledFrame || sampledFrame.atlas != frame.atlas || sampledFrame.frame != frame.frame)
            {
                animator.Play(Animator.StringToHash("Base Layer." + frame.atlas), 0, (frame.frame + .05f) / 12f);
                animator.Update(0);
                sampledFrame = frame; hasSampledFrame = true;
            }
            if (body.sprite != set.body[frame.frame]) throw new InvalidOperationException("Animator did not sample the requested whole-body sprite: " + frame.atlas + "/" + frame.frame);
            body.flipX = outlineRenderer.flipX = flashRenderer.flipX = frame.flip;
            body.color = tint;
            outlineRenderer.sprite = set.outline[frame.frame]; outlineRenderer.color = tint;
            flashRenderer.sprite = set.flash[frame.frame]; flashRenderer.color = new Color(1, 1, 1, flash * tint.a);
            if (target.IsCreated() && (outline ? hasOutline && outlineKey.Matches(key) : hasNormal && normalKey.Matches(key))) return target;
            if (!target.IsCreated() && !target.Create()) throw new InvalidOperationException("The native hero render target was lost and could not be recreated.");
            RenderTexture previous = renderCamera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            bool previousSrgbWrite = GL.sRGBWrite;
            try
            {
                body.enabled = !outline; outlineRenderer.enabled = outline; flashRenderer.enabled = !outline && flash > 0;
                if (!premultipliedTarget.IsCreated() && !premultipliedTarget.Create()) throw new InvalidOperationException("The native hero compositing scratch target was lost.");
                // UnitySprites.cginc SpriteFrag multiplies RGB by alpha. GUI's
                // alpha-blended texture path expects straight RGB; undo exactly
                // this boundary multiplication before the original GUI slot.
                renderCamera.targetTexture = premultipliedTarget; renderCamera.Render();
                GL.sRGBWrite = target.sRGB;
                Graphics.Blit(premultipliedTarget, target, straightAlphaMaterial); renderCount++;
                if (outline) { outlineKey = key; hasOutline = true; } else { normalKey = key; hasNormal = true; }
            }
            finally
            {
                body.enabled = outlineRenderer.enabled = flashRenderer.enabled = false;
                renderCamera.targetTexture = previous;
                RenderTexture.active = previousActive;
                GL.sRGBWrite = previousSrgbWrite;
            }
            return target;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (renderCamera != null) renderCamera.targetTexture = null;
            if (normalTarget != null) { normalTarget.Release(); DestroyOwned(normalTarget); }
            if (outlineTarget != null) { outlineTarget.Release(); DestroyOwned(outlineTarget); }
            if (premultipliedTarget != null) { premultipliedTarget.Release(); DestroyOwned(premultipliedTarget); }
            DestroyOwned(root); DestroyOwned(spriteMaterial); DestroyOwned(straightAlphaMaterial);
        }

        private static void DestroyOwned(UnityEngine.Object value)
        { if (value == null) return; if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); }
    }
}
#endif
