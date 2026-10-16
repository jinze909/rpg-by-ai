// Executes production input and the whole-body offline rendering fallback.
// SpriteRenderer/Animator/GPU/Frontier scene acceptance requires native PlayMode.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeHeroAnimationChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly Point[] Headings={new Point(1,0),new Point(-1,0),new Point(0,-1),new Point(0,1)};
    private static string folder;
    private static int captures;
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private static object Call(FrontierRuntime app,string name,params object[] args){return typeof(FrontierRuntime).GetMethod(name,Private).Invoke(app,args);}
    private static void Set(FrontierRuntime app,string name,object value){typeof(FrontierRuntime).GetField(name,Private).SetValue(app,value);}
    private static void Mouse(FrontierRuntime app,Point target)
    {Point point=(Point)Call(app,"ScreenPoint",target);Input.mousePosition=new Vector3(point.x,Screen.height-point.y,0);}
    private static void Update(FrontierRuntime app){Call(app,"Update");Input.keys.Clear();}
    private static void Prepare(FrontierRuntime app,int profession)
    {
        Input.Clear();Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;
        app.BeginVisualReview(profession,1);app.VisualReviewPaused=false;var g=app.Game;
        g.enemies.Clear();g.save.companion=-1;g.save.reducedMotion=true;g.save.x=12;g.save.y=14;g.invulnerable=0;g.save.energy=100;
        Set(app,"camera",g.Position);Set(app,"applicationPaused",false);Set(app,"controllerUsed",false);Set(app,"pointerSeen",false);Call(app,"Resize");
    }
    private static void Draw(FrontierRuntime app,string name)
    {
        GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"DrawPlayerActor");
        HeroVisualFrame selected=HeroAnimationState.Select(app.Game);
        var actual=GUI.textureCalls.Where(c=>c.texture.name.StartsWith("resource:Frontier/Art/")&&!c.texture.name.Contains("/ActorMasks/")&&!c.texture.name.EndsWith("/Effects")).ToArray();
        Check(actual.Length==1,"Every actual protagonist draw must contain exactly one complete authored body, without a detached identity overlay");
        GUI.TextureCall body=actual[0];
        Check(body.texture.name=="resource:Frontier/Art/"+selected.atlas,"Actual production drawing must use the shared whole-body selection");
        Check(body.texture.width==1024&&body.texture.height==768,"Whole-body source uses its existing 4x3 256-pixel cells");
        Check(Math.Abs(body.uv.width)==.25f&&Math.Abs(body.uv.height-1f/3)<.00001f,"A body cannot be fragmented, cropped around a head, or stretched from a partial cell");
        int column=(int)Math.Round(body.uv.x*4)-(body.uv.width<0?1:0);
        int row=2-(int)Math.Round(body.uv.y*3);
        Check(row*4+column==selected.frame&&(body.uv.width<0)==selected.flip,"Actual full-body UV and mirror follow the captured pose and facing");
        Check(Math.Abs(body.rect.width-192)<.001f&&Math.Abs(body.rect.height-192)<.001f,"All classes/actions keep existing pixel density and complete canvas");
        Point feet=(Point)Call(app,"ScreenPoint",app.Game.Position);
        Check(Math.Abs(body.rect.x+96-feet.x)<.001f&&Math.Abs(body.rect.y+180-feet.y)<.001f,"Whole-body root remains the original collision feet");
        foreach(var draw in GUI.textureCalls.Where(c=>c.texture.name.Contains("/ActorMasks/")&&HeroAnimationState.Atlases.Any(a=>c.texture.name.EndsWith("_"+a))))
            Check(Math.Abs(draw.rect.width-192)<.001f&&Math.Abs(draw.rect.height-192)<.001f&&Math.Abs(draw.uv.width)==.25f&&Math.Abs(draw.uv.height-1f/3)<.00001f,"Flash/outline cannot reintroduce a body fragment");
        if(folder!=null){GUI.Export(folder,"hero-"+(captures++).ToString("D4")+"-"+name);}
    }
    private static void HoldMove(Point direction)
    {Input.axes["Horizontal"]=direction.x;Input.axes["Vertical"]=-direction.y;Input.held.Add(Math.Abs(direction.x)>0?direction.x>0?KeyCode.D:KeyCode.A:direction.y<0?KeyCode.W:KeyCode.S);}
    public static void RealIdleWalkingAndBasicContact(FrontierRuntime app)
    {
        for(int profession=0;profession<3;profession++)for(int heading=0;heading<4;heading++)
        {
            Prepare(app,profession);var g=app.Game;Point direction=Headings[heading];Mouse(app,g.Position+direction*4);Update(app);Draw(app,"idle-"+profession+"-"+heading);
            HoldMove(direction);for(int step=0;step<20;step++){Mouse(app,g.Position+direction*4);Update(app);if(step%4==0)Draw(app,"walk-"+profession+"-"+heading+"-"+step);}
            Check(g.ActuallyMoving,"Actual keyboard walking moves the body on real ground");Input.Clear();Mouse(app,g.Position+direction*4);Input.held.Add(KeyCode.J);Update(app);Input.held.Clear();
            var action=g.playerAction;Check(action!=null&&action.kind=="attack"&&!action.released,"Actual J begins existing basic preparation");Draw(app,"basic-prepare-"+profession+"-"+heading);
            for(int frame=0;frame<80&&!action.released;frame++){Mouse(app,g.Position-direction*4);Update(app);}
            Check(action.released&&g.PlayerPhase==ActionPhase.Active,"Actual J reaches its shared visible contact tick");
            Check((g.VisualFacing-direction).Length<.001f,"Live mouse reversal cannot replace prepared facing");Draw(app,"basic-contact-"+profession+"-"+heading);
        }
        Console.WriteLine("PASS Hero real idle, walking and J contacts: all three classes, four directions, one complete anchored body");
    }
    public static void RealQCrossEquipmentRecovery(FrontierRuntime app)
    {
        foreach(int equipped in new[]{1,2})foreach(Point direction in Headings)
        {
            Prepare(app,0);var g=app.Game;g.Equip(equipped);Mouse(app,g.Position+direction*4);Input.keys.Add(KeyCode.Q);Update(app);
            Check(g.playerAction!=null&&g.playerAction.weapon==0&&g.AppearanceWeapon==0,"Actual Q retains class weapon while another weapon is equipped");Draw(app,"cross-q-prepare-"+equipped);
            for(int frame=0;frame<14;frame++)Update(app);
            Check(g.playerAction.released&&g.PlayerPose==7,"Actual 20ms Q release selects contact before moving recovery");Draw(app,"cross-q-contact-"+equipped);
            for(int frame=0;frame<6;frame++)Update(app);HoldMove(direction);Mouse(app,g.Position+direction*4);Update(app);
            Check(g.RecoveryWalking&&g.AppearanceWeapon==equipped,"Keyboard recovery movement restores current equipment without changing Q timing");Draw(app,"cross-q-walk-"+equipped);
        }
        Console.WriteLine("PASS Hero real cross-equipped Q preparation/contact/recovery preserves class skill, equipped movement and direction");
    }
    public static void LastDodgeActualHitAndDeath(FrontierRuntime app)
    {
        for(int profession=0;profession<3;profession++)foreach(Point direction in Headings)
        {
            Prepare(app,profession);var g=app.Game;HoldMove(direction*-1);Mouse(app,g.Position-direction*4);Update(app);Input.Clear();Mouse(app,g.Position+direction*4);Input.keys.Add(KeyCode.Space);Update(app);
            Check(g.dashTime>0,"Actual Space starts the existing dodge");
            for(int frame=0;frame<30&&g.dashTime>0;frame++)Update(app);
            Check(g.dashTime<=0&&g.ActuallyMoving&&g.PlayerPose==8&&(g.VisualFacing-direction).Length<.001f,"Final moving dodge retains its captured facing after timer zero");Draw(app,"final-dodge-"+profession);
            Update(app);Check(g.PlayerPose!=8,"Next ordinary update releases final dodge");
            g.invulnerable=0;g.projectiles.Add(new Projectile{position=g.Position,damage=1,velocity=new Point(.01f,0),visual="enemyOrb",source="shot"});Update(app);
            Check(g.playerHitTime>0&&!g.Dying,"Actual hostile contact enters hit reaction");Draw(app,"hit-"+profession);
            for(int frame=0;frame<25;frame++)Update(app);
            g.invulnerable=0;g.save.health=1;g.projectiles.Add(new Projectile{position=g.Position,damage=999,velocity=new Point(.01f,0),visual="enemyOrb",source="shot"});Update(app);
            Check(g.Dying,"Actual lethal contact enters death");Draw(app,"death-"+profession);
            HeroVisualFrame expected=HeroAnimationState.Select(g);Mouse(app,g.Position-direction*4);g.Equip((profession+1)%3);Update(app);var actual=HeroAnimationState.Select(g);
            Check(actual.atlas==expected.atlas&&actual.frame==expected.frame&&actual.flip==expected.flip,"Death keeps captured complete appearance despite later pointer/equipment changes");Draw(app,"death-retained-"+profession);
        }
        Console.WriteLine("PASS Hero real final dodge, hostile hit and death retain captured full-body equipment and facing");
    }
    public static void DrawingNeverAdvancesPausedOrStoppedSimulation(FrontierRuntime app)
    {
        for(int profession=0;profession<3;profession++)
        {
            Prepare(app,profession);var g=app.Game;Mouse(app,g.Position+new Point(1,0)*4);Input.keys.Add(KeyCode.Q);Update(app);var action=g.playerAction;float elapsed=action.elapsed,health=g.save.health,energy=g.save.energy;
            g.mode="pause";for(int frame=0;frame<5;frame++){Update(app);Draw(app,"paused-"+profession+"-"+frame);}
            Check(action.elapsed==elapsed&&g.save.health==health&&g.save.energy==energy&&!action.released,"Actual paused Update and repeated draws cannot advance or pay contacts");
            g.Close();g.hitStop=.08f;for(int frame=0;frame<3;frame++){Update(app);Draw(app,"hitstop-"+profession+"-"+frame);}
            Check(action.elapsed==elapsed&&!action.released,"Existing hitstop freezes the shared visual/contact clock");
            app.VisualReviewPaused=true;for(int frame=0;frame<3;frame++)Update(app);Check(action.elapsed==elapsed,"Native-review freeze cannot continue the offline presentation clock");app.VisualReviewPaused=false;
        }
        Prepare(app,0);var before=app.Game;before.Attack(new Point(1,0));float stopped=before.playerAction.elapsed;
        Set(app,"bootError","Native hero initialization failed (regression fixture)");
        try
        {
            Update(app);GUI.commands.Clear();GUI.textureCalls.Clear();Call(app,"OnGUI");
            Check(before.playerAction.elapsed==stopped,"A failed native initialization must not run a hidden combat simulation");
            Check(!(bool)Call(app,"Save"),"A partially initialized native runtime must never write a save");
            Check(GUI.textureCalls.Count==0,"Failed native initialization must show the boot error instead of drawing a partial world");
        }
        finally{Set(app,"bootError",null);}
        LateHeroFailureStopsHiddenCombatAndSaving(app);
        Console.WriteLine("PASS Hero production pause, hitstop, read-only drawing and failed-boot simulation/save boundary");
    }
    private static string StoredFiles()
    {
        Directory.CreateDirectory(Application.persistentDataPath);
        return string.Join("|",Directory.GetFiles(Application.persistentDataPath,"*",SearchOption.AllDirectories).OrderBy(p=>p,StringComparer.Ordinal).Select(p=>p+":"+Convert.ToBase64String(File.ReadAllBytes(p))).ToArray());
    }
    private static void LateHeroFailureStopsHiddenCombatAndSaving(FrontierRuntime app)
    {
        for(int profession=0;profession<3;profession++)
        {
            Prepare(app,profession);var game=app.Game;game.Attack(new Point(1,0));Check(game.playerAction!=null,"The late-failure fixture begins a real accepted attack");
            var action=game.playerAction;float elapsed=action.elapsed,health=game.save.health,energy=game.save.energy,time=game.save.totalTime;Point feet=game.Position;
            Directory.CreateDirectory(Application.persistentDataPath);File.WriteAllText(Path.Combine(Application.persistentDataPath,"starfall-slot-2.json.bak"),"unchanged-late-render-backup");string files=StoredFiles();
            var review=typeof(FrontierRuntime).GetField("reviewOriginal",Private).GetValue(app);
            var error=new InvalidOperationException("Injected whole-body presentation failure class "+profession);
            GUI.failTextureName="resource:Frontier/Art/"+HeroAnimationState.Select(game).atlas;GUI.textureFailure=error;
            var matrix=Matrix4x4.TRS(new Vector3(19,31,0),Quaternion.Euler(0,0,17),new Vector3(1.4f,1.4f,1));var color=new Color(.3f,.5f,.7f,.8f);
            try
            {
                GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();GUI.matrix=matrix;GUI.color=color;GUI.enabled=false;
                try{Call(app,"OnGUI");}catch(TargetInvocationException ex){throw new Exception("Late hero draw failure escaped instead of entering the fatal presentation boundary",ex.InnerException);}
                string fatal=(string)typeof(FrontierRuntime).GetField("bootError",Private).GetValue(app);
                Check(fatal!=null&&fatal.Contains(error.Message)&&fatal.Contains("InvalidOperationException"),"Fatal diagnostics retain the original presentation error and its stack");
                Check(!string.IsNullOrEmpty(error.StackTrace)&&fatal.Contains(error.StackTrace),"Fatal diagnostics include the complete original throw stack, not only its message/type");
                Check(GUI.commands.Any(c=>c.Contains(error.Message)),"The same failed OnGUI displays the actual error instead of silently losing the hero");
                Check(GUI.matrix.m00==matrix.m00&&GUI.matrix.m01==matrix.m01&&GUI.matrix.m03==matrix.m03&&GUI.matrix.m10==matrix.m10&&GUI.matrix.m11==matrix.m11&&GUI.matrix.m13==matrix.m13&&GUI.color.r==color.r&&GUI.color.g==color.g&&GUI.color.b==color.b&&GUI.color.a==color.a&&!GUI.enabled,"Fatal presentation restores incoming GUI matrix/color/enabled state");
                // Exercise the normal Save gate, rather than getting a no-op
                // merely because this is an isolated visual-review fixture.
                Set(app,"reviewOriginal",null);Input.held.Add(KeyCode.J);Input.axes["Horizontal"]=1;Input.keys.Add(KeyCode.Q);
                for(int frame=0;frame<12;frame++)Update(app);
                Check(ReferenceEquals(action,game.playerAction)&&action.elapsed==elapsed&&game.save.health==health&&game.save.energy==energy&&game.save.totalTime==time&&(game.Position-feet).Length==0,"A late render failure must stop hidden movement, combat, costs and world clocks");
                Check(!(bool)Call(app,"Save")&&StoredFiles()==files,"Fatal presentation blocks the real save path and preserves every slot/backup byte");
                Call(app,"OnApplicationFocus",false);Call(app,"OnApplicationPause",true);Call(app,"OnApplicationQuit");Check(StoredFiles()==files,"Focus/pause/quit callbacks must not persist an invisible battle");
                GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
                Check(GUI.textureCalls.Count==0&&GUI.buttonCalls.Count==0&&GUI.commands.Any(c=>c.Contains(error.Message)),"Later frames show only the same fatal error, with no repeated body/world rendering or controls");
            }
            finally{GUI.failTextureName=null;GUI.textureFailure=null;Set(app,"reviewOriginal",review);Set(app,"bootError",null);Set(app,"applicationPaused",false);GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Input.Clear();}
        }
        Prepare(app,0);GUI.failTextureName="resource:Frontier/Art/TerrainVariants";var unrelated=new InvalidOperationException("Unrelated terrain diagnostic must propagate");GUI.textureFailure=unrelated;
        try
        {
            bool propagated=false;try{Call(app,"OnGUI");}catch(TargetInvocationException ex){propagated=ReferenceEquals(ex.InnerException,unrelated);}
            Check(propagated&&typeof(FrontierRuntime).GetField("bootError",Private).GetValue(app)==null,"The hero boundary cannot swallow or relabel unrelated world errors");
        }
        finally{GUI.failTextureName=null;GUI.textureFailure=null;}
        Console.WriteLine("PASS Hero late-draw injected failures in three classes stop hidden Update/Save, show original diagnostics, preserve GUI/backups and do not mask unrelated errors; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string original=Application.persistentDataPath;Application.persistentDataPath=Path.Combine(Path.GetTempPath(),"starfall-hero-"+Guid.NewGuid());
        if(args.Length>0)folder=args[0];int failed=0;var app=new FrontierRuntime();
        try
        {
            Call(app,"Awake");Check(app.Game!=null,"Production runtime imports shipped campaign");
            foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{RealIdleWalkingAndBasicContact,RealQCrossEquipmentRecovery,LastDodgeActualHitAndDeath,DrawingNeverAdvancesPausedOrStoppedSimulation})
            {try{test(app);}catch(Exception ex){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(ex.InnerException??ex).Message);}}
            Console.WriteLine("Hero offline input/render groups: "+(4-failed)+"/4; NATIVE_UNVERIFIED");return failed==0?0:1;
        }
        finally{app.EndVisualReview();Call(app,"OnDestroy");Input.Clear();Application.persistentDataPath=original;}
    }
}
