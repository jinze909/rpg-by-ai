// Actual production Update/OnGUI/SaveStore with offline Unity declarations.
// Staged event/layout fixtures are distinguished from the unmodified 300 HP fight.
// This does not verify native Unity import, GPU rendering, hardware input or feel.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeSignalRepriseChecks
{
    private const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static string captureFolder;
    private static int captures;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static bool Near(float a, float b, float tolerance = .004f) { return Math.Abs(a - b) <= tolerance; }
    private static object Call(object obj, string name, params object[] args)
    { var method = obj.GetType().GetMethod(name, All); Check(method != null, "Production entry point exists: " + name); return method.Invoke(obj, args); }
    private static void Set(object obj, string name, object value)
    { var field = obj.GetType().GetField(name, All); Check(field != null, "Fixture field exists: " + name); field.SetValue(obj, value); }
    private static T Get<T>(object obj, string name)
    { var field = obj.GetType().GetField(name, All); Check(field != null, "Fixture field exists: " + name); return (T)field.GetValue(obj); }
    private static T Property<T>(object obj, string name)
    { var property = obj.GetType().GetProperty(name, All); Check(property != null, "Shipped reprise behavior exists: " + name); return (T)property.GetValue(obj, null); }
    private static T Field<T>(object obj, string name)
    { Check(obj != null, "An actual attempt record exists before reading " + name); var field = obj.GetType().GetField(name); Check(field != null && field.IsInitOnly, "Attempt record is immutable: " + name); return (T)field.GetValue(obj); }
    private static bool Active(GameCore g) { return Property<bool>(g, "SignalRepriseActive"); }
    private static object Last(GameCore g) { return Property<object>(g, "LastSignalReprise"); }
    private static object Previous(GameCore g) { return Property<object>(g, "PreviousSignalReprise"); }
    private static float Clock(GameCore g) { return Property<float>(g, "SignalRepriseTime"); }
    private static bool Same(Rect a, Rect b) { return Near(a.x,b.x) && Near(a.y,b.y) && Near(a.width,b.width) && Near(a.height,b.height); }
    private static bool Overlap(Rect a, Rect b) { return a.x < b.x+b.width && b.x < a.x+a.width && a.y < b.y+b.height && b.y < a.y+a.height; }
    private static bool Contains(Rect r, Point p) { return p.x >= r.x && p.x < r.x+r.width && p.y >= r.y && p.y < r.y+r.height; }
    private static void Update(FrontierRuntime app) { Call(app, "Update"); Input.keys.Clear(); }
    private static void Frames(FrontierRuntime app, int count) { for (int i=0;i<count;i++) Update(app); }
    private static void Key(FrontierRuntime app, KeyCode key) { Input.keys.Add(key); Update(app); }
    private static void Draw(FrontierRuntime app, string capture = null)
    {
        GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); Call(app, "OnGUI");
        if (captureFolder != null && capture != null)
        {
            var commands=GUI.commands.ToArray(); var textures=GUI.textureCalls.ToArray(); var buttons=GUI.buttonCalls.ToArray();
            GUI.Export(captureFolder,"qa-reprise-"+capture); captures++;
            GUI.commands.AddRange(commands); GUI.textureCalls.AddRange(textures); GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static bool Text(string text) { return GUI.commands.Exists(c=>c.Contains("\"k\":\"text\"") && c.Contains(text)); }
    private static void Pointer(FrontierRuntime app, Point p)
    { float scale=Get<float>(app,"scale"); Input.mousePosition=new Vector3(Get<float>(app,"offsetX")+p.x*scale,Screen.height-Get<float>(app,"offsetY")-p.y*scale,0); }
    private static void Position(FrontierRuntime app, Point foot)
    { Check(app.SetVisualReviewPosition(foot), "Fixture uses valid authored world footing"); Set(app,"camera",app.Game.Position); }
    private static WorldObject Lamp(GameCore g)
    { var lamp=Array.Find(g.Map.objects,o=>o.kind=="watchSignal2"); Check(lamp!=null,"The authored third forest signal exists"); return lamp; }
    private static void AtLamp(FrontierRuntime app)
    { var lamp=Lamp(app.Game); Position(app,new Point(lamp.x,lamp.y-1)); }
    private static void InteractLamp(FrontierRuntime app) { AtLamp(app); Key(app,KeyCode.E); Check(app.Game.mode=="dialog","Actual E opens the third lamp dialogue"); }
    private static void Choose(FrontierRuntime app, string action, string capture = null)
    {
        var g=app.Game; Check(g.mode=="dialog" && g.conversation!=null,"Numbered selection belongs to an actual dialogue");
        int index=Array.FindIndex(g.conversation.choices,c=>c.action==action);
        Check(index>=0 && index<9,"Actual dialogue exposes numbered choice "+action);
        int page=0;
        do { Draw(app,capture==null?null:capture+"-page"+page++); if((bool)Call(app,"DialogueChoicesVisible"))break; Key(app,KeyCode.Return); } while(page<20);
        Check((bool)Call(app,"DialogueChoicesVisible"),"All rules pages can be read before choosing");
        Key(app,(KeyCode)((int)KeyCode.Alpha1+index));
    }
    private static void Click(FrontierRuntime app, Rect rect)
    { Draw(app); Check(GUI.buttonCalls.Exists(b=>Same(b.rect,rect)&&b.enabled),"Actual visible travel control is enabled"); GUI.queuedClick=rect; Draw(app); Check(!GUI.queuedClick.HasValue,"Production OnGUI consumes the travel click"); }
    private static void Travel(FrontierRuntime app, int room)
    { Key(app,KeyCode.M); Set(app,"selectedRegion",room); Click(app,new Rect(888,616,306,47)); Check(app.Game.mode=="world" && app.Game.save.room==room,"Ordinary map input travels to region "+room); Set(app,"camera",app.Game.Position); }
    private static void Prepare(FrontierRuntime app, int profession=0, bool english=true, bool large=false)
    {
        Input.Clear(); GUI.queuedClick=null; GUI.matrix=Matrix4x4.identity; GUI.color=Color.white; GUI.enabled=true;
        Screen.width=1280; Screen.height=800; Time.unscaledDeltaTime=.02f; Event.current.type=EventType.Repaint;
        app.BeginVisualReview(profession,0); app.VisualReviewPaused=false; var g=app.Game;
        Check(g.save.profession==profession,"Visual fixture uses the requested actual profession");
        foreach(string flag in new[]{"intro","scout_saved","cache_1","watch_accept","watch_signal_0","watch_signal_1","watch_signal_2","watch_complete","signal_started","signal_complete"})
            if(!g.Has(flag))g.save.flags.Add(flag);
        g.save.english=english; g.save.largeText=large; g.save.showGuidance=false; g.save.reducedMotion=true; g.save.effectsQuality=0; g.save.companion=-1;
        Set(app,"applicationPaused",false); Set(app,"pointerSeen",false); Set(app,"controllerUsed",false); Set(app,"combatHudTime",0f); Set(app,"resumeTime",0f); Set(app,"arrivalTime",0f); Call(app,"Resize");
        g.enemies.Clear(); Travel(app,1); g.enemies.Clear(); g.fallen.Clear(); g.projectiles.Clear(); g.effects.Clear(); g.hazards.Clear(); g.invulnerable=0;
        Pointer(app,new Point(900,390)); AtLamp(app);
    }
    private static void Start(FrontierRuntime app, string capture=null)
    {
        InteractLamp(app); var g=app.Game;
        Check(g.conversation.choices[0].action=="close","Original completed first choice remains close");
        Choose(app,"signal_reprise_menu",capture==null?null:capture+"-completed");
        Check(g.conversation.choices[0].action=="signal_reprise_start","New first rules choice begins a voluntary reprise");
        Choose(app,"signal_reprise_start",capture==null?null:capture+"-rules");
        Enemy boss=g.enemies.Find(GameCore.IsSignalWarden);
        Check(Active(g) && boss!=null && Near(boss.hp,300) && Near(boss.maxHp,300),"Actual E/pages/Alpha2/Alpha1 start exactly one full 300 HP Warden");
    }
    private static void Stop(FrontierRuntime app)
    { InteractLamp(app); Choose(app,"signal_reprise_stop"); Check(!Active(app.Game),"Actual lamp stop choice ends its owned attempt"); }
    private static string Progress(GameCore g)
    { var s=g.save; return s.coins+"/"+s.xp+"/"+s.level+"/"+s.shards+"/"+s.skillPoints+"/"+s.bounty+"/"+s.bountyProgress+"/"+s.bountyCycle+"/"+string.Join(",",s.mastery)+"/"+string.Join(",",s.kills)+"/"+string.Join(",",s.flags.ToArray()); }
    private static Projectile Incoming(GameCore g,float damage=13)
    { var p=new Projectile {position=g.Position,damage=damage,source="shot"}; g.projectiles.Add(p); return p; }
    private static void Contact(FrontierRuntime app, Enemy enemy, float damage, bool playerOwned)
    { app.Game.projectiles.Add(new Projectile {position=enemy.Position,friendly=true,technique=true,damage=damage,impact=new ImpactProfile(0,0,0,0,0,playerOwned)}); Update(app); }

    private static void CompletedStoryAndPhysicalStart(FrontierRuntime app)
    {
        for(int language=0;language<2;language++)for(int large=0;large<2;large++)
        {
            Prepare(app,0,language==1,large==1); var g=app.Game; string progress=Progress(g);
            InteractLamp(app); Check(g.conversation.choices[0].action=="close","Mature completed lamp keeps close at Alpha1"); Choose(app,"close");
            Check(!Active(g)&&!g.SignalActive&&Progress(g)==progress,"Original first choice preserves completion/rewards and closes without spawning");
            Start(app,"story-"+language+"-"+large);
            Check(Progress(g)==progress && Near(Clock(g),0),"Starting has no completion, reward, progression or artificial elapsed-time mutation");
            Check(!((bool)Call(g,"StartSignalReprise")) && g.enemies.FindAll(GameCore.IsSignalWarden).Count==1,"Duplicate start cannot clone the boss");
        }
        Console.WriteLine("PASS completed first close, appended numbered reprise/rules start, bilingual normal/large pages, 300 HP and duplicate guard; NATIVE_UNVERIFIED");
    }
    private static void GenuineFightAndNormalCosts(FrontierRuntime app)
    {
        Prepare(app); Start(app); var g=app.Game; string progress=Progress(g); float world=g.save.totalTime,health=g.save.health;
        Incoming(g); Update(app);
        Check(g.save.health<health && Property<int>(g,"SignalRepriseHitsTaken")==1,"Real hostile projectile collision deals ordinary damage and records a hit");
        int stew=g.save.inventory[6],frames=0,techniques=0,dodges=0,meals=1; float energy=g.save.energy;
        Key(app,KeyCode.F); Check(g.save.inventory[6]==stew-1 && g.save.foodUntil>g.save.totalTime,"Actual F consumes existing stew and applies ordinary food");
        bool energySpent=false;
        while(Active(g)&&frames++<16000)
        {
            Check(!g.Dying && g.save.room==1,"Starter Vanguard survives the actual reprise"); Enemy target=g.enemies.Find(GameCore.IsSignalWarden); float distance=(target.Position-g.Position).Length;
            Point movement=distance>1.4f?g.DirectionTo(g.Position,target.Position):new Point();
            if(target.state==1 && (g.Position-target.target).Length<3.4f)
            { movement=(g.Position-target.target).Length>.05f?(g.Position-target.target).Normalized:new Point(-1,0); if(g.dashCooldown<=0){Input.keys.Add(KeyCode.Space);dodges++;} }
            Input.axes["Horizontal"]=movement.x; Input.axes["Vertical"]=-movement.y; Input.held.Add(KeyCode.J);
            Set(app,"camera",g.Position); Point screen=(Point)Call(app,"ScreenPoint",target.Position); Pointer(app,screen);
            if(distance<3&&g.skillCooldown<=0&&g.save.energy>=40){Input.keys.Add(KeyCode.Q);techniques++;}
            if(g.save.health<g.save.maxHealth*.6f&&g.save.inventory[6]>0){Input.keys.Add(KeyCode.F);meals++;}
            float before=g.save.energy; Update(app); energySpent|=g.save.energy<before-.1f;
        }
        Input.held.Clear(); Input.axes.Clear();
        object result=Last(g);
        Check(frames<16000&&!g.Dying&&result!=null&&Field<bool>(result,"won")&&techniques>0&&dodges>0&&meals>0&&energySpent,
            "Actual held J/Q/Space/F wins full 300 HP without forced health, boss HP, invulnerability or attack clocks");
        Check(Near(Field<float>(result,"damage"),300,.02f)&&Field<int>(result,"contacts")>0&&Field<int>(result,"breaks")>0&&Field<int>(result,"hitsTaken")>0,"Real fight records capped own damage, contacts, breaks and actual incoming hits");
        Check(Progress(g)==progress && g.SignalComplete && g.Has("watch_complete") && !g.Has("boss_1") && g.save.totalTime>world && g.save.inventory[6]<stew,"Victory advances ordinary world time/consumables but no XP, mastery, bounty, chapter flags or repeat reward");
        Draw(app,"genuine-victory"); Check(!Text("loot secured")&&!Text("Loot secured")&&!Text("战利品已收取")&&Text("no repeat reward"),"Reprise fallen-body HUD cannot claim loot was secured");
        Console.WriteLine("  Genuine Vanguard reprise: "+frames+" frames; Q="+techniques+", Space="+dodges+", F="+meals+", health="+g.save.health+", energy="+g.save.energy+", prior energy="+energy);
        Console.WriteLine("PASS genuine 300 HP J/Q/Space/F win, ordinary damage/energy/supplies/time, capped battle record and no duplicate progress or false loot; NATIVE_UNVERIFIED");
    }
    private static void EventRecordsAndPrivateClock(FrontierRuntime app)
    {
        Prepare(app); Start(app); var g=app.Game; Enemy boss=g.enemies.Find(GameCore.IsSignalWarden); float world=g.save.totalTime;
        Check(typeof(GameCore).GetField("signalRepriseClock",BindingFlags.Instance|BindingFlags.NonPublic)!=null,"Attempt clock is private simulation state");
        Frames(app,10); Check(Near(Clock(g),g.save.totalTime-world),"Private clock follows actual accepted world ticks");
        foreach(string pause in new[]{"menu","application","review","dialog"})
        {
            if(pause=="menu")Key(app,KeyCode.Escape); else if(pause=="application")Call(app,"OnApplicationPause",true); else if(pause=="review")app.VisualReviewPaused=true; else InteractLamp(app);
            float t=Clock(g),w=g.save.totalTime; Frames(app,12); Draw(app,"paused-"+pause);
            Check(Near(Clock(g),t)&&Near(g.save.totalTime,w),"Actual "+pause+" pauses both attempt and world clock");
            if(pause=="menu")Key(app,KeyCode.Escape); else if(pause=="application")Call(app,"OnApplicationPause",false); else if(pause=="review")app.VisualReviewPaused=false; else Choose(app,"close");
        }
        Contact(app,boss,10,false); Check(Near(Property<float>(g,"SignalRepriseDamage"),0)&&Property<int>(g,"SignalRepriseContacts")==0,"Companion-owned actual projectile damage never counts as own damage/contact");
        float hp=boss.hp; Contact(app,boss,140,true);
        Check(Near(Property<float>(g,"SignalRepriseDamage"),hp-boss.hp)&&Property<int>(g,"SignalRepriseContacts")==1&&Property<int>(g,"SignalRepriseBreaks")==1,"Actual player projectile contact records actual HP loss and its real stagger break");
        Incoming(g); Update(app); Check(Property<int>(g,"SignalRepriseHitsTaken")==1,"Actual incoming collision increments own hits taken");
        Contact(app,boss,1000,true); object first=Last(g);
        Check(Field<bool>(first,"won")&&Near(Field<float>(first,"damage"),290)&&Field<int>(first,"contacts")==2&&Field<int>(first,"breaks")==1&&Field<int>(first,"hitsTaken")==1,"Overkill is capped to remaining HP; companion damage is excluded in immutable result");
        string before=JsonUtility.ToJson(g.save)+"/"+Clock(g)+"/"+g.enemies.Count+"/"+g.effects.Count;
        Draw(app,"event-result"); Draw(app);
        Check(before==JsonUtility.ToJson(g.save)+"/"+Clock(g)+"/"+g.enemies.Count+"/"+g.effects.Count,"OnGUI is read-only for simulation, counters and save");
        Frames(app,20); Start(app); Check(ReferenceEquals(first,Last(g))&&Near(Property<float>(g,"SignalRepriseDamage"),0)&&Property<int>(g,"SignalRepriseContacts")==0,"Retry resets current counters and keeps the last actual result");
        Stop(app); object second=Last(g);
        Check(!Field<bool>(second,"won")&&ReferenceEquals(first,Previous(g))&&Field<float>(second,"seconds")>=0,"Stopped retry records real elapsed time and keeps exactly previous result");
        Draw(app,"rapid-retry-result"); Check(!Text("loot secured")&&!Text("Loot secured")&&Text("no repeat reward"),
            "A previous reprise's still-visible fallen body keeps its no-reward label after a rapid stopped retry");
        // A first story victory can also leave a body while menus pause its
        // short fade. Its genuine awarded loot must not be relabeled by retry.
        Prepare(app); g=app.Game; g.save.flags.Remove("signal_complete"); InteractLamp(app); Choose(app,"signal_start");
        boss=g.enemies.Find(GameCore.IsSignalWarden); Contact(app,boss,1000,true);
        Check(g.SignalComplete&&Last(g)==null&&g.fallen.Exists(f=>ReferenceEquals(f.actor,boss)),"Original first victory leaves its actual rewarded body");
        Frames(app,20); Start(app); Stop(app); Draw(app,"original-loot-after-retry");
        Check(Text("loot secured")&&!Text("Reprise complete"),"A rapid reprise stop cannot relabel the original first-win loot body as a no-reward victory");
        Console.WriteLine("PASS real own/companion projectile events, HP cap, breaks/hits, immutable current/previous records, private world clock and four pause paths/read-only OnGUI; NATIVE_UNVERIFIED");
    }
    private static void OwnershipCleanupAndReset(FrontierRuntime app)
    {
        Prepare(app,2); var g=app.Game; g.save.flags.Add("technique_2"); g.save.technique=1;
        Key(app,KeyCode.Q); Frames(app,35); Check(Get<object>(g,"frost")!=null,"Real alternate Arcanist Q leaves a persistent pre-attempt frost field");
        var ordinary=new Enemy(81111,0,30,16,43); g.enemies.Add(ordinary);
        var friendly=new Projectile{position=new Point(30,16),life=100,friendly=true,damage=2}; var hostile=new Projectile{position=new Point(29,16),life=100,damage=2,ownerId=81112};
        g.projectiles.Add(friendly); g.projectiles.Add(hostile); Start(app);
        Check(Get<object>(g,"frost")==null&&!g.projectiles.Contains(friendly)&&g.projectiles.Contains(hostile)&&g.enemies.Contains(ordinary),"Start retires old frost/friendly arrows while preserving ordinary actors and unrelated hostile shots");
        var boss=g.enemies.Find(GameCore.IsSignalWarden); var ownShot=new Projectile{position=new Point(28,16),life=100,damage=3,ownerId=GameCore.SignalWardenId};
        g.projectiles.Add(ownShot); g.projectiles.Add(friendly); Stop(app);
        Check(!g.enemies.Contains(boss)&&g.enemies.Contains(ordinary)&&!g.projectiles.Contains(ownShot)&&!g.projectiles.Contains(friendly)&&g.projectiles.Contains(hostile),"Stop removes exactly its boss/owned attacks and old friendly actions, preserving world actors/hostiles");
        Start(app); Check(Get<object>(g,"frost")==null&&g.enemies.Find(GameCore.IsSignalWarden).hp==300,"Retry starts clean without old frost damage or inherited boss HP"); Stop(app);
        foreach(string rejection in new[]{"remote","wrongroom","menu","pending","dodge","hit","boss","notcomplete","dead","training"})
        {
            Prepare(app); g=app.Game;
            if(rejection=="remote")Position(app,new Point(g.Map.spawnX,g.Map.spawnY));
            else if(rejection=="wrongroom")g.Enter(0);
            else if(rejection=="menu")Key(app,KeyCode.Escape);
            else if(rejection=="pending")Key(app,KeyCode.Q);
            else if(rejection=="dodge")Key(app,KeyCode.Space);
            else if(rejection=="hit"){Incoming(g);Update(app);}
            else if(rejection=="boss")g.enemies.Add(new Enemy(81113,2,30,16,20,true));
            else if(rejection=="notcomplete")g.save.flags.Remove("signal_complete");
            else if(rejection=="dead"){Incoming(g,10000);Update(app);}
            else {g.Enter(0);g.StartPractice();}
            int count=g.enemies.Count; Call(g,"ChooseSignal","signal_reprise_start");
            Check(!Active(g)&&g.enemies.Count==count,"Stale/direct start callback rejects "+rejection+" physical state");
        }
        foreach(string reset in new[]{"travel","death","training"})
        {
            Prepare(app); Start(app); g=app.Game;
            if(reset!="death")
            {
                Check(!g.SafeTravel,"Original nearby-enemy fast-travel restriction still applies during reprise");
                Position(app,new Point(30,16)); Check(g.SafeTravel,"Staged valid remote footing leaves combat before real map travel");
            }
            if(reset=="travel")Travel(app,0);
            else if(reset=="death"){Incoming(g,10000);Update(app);Check(g.Dying,"Production damage enters real death");Frames(app,60);}
            else {Travel(app,0);g.StartPractice();Check(g.practicing,"Mature training starts normally");g.EndPractice();}
            Check(!Active(g)&&Last(g)==null&&Previous(g)==null&&g.SignalComplete&&g.Has("watch_complete"),reset+" clears session results while preserving old completion");
        }
        Console.WriteLine("PASS real frost/old-arrow cleanup, own actor retirement with world actor/hostile preservation, ten rejected callback states and travel/death/training resets; NATIVE_UNVERIFIED");
    }
    private static void SaveStoreAndLegacyLoads(FrontierRuntime app)
    {
        Prepare(app); Start(app); var g=app.Game; Enemy boss=g.enemies.Find(GameCore.IsSignalWarden); Contact(app,boss,40,true); string progress=Progress(g); g.PrepareSave();
        Check(g.save.snapshotRoom==1&&Array.Exists(g.save.worldEnemies,e=>e.id==GameCore.SignalWardenId),"Production snapshot records a genuine in-progress reprise actor");
        string json=JsonUtility.ToJson(g.save); Check(!json.ToLowerInvariant().Contains("reprise"),"Transient attempt/results add no fields to v1/v2 SaveData");
        Check(SaveStore.Write(g.save,2),"Production SaveStore writes reprise world snapshot"); string path=SaveStore.PathFor(2),envelope=File.ReadAllText(path),warning;
        var decoded=JsonUtility.FromJson<SaveEnvelope>(envelope); Check(decoded.payload!=null&&decoded.checksum.Length==64,"Actual envelope has payload and SHA256 checksum");
        SaveData loaded=SaveStore.Read(2,out warning); Check(loaded!=null&&warning==null,"Production checksum envelope decodes successfully");
        g.Load(loaded); g.Start(); Check(!Active(g)&&!g.SignalActive&&Last(g)==null&&g.SignalComplete&&Progress(g)==progress,"Envelope reload aborts transient reprise and keeps mature completion/rewards");
        decoded.checksum="BAD"; File.WriteAllText(path,JsonUtility.ToJson(decoded)); foreach(string suffix in new[]{".bak",".tmp"})if(File.Exists(path+suffix))File.Delete(path+suffix);
        Check(SaveStore.Read(2,out warning)==null&&warning!=null&&warning.Contains("checksum"),"Tampered actual envelope is rejected by checksum");
        foreach(int version in new[]{1,2})
        {
            SaveData raw=JsonUtility.FromJson<SaveData>(json); raw.version=version; File.WriteAllText(path,JsonUtility.ToJson(raw));
            loaded=SaveStore.Read(2,out warning); Check(loaded!=null&&warning==null,"Unwrapped legacy v"+version+" JSON still decodes"); g.Load(loaded);g.Start();
            Check(!Active(g)&&!g.SignalActive&&Last(g)==null&&Previous(g)==null&&g.SignalComplete&&g.Has("watch_complete"),"Raw v"+version+" load drops reprise actor/records and retains original completion");
        }
        Console.WriteLine("PASS real snapshot/SaveStore SHA256 envelope, tamper rejection, unchanged schema, envelope/raw v1/v2 abort with old completion retained; NATIVE_UNVERIFIED");
    }
    private static void AssertHudPair(FrontierRuntime app,string capture,bool combat)
    {
        var g=app.Game; Check(Property<bool>(app,"CombatHud")==combat,"Fixture reaches actual "+(combat?"combat":"exploration")+" HUD");
        Rect reprise=(Rect)Call(app,"SignalRepriseHudBounds"),companion=(Rect)Call(app,"CompanionFeedbackBounds");
        Check(!Overlap(reprise,companion),"Reprise statistics and actual companion preparation/receipt cannot overlap in "+(combat?"combat":"exploration"));
        Check(Property<bool>(app,"CompanionFeedbackVisible")&&Property<bool>(app,"SignalRepriseHudVisible"),"Both actual feedback systems are simultaneously visible");
        string before=JsonUtility.ToJson(g.save)+"/"+Clock(g)+"/"+g.companionCooldown; Draw(app,capture);
        Check(GUI.textureCalls.Exists(t=>Same(t.rect,reprise))&&GUI.textureCalls.Exists(t=>Same(t.rect,companion)),"OnGUI draws both actual separate status panels");
        Check(!GUI.buttonCalls.Exists(b=>Overlap(b.rect,reprise)),"Attempt statistics add no clickable controls");
        var pointers=(List<Rect>)Call(app,"HudPointerRegions"); Point point=new Point(reprise.x+reprise.width/2,reprise.y+10);
        Check(!pointers.Exists(r=>Contains(r,point))&&!((bool)Call(app,"PointerOverInterface",point)),"Read-only reprise panel passes world pointer input through");
        Check(before==JsonUtility.ToJson(g.save)+"/"+Clock(g)+"/"+g.companionCooldown,"Drawing both feedback panels is read-only");
    }
    private static void BilingualHudCompanionAndDanger(FrontierRuntime app)
    {
        for(int language=0;language<2;language++)for(int large=0;large<2;large++)
        {
            Prepare(app,0,language==1,large==1); Start(app); var g=app.Game; string suffix="-"+language+"-"+large;
            g.save.flags.Add("companion_0");g.save.companion=0;g.companionPosition=g.Position+new Point(-1,0);g.companionCooldown=0;
            Key(app,KeyCode.Z); Check(g.companionAction!=null&&!g.companionAction.released,"Actual Z has a companion preparation phase"); AssertHudPair(app,"combat-preparing"+suffix,true);
            Frames(app,11); Check(g.LastCompanionReceipt!=null,"Actual command contact produces a receipt"); AssertHudPair(app,"combat-receipt"+suffix,true);
            Rect rect=(Rect)Call(app,"SignalRepriseHudBounds"); Point center=new Point(rect.x+rect.width/2,rect.y+rect.height/2),world=(Point)Call(app,"WorldAtPointer",center);
            // Staged hazard exercises actual HUD fading and foreground contour path.
            g.hazards.Add(new Hazard {position=world,radius=.65f,timer=2,damage=1}); Draw(app,"danger-over-statistics"+suffix);
            Check((bool)Call(app,"ThreatOver",rect)&&GUI.textureCalls.Exists(t=>Same(t.rect,rect)&&t.color.a<=.121f),"Actual danger fades the reprise panel background");
            GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"DrawHudThreatWitness");Check(GUI.textureCalls.Count>25,"Foreground hazard witness remains above the statistics overlay");
            g.hazards.Clear(); Stop(app); object first=Last(g); Frames(app,100); Set(app,"combatHudTime",0f); g.toastTime=0;
            g.companionCooldown=0;Key(app,KeyCode.Z);AssertHudPair(app,"exploration-preparing"+suffix,false);
            Frames(app,11);AssertHudPair(app,"exploration-receipt-result"+suffix,false);
            Check(Text(language==1?"Reprise ended":"复试结束"),"Localized result remains visible beside receipt");
            // Let command animation finish before retry, then retain both results.
            Frames(app,20);Start(app);Stop(app);Check(ReferenceEquals(first,Previous(g)),"Previous attempt survives the next real stop");
            Draw(app,"previous-result"+suffix);Check(Text(language==1?"last ":"上次"),"Localized previous result is presented");
            Key(app,KeyCode.Escape);Draw(app,"paused-result"+suffix);Check(!Property<bool>(app,"SignalRepriseHudVisible"),"Statistics hide while the real pause menu owns the screen");
        }
        Console.WriteLine("PASS bilingual normal/large low-FX reduced-motion current/previous/paused HUD, actual combat/exploration companion preparation+receipt non-overlap, pointer transparency and faded danger foreground witness; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-reprise-check-"+Guid.NewGuid());Application.persistentDataPath=directory;
        captureFolder=args.Length>0?args[0]:null;if(captureFolder!=null)Directory.CreateDirectory(captureFolder);
        var app=new FrontierRuntime();int failed=0;
        try
        {
            Call(app,"Awake");Check(app.Game!=null,"Actual runtime Awake loads the full shipped campaign");
            foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{CompletedStoryAndPhysicalStart,GenuineFightAndNormalCosts,EventRecordsAndPrivateClock,OwnershipCleanupAndReset,SaveStoreAndLegacyLoads,BilingualHudCompanionAndDanger})
                try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}
            app.EndVisualReview();Call(app,"OnDestroy");
        }
        finally{Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime Signal Reprise: "+(6-failed)+"/6 groups passed; captures="+captures+"; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
