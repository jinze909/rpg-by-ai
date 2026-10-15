#if UNITY_INCLUDE_TESTS && UNITY_5_3_OR_NEWER
using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Starfall.Tests
{
    // These require a licensed Unity Editor/Player and real GPU readback. The
    // offline suite must not count them as executed or emulate their result.
    public sealed class FrontierHeroNativeChecks
    {
        [Test] public void NativeLibraryKeepsEveryWholeFrameAndOneFootPivot()
        {
            HeroAnimationLibrary library = Resources.Load<HeroAnimationLibrary>(HeroAnimationLibrary.ResourcePath);
            Assert.NotNull(library, "The Editor/play/build asset-generation gate must run before Frontier.");
            library.Validate();
            Assert.AreEqual(10, library.Sets.Length);
            foreach (HeroAnimationLibrary.FrameSet set in library.Sets)
                for (int frame = 0; frame < 12; frame++)
                {
                    Assert.AreEqual(new Rect(frame % 4 * 256, (2 - frame / 4) * 256, 256, 256), set.body[frame].rect);
                    Assert.AreEqual(new Vector2(128, 16), set.body[frame].pivot);
                    Assert.AreEqual(set.body[frame].rect, set.outline[frame].rect);
                    Assert.AreEqual(set.body[frame].rect, set.flash[frame].rect);
                }
        }

        [UnityTest] public IEnumerator FrontierSceneBootsAndSamplesRealAttacksForAllThreeClasses()
        {
            yield return SceneManager.LoadSceneAsync("Frontier", LoadSceneMode.Single);
            yield return null;
            FrontierRuntime app = UnityEngine.Object.FindObjectOfType<FrontierRuntime>();
            Assert.NotNull(app, "Open the production Frontier scene, not the legacy SampleScene.");
            Assert.NotNull(app.Game);
            HeroAnimationLibrary library = Resources.Load<HeroAnimationLibrary>(HeroAnimationLibrary.ResourcePath);
            Assert.NotNull(library); library.Validate();
            try
            {
                Point[] directions = { new Point(1, 0), new Point(-1, 0), new Point(0, -1), new Point(0, 1) };
                for (int profession = 0; profession < 3; profession++)
                    foreach (Point direction in directions)
                    {
                        // The no-save review isolates visual sampling. Actions
                        // still use the production GameCore attack/dash clocks.
                        app.BeginVisualReview(profession, 1); app.Game.enemies.Clear(); app.Game.save.companion = -1;
                        app.Game.Attack(direction);
                        ActorAction action = app.Game.playerAction;
                        Assert.NotNull(action); Assert.AreEqual(profession, action.weapon);
                        HeroVisualFrame windup = HeroAnimationState.Select(app.Game);
                        float windupHealth = app.Game.save.health, windupEnergy = app.Game.save.energy, windupTime = app.Game.save.totalTime;
                        yield return new WaitForEndOfFrame();
                        AssertProductionSprite(library, windup);
                        Assert.AreSame(action, app.Game.playerAction, "Rendering cannot replace an accepted combat action.");
                        Assert.AreEqual(0, action.elapsed, .00001f, "Animator cannot advance the authoritative attack clock.");
                        Assert.AreEqual(windupHealth, app.Game.save.health, .00001f);
                        Assert.AreEqual(windupEnergy, app.Game.save.energy, .00001f);
                        Assert.AreEqual(windupTime, app.Game.save.totalTime, .00001f);
                        for (int tick = 0; tick < 40 && !action.released; tick++) app.AdvanceVisualReview(.02f, new Point());
                        Assert.IsTrue(action.released, "The ordinary Core contact boundary must be reached.");
                        HeroVisualFrame release = HeroAnimationState.Select(app.Game);
                        Assert.IsTrue(release.atlas != windup.atlas || release.frame != windup.frame);
                        // Energy may legitimately regenerate in the manual Core
                        // ticks above. Compare only the paused rendering wait.
                        float beforeHealth = app.Game.save.health, beforeEnergy = app.Game.save.energy, beforeTime = app.Game.save.totalTime;
                        yield return new WaitForEndOfFrame();
                        AssertProductionSprite(library, release);
                        Assert.AreEqual(beforeHealth, app.Game.save.health, .00001f);
                        Assert.AreEqual(beforeEnergy, app.Game.save.energy, .00001f, "Sprite events may not charge or resolve gameplay.");
                        Assert.AreEqual(beforeTime, app.Game.save.totalTime, .00001f);
                        for (int tick = 0; tick < 150 && (app.Game.playerAction != null || app.Game.dashCooldown > 0); tick++) app.AdvanceVisualReview(.02f, new Point());
                        Assert.IsNull(app.Game.playerAction, "Finish the real attack before staging the next visual action.");
                        Assert.LessOrEqual(app.Game.dashCooldown, 0);
                        app.Game.Dash(direction);
                        Assert.Greater(app.Game.dashTime, 0, "The production dash must actually accept the input.");
                        Assert.AreEqual(8, app.Game.PlayerPose);
                        yield return new WaitForEndOfFrame();
                        AssertProductionSprite(library, HeroAnimationState.Select(app.Game));
                    }
            }
            finally { if (app != null) app.EndVisualReview(); }
        }

        private static void AssertProductionSprite(HeroAnimationLibrary library, HeroVisualFrame expected)
        {
            GameObject rig = GameObject.Find("Viola native whole-body presenter");
            Assert.NotNull(rig, "The actual Frontier boot must instantiate the native presenter.");
            SpriteRenderer body = rig.GetComponent<SpriteRenderer>(); Animator animator = rig.GetComponent<Animator>();
            Assert.NotNull(body); Assert.NotNull(animator); Assert.AreSame(library.Controller, animator.runtimeAnimatorController);
            Assert.AreSame(library.Find(expected.atlas).body[expected.frame], body.sprite);
            Assert.AreEqual(expected.flip, body.flipX); Assert.AreEqual(0, animator.speed);
            Assert.IsFalse(animator.applyRootMotion); Assert.IsFalse(body.enabled, "The isolated renderer must not ghost into Main Camera.");
            Assert.AreEqual(1, rig.GetComponentsInChildren<Animator>(true).Length);
        }

        [UnityTest] public IEnumerator RealAnimatorAndCameraPreserveWholeFrameAlphaInEveryAtlas()
        {
            GameObject owner = new GameObject("Native hero GPU regression");
            FrontierHeroPresenter presenter = null;
            Texture2D readback = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            // Imported body pixels and the sRGB GUI target expose encoded RGB
            // bytes to GetPixels32. Decode RGB before the source-over oracle;
            // alpha is coverage and must never receive an sRGB conversion.
            float[] linearRgb = new float[256];
            for (int i = 0; i < linearRgb.Length; i++) linearRgb[i] = Mathf.GammaToLinearSpace(i / 255f);
            Color[] linearBackgrounds = { new Color(.17f, .31f, .53f).linear, new Color(.83f, .67f, .29f).linear };
            try
            {
                Assert.AreEqual(ColorSpace.Linear, QualitySettings.activeColorSpace, "Run the native hero regression in the project's Linear color space.");
                presenter = new FrontierHeroPresenter(owner);
                foreach (HeroAnimationLibrary.FrameSet set in presenter.Library.Sets)
                {
                    Color32[] source = set.source.GetPixels32();
                    for (int frame = 0; frame < 12; frame++)
                        for (int mirrored = 0; mirrored < 2; mirrored++)
                            foreach (float opacity in new[] { 1f, .5f })
                        {
                            HeroVisualFrame selected = new HeroVisualFrame(set.atlas, frame, mirrored == 1);
                            Color tint = new Color(1, 1, 1, opacity);
                            RenderTexture target = presenter.Render(selected, tint, false, 0);
                            Assert.IsTrue(target.sRGB, "The Linear presenter must return sRGB RGB for the GUI texture path.");
                            Assert.AreSame(set.body[frame], presenter.BodyRenderer.sprite, "Animator's actual sprite binding must choose the complete source frame.");
                            int renders = presenter.RenderCount;
                            Assert.AreSame(target, presenter.Render(selected, tint, false, 0));
                            Assert.AreEqual(renders, presenter.RenderCount, "IMGUI Layout/Repaint must reuse unchanged native targets.");
                            HeroVisualFrame other = new HeroVisualFrame(set.atlas, (frame + 1) % 12, mirrored == 1);
                            presenter.Render(other, new Color(1, .56f, .35f, .9f), true, 0);
                            Assert.AreSame(target, presenter.Render(selected, tint, false, 0));
                            Assert.AreSame(set.body[frame], presenter.BodyRenderer.sprite, "A cached body target must restore the Animator after another outline sample.");
                            RenderTexture previous = RenderTexture.active;
                            try { RenderTexture.active = target; readback.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); readback.Apply(false); }
                            finally { RenderTexture.active = previous; }
                            Color32[] actual = readback.GetPixels32(); int opaque = 0, rgbChecked = 0, partialCoverage = 0;
                            string sample = set.atlas + "/" + frame + " flip=" + mirrored + " opacity=" + opacity;
                            for (int y = 0; y < 256; y++)
                                for (int x = 0; x < 256; x++)
                                {
                                    int sx = frame % 4 * 256 + (mirrored == 1 ? 255 - x : x), sy = (2 - frame / 4) * 256 + y;
                                    Color32 expected = source[sy * set.source.width + sx], pixel = actual[y * 256 + x];
                                    float expectedAlpha = expected.a / 255f * opacity;
                                    if (expected.a >= 128) opaque++;
                                    if (Math.Abs(pixel.a - expected.a * opacity) > 2)
                                        Assert.Fail("Native whole-body alpha changed at " + sample + " pixel " + x + "," + y);
                                    if (expected.a == 0 && (pixel.r != 0 || pixel.g != 0 || pixel.b != 0))
                                        Assert.Fail("Transparent native RGB must be zero at " + sample + " pixel " + x + "," + y);
                                    // Avoid amplifying half-float quantization at
                                    // almost invisible source edges. Even the
                                    // .5-opacity pass must retain straight RGB.
                                    if (expected.a < 16) continue;
                                    rgbChecked++;
                                    if (expectedAlpha >= .03f && expectedAlpha <= .95f) partialCoverage++;
                                    if (Math.Abs(pixel.r - expected.r) > 3 || Math.Abs(pixel.g - expected.g) > 3 || Math.Abs(pixel.b - expected.b) > 3)
                                        Assert.Fail("Native straight RGB changed at " + sample + " pixel " + x + "," + y);
                                    // This CPU source-over oracle uses real GPU
                                    // readback. It checks the GUI boundary's
                                    // straight-alpha contract in linear space;
                                    // it is not an actual GUI framebuffer test.
                                    float actualAlpha = pixel.a / 255f;
                                    foreach (Color background in linearBackgrounds)
                                    {
                                        float expectedR = linearRgb[expected.r] * expectedAlpha + background.r * (1 - expectedAlpha);
                                        float expectedG = linearRgb[expected.g] * expectedAlpha + background.g * (1 - expectedAlpha);
                                        float expectedB = linearRgb[expected.b] * expectedAlpha + background.b * (1 - expectedAlpha);
                                        float actualR = linearRgb[pixel.r] * actualAlpha + background.r * (1 - actualAlpha);
                                        float actualG = linearRgb[pixel.g] * actualAlpha + background.g * (1 - actualAlpha);
                                        float actualB = linearRgb[pixel.b] * actualAlpha + background.b * (1 - actualAlpha);
                                        if (Math.Abs(actualR - expectedR) > .025f || Math.Abs(actualG - expectedG) > .025f || Math.Abs(actualB - expectedB) > .025f)
                                            Assert.Fail("Native linear-space source-over changed at " + sample + " pixel " + x + "," + y);
                                    }
                                }
                            Assert.Greater(opaque, 100, "An empty target cannot satisfy the body-continuity check.");
                            Assert.Greater(rgbChecked, 100, "RGB preservation must cover visible body pixels.");
                            if (opacity < 1) Assert.Greater(partialCoverage, 100, "The fade must exercise partially covered source-over pixels.");
                            Assert.IsFalse(presenter.BodyRenderer.enabled);
                        }
                    yield return null;
                }
            }
            finally
            {
                if (presenter != null) presenter.Dispose();
                UnityEngine.Object.Destroy(owner); UnityEngine.Object.Destroy(readback);
            }
        }

        [UnityTest] public IEnumerator NativePresenterDisposalReleasesTargetsAndSceneObjects()
        {
            GameObject owner = new GameObject("Native hero lifecycle regression");
            FrontierHeroPresenter presenter = new FrontierHeroPresenter(owner);
            RenderTexture target = presenter.Render(new HeroVisualFrame("PlayerVanguard", 0, false), Color.white, false, 0);
            Camera camera = presenter.RenderCamera;
            presenter.Dispose(); presenter.Dispose();
            Assert.IsFalse(target.IsCreated());
            Assert.Throws<ObjectDisposedException>(() => presenter.Render(new HeroVisualFrame("PlayerVanguard", 0, false), Color.white, false, 0));
            yield return null;
            Assert.IsTrue(camera == null); Assert.IsTrue(target == null);
            Assert.AreEqual(0, owner.transform.childCount);
            UnityEngine.Object.Destroy(owner);
        }
    }
}
#endif
