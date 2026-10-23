// Real B / mouse / Tab / pad town construction reviews through shipped Update/OnGUI.
// Offline command streams are not native Editor/GPU/input acceptance.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeTownProjectChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly Rect Cancel = new Rect(338,626,288,46), Confirm = new Rect(654,626,288,46);
    static string captureFolder,capturePrefix; static int captures;
    [Serializable] public class Label { public string k,text; public float[] r; public int size; public int[] padding; }
    static void Check(bool ok,string message) { if(!ok)throw new Exception(message); }
    static object Call(object target,string name,params object[] args) { var m=target.GetType().GetMethod(name,Private|BindingFlags.Public);Check(m!=null,"Production method exists: "+name);return m.Invoke(target,args); }
    static T Get<T>(object target,string name) { var f=target.GetType().GetField(name,Private);Check(f!=null,"Production field exists: "+name);return(T)f.GetValue(target); }
    static void Set(object target,string name,object value) { var f=target.GetType().GetField(name,Private);Check(f!=null,"Production field exists: "+name);f.SetValue(target,value); }
    static object Pending(FrontierRuntime app) { return Get<object>(app,"townProjectConfirmation"); }
    static SaveData Copy(SaveData s) { return JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s)); }
    static bool Near(float a,float b) { return Math.Abs(a-b)<.003f; }
    static bool Same(Rect a,Rect b) { return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height); }
    static Rect Build(int i) { return new Rect(296+318*i,570,268,42); }
    static string Id(FrontierRuntime app,Rect r) { return(string)Call(app,"ControlId",r.x,r.y,r.width,r.height); }
    static void Update(FrontierRuntime app) { Call(app,"Update");Input.keys.Clear(); }
    static void Key(FrontierRuntime app,KeyCode key) { Input.keys.Add(key);Update(app); }
    static void Draw(FrontierRuntime app,string name=null)
    {
        GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        if(captureFolder!=null&&name!=null){var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();GUI.Export(captureFolder,capturePrefix+"-"+name);captures++;GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);}
    }
    static void Click(FrontierRuntime app,Rect r) { GUI.queuedClick=r;Draw(app);Check(!GUI.queuedClick.HasValue,"Actual enabled mouse button consumes its click at "+r.x+","+r.y); }
    static GUI.ButtonCall Button(Rect r) { return GUI.buttonCalls.Find(b=>Same(b.rect,r)); }
    static string Economy(GameCore g) { return g.save.coins+"/"+string.Join(",",g.save.inventory)+"/"+string.Join(",",g.save.projects)+"/"+string.Join(",",g.save.reputation)+"/"+string.Join(",",g.save.flags); }
    static string Permanent(GameCore g) { var s=g.save;return s.health.ToString(CultureInfo.InvariantCulture)+"/"+s.maxHealth+"/"+s.energy+"/"+s.weapon+"/"+s.profession+"/"+s.technique+"/"+s.skillPoints+"/"+s.stage+"/"+s.relicRank+"/"+string.Join(",",s.relics)+"/"+string.Join(",",s.talents)+"/"+string.Join(",",s.weaponStyles)+"/"+string.Join(",",s.mastery)+"/"+JsonUtility.ToJson(s.battlePlans[0]); }
    static void Prepare(FrontierRuntime app,int room=0)
    {
        Input.Clear();GUI.queuedClick=null;GUI.enabled=true;GUI.color=Color.white;GUI.matrix=Matrix4x4.identity;Event.current.type=EventType.Repaint;Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;
        app.BeginVisualReview(1,room);app.VisualReviewPaused=false;var g=app.Game;g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();g.effects.Clear();g.fallen.Clear();g.save.companion=-1;g.save.english=true;g.save.largeText=false;g.save.showGuidance=false;g.save.reducedMotion=true;g.save.effectsQuality=0;
        g.save.inventory=new[]{20,20,14,11,20,2,3,4};g.save.projects=new[]{0,0,0};g.save.reputation=new[]{2,3,4};g.save.coins=140;g.save.shards=17;g.save.health=53;g.save.maxHealth=140;g.save.energy=61;g.save.flags.Remove("relic_1");if(!g.Has("return_letter_complete"))g.save.flags.Add("return_letter_complete");g.toastTime=0;
        Set(app,"applicationPaused",false);Set(app,"pointerSeen",false);Set(app,"controllerUsed",false);Set(app,"resumeTime",0f);Call(app,"Resize");Key(app,KeyCode.B);Check(g.mode=="camp","Actual B opens the original Town projects page");Draw(app);
    }
    static void Open(FrontierRuntime app,int project,bool canPay=true)
    {
        string before=Economy(app.Game);Click(app,Build(project));Check(Economy(app.Game)==before,"Original facility button now previews without immediately consuming stock or coins");Draw(app);Check(app.Game.mode=="camp"&&Pending(app)!=null,"The owned project review overlays the original camp mode");var nav=Get<MenuNavigation>(app,"navigation");Check(nav.scope.Contains("/town-project/"),"Project modal has its own quote-token navigation scope");Check(nav.focused==Id(app,Cancel),"A new project review gives Cancel the first keyboard/controller focus");Check(nav.CanActivate(Id(app,Cancel)),"Cancel is available for every project review");Check(nav.CanActivate(Id(app,Confirm))==canPay,"Only currently payable confirmations own activation");Check(Button(Confirm)!=null&&Button(Confirm).enabled==canPay,"Actual confirmation button is drawn with the correct enabled state");
    }
    static void Deliberate(FrontierRuntime app,KeyCode key) { Key(app,KeyCode.Tab);Draw(app);Check(Get<MenuNavigation>(app,"navigation").focused==Id(app,Confirm),"One deliberate Tab reaches Confirm after Cancel");Key(app,key);Draw(app); }
    static void Paid(FrontierRuntime app,SaveData before,int project)
    {
        var s=app.Game.save;int tier=before.projects[project];Check(s.coins==before.coins-(30+tier*20)&&s.inventory[0]==before.inventory[0]-(4+tier*2)&&s.inventory[1]==before.inventory[1]-(2+tier)&&s.inventory[4]==before.inventory[4]-(3+tier),"Confirmed construction charges exactly the mature tier-dependent four costs");
        for(int i=0;i<3;i++)Check(s.projects[i]==before.projects[i]+(i==project?1:0),"Exactly the selected project gains its next tier");Check(s.reputation[2]==Math.Min(10,before.reputation[2]+1)&&s.reputation[0]==before.reputation[0]&&s.reputation[1]==before.reputation[1],"Original Wild Assembly trust rises once and respects its cap");Check(Near(s.health,before.health)&&Near(s.maxHealth,before.maxHealth)&&Near(s.energy,before.energy),"Construction neither heals nor changes maximum health or energy");Check(app.Game.Has("relic_1")==((project==0&&tier==1)||before.flags.Contains("relic_1")),"Only the original second canal tier grants Amber Seed");Check(Pending(app)==null&&app.Game.mode=="camp","Accepted project review retires and remains on the original camp page");
    }
    public static void ThreeFacilitiesCostsAndDeliberateExactlyOnce(FrontierRuntime app)
    {
        foreach(KeyCode key in new[]{KeyCode.Return,KeyCode.JoystickButton0,KeyCode.Escape,KeyCode.JoystickButton1}){Prepare(app);string before=Economy(app.Game);Open(app,0);Key(app,key);Draw(app);Check(Pending(app)==null&&app.Game.mode=="camp"&&Economy(app.Game)==before,"Default Enter/pad A and explicit Esc/pad B cancel without spending");}
        Prepare(app);string cancelled=Economy(app.Game);Open(app,2);Click(app,Cancel);Check(Pending(app)==null&&Economy(app.Game)==cancelled&&app.Game.mode=="camp","Actual mouse Cancel preserves materials, progress and mode");
        for(int project=0;project<3;project++)for(int tier=0;tier<2;tier++)
        {
            Prepare(app);app.Game.save.projects[project]=tier;app.Game.save.reputation[2]=tier==1?10:4;Draw(app);var before=Copy(app.Game.save);string permanent=Permanent(app.Game);Open(app,project);if(project==0)Click(app,Confirm);else Deliberate(app,project==1?KeyCode.Return:KeyCode.JoystickButton0);Paid(app,before,project);Check(Permanent(app.Game)==permanent,"Original build/story/equipment/talents remain intact through construction");string paid=Economy(app.Game);GUI.queuedClick=Confirm;Draw(app);GUI.queuedClick=null;Key(app,KeyCode.Return);Draw(app);Check(Economy(app.Game)==paid,"Retired mouse and repeated Enter cannot purchase another tier or charge twice");
        }
        Prepare(app);app.Game.save.inventory[0]=0;app.Game.save.inventory[1]=0;app.Game.save.inventory[4]=0;app.Game.save.coins=0;Draw(app);string shortStock=Economy(app.Game);Open(app,1,false);GUI.queuedClick=Confirm;Draw(app);Check(GUI.queuedClick.HasValue&&Economy(app.Game)==shortStock&&Pending(app)!=null,"Shortfall preview rejects payment and keeps all original balances");GUI.queuedClick=null;Key(app,KeyCode.Escape);Draw(app);
        Console.WriteLine("PASS actual B and all three original facility buttons, two tiers, nonspending preview, Cancel-first keyboard/pad/mouse, exact old costs/trust/Amber Seed, no healing, single acceptance and material-shortfall rejection; NATIVE_UNVERIFIED");
    }
    public static void ModalOwnershipStaleQuotesAndPauseGuards(FrontierRuntime app)
    {
        Prepare(app);Open(app,0);string before=Economy(app.Game);object quote=Pending(app);
        foreach(Rect parent in new[]{Build(0),Build(1),Build(2),new Rect(48,180,174,43)}){GUI.queuedClick=parent;Draw(app);Check(GUI.queuedClick.HasValue&&ReferenceEquals(Pending(app),quote)&&Economy(app.Game)==before,"Modal blocks every underlying project and sidebar mouse control");GUI.queuedClick=null;}
        foreach(KeyCode key in new[]{KeyCode.I,KeyCode.K,KeyCode.M,KeyCode.G,KeyCode.C,KeyCode.B,KeyCode.N,KeyCode.F1,KeyCode.JoystickButton4,KeyCode.JoystickButton5}){Key(app,key);Draw(app);Check(app.Game.mode=="camp"&&ReferenceEquals(Pending(app),quote)&&Economy(app.Game)==before,"Project review blocks parent hotkeys and LB/RB menu switching");}
        foreach(bool review in new[]{false,true}){if(review)app.VisualReviewPaused=true;else Call(app,"OnApplicationPause",true);Draw(app);GUI.queuedClick=Confirm;Draw(app);Check(GUI.queuedClick.HasValue&&Economy(app.Game)==before,"Paused application/review cannot accept a mouse payment");GUI.queuedClick=null;Key(app,KeyCode.Tab);Key(app,KeyCode.Return);Draw(app);Check(Economy(app.Game)==before,"Paused keyboard cannot accept payment");if(review)app.VisualReviewPaused=false;else Call(app,"OnApplicationPause",false);}
        Key(app,KeyCode.Escape);Draw(app);Prepare(app);Open(app,0);before=Economy(app.Game);Call(app,"OnApplicationFocus",false);Draw(app);Check(Pending(app)==null&&app.Game.mode=="camp"&&Economy(app.Game)==before,"Actual focus loss retires the project confirmation");Call(app,"OnApplicationFocus",true);GUI.queuedClick=Confirm;Draw(app);GUI.queuedClick=null;Check(Economy(app.Game)==before,"Refocus cannot accept a retired mouse confirmation");
        foreach(string mutation in new[]{"coins","wood","crystals","parts","other-project","trust","ending","save-owner","world-generation","game-owner","mode","death"})
        {
            Prepare(app);Open(app,0);GameCore old=app.Game;string visible=LabelAt(476);object pending=Pending(app);
            if(mutation=="coins")old.save.coins--;else if(mutation=="wood")old.save.inventory[0]--;else if(mutation=="crystals")old.save.inventory[1]--;else if(mutation=="parts")old.save.inventory[4]--;else if(mutation=="other-project")old.save.projects[1]++;else if(mutation=="trust")old.save.reputation[2]++;else if(mutation=="ending")old.save.frontierEnding=old.save.frontierEnding==1?2:1;
            else if(mutation=="save-owner"){old.Load(Copy(old.save));old.mode="camp";}else if(mutation=="world-generation"){old.Enter(0);old.Close();old.mode="camp";}else if(mutation=="game-owner"){var next=new GameCore(old.data,Copy(old.save));next.mode="camp";typeof(FrontierRuntime).GetProperty("Game").GetSetMethod(true).Invoke(app,new object[]{next});}else if(mutation=="mode")old.mode="inventory";else{old.mode="world";old.save.health=1;old.invulnerable=0;Call(old,"Hurt",999f,"environment",(Point?)null);old.mode="camp";}
            string changed=Economy(app.Game),original=Economy(old);GUI.queuedClick=Confirm;Draw(app);GUI.queuedClick=null;Check(Economy(app.Game)==changed&&Economy(old)==original,"Stale "+mutation+" cannot pay for either original or new game/save owner");bool values=Array.IndexOf(new[]{"coins","wood","crystals","parts","other-project","trust","ending"},mutation)>=0;Check((Pending(app)!=null)==values,"Value changes preserve the disabled old review; invalid owner/mode/death retires it: "+mutation);if(values){Check(ReferenceEquals(pending,Pending(app))&&Button(Confirm)!=null&&!Button(Confirm).enabled&&LabelAt(476)==visible,"Changed values leave the original coin quote visible and disable confirmation");Key(app,KeyCode.Escape);Draw(app);Check(Pending(app)==null&&Economy(app.Game)==changed,"Stale review remains safely cancellable");}
        }
        Prepare(app);Open(app,0);app.Game.save.inventory[2]++;var unaffected=Copy(app.Game.save);Click(app,Confirm);Paid(app,unaffected,0);
        Console.WriteLine("PASS parent mouse/menu-key/LB/RB isolation, application/review pause and focus guards, immutable disabled stock/project/trust/ending reviews, invalid owner/load/generation/mode/death retirement and unrelated herb change acceptance; NATIVE_UNVERIFIED");
    }
    public static void LegacySaveAndPreparedCombatContinuity(FrontierRuntime app)
    {
        for(int version=1;version<=2;version++)
        {
            Prepare(app);var g=app.Game;g.save.version=version;File.WriteAllText(SaveStore.PathFor(1),JsonUtility.ToJson(g.save));string warning;var saved=SaveStore.Read(1,out warning);Check(saved!=null&&warning==null,"Production SaveStore reads raw old v"+version);g.Load(saved);g.Start();Key(app,KeyCode.B);Draw(app);var before=Copy(g.save);string permanent=Permanent(g);Open(app,1);Deliberate(app,KeyCode.Return);Paid(app,before,1);Check(Permanent(g)==permanent,"Mature migrated story/build remains intact");g.PrepareSave();Check(SaveStore.Write(g.save,0),"Production checksum writer accepts paid project progress");var roundtrip=SaveStore.Read(0,out warning);Check(roundtrip!=null&&warning==null&&roundtrip.projects[1]==1&&roundtrip.coins==before.coins-30,"Checksum read retains exact old-version construction cost and tier");string json=JsonUtility.ToJson(roundtrip);Check(!json.Contains("townProject")&&!json.Contains("coinsBefore")&&!json.Contains("nextTier"),"Transient quote adds no save schema fields");g.Load(roundtrip);g.Start();Draw(app);Check(Permanent(g)==permanent&&Pending(app)==null,"Load restores permanent build without an active confirmation");
        }
        foreach(bool flying in new[]{false,true})
        {
            Prepare(app);var g=app.Game;Key(app,KeyCode.Escape);g.Enter(0);g.Close();g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();Check(app.SetVisualReviewPosition(new Point(18,15)),"Legal unchanged town feet accept combat fixture");g.save.weapon=1;g.aim=new Point(1,0);g.save.seed=0;Set(app,"camera",g.Position);var enemy=new Enemy(67891,0,g.Position.x+4.5f,g.Position.y,999){timer=1000};g.enemies.Add(enemy);Key(app,KeyCode.J);var action=g.playerAction;Check(action!=null&&action.kind=="attack"&&!action.released,"Actual J prepares the original bow attack");if(flying){int ticks=0;while(!action.released&&ticks++<50)Update(app);Check(action.released&&g.projectiles.Count>0&&enemy.hp==999,"Actual released bow projectile remains physically in flight");}var shot=g.projectiles.Count>0?g.projectiles[0]:null;float damage=action.damage;var impact=action.impact;float critical=impact.criticalChance;Key(app,KeyCode.B);Draw(app);float elapsed=action.elapsed,life=shot==null?0:shot.life,total=g.save.totalTime;Open(app,2);for(int n=0;n<15;n++)Update(app);Check(g.save.totalTime==total&&action.elapsed==elapsed&&(shot==null||shot.life==life),"Actual camp review pauses prepared/flying combat and world clocks");var before=Copy(g.save);Click(app,Confirm);Paid(app,before,2);Check(ReferenceEquals(g.playerAction,action)&&action.damage==damage&&ReferenceEquals(action.impact,impact)&&impact.criticalChance==critical,"Construction never rewrites prepared damage, critical snapshot or action ownership");if(shot!=null)Check(ReferenceEquals(g.projectiles[0],shot)&&ReferenceEquals(shot.impact,impact),"Flying projectile retains its original impact through the project payment");Key(app,KeyCode.Escape);int frames=0;while(enemy.hp==999&&frames++<150)Update(app);Check(enemy.hp<999,"Original prepared/flying bow physically resumes and contacts after closing camp");
        }
        Console.WriteLine("PASS raw v1/v2 and checksummed production SaveStore, permanent story/build/project/cost retention, transient-free schema, actual J preparation/flying bow pause and immutable combat contact after construction; NATIVE_UNVERIFIED");
    }
    static string LabelAt(float y) { foreach(string c in GUI.commands)if(c.StartsWith("{\"k\":\"text\"",StringComparison.Ordinal)){var l=JsonUtility.FromJson<Label>(c);if(l.r!=null&&l.r.Length==4&&Near(l.r[0],338)&&Near(l.r[1],y))return l.text;}return ""; }
    static void VisibleFacts(FrontierRuntime app,int project,int tier)
    {
        string title=LabelAt(164),progress=LabelAt(218),now=LabelAt(253),after=LabelAt(296);Check(title.Length>0&&progress.Contains(tier+" → "+Math.Min(2,tier+1))&&now.Length>0&&after.Length>0,"Actual project title, current and next tier/benefits are visibly drawn");Check(LabelAt(386).Contains("20")&&LabelAt(416).Contains("20")&&LabelAt(446).Contains("20")&&LabelAt(476).Contains("140"),"Actual four quote rows show the captured materials and coin balances");if(tier<2){Check(LabelAt(386).Contains("cost "+(4+tier*2))||LabelAt(386).Contains("费用"+(4+tier*2)),"Visible timber cost is the exact selected tier cost");Check(LabelAt(476).Contains(" → "+(140-(30+tier*20))),"Visible coin row shows the exact post-payment balance");}
        Check(Button(Cancel)!=null&&Button(Confirm)!=null,"Both safe and committing buttons are actually drawn");foreach(var b in new[]{Button(Cancel),Button(Confirm)})Check(b.rect.x>=24&&b.rect.y>=24&&b.rect.x+b.rect.width<=1256&&b.rect.y+b.rect.height<=776,"Actual modal actions fit authored canvas bounds");
    }
    public static void BilingualLayoutReadOnlyAndUnavailablePlans(FrontierRuntime app)
    {
        foreach(int width in new[]{960,1280})foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})
        {
            Prepare(app);app.Game.save.english=english;app.Game.save.largeText=large;Screen.width=width;Screen.height=width==960?600:800;Call(app,"Resize");Draw(app);int project=(english?1:0)+(large?1:0);Open(app,project);VisibleFacts(app,project,0);Draw(app,"preview-"+(english?"en":"zh")+(large?"-large":"-normal")+"-"+width);string json=JsonUtility.ToJson(app.Game.save);int revision=app.Game.revision;float clock=Get<float>(app,"clock");object quote=Pending(app);var art=Get<PixelArt>(app,"art");int textures=Get<List<Texture2D>>(art,"owned").Count,cache=Get<Dictionary<string,Texture2D>>(art,"cache").Count;
            foreach(EventType evt in new[]{EventType.Layout,EventType.Repaint}){Event.current.type=evt;GUI.enabled=false;GUI.color=new Color(.2f,.3f,.4f,.5f);GUI.matrix=Matrix4x4.TRS(new Vector3(3,5,0),Quaternion.Euler(0,0,10),new Vector3(1.4f,1.4f,1));var matrix=GUI.matrix;Draw(app);Check(JsonUtility.ToJson(app.Game.save)==json&&app.Game.revision==revision&&Get<float>(app,"clock")==clock&&ReferenceEquals(Pending(app),quote),"Repeated Layout/Repaint preserves complete save, revision, UI clock and quote identity");Check(!GUI.enabled&&Near(GUI.color.a,.5f)&&Near(GUI.matrix.m00,matrix.m00)&&Near(GUI.matrix.m03,matrix.m03),"OnGUI restores incoming enabled/color/matrix state");}
            Check(textures==Get<List<Texture2D>>(art,"owned").Count&&cache==Get<Dictionary<string,Texture2D>>(art,"cache").Count,"Repeated preview draws allocate no new art textures or cached icons");GUI.enabled=true;GUI.color=Color.white;GUI.matrix=Matrix4x4.identity;Event.current.type=EventType.Repaint;
        }
        foreach(string state in new[]{"shortfall","capped","away"})foreach(bool english in new[]{false,true})
        {
            Prepare(app,state=="away"?1:0);app.Game.save.english=english;app.Game.save.largeText=true;Screen.width=960;Screen.height=600;Call(app,"Resize");if(state=="shortfall"){app.Game.save.inventory[0]=app.Game.save.inventory[1]=app.Game.save.inventory[4]=app.Game.save.coins=0;}if(state=="capped")app.Game.save.projects[2]=2;Draw(app);string before=Economy(app.Game);Open(app,2,false);Check(LabelAt(516).Length>0,"Unavailable plan shows its actual reason");Draw(app,state+"-"+(english?"en":"zh")+"-large-960");GUI.queuedClick=Confirm;Draw(app);Check(GUI.queuedClick.HasValue&&Economy(app.Game)==before,"Unavailable "+state+" cannot take a payment");GUI.queuedClick=null;Key(app,KeyCode.Escape);Draw(app);Check(Pending(app)==null&&app.Game.mode=="camp","Unavailable review is safely dismissible in original camp mode");
        }
        Prepare(app);app.Game.save.projects[0]=1;Draw(app);Open(app,0);VisibleFacts(app,0,1);Draw(app,"canal-tier2-en-normal-1280");
        Console.WriteLine("PASS bilingual normal/large 960/1280 previews and large-text shortfall/cap/away reasons, visible captured costs/current-next benefits, canvas bounds, repeated read-only OnGUI/save/revision/clock/GUI-state/texture-cache contracts and offline command captures; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string oldPath=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-town-project-"+Guid.NewGuid());Application.persistentDataPath=directory;captureFolder=args.Length>0&&args[0]!="-"?args[0]:null;capturePrefix=args.Length>1?args[1]:"qa-town-project";int failed=0;var app=new FrontierRuntime();
        try{Directory.CreateDirectory(directory);if(captureFolder!=null)Directory.CreateDirectory(captureFolder);Call(app,"Awake");foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{ThreeFacilitiesCostsAndDeliberateExactlyOnce,ModalOwnershipStaleQuotesAndPauseGuards,LegacySaveAndPreparedCombatContinuity,BilingualLayoutReadOnlyAndUnavailablePlans})try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error));}app.EndVisualReview();Call(app,"OnDestroy");}
        finally{Application.persistentDataPath=oldPath;Input.Clear();GUI.queuedClick=null;GUI.enabled=true;GUI.color=Color.white;GUI.matrix=Matrix4x4.identity;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime town project: "+(4-failed)+"/4 groups passed; captures="+captures+"; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
