#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Starfall.Tests
{
    // Native checks to run in the Unity Test Runner. The offline suite only
    // checks this file's syntax; it does not execute Unity's IMGUI or GPU.
    public sealed class GuiPresentationTests
    {
        [Test] public void ActorSourcesAndPreparedMasksSurviveNativeImport()
        {
            string[] names={"PlayerVanguard","PlayerRanger","PlayerArcanist","PlayerRear","PlayerSouth","PlayerReactionsRear","PlayerReactionsSouth","Monsters","EnemyArcherVertical","Bosses","CompanionMira","CompanionIvo","CompanionSen","WalkVanguard","WalkRanger","WalkArcanist","WalkMira","WalkIvo","WalkSen","WalkMonsters","WalkBossA","WalkBossB"};
            foreach(string name in names)
            {
                Texture2D source=Resources.Load<Texture2D>("Frontier/Art/"+name);Assert.NotNull(source);Assert.IsTrue(source.isReadable);Assert.AreEqual(FilterMode.Point,source.filterMode);Assert.AreEqual(1,source.mipmapCount);
                foreach(string kind in new[]{"flash","outline"})
                {Texture2D mask=Resources.Load<Texture2D>("Frontier/Art/ActorMasks/"+kind+"_"+name);Assert.NotNull(mask);Assert.AreEqual(source.width,mask.width);Assert.AreEqual(source.height,mask.height);Assert.AreEqual(FilterMode.Point,mask.filterMode);Assert.AreEqual(1,mask.mipmapCount);Assert.IsFalse(mask.isReadable);}
            }
        }
        [Test] public void RotatedEffectsStayOnTheirCanvasAnchor()
        {
            int[,] sizes = {{1280,800},{1920,1080},{2560,1440},{800,600},{3840,2160},{900,1600},{3440,1440}};
            Rect rect = new Rect(572,338,88,88);
            Vector3 center = new Vector3(rect.x+rect.width/2,rect.y+rect.height/2,0);
            for (int viewport=0;viewport<sizes.GetLength(0);viewport++)
            {
                float scale = Math.Min(sizes[viewport,0]/1280f,sizes[viewport,1]/800f);
                Vector3 offset = new Vector3((sizes[viewport,0]-1280*scale)/2,(sizes[viewport,1]-800*scale)/2,0);
                Matrix4x4 canvas = Matrix4x4.TRS(offset,Quaternion.identity,new Vector3(scale,scale,1));
                for(int angle=0;angle<360;angle+=45)
                {
                    Matrix4x4 rotated = GuiGeometry.RotateInCanvas(canvas,rect,angle);
                    Vector3 actual = rotated.MultiplyPoint3x4(center), expected = canvas.MultiplyPoint3x4(center);
                    Assert.IsTrue(Math.Abs(actual.x-expected.x)<.035f && Math.Abs(actual.y-expected.y)<.035f,"A rotated effect may not move off its actor at a letterboxed resolution");
                    Vector3 tip = rotated.MultiplyPoint3x4(new Vector3(rect.x+rect.width,center.y,0));
                    double radians = angle*Math.PI/180;
                    Assert.IsTrue(Math.Abs(tip.x-actual.x-Math.Cos(radians)*rect.width*.5f*scale)<.035f && Math.Abs(tip.y-actual.y-Math.Sin(radians)*rect.width*.5f*scale)<.035f,"Texture axis must match the attack or flight direction");
                }
            }
        }
        [UnityTest] public IEnumerator NativeSkinCannotCoverAuthoredControls()
        {
            FrontierRuntime app = UnityEngine.Object.FindObjectOfType<FrontierRuntime>(); GameObject host = null;
            if(app==null) {host=new GameObject("Starfall native GUI regression");app=host.AddComponent<FrontierRuntime>();}
            try
            {
                yield return null; Assert.NotNull(app.Game); app.BeginVisualReview(0,1); app.Game.enemies.Clear();
                yield return null; yield return new WaitForEndOfFrame();
                foreach(string field in new[]{"textStyle","buttonStyle","disabledButtonStyle"})
                {
                    GUIStyle style=(GUIStyle)typeof(FrontierRuntime).GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(app); Assert.NotNull(style);
                    foreach(GUIStyleState state in new[]{style.normal,style.hover,style.active,style.focused,style.onNormal,style.onHover,style.onActive,style.onFocused})
                    {
                        Assert.IsTrue(state.background==null,"Authored controls must not borrow opaque native backgrounds");
#if UNITY_EDITOR
                        Assert.IsTrue(state.scaledBackgrounds==null || state.scaledBackgrounds.Length==0,"High-DPI editor backgrounds must not cover skill icons");
#endif
                    }
                }
            }
            finally {if(app!=null)app.EndVisualReview();if(host!=null)UnityEngine.Object.Destroy(host);}
        }
    }
}
#endif
