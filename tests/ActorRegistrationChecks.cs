// Production draw-call regression and review sheets. This is not native Unity.
using System;
using System.Linq;
using System.Reflection;
using Starfall;
using UnityEngine;
public static class ActorRegistrationChecks
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly Point[] Directions={new Point(1,0),new Point(-1,0),new Point(0,-1),new Point(0,1)};
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static object Call(object app,string name,params object[] args){return app.GetType().GetMethod(name,Private).Invoke(app,args);}
    static void Set(object app,string name,object value){app.GetType().GetField(name,Private).SetValue(app,value);}
    static void Clear(){GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;}
    static void Place(FrontierRuntime app,Point feet,Point pixel){Set(app,"camera",feet-new Point((pixel.x-640)/36,(pixel.y-390)/36));}
    static bool ActorTexture(GUI.TextureCall c){return c.texture.name.StartsWith("resource:Frontier/Art/")&&!c.texture.name.Contains("Effects");}
    static void Density(GUI.TextureCall[] calls,float density)
    {
        foreach(var c in calls.Where(ActorTexture))
        {
            Check(Math.Abs(c.rect.width/(Math.Abs(c.uv.width)*c.texture.width)-density)<.001f,"Actor pixel density changes across poses: "+c.texture.name);
            Check(c.uv.x>=0&&c.uv.x<=1&&c.uv.y>=0&&c.uv.y+c.uv.height<=1.0001f,"Actor crop escapes its atlas");
            Check(c.rect.width>0&&c.rect.height>0&&c.color.a>=0&&c.color.a<=1,"Actor geometry/opacity must stay valid");
        }
    }
    static GUI.TextureCall[] Draw(FrontierRuntime app,string method,params object[] args)
    {int start=GUI.textureCalls.Count;Call(app,method,args);return GUI.textureCalls.Skip(start).ToArray();}
    static void Caption(Point p,string s){GUI.Label(new Rect(p.x-38,p.y+8,92,22),s,new GUIStyle{fontSize=12});}
    static void Players(FrontierRuntime app,string folder)
    {
        string[] states={"idle","left step","pass 1","right step","pass 2","wind-up","contact","Q prepare","Q release","dodge","brace","hit","death"};int total=0;
        for(int direction=0;direction<4;direction++)
        {
            Clear();
            for(int weapon=0;weapon<3;weapon++)for(int state=0;state<states.Length;state++)
            {
                app.BeginVisualReview(weapon,0);var g=app.Game;g.enemies.Clear();g.save.companion=-1;g.save.upgrades=0;g.invulnerable=0;g.SetAim(Directions[direction]);
                if(state>=1&&state<=4){g.movementDirection=Directions[direction];g.locomotionFacing=Directions[direction];g.walkDistance=(state-1)*GameCore.WalkStride/4;}
                if(state>=5&&state<=8)g.playerAction=new ActorAction{kind=state<7?"attack":"skill",weapon=weapon,direction=Directions[direction],duration=1,contact=.4f,elapsed=state%2==1?.2f:.41f,released=state%2==0};
                if(state==9){g.dashTime=.1f;Set(g,"dashDirection",Directions[direction]);}if(state==10)g.guardTime=1;if(state==11)g.playerHitTime=.1f;if(state==12)g.deathTime=.7f;
                Point pixel=new Point(54+state*96,210+weapon*190);Place(app,g.Position,pixel);
                var calls=Draw(app,"DrawPlayerActor");Density(calls,.75f);var actors=calls.Where(ActorTexture).ToArray();Check(actors.Length>0,"Playable actor has no visible pixels");
                var expected=HeroAnimationState.Select(g);
                Check(actors.Length==1&&actors[0].texture.name.EndsWith("/"+expected.atlas),"One complete authored frame must carry the blond identity and its own equipment");
                var body=actors[0];
                Check(Math.Abs(body.rect.width-192)<.001f&&Math.Abs(body.rect.height-192)<.001f&&Math.Abs(Math.Abs(body.uv.width)-.25f)<.0001f&&Math.Abs(body.uv.height-1f/3)<.0001f,"A whole-body cell cannot be split into head/torso rectangles");
                Check(Math.Abs(body.rect.x-(pixel.x-96))<.001f&&Math.Abs(body.rect.y-(pixel.y-180))<.001f,"All authored frames retain the exact foot pivot, including death");
                Caption(pixel,states[state]);total++;
            }
            GUI.Export(folder,"review-registered-player-"+direction);
        }
        Console.WriteLine("PASS "+total+" player combinations: exact foot/canvas density, whole authored blonde body, four directions, contact, guard, recoil and death");
    }
    static void Companions(FrontierRuntime app,string folder)
    {
        int total=0;string[] names={"Mira","Ivo","Sen"};
        for(int direction=0;direction<4;direction++)
        {
            Clear();for(int id=0;id<3;id++)for(int state=0;state<7;state++)
            {
                app.BeginVisualReview(0,0);var g=app.Game;g.save.companion=id;g.companionAim=Directions[direction];g.companionAction=null;g.companionMoving=state>=1&&state<=4;g.companionMoveTime=Math.Max(0,state-1)*GameCore.WalkStride/4;
                if(state>=5)g.companionAction=new ActorAction{direction=Directions[direction],released=state==6,kind="attack"};
                Point pixel=new Point(140+state*160,230+id*180);Place(app,g.companionPosition,pixel);var calls=Draw(app,"DrawCompanionActor");Density(calls,.75f);
                var face=calls.Where(ActorTexture).Last();Check(face.texture.name.EndsWith("/Walk"+names[id]),"Companion identity must match its own authored actor");Caption(pixel,"state "+state);total++;
            }GUI.Export(folder,"review-registered-companion-"+direction);
        }Console.WriteLine("PASS "+total+" companion combinations: fixed density, own face and directional preparation/release");
    }
    static void Enemies(FrontierRuntime app,string folder)
    {
        int total=0;
        for(int batch=0;batch<3;batch++)
        {
            Clear();Screen.width=batch==0?1280:2560;Screen.height=1000;
            int count=batch==0?3:4;
            for(int id=0;id<count;id++)for(int state=0;state<9;state++)
            {
                int room=batch==0?0:(batch-1)*4+id+1;app.BeginVisualReview(0,room);var g=app.Game;g.enemies.Clear();g.invulnerable=0;
                var e=new Enemy(800+id,id,g.Position.x,g.Position.y,100,batch>0){facing=new Point(1,0),timer=.4f,windupDuration=.65f};
                if(state>=1&&state<=4){e.moving=true;e.moveTime=(state-1)*(e.boss?2.8f:e.kind==0?1.35f:1.8f)/4;}if(state==5)e.state=1;if(state==6)e.releaseTime=.1f;if(state==7){e.hitTime=.1f;e.flash=.08f;}
                Point pixel=new Point(batch==0?80+state*137:150+state*280,220+id*225);Place(app,e.Position,pixel);
                var calls=state==8?Draw(app,"DrawFallen",new FallenActor{actor=e,room=room,life=.8f,duration=1}):Draw(app,"DrawEnemyActor",e);
                Density(calls,e.boss?(room==5?264:240)/320f:(e.kind==2?106:96)/128f);Caption(pixel,"state "+state);total++;
            }GUI.Export(folder,"review-registered-enemies-"+batch);
        }Screen.width=1280;Screen.height=800;Console.WriteLine("PASS "+total+" enemy/Boss combinations: gait, wind-up, release, flash, recoil and retained death density");
    }
    static void PreparedMasks()
    {
        var source=Resources.Load<Texture2D>("Frontier/Art/Bosses");Check(source!=null&&source.isReadable,"Actual metadata must keep source alpha readable after import");
        source.isReadable=false;var art=new PixelArt();var flash=art.FlashMask(source);var outline=art.OutlineMask(source);
        Check(source.pixelReads==0&&flash.width==source.width&&outline.height==source.height,"Prepared masks cannot read or scan the large source at first hit");
        for(int i=0;i<1000;i++)Check(ReferenceEquals(flash,art.FlashMask(source))&&ReferenceEquals(outline,art.OutlineMask(source)),"Repeated hits must reuse exact GPU resources");
        art.Dispose();Check(!flash.destroyed&&!outline.destroyed,"Disposal cannot destroy borrowed Resources textures");
        Console.WriteLine("PASS Prebaked masks: zero CPU source reads, 1000 cached draws and resource ownership");
    }
    static void Muzzles(FrontierRuntime app,string folder)
    {
        Clear();
        for(int weapon=1;weapon<=2;weapon++)for(int direction=0;direction<4;direction++)
        {
            app.BeginVisualReview(weapon,0);var g=app.Game;g.enemies.Clear();g.save.upgrades=0;g.invulnerable=0;
            g.playerAction=new ActorAction{kind="attack",weapon=weapon,direction=Directions[direction],duration=1,contact=.4f,elapsed=.41f,released=true};
            Point pixel=new Point(220+direction*280,300+(weapon-1)*280);Place(app,g.Position,pixel);Draw(app,"DrawPlayerActor");
            Point tip=(Point)Call(app,"ScreenPoint",GameCore.WeaponOrigin(g.Position,weapon,Directions[direction]));tip.y-=GameCore.WeaponHeight(weapon,Directions[direction]);
            GUI.color=new Color(1,.25f,.18f);GUI.DrawTexture(new Rect(tip.x-1,tip.y-4,2,8),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(tip.x-4,tip.y-1,8,2),Texture2D.whiteTexture);GUI.color=Color.white;
            Caption(pixel,"release "+direction);
        }GUI.Export(folder,"review-registered-muzzles");
    }
    public static void Run(FrontierRuntime app,string folder)
    {
        try{PreparedMasks();Players(app,folder);Companions(app,folder);Enemies(app,folder);Muzzles(app,folder);}
        finally{Screen.width=1280;Screen.height=800;app.EndVisualReview();Clear();}
    }
}
