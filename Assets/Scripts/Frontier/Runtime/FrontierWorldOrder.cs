using System;
using System.Collections.Generic;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private enum WorldDrawKind { Decoration, Object, Enemy, Fallen, Player, Companion }
        private struct WorldDrawEntry
        {
            public float depth;
            public int order;
            public WorldDrawKind kind;
            public Decoration decoration;
            public WorldObject obj;
            public Enemy enemy;
            public FallenActor fallen;
        }
        private sealed class WorldDepthComparer:IComparer<WorldDrawEntry>
        {
            public int Compare(WorldDrawEntry a,WorldDrawEntry b)
            {int depth=a.depth.CompareTo(b.depth);return depth!=0?depth:a.order.CompareTo(b.order);}
        }
        private static readonly WorldDepthComparer worldDepthComparer=new WorldDepthComparer();
        private readonly List<WorldDrawEntry> worldDrawEntries=new List<WorldDrawEntry>(256);
        private void AddWorldEntry(WorldDrawEntry entry)
        {entry.order=worldDrawEntries.Count;worldDrawEntries.Add(entry);}
        private void DrawOrderedWorld()
        {
            // Reuse storage and explicit references. Captured per-actor callbacks
            // used to allocate on every IMGUI pass; equal-depth ties also flickered.
            worldDrawEntries.Clear();
            foreach(LightRay ray in Game.LightRays())DrawRay(ray.from,ray.to,gold);
            foreach(Decoration d in Game.Map.decor)if(WorldSceneryVisible(d))AddWorldEntry(new WorldDrawEntry{depth=d.y,kind=WorldDrawKind.Decoration,decoration=d});
            foreach(WorldObject o in Game.Map.objects)AddWorldEntry(new WorldDrawEntry{depth=o.y+.01f,kind=WorldDrawKind.Object,obj=o});
            foreach(Enemy e in Game.enemies)
            {DrawEnemyWarning(e);AddWorldEntry(new WorldDrawEntry{depth=e.y,kind=WorldDrawKind.Enemy,enemy=e});}
            foreach(FallenActor f in Game.fallen)AddWorldEntry(new WorldDrawEntry{depth=f.actor.y-.02f,kind=WorldDrawKind.Fallen,fallen=f});
            AddWorldEntry(new WorldDrawEntry{depth=Game.save.y+.002f,kind=WorldDrawKind.Player});
            if(Game.save.companion>=0)AddWorldEntry(new WorldDrawEntry{depth=Game.companionPosition.y,kind=WorldDrawKind.Companion});
            worldDrawEntries.Sort(worldDepthComparer);
            try
            {
                foreach(WorldDrawEntry entry in worldDrawEntries)
                    switch(entry.kind)
                    {
                        case WorldDrawKind.Decoration:DrawWorldDecoration(entry.decoration);break;
                        case WorldDrawKind.Object:DrawObject(entry.obj);break;
                        case WorldDrawKind.Enemy:DrawEnemyWithBars(entry.enemy);break;
                        case WorldDrawKind.Fallen:DrawFallen(entry.fallen);break;
                        case WorldDrawKind.Player:DrawHero();break;
                        case WorldDrawKind.Companion:DrawCompanionActor();break;
                    }
            }
            finally{worldDrawEntries.Clear();}
        }
        private void DrawWorldDecoration(Decoration prop)
        {
            Point p=ScreenPoint(new Point(prop.x,prop.y));
            float w=WorldDecorationWidth(prop.kind);
            int variant=prop.variant;
            if(prop.kind=="garden")variant=Game.save.plantedAt<0?0:Game.save.totalTime-Game.save.plantedAt>=60?2:1;
            float h=w*112/96;
            bool canopy=prop.kind=="tree"||prop.kind=="palm"||prop.kind=="snowpine",cover=canopy||prop.kind=="ruin";
            float opacity=cover?SceneryOpacity(prop):1;
            Color tint=new Color(1,1,1,opacity);
            Rect rect=new Rect(p.x-w/2,p.y-h+8,w,h);
            if(canopy)DrawRootedPlant(prop,rect,tint);else PropImage(prop.kind,variant,rect.x,rect.y,rect.width,rect.height,tint);
            if(prop.kind=="well"&&Game.Has("ally_3"))Box(p.x-5,p.y-16,10,5,mint);
            DrawWaterGardenScenery(prop);
        }
        private float SceneryOpacity(Decoration prop)
        {
            // Feather actor-cover edges spatially in every region.
            // No reveal timers or frame history: reduced motion and minimal FX
            // retain exactly the same readability during a slow approach.
            float cover=SceneryCoverWeight(Game.Position,prop,1.4f,2.7f);
            foreach(Enemy actor in Game.enemies)cover=Math.Max(cover,SceneryCoverWeight(actor.Position,prop,1.6f,2.9f));
            // Match the world draw list: only an actually selected companion
            // witnesses cover, using the same body envelope as the protagonist.
            if(Game.save.companion>=0)cover=Math.Max(cover,SceneryCoverWeight(Game.companionPosition,prop,1.4f,2.7f));
            return 1-.6f*cover;
        }
        private static float SceneryCoverWeight(Point actor,Decoration prop,float halfWidth,float height)
        {
            float dx=Math.Max(0,Math.Abs(actor.x-prop.x)-halfWidth);
            float dy=Math.Max(0,Math.Max(actor.y-prop.y,prop.y-height-actor.y));
            float t=GameCore.Clamp(1-(float)Math.Sqrt(dx*dx+dy*dy)/.8f,0,1);
            return t*t*(3-2*t);
        }
        private static float WorldDecorationWidth(string kind)
        {return kind=="house"?170:kind=="tree"?100:kind=="ruin"?150:kind=="bed"||kind=="bookshelf"?90:kind=="table"||kind=="counter"?80:76;}
        private Rect WorldDecorationBounds(Decoration prop)
        {
            Point p=ScreenPoint(new Point(prop.x,prop.y));
            float w=WorldDecorationWidth(prop.kind),h=w*112/96;
            // Union the authored fallback with the square field-prop Actor.
            // Counters have two wings and rooted plants retain their full wind
            // excursion. A ground anchor outside view must not hide its crown.
            float side=Math.Max(w,h),wing=prop.kind=="counter"?45:0;
            float wind=prop.kind=="tree"||prop.kind=="palm"||prop.kind=="snowpine"?1.2f:0;
            float left=p.x-side/2-wing-wind,top=Math.Min(p.y-h+8,p.y-side*120/128);
            float right=p.x+side/2+wing+wind,bottom=Math.Max(p.y+8,p.y+side*8/128);
            return new Rect(left,top,right-left,bottom-top);
        }
        private bool WorldSceneryVisible(Decoration prop)
        {
            Rect bounds=WorldDecorationBounds(prop);
            // Static scenery is ready two ground tiles before its complete
            // sprite reaches view. Actors, interactions and danger geometry
            // remain authoritative on their original unconditional draw paths.
            float apron=Tile*2;
            return bounds.x+bounds.width>=-apron&&bounds.x<=Width+apron&&bounds.y+bounds.height>=-apron&&bounds.y<=Height+apron;
        }
        private void DrawEnemyWithBars(Enemy enemy)
        {
            DrawEnemyActor(enemy);
            if(Game.practicing&&!Game.SafeTrainingActive)return;
            DrawEnemyGuard(enemy);
            DrawEnemyStunIndicator(enemy);
            if(!Game.SafeTrainingActive&&!enemy.boss&&enemy.hp>=enemy.maxHp&&enemy.state!=1&&!ReferenceEquals(Game.CombatFocusTarget,enemy))return;
            Point p=ScreenPoint(enemy.Position);float width=enemy.boss?104:52,top=p.y-(enemy.boss?135:enemy.kind==0?54:79);
            Bar(p.x-width/2,top,width,enemy.hp/enemy.maxHp,PixelArt.C("#d58e79"),4);
            if(enemy.stagger>0)Bar(p.x-width/2,top+6,width,enemy.stagger/(enemy.boss?95:30),mint,2);
        }
        private bool EnemyStunVisible(Enemy enemy)
        {
            return Game.mode=="world"&&enemy.hp>0&&enemy.stun>0&&
                !(Game.practicing&&!Game.SafeTrainingActive)&&
                !(TrainingReview);
        }
        private Rect EnemyStunBounds(Enemy enemy)
        {
            Point p=ScreenPoint(enemy.Position);
            // Stable feet/bar anchor: changing hurt/idle frames must not make
            // the status jump with the authored body's changing opaque bounds.
            float half=enemy.boss?52:26,top=p.y-(enemy.boss?135:enemy.kind==0?54:79);
            return new Rect(p.x+half+4,top-6,26,12);
        }
        private void DrawEnemyStunIndicator(Enemy enemy)
        {
            if(drawingOutline||!EnemyStunVisible(enemy))return;
            Rect bounds=EnemyStunBounds(enemy);
            // Three static four-point pixel stars describe the real control
            // window. No orbit, timer, FX setting or gameplay state is added.
            for(int i=0;i<3;i++)
            {
                float x=bounds.x+4+i*9,y=bounds.y+8-(i==1?4:0);
                Color backing=new Color(ink.r,ink.g,ink.b,.95f);
                Box(x-2,y-4,4,8,backing);Box(x-4,y-2,8,4,backing);
                Box(x-1,y-3,2,6,gold);Box(x-3,y-1,6,2,gold);
            }
        }
    }
}
