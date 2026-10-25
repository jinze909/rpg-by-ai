// Production Update / OnGUI / SaveStore source-trail contracts through offline
// Unity declarations. Native import, GPU/fonts, hardware and play acceptance
// remain NATIVE_UNVERIFIED. Progress/room staging below is labelled explicitly.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeRelicTrailChecks
{
    const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static string captureFolder, capturePrefix;
    static int captures;
    [Serializable] public sealed class Label { public string k, text; public float[] r, m; public int size; public int[] padding; }
    static void Check(bool value, string why) { if (!value) throw new Exception(why); }
    static object Call(object obj, string name, params object[] args)
    { var method = obj.GetType().GetMethod(name, Instance); Check(method != null, "Production method exists: " + name); return method.Invoke(obj, args); }
    static T Get<T>(object obj, string field) { var info = obj.GetType().GetField(field, Instance); Check(info != null, "Production field exists: " + field); return (T)info.GetValue(obj); }
    static void Set(object obj, string field, object value) { var info = obj.GetType().GetField(field, Instance); Check(info != null, "Production field exists: " + field); info.SetValue(obj, value); }
    static bool Near(float a, float b) { return Math.Abs(a-b) < .002f; }
    static bool Same(Rect a, Rect b) { return Near(a.x,b.x) && Near(a.y,b.y) && Near(a.width,b.width) && Near(a.height,b.height); }
    static Rect Bounds(Label label) { return new Rect(label.r[0],label.r[1],label.r[2],label.r[3]); }
    static Rect Source(int relic) { return new Rect(279+relic%3*318+216,244+relic/3*174+22,69,26); }
    static Rect Equip(int relic) { return new Rect(279+relic%3*318+17,244+relic/3*174+120,268,27); }
    static string L(FrontierRuntime app, string zh, string en) { return app.Game.L(zh,en); }
    static void Update(FrontierRuntime app) { Call(app,"Update"); Input.keys.Clear(); }
    static void Key(FrontierRuntime app, KeyCode key) { Input.keys.Add(key); Update(app); }
    static void Draw(FrontierRuntime app, string capture = null)
    {
        GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); Call(app,"OnGUI");
        if(captureFolder != null && capture != null)
        {
            var commands = GUI.commands.ToArray(); var textures = GUI.textureCalls.ToArray(); var buttons = GUI.buttonCalls.ToArray();
            GUI.Export(captureFolder,capturePrefix+"-"+capture); captures++;
            GUI.commands.AddRange(commands); GUI.textureCalls.AddRange(textures); GUI.buttonCalls.AddRange(buttons);
        }
    }
    static List<Label> Labels()
    {
        var result = new List<Label>(); foreach(string command in GUI.commands)
            if(command.StartsWith("{\"k\":\"text\"",StringComparison.Ordinal)) result.Add(JsonUtility.FromJson<Label>(command));
        return result;
    }
    static GUI.ButtonCall Control(FrontierRuntime app, Rect rect, bool enabled = true)
    {
        Draw(app); var found = GUI.buttonCalls.Find(button=>Same(button.rect,rect));
        Check(found != null,"Actual OnGUI exposes a relic source/action control at "+rect.x+","+rect.y);
        Check(found.enabled == enabled,"Actual source/action control has truthful enabled state at "+rect.x+","+rect.y);
        return found;
    }
    static Rect Button(FrontierRuntime app, string text, bool enabled = true)
    {
        Draw(app); foreach(Label label in Labels()) if(label.text == text)
        {
            Rect rect = Bounds(label); var found = GUI.buttonCalls.Find(button=>Same(button.rect,rect));
            if(found != null) { Check(found.enabled == enabled,"Actual named control has truthful availability: "+text); return rect; }
        }
        throw new Exception("Actual OnGUI is missing the "+text+" button");
    }
    static void Click(FrontierRuntime app, Rect rect)
    { GUI.queuedClick = rect; Draw(app); Check(!GUI.queuedClick.HasValue,"Actual enabled control consumes its click"); }
    static void Click(FrontierRuntime app, string text) { Click(app,Button(app,text)); }
    static void Activate(FrontierRuntime app, Rect rect, bool pad = false)
    {
        Control(app,rect); var nav=Get<MenuNavigation>(app,"navigation"); string id=(string)Call(app,"ControlId",rect.x,rect.y,rect.width,rect.height);
        int limit=0; while(nav.focused!=id && limit++<80) { Key(app,KeyCode.Tab); Draw(app); }
        Check(nav.focused==id && nav.CanActivate(id),"Actual Tab reaches the intended enabled source/action control");
        Key(app,pad?KeyCode.JoystickButton0:KeyCode.Return); Draw(app);
    }
    static void Open(FrontierRuntime app)
    { if(app.Game.mode!="world")app.Game.Close(); Key(app,KeyCode.G); Check(app.Game.mode=="loadout","Actual G opens the mature relic loadout"); Draw(app); }
    static void OpenSource(FrontierRuntime app,int id,bool keyboard=false,bool pad=false)
    {
        if(app.Game.mode!="loadout")Open(app); Control(app,Source(id));
        Check(Labels().Exists(label=>Same(Bounds(label),Source(id)) && label.text==L(app,"来源","Source")),"Each relic has a labelled enabled Source control, including locked relics");
        if(keyboard)Activate(app,Source(id),pad); else Click(app,Source(id));
        Check(app.Game.mode=="relictrail","Source opens its read-only relic trail mode rather than equipping"); Draw(app);
        Check(Labels().Exists(label=>label.text.Contains(L(app,GameCore.RelicNames[id],GameCore.RelicNamesEn[id]))),"The actual trail page identifies the selected relic");
    }
    // Reflection preserves compile success on immutable pre-feature production.
    static object Trail(GameCore game,int id) { return Call(game,"GetRelicTrail",id); }
    static void Prepare(FrontierRuntime app,bool english=true,bool large=false)
    {
        Input.Clear(); GUI.queuedClick=null; GUI.enabled=true; GUI.color=Color.white; GUI.matrix=Matrix4x4.identity;
        Event.current.type=EventType.Repaint; Screen.width=1280; Screen.height=800; Time.unscaledDeltaTime=.02f;
        app.BeginVisualReview(1,0); app.VisualReviewPaused=false; var game=app.Game;
        // Progress staging: fresh canonical save, real town geometry and entry.
        game.NewGame(1); game.Close(); game.SkipTutorial(); game.enemies.Clear(); game.projectiles.Clear(); game.hazards.Clear();
        game.save.companion=-1; game.save.english=english; game.save.largeText=large; game.save.showGuidance=false;
        game.save.health=47; game.save.energy=63; game.save.coins=313; game.save.shards=17; game.save.xp=9;
        game.save.reducedMotion=true; game.save.effectsQuality=0; game.toastTime=0;
        Set(app,"relicSlot",1); Set(app,"applicationPaused",false); Set(app,"pointerSeen",false); Set(app,"controllerUsed",false);
        Set(app,"combatHudTime",0f); Set(app,"resumeTime",0f); Set(app,"saveTimer",-1000f); Set(app,"lastRevision",game.revision); Call(app,"Resize");
    }
    static string Files()
    {
        if(!Directory.Exists(Application.persistentDataPath))return "";
        string[] paths=Directory.GetFiles(Application.persistentDataPath,"*",SearchOption.AllDirectories); Array.Sort(paths,StringComparer.Ordinal);
        string result=""; foreach(string path in paths)result+=path+":"+Convert.ToBase64String(File.ReadAllBytes(path))+"\n"; return result;
    }
    sealed class Snapshot
    {
        readonly string save,files,actionJson,projectiles; readonly int revision; readonly ActorAction action; readonly ImpactProfile impact;
        readonly float attack,dash,skill,companion,combo,guard,riposte;
        public Snapshot(FrontierRuntime app)
        {
            var game=app.Game; save=JsonUtility.ToJson(game.save); files=Files(); revision=game.revision;
            action=game.playerAction; impact=action==null?null:action.impact; actionJson=JsonUtility.ToJson(action); projectiles=JsonUtility.ToJson(game.projectiles);
            attack=game.attackCooldown; dash=game.dashCooldown; skill=game.skillCooldown; companion=game.companionCooldown; combo=game.comboTimer; guard=game.guardTime; riposte=game.riposteTime;
        }
        public void Assert(FrontierRuntime app,string why)
        {
            var game=app.Game;
            Check(JsonUtility.ToJson(game.save)==save && game.revision==revision && Files()==files,why+": exact save/economy/resources/health/story/seed/revision and real save bytes remain unchanged");
            AssertCombat(app,why);
        }
        public void AssertCombat(FrontierRuntime app,string why)
        {
            var game=app.Game;
            Check(game.attackCooldown==attack && game.dashCooldown==dash && game.skillCooldown==skill && game.companionCooldown==companion && game.comboTimer==combo && game.guardTime==guard && game.riposteTime==riposte,why+": simulation cooldowns do not advance");
            Check(ReferenceEquals(game.playerAction,action) && JsonUtility.ToJson(action)==actionJson && (action==null||ReferenceEquals(action.impact,impact)) && JsonUtility.ToJson(game.projectiles)==projectiles,why+": prepared/released actions and captured impact remain intact");
        }
    }

    static void RealControlsAndExistingEquip(FrontierRuntime app)
    {
        Prepare(app); var game=app.Game; Open(app);
        // First assertion is semantic RED on the immutable baseline, before any
        // reflection query asks for new production types or methods.
        for(int id=0;id<6;id++)Control(app,Source(id));
        for(int id=0;id<6;id++)
        {
            Control(app,Equip(id),false); var state=new Snapshot(app);
            OpenSource(app,id,id%3!=0,id%3==2); state.Assert(app,"Opening locked source "+id);
            Key(app,id%2==0?KeyCode.Escape:KeyCode.JoystickButton1); Check(game.mode=="loadout" && Get<int>(app,"relicSlot")==1,"Escape/controller B returns to loadout while preserving selected relic slot");
            state.Assert(app,"Back from source "+id);
        }
        OpenSource(app,0); Key(app,KeyCode.JoystickButton4); Check(game.mode=="inventory","Source LB treats its sidebar position as Relics and goes to Inventory");
        OpenSource(app,1); Key(app,KeyCode.JoystickButton5); Check(game.mode=="talents","Source RB treats its sidebar position as Relics and goes to Talents");
        foreach(int id in new[]{0,3,5})game.save.flags.Add("relic_"+id); Open(app);
        for(int id=0;id<6;id++)Control(app,Equip(id),game.Has("relic_"+id));
        foreach(int id in new[]{0,3,5})
        {
            var state=new Snapshot(app); OpenSource(app,id,true,true); state.Assert(app,"Viewing owned source "+id); Key(app,KeyCode.Escape);
            Click(app,Equip(id)); Check(game.save.relics[1]==id && game.save.relics[0]==-1,"Original unchanged Equip rectangle equips only the selected owned relic in its selected slot");
        }
        Console.WriteLine("PASS six locked/owned actual Source controls, mouse/Tab-Enter/controller A entry, Escape/controller B and LB/RB parent flows, original Equip geometry/availability/slot behavior; NATIVE_UNVERIFIED");
    }

    static readonly Rect Primary = new Rect(279,655,454,38), Return = new Rect(750,655,467,38);
    static object RequireTrail(FrontierRuntime app,int id,string action,string quest="")
    {
        Draw(app); object trail=Trail(app.Game,id); Check(trail!=null && Get<int>(trail,"id")==id,"Query identifies the selected existing relic");
        Check(Get<object>(trail,"action").ToString()==action && Get<string>(trail,"questId")==quest,"Live source exposes the authoritative existing route/action: "+id+" / "+action+" / "+quest);
        Check(Labels().Exists(label=>label.text==Get<string>(trail,"source")) && Labels().Exists(label=>label.text==Get<string>(trail,"next")),"Actual source page renders both the full source description and its live next step");
        Check(Labels().Exists(label=>label.text==L(app,Get<bool>(trail,"owned")?"已获得":"尚未获得",Get<bool>(trail,"owned")?"Owned":"Not owned")),"Visible ownership remains separate from source completion");
        Control(app,Primary,action!="None"); Check(Same(Button(app,L(app,"返回遗物栏","Back to relics")),Return),"Explicit source Back uses its agreed parent rectangle");
        return trail;
    }
    static void TrackClick(FrontierRuntime app,int id,string quest,bool controller=false)
    {
        var game=app.Game; RequireTrail(app,id,"Quest",quest); SaveData before=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(game.save)); string files=Files(); int revision=game.revision;
        Check(Same(Button(app,L(app,"追踪当前前置任务","Track current prerequisite")),Primary),"Explicit track is visibly labelled as the current prerequisite");
        if(controller)Activate(app,Primary,true); else Click(app,Primary);
        Check(game.mode=="relictrail" && game.save.trackedQuest==quest && game.revision==revision+1,"Only deliberate Track changes the existing tracked quest while staying on the source page");
        before.trackedQuest=quest; before.frontierFocus=quest=="shore";
        Check(JsonUtility.ToJson(game.save)==JsonUtility.ToJson(before) && Files()==files,"Tracking changes no health/equipment/cost/reward/story/RNG fields or disk bytes");
        RequireTrail(app,id,"Quest",quest);
    }
    static void FreshStagesExplicitActionsAndNoRewards(FrontierRuntime app)
    {
        foreach(int id in new[]{0,2,3,4,5})
        {
            Prepare(app); app.Game.save.trackedQuest="bounty"; OpenSource(app,id); var state=new Snapshot(app);
            RequireTrail(app,id,"Quest","main"); state.Assert(app,"Fresh source display does not silently track"); TrackClick(app,id,"main",id==3);
            app.Game.save.stage=1; RequireTrail(app,id,"Quest","shore"); TrackClick(app,id,"shore");
        }
        Prepare(app); var game=app.Game; OpenSource(app,4); RequireTrail(app,4,"Quest","main");
        game.save.flags.Add("companion_0"); RequireTrail(app,4,"Quest","personal0"); TrackClick(app,4,"personal0",true);
        game.save.flags.Add("personal_0"); game.save.flags.Add("companion_1"); RequireTrail(app,4,"Quest","personal1"); TrackClick(app,4,"personal1");
        Prepare(app); game=app.Game; game.save.inventory=new[]{20,20,20,20,20,2,1,3}; OpenSource(app,1); RequireTrail(app,1,"Projects");
        var projectState=new Snapshot(app); Click(app,L(app,"查看水渠建设计划","Review canal project")); Check(game.mode=="camp","Amber's explicit action opens existing town projects without building"); projectState.Assert(app,"Reviewing Amber's canal source");
        Click(app,new Rect(296,570,268,42)); Check(Get<object>(app,"townProjectConfirmation")!=null,"Existing project review still requires its real cost/benefit confirmation"); projectState.Assert(app,"Original canal confirmation before payment");
        Key(app,KeyCode.Escape); Check(Get<object>(app,"townProjectConfirmation")==null && game.mode=="camp","Original safe Cancel retires the project quote"); projectState.Assert(app,"Cancel from source-opened project review");
        Prepare(app); game=app.Game; game.save.frontierEnding=1; OpenSource(app,5); RequireTrail(app,5,"AbyssMap"); var mapState=new Snapshot(app);
        Click(app,L(app,"查看深渊地图","View abyss map")); Check(game.mode=="map" && Get<int>(app,"selectedRegion")==8 && !game.InAbyss,"Rift's explicit map action selects region eight without starting or teleporting"); mapState.Assert(app,"Viewing abyss destination");
        game.Close(); game.StartAbyss(0); Check(game.InAbyss && game.save.abyssFloor==1,"A real original abyss entry stages an active expedition");
        game.save.runCoins=17; game.save.runShards=8; game.save.runXp=23; Key(app,KeyCode.G); OpenSource(app,5); RequireTrail(app,5,"AbyssRun"); var runState=new Snapshot(app); int generation=game.WorldGeneration;
        Click(app,L(app,"返回当前远征","Return to expedition")); Check(game.mode=="world" && game.InAbyss && game.WorldGeneration==generation,"Rift resumes the exact current run without StartAbyss/Enter/BankFloor"); runState.Assert(app,"Explicit return retains every unbanked reward, floor and prepared actor");
        string[] completed={"harbor_report",null,"desert_report","mountain_report",null,null};
        for(int id=0;id<6;id++) foreach(bool owned in new[]{false,true})
        {
            Prepare(app); game=app.Game;
            if(owned)game.save.flags.Add("relic_"+id);
            else if(completed[id]!=null)game.save.flags.Add(completed[id]);
            else if(id==1)game.save.projects[0]=2;
            else if(id==4)foreach(string flag in new[]{"personal_0","personal_1","personal_2"})game.save.flags.Add(flag);
            else if(id==5)game.save.bestFloor=3;
            OpenSource(app,id); var unchanged=new Snapshot(app); object trail=RequireTrail(app,id,"None");
            Check(Get<bool>(trail,"owned")==owned && (owned || Get<bool>(trail,"sourceComplete")),"Owned/completed states are separately truthful without synthesizing ownership");
            GUI.queuedClick=Primary; Draw(app); Check(GUI.queuedClick.HasValue,"No tracking needed is genuinely disabled"); GUI.queuedClick=null;
            Check(!(bool)Call(game,"TrackRelicTrail",id),"Direct deliberate tracking also refuses an owned/completed source"); unchanged.Assert(app,"No reward replay from complete/owned source");
        }
        Console.WriteLine("PASS fresh main/shore and recruited personal prerequisite tracking only on explicit click, Amber original paid-project confirmation/cancel, Rift map/current-run return, all six owned/complete no-op guards without spending/healing/reward replay; NATIVE_UNVERIFIED");
    }

    static void LegacySavesPreparedActionsAndPauses(FrontierRuntime app)
    {
        foreach(int version in new[]{1,2})
        {
            Prepare(app); var game=app.Game; game.save.version=version; game.save.flags.Add("relic_0"); game.save.relics[0]=0;
            Directory.CreateDirectory(Application.persistentDataPath); File.WriteAllText(SaveStore.PathFor(1),JsonUtility.ToJson(game.save));
            string warning; SaveData old=SaveStore.Read(1,out warning); Check(old!=null && warning==null,"Actual raw legacy SaveStore reads v"+version);
            game.Load(old); game.Start(); OpenSource(app,0); var state=new Snapshot(app);
            for(int count=0;count<6;count++) { Trail(game,0); Trail(game,5); Update(app); Draw(app); state.Assert(app,"Legacy migrated source redraw"); }
            game.PrepareSave(); Check(SaveStore.Write(game.save,0),"Production checksummed writer preserves migrated progress"); SaveData restored=SaveStore.Read(0,out warning);
            Check(restored!=null && warning==null && restored.version==2 && restored.flags.Contains("relic_0") && restored.coins==313 && restored.health==47,"Actual checksum roundtrip retains story/relic/economy/health after source inspection");
            string json=JsonUtility.ToJson(restored); Check(!json.Contains("relicTrail") && !json.Contains("sourceAction") && !json.Contains("trailTitle"),"Read-only trail adds no serialized save fields");
        }
        foreach(bool flying in new[]{false,true})
        {
            Prepare(app); var game=app.Game; game.save.weapon=1; game.save.energy=100;
            Check(app.SetVisualReviewPosition(new Point(13,13)),"Prepared-action fixture uses actual legal town ground"); Set(app,"camera",game.Position); game.aim=new Point(1,0);
            Key(app,KeyCode.J); ActorAction action=game.playerAction; Check(action!=null && !action.released && action.weapon==1,"Actual J prepares an original bow attack");
            if(flying) { int frames=0; while(!action.released && frames++<50)Update(app); Check(action.released && game.projectiles.Count>0,"Actual frames release a genuine in-flight projectile"); }
            Key(app,KeyCode.G); Check(game.mode=="loadout","Actual G pauses world combat at the original relic page");
            Set(app,"reviewOriginal",null); Set(app,"saveTimer",-1000f); Set(app,"lastRevision",game.revision);
            Check(SaveStore.Write(game.save,0),"Purity test protects actual checksum bytes rather than the visual review no-save shortcut");
            var state=new Snapshot(app); OpenSource(app,2); for(int frame=0;frame<12;frame++) { Trail(game,2); Update(app); Draw(app); state.Assert(app,"Source inspection while "+(flying?"projectile flying":"attack preparing")); }
            // The mature application-pause lifecycle deliberately captures and
            // writes an autosave. Protect that completed save during later
            // paused queries/draws instead of mislabelling it a source write.
            SaveData beforePause=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(game.save)); int pauseRevision=game.revision;
            Call(app,"OnApplicationPause",true); state.AssertCombat(app,"Original application pause retains accepted combat");
            SaveData afterPause=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(game.save));
            // PrepareSave's existing world-capture fields and writer timestamp
            // are the only allowed lifecycle differences. Resource/progression
            // fields remain exact, independent of the later snapshot reset.
            afterPause.savedAtUtc=beforePause.savedAtUtc; afterPause.snapshotRoom=beforePause.snapshotRoom;
            afterPause.worldEnemies=beforePause.worldEnemies; afterPause.savedRunes=beforePause.savedRunes;
            Check(JsonUtility.ToJson(afterPause)==JsonUtility.ToJson(beforePause) && game.revision==pauseRevision,"Original application-pause save cannot spend/heal/reward/change story/equipment/RNG or action clocks");
            string lifecycleWarning; SaveData autosave=SaveStore.Read(out lifecycleWarning);
            Check(autosave!=null && lifecycleWarning==null && JsonUtility.ToJson(autosave)==JsonUtility.ToJson(game.save),"Actual application pause writes a readable original checksum autosave with exact current content");
            var paused=new Snapshot(app); float clock=Get<float>(app,"clock"); for(int frame=0;frame<8;frame++) { Update(app); Draw(app); }
            paused.Assert(app,"Paused source inspection after the original lifecycle save"); Check(Get<float>(app,"clock")==clock,"Application pause freezes presentation time"); Call(app,"OnApplicationPause",false);
            Key(app,KeyCode.JoystickButton1); paused.Assert(app,"Controller B returns without changing accepted combat"); Check(game.mode=="loadout","Source Back remains in menu rather than advancing the attack");
            Key(app,KeyCode.Escape); Check(game.mode=="world","Second Back resumes the original world"); float elapsed=action.elapsed; Update(app);
            Check(action.released || action.elapsed>elapsed,"Original action really resumes after leaving the source parent");
        }
        Console.WriteLine("PASS actual v1/v2 raw and checksum SaveStore continuity, no new save fields, repeated pure queries/draws, real prepared/flying bow and captured impact pause, application pause and safe resume; NATIVE_UNVERIFIED");
    }

    static void BilingualLayoutsAndLiveRefresh(FrontierRuntime app)
    {
        foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})foreach(int width in new[]{960,1920})
        {
            Prepare(app,english,large); Screen.width=width; Screen.height=width==960?600:1080; Call(app,"Resize"); Open(app);
            string suffix=(english?"en":"zh")+(large?"-large-":"-normal-")+width; Draw(app,"loadout-"+suffix);
            for(int id=0;id<6;id++)
            {
                OpenSource(app,id); Draw(app,"source-"+id+"-"+suffix);
                Check(GUI.buttonCalls.Count>=12,"Each localized source page retains its sidebar and return actions");
                // World portal labels remain underneath the original dimmed
                // menu. Inspect only coordinates owned by this new page.
                foreach(Label label in Labels()) if((Near(label.r[0],371) || Near(label.r[0],302) || Near(label.r[0],1001) || Near(label.r[0],279) || Near(label.r[0],750) || Near(label.r[0],282)) && label.r[1]>=178 && label.r[1]<730)
                {
                    Rect rect=Bounds(label); Check(rect.x+rect.width<=1217 && rect.y+rect.height<=730,"New source content fits inside the existing window card: "+label.text);
                    Check(rect.height-label.padding[2]-label.padding[3]>=label.size,"New source text retains a complete glyph height: "+label.text);
                    Check(label.m[2]>=0 && label.m[5]>=0,"960/1920 viewport retains positive canvas translation");
                }
                Key(app,KeyCode.Escape);
            }
        }
        Check(captureFolder==null || captures==56,"This execution produced all 48 source pages plus eight loadout command-stream captures");
        Prepare(app); OpenSource(app,1); object original=RequireTrail(app,1,"Projects");
        Check(!Get<bool>(original,"sourceComplete"),"Original Amber source begins incomplete"); app.Game.save.projects[0]=2;
        object fresh=RequireTrail(app,1,"None"); Check(Get<bool>(fresh,"sourceComplete") && !Get<bool>(fresh,"owned") && !Get<bool>(original,"sourceComplete"),"Real repeated OnGUI refreshes existing progress; an earlier read-only snapshot cannot change or grant ownership");
        app.Game.save.flags.Add("relic_1"); RequireTrail(app,1,"None"); Check(Labels().Exists(label=>label.text==L(app,"已获得","Owned")),"Ownership label updates without reopening after a real saved flag change");
        Prepare(app); OpenSource(app,0); RequireTrail(app,0,"Quest","main"); app.Game.save.flags.Add("harbor_report");
        RequireTrail(app,0,"None"); Check(Labels().Exists(label=>label.text.Contains(L(app,"不会重复发放","Rewards are not replayed"))),"A completed regional report with absent reward flag visibly explains why viewing does not replay it");
        Console.WriteLine("PASS eight bilingual normal/large 960/1920 policies across all six source pages and loadout source controls, inner-card glyph geometry, live project/report/ownership refresh and immutable earlier snapshots; NATIVE_UNVERIFIED");
    }

    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-relic-trail-"+Guid.NewGuid());
        Application.persistentDataPath=directory; captureFolder=args.Length>0 && args[0]!="-"?args[0]:null; capturePrefix=args.Length>1?args[1]:"qa-relic-trail";
        int failed=0; var app=new FrontierRuntime();
        try
        {
            Call(app,"Awake"); Check(app.Game!=null,"Actual Awake loads the complete campaign");
            foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[] { RealControlsAndExistingEquip,FreshStagesExplicitActionsAndNoRewards,LegacySavesPreparedActionsAndPauses,BilingualLayoutsAndLiveRefresh })
                try { test(app); } catch(Exception error) { failed++; Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message); }
            app.EndVisualReview(); Call(app,"OnDestroy");
        }
        finally { Application.persistentDataPath=previous; Input.Clear(); GUI.queuedClick=null; if(Directory.Exists(directory))Directory.Delete(directory,true); }
        Console.WriteLine("Runtime relic trail: "+(4-failed)+"/4 groups passed; NATIVE_UNVERIFIED"); return failed==0?0:1;
    }
}
