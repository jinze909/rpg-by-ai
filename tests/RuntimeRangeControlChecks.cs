// Actual production Update, OnGUI and SaveStore with offline declarations.
// Unity import, GPU rendering, hardware input and feel are NATIVE_UNVERIFIED.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeRangeControlChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static string captures;
    static int captureCount, padNavigationMoves, keyboardTabMoves;
    static readonly Rect Entry=new Rect(594,590,623,34), Reset=new Rect(279,493,938,44), Resume=new Rect(279,642,460,45), Finish=new Rect(756,642,461,45);
    static Rect Layout(int index) { return new Rect(279+index*318,365,301,46); }
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); }
    static object Call(object obj, string name, params object[] args)
    {
        var method = obj.GetType().GetMethod(name, Private | BindingFlags.Public);
        Check(method != null, "Production method exists: " + name);
        return method.Invoke(obj, args);
    }
    static void Set(FrontierRuntime app, string name, object value) { typeof(FrontierRuntime).GetField(name, Private).SetValue(app, value); }
    static T Get<T>(FrontierRuntime app, string name) { return (T)typeof(FrontierRuntime).GetField(name, Private).GetValue(app); }
    static void Update(FrontierRuntime app) { Call(app, "Update"); Input.keys.Clear(); }
    static void Key(FrontierRuntime app, KeyCode key) { Input.keys.Add(key); Update(app); }
    static void Frames(FrontierRuntime app, int count) { for (int i = 0; i < count; i++) Update(app); }
    static bool Same(Rect a, Rect b) { return a.x == b.x && a.y == b.y && a.width == b.width && a.height == b.height; }
    static bool Overlap(Rect a, Rect b) { return a.x < b.x+b.width && b.x < a.x+a.width && a.y < b.y+b.height && b.y < a.y+a.height; }
    static string Id(FrontierRuntime app, Rect r) { return (string)Call(app, "ControlId", r.x, r.y, r.width, r.height); }
    static bool Has(string text) { return GUI.commands.Exists(c => c.Contains("\"text\":" + GUI.S(text))); }
    static Rect TextRect(string text)
    {
        foreach (string command in GUI.commands) if (command.Contains("\"text\":" + GUI.S(text)))
        {
            int begin = command.IndexOf("\"r\":[", StringComparison.Ordinal) + 5, end = command.IndexOf(']', begin);
            string[] p = command.Substring(begin, end-begin).Split(',');
            return new Rect(float.Parse(p[0], CultureInfo.InvariantCulture), float.Parse(p[1], CultureInfo.InvariantCulture),
                float.Parse(p[2], CultureInfo.InvariantCulture), float.Parse(p[3], CultureInfo.InvariantCulture));
        }
        throw new Exception("Actual GUI lacks " + text);
    }
    static void Draw(FrontierRuntime app, string capture = null)
    {
        GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); Call(app, "OnGUI");
        if (captures != null && capture != null)
        {
            var commands=GUI.commands.ToArray(); var textures=GUI.textureCalls.ToArray(); var buttons=GUI.buttonCalls.ToArray();
            GUI.Export(captures, "qa-range-controls-" + capture); GUI.commands.AddRange(commands); GUI.textureCalls.AddRange(textures); GUI.buttonCalls.AddRange(buttons); captureCount++;
        }
    }
    static void Click(FrontierRuntime app, Rect r)
    {
        Draw(app); Check(GUI.buttonCalls.Exists(b => Same(b.rect,r) && b.enabled), "Actual enabled range control is drawn at " + r.x + "," + r.y);
        GUI.queuedClick=r; Draw(app); Check(!GUI.queuedClick.HasValue, "Actual displayed button consumes its click"); Draw(app);
    }
    static void Click(FrontierRuntime app, string text) { Draw(app); Click(app,TextRect(text)); }
    static void Focus(FrontierRuntime app, Rect r)
    {
        Draw(app); string id=Id(app,r); var nav=Get<MenuNavigation>(app,"navigation"); int count=0;
        while (nav.focused != id && count++<80) { Key(app,KeyCode.Tab); Draw(app); keyboardTabMoves++; }
        Check(nav.focused==id && nav.CanActivate(id), "Real Tab reaches enabled range control");
    }
    static void PadFocus(FrontierRuntime app, Rect r)
    {
        Draw(app); string target=Id(app,r); var actual=Get<MenuNavigation>(app,"navigation");
        var controls=(List<FocusControl>)typeof(MenuNavigation).GetField("controls",Private).GetValue(actual);
        var planner=new MenuNavigation(); planner.SetControls(controls);
        var queue=new Queue<string>(); var previous=new Dictionary<string,string>(); var steps=new Dictionary<string,Point>();
        string start=actual.focused; queue.Enqueue(start); previous[start]=null;
        Point[] directions={new Point(-1,0),new Point(1,0),new Point(0,-1),new Point(0,1)};
        while(queue.Count>0 && !previous.ContainsKey(target))
        {
            string from=queue.Dequeue();
            foreach(Point direction in directions)
            {
                // The independent planner calls the production geometry rule.
                // It never changes the runtime's focus or substitutes a select.
                planner.focused=from; planner.Move(direction); string next=planner.focused;
                if(previous.ContainsKey(next))continue; previous[next]=from;steps[next]=direction;queue.Enqueue(next);
            }
        }
        Check(previous.ContainsKey(target),"Actual enabled controls have a production-Move stick route to the requested button");
        var path=new List<string>();for(string at=target;at!=start;at=previous[at])path.Add(at);path.Reverse();
        foreach(string at in path)
        {
            Input.axes.Clear();Frames(app,11);Point direction=steps[at];
            Input.axes["Debug Horizontal"]=direction.x;Input.axes["Debug Vertical"]=-direction.y;
            Update(app);Input.axes.Clear();Draw(app);padNavigationMoves++;
            Check(actual.focused==at && Get<bool>(app,"controllerUsed"),"Real stick-axis Update follows each planned focus edge with controller hints");
        }
        Check(actual.focused==target && actual.CanActivate(target),"Pure stick navigation reaches the enabled actual control without Tab or focus injection");
    }
    static void Activate(FrontierRuntime app, Rect r, bool pad=false)
    { if(pad)PadFocus(app,r);else Focus(app,r); Key(app,pad ? KeyCode.JoystickButton0 : KeyCode.Return); Draw(app); }
    static void Prepare(FrontierRuntime app, bool english=true, bool large=false)
    {
        Input.Clear(); GUI.queuedClick=null; GUI.enabled=true; GUI.color=Color.white; GUI.matrix=Matrix4x4.identity;
        Screen.width=1280; Screen.height=800; Time.unscaledDeltaTime=.02f; Event.current.type=EventType.Repaint;
        app.BeginVisualReview(0,0); app.VisualReviewPaused=false; var g=app.Game; g.enemies.Clear(); g.projectiles.Clear();
        foreach (string f in new[]{"core_1","weapon_1","weapon_2","technique_0","range_controls_mature_story"}) if(!g.Has(f))g.save.flags.Add(f);
        g.save.english=english; g.save.largeText=large; g.save.showGuidance=false; g.save.reducedMotion=true; g.save.effectsQuality=0;
        g.save.practiceBits=7; g.save.companion=-1; g.save.weapon=0; g.save.weaponStyles=new int[3]; g.save.technique=0;
        g.save.health=57; g.save.energy=43; g.save.heat=33; g.save.cold=27; g.save.level=5;
        g.save.coins=313; g.save.xp=9; g.save.shards=17; g.save.skillPoints=2; g.save.tea=4; g.save.inventory[6]=3;
        g.save.bounty=0; g.save.bountyProgress=1; g.save.totalTime=100; g.save.foodUntil=180; g.save.teaUntil=165; g.save.mealKind=3; g.save.mealUntil=190;
        Set(app,"applicationPaused",false); Set(app,"pointerSeen",false); Set(app,"controllerUsed",false); Set(app,"resumeTime",0f); Set(app,"arrivalTime",0f); Set(app,"combatHudTime",0f);
        Call(app,"Resize");
    }
    static void Choose(FrontierRuntime app, string action)
    {
        var g=app.Game; Check(g.mode=="dialog" && g.conversation!=null,"Actual choice belongs to dialogue");
        int index=Array.FindIndex(g.conversation.choices,c=>c.action==action); Check(index>=0 && index<9,"Original numbered choice exists: " + action);
        int page=0; Draw(app);
        while (!(bool)Call(app,"DialogueChoicesVisible")) { Key(app,KeyCode.Return); Draw(app); Check(++page<30,"Read actual dialogue pages"); }
        Key(app,(KeyCode)((int)KeyCode.Alpha1+index)); Draw(app);
    }
    static Point Start(FrontierRuntime app, int layout=0)
    {
        var g=app.Game; WorldObject mentor=Array.Find(g.Map.objects,o=>o.kind=="mentor"); Check(mentor!=null,"Original Ada remains");
        Point origin=new Point(mentor.x,mentor.y-.7f); Check(g.CanStand(origin) && app.SetVisualReviewPosition(origin) && (g.Position-origin).Length<.001f,"Fixture uses exact legal mentor feet");
        Set(app,"camera",origin); Check(g.Nearest()==mentor,"Actual E will select Ada"); Key(app,KeyCode.E); Choose(app,"range_menu"); Choose(app,"range_start_"+layout);
        Check(g.RangeActive && g.mode=="world" && g.RangeLayout==layout && !g.RangeStarted,"Original manual dialogue starts chosen unstarted range");
        Set(app,"camera",g.Position); return origin;
    }
    static string Economy(GameCore g)
    {
        var s=g.save; return s.coins+"/"+s.xp+"/"+s.shards+"/"+s.skillPoints+"/"+s.level+"/"+s.deaths+"/"+s.totalTime+"/"+s.foodUntil+"/"+s.teaUntil+"/"+s.mealUntil+"/"+s.mealKind+"/"+s.plantedAt+"/"+
            s.bounty+"/"+s.bountyProgress+"/"+s.bountyCycle+"/"+s.companion+"/"+s.profession+"/"+s.tutorialBits+"/"+s.practiceBits+"/"+
            JsonUtility.ToJson(s.inventory)+"/"+JsonUtility.ToJson(s.mastery)+"/"+JsonUtility.ToJson(s.kills)+"/"+JsonUtility.ToJson(s.flags)+"/"+JsonUtility.ToJson(s.moments);
    }
    static string Stats(GameCore g)
    { return g.RangeLayout+"/"+g.RangeStarted+"/"+g.RangeFinished+"/"+g.RangeTime+"/"+g.RangeDamage+"/"+g.RangeContacts+"/"+g.RangeBreaks+"/"+g.RangeFlanks+"/"+JsonUtility.ToJson(g.enemies); }
    static void Aim(FrontierRuntime app, Point target)
    {
        Set(app,"camera",app.Game.Position); Point point=(Point)Call(app,"ScreenPoint",target); float scale=Get<float>(app,"scale");
        Input.mousePosition=new Vector3(Get<float>(app,"offsetX")+point.x*scale,Screen.height-Get<float>(app,"offsetY")-point.y*scale,0);
    }
    static void StrikeHeavy(FrontierRuntime app)
    {
        var g=app.Game; Check(g.RangeActive && !g.RangeFinished && g.enemies.Count==1,"A real heavy trial has one live target");
        Enemy target=g.enemies[0]; Point foot=new Point(target.x+1.25f,target.y);
        Check(g.CanStand(foot) && app.SetVisualReviewPosition(foot) && (g.Position-foot).Length<.001f,"Stage exact legal feet behind unchanged heavy guard");
        Aim(app,target.Position); Key(app,KeyCode.J); Check(g.playerAction!=null && g.playerAction.kind=="attack","Actual J accepts a normal sword action"); Frames(app,45);
    }
    static void OpenControls(FrontierRuntime app, bool pad=false)
    {
        Check(app.Game.mode=="world" && app.Game.RangeActive,"Controls entry belongs to a live world range");
        Key(app,pad ? KeyCode.JoystickButton7 : KeyCode.Escape); Draw(app);
        Check(app.Game.mode=="pause","Actual Esc/START opens Pause without ending the trial");
        Check(Has(app.Game.L("试装训练场控制","Loadout range controls")) && GUI.buttonCalls.Exists(b=>Same(b.rect,Entry) && b.enabled),
            "Actual paused range exposes its keyboard/controller-accessible Loadout range controls entry");
        Activate(app,Entry,pad); Check(app.Game.mode=="rangecontrols","Actual Tab and Enter/A enter the range-control page"); Draw(app);
    }
    static void CleanAttempt(GameCore g, int layout)
    {
        Check(g.RangeActive && g.mode=="world" && g.RangeLayout==layout && !g.RangeStarted && !g.RangeFinished && g.RangeTime==0 && g.RangeDamage==0 &&
            g.RangeContacts==0 && g.RangeBreaks==0 && g.RangeFlanks==0,"Deliberate original layout/reset transaction starts one clean unstarted attempt");
        Check(g.enemies.Count==(layout==2 ? 1 : 3) && g.enemies.TrueForAll(e=>g.IsRangeEnemy(e) && e.hp==300 && e.maxHp==300),"Original target layout receives exactly its full-health canonical roster");
    }
    static void EquipBow(FrontierRuntime app)
    {
        Key(app,KeyCode.I); Check(app.Game.mode=="inventory","Actual I retains range equipment access");
        Click(app,app.Game.L("猎弓","Bow")); Click(app,app.Game.L("蓄势","Focused")); Click(app,app.Game.L("装备此打法","Equip style"));
        Check(app.Game.save.weapon==1 && app.Game.Style==1,"Original actual inventory equips earned focused bow"); Key(app,KeyCode.Escape);
    }

    // The public input and visible control assertions below intentionally avoid
    // references to newly introduced production types, so old source compiles.
    static void KeyboardAndPadReachOriginalTransactions(FrontierRuntime app)
    {
        Prepare(app); Point origin=Start(app); var g=app.Game; string economy=Economy(g); var record=g.save.sparringReturn;
        OpenControls(app); Check(GUI.buttonCalls.Exists(b=>Same(b.rect,Layout(0)) && !b.enabled),"Current layout is truthfully disabled");
        Focus(app,Layout(1)); Key(app,KeyCode.RightArrow); Draw(app);
        Check(Get<MenuNavigation>(app,"navigation").focused==Id(app,Layout(2)),"Actual right arrow moves focus from cluster to frontal armor");
        Key(app,KeyCode.Return); Draw(app); CleanAttempt(g,2); Check(Economy(g)==economy,"Keyboard layout transaction grants no reward or world time");
        Enemy original=g.enemies[0]; OpenControls(app,true); Activate(app,Reset,true); CleanAttempt(g,2);
        Check(!ReferenceEquals(original,g.enemies[0]),"Actual pad A reset really rebuilds the target rather than dismissing a page");
        OpenControls(app,true); Activate(app,Layout(1),true); CleanAttempt(g,1);
        OpenControls(app,true); Activate(app,Finish,true);
        Check(!g.RangeActive && !g.practicing && g.mode=="world" && (g.Position-origin).Length<.001f && g.save.sparringReturn==null &&
            g.save.health==record.health && g.save.energy==record.energy,"Actual keyboard Finish returns original mentor feet and stored vitals once");
        Check(Economy(g)==economy,"All actual keyboard/pad controls preserve mature economy and teaching/story state");
        Console.WriteLine("PASS keyboard Esc/Tab/arrows/Enter plus pure pad START/stick-axis/A reach layout/reset/finish and pause entry; actual stick moves="+padNavigationMoves+", original return, truthful disabled current layout and no rewards; NATIVE_UNVERIFIED");
    }
    static void BackAndResumeKeepAttemptAndWorldCombat(FrontierRuntime app)
    {
        Prepare(app); Start(app,2); var g=app.Game; StrikeHeavy(app); Check(g.RangeStarted && g.RangeDamage>0 && !g.RangeFinished,"Real contact creates partial live statistics");
        OpenControls(app);
        foreach(bool next in new[]{false,true})
        {
            string beforeSwitch=Stats(g); Key(app,next ? KeyCode.JoystickButton5 : KeyCode.JoystickButton4); Draw(app);
            Check(g.mode==(next ? "loadout" : "help") && g.RangeActive && Stats(g)==beforeSwitch,"Actual LB/RB leaves controls through its promised adjacent menu without altering the attempt");
            DisabledStalePage(app,next ? "RB menu exit" : "LB menu exit");
            Activate(app,Entry,true); Check(g.mode=="rangecontrols" && GUI.buttonCalls.Exists(b=>Same(b.rect,Resume) && b.enabled),
                "A fresh actual Pause entry reacquires valid ownership after LB/RB cleared the old page");
        }
        string stats=Stats(g), economy=Economy(g); Point position=g.Position; float energy=g.save.energy;
        Input.axes["Horizontal"]=1; Frames(app,120); Input.axes.Clear(); Draw(app);
        Check(Stats(g)==stats && Economy(g)==economy && (g.Position-position).Length<.001f && g.save.energy==energy,"Range/world clocks, bodies, damage, movement and energy remain frozen throughout real menu Update");
        Key(app,KeyCode.JoystickButton1); Draw(app); Check(g.mode=="pause" && Stats(g)==stats,"First B returns to Pause with the exact same attempt");
        Frames(app,30); Key(app,KeyCode.JoystickButton1); Draw(app); Check(g.mode=="world" && Stats(g)==stats,"Second B resumes the same world attempt without resetting or stepping time");
        OpenControls(app); stats=Stats(g); Key(app,KeyCode.Escape); Draw(app); Check(g.mode=="pause" && Stats(g)==stats,"First Escape returns only to Pause");
        Key(app,KeyCode.Escape); Draw(app); Check(g.mode=="world" && Stats(g)==stats,"Second Escape resumes unchanged targets and result");
        OpenControls(app); stats=Stats(g); Activate(app,Resume,true); Check(g.mode=="world" && Stats(g)==stats,"Explicit A Resume keeps the same statistics and target state");
        Enemy body=g.enemies[0]; float damage=g.RangeDamage; int layout=g.RangeLayout; Key(app,KeyCode.E); Key(app,KeyCode.Return);
        Check(g.RangeActive && g.mode=="world" && g.RangeLayout==layout && ReferenceEquals(body,g.enemies[0]) && g.RangeDamage==damage,"World E and Enter retain their existing no-reset/no-layout/no-exit semantics");
        Aim(app,body.Position); Key(app,KeyCode.JoystickButton2); Check(g.mode=="world" && g.playerAction!=null && g.playerAction.kind=="attack","World pad X still attacks, rather than selecting a range button"); Frames(app,50);
        Key(app,KeyCode.JoystickButton0); Check(g.mode=="world" && g.dashTime>0,"World pad A still dodges, rather than activating a range button"); Frames(app,50);
        Aim(app,body.Position); Key(app,KeyCode.Q); Check(g.mode=="world" && g.playerAction!=null && g.playerAction.kind=="skill","World Q still creates its class technique");
        Check(g.RangeLayout==layout && ReferenceEquals(body,g.enemies[0]) && Economy(g)==economy,"World combat controls retain the live attempt with no inventory, bounty or story credit");
        Console.WriteLine("PASS pure stick-axis/A Resume, LB/RB menu switching clears stale ownership and Pause reacquires it, partial attempt frozen, B/Escape two-stage return and unchanged world E/Enter/pad X/pad A/Q; NATIVE_UNVERIFIED");
    }
    static void EveryAttemptStateAndSaveReturnStayCanonical(FrontierRuntime app)
    {
        foreach(string state in new[]{"unstarted","partial","finished"})
        {
            Prepare(app); Point origin=Start(app,2); var g=app.Game; var record=g.save.sparringReturn; string economy=Economy(g);
            if(state!="unstarted")StrikeHeavy(app);
            if(state=="partial")Check(g.RangeDamage>0 && !g.RangeFinished,"Partial fixture comes from actual damaging input");
            if(state=="finished")
            {
                for(int attempt=0;attempt<50 && !g.RangeFinished;attempt++)StrikeHeavy(app);
                Check(g.RangeFinished && g.enemies.Count==0 && g.RangeDamage==300,"Actual sword contacts clear the original 300-HP armored trial with no overkill credit");
            }
            OpenControls(app); Draw(app,"state-"+state);
            if(state=="finished"){Activate(app,Layout(1),true);CleanAttempt(g,1);}else{Activate(app,Reset,state=="partial");CleanAttempt(g,2);}
            Check(Economy(g)==economy,"Reset/layout from "+state+" does not reward training or alter mature progress");
            EquipBow(app); OpenControls(app); Activate(app,Finish,state!="unstarted");
            Check(!g.RangeActive && !g.practicing && (g.Position-origin).Length<.001f && g.save.health==record.health && g.save.energy==record.energy &&
                g.save.heat==record.heat && g.save.cold==record.cold && g.save.weapon==1 && g.Style==1,"Finish after "+state+" restores original feet/vitals/climate and keeps explicit equipment choices");
            Check(Economy(g)==economy,"Canonical return from "+state+" changes no rewards, mastery, kills, bounty or story");
        }
        Prepare(app); Point saveOrigin=Start(app,2); var game=app.Game; StrikeHeavy(app); EquipBow(app); OpenControls(app);
        var savedReturn=game.save.sparringReturn; string progress=Economy(game); game.PrepareSave(); Check(SaveStore.Write(game.save,0),"Actual SaveStore writes menu-paused training snapshot");
        string warning; SaveData saved=SaveStore.Read(0,out warning); Check(saved!=null && warning==null && saved.sparringReturn!=null,"Checksummed payload retains original optional training return record");
        game.Load(saved); game.Start();
        Check(!game.RangeActive && !game.practicing && game.save.sparringReturn==null && (game.Position-saveOrigin).Length<.001f && game.save.health==savedReturn.health &&
            game.save.energy==savedReturn.energy && game.save.heat==savedReturn.heat && game.save.cold==savedReturn.cold && game.save.weapon==1 && game.Style==1 && Economy(game)==progress,
            "Loading the actual menu save uses mature recovery, restores original feet/resources, retains changed gear and creates no live range");
        foreach(int version in new[]{1,2})
        {
            SaveData old=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(game.save)); old.version=version; old.sparringReturn=null;
            string json=JsonUtility.ToJson(old).Replace("\"sparringReturn\":null,","").Replace(",\"sparringReturn\":null","");
            Check(!json.Contains("sparringReturn"),"Legacy fixture omits optional return record"); File.WriteAllText(SaveStore.PathFor(1),json);
            saved=SaveStore.Read(1,out warning); Check(saved!=null && warning==null,"Actual SaveStore accepts raw mature v"+version); game.Load(saved); game.Start();
            Check(!game.RangeActive && game.save.weapon==1 && game.Style==1 && game.Has("range_controls_mature_story"),"Legacy save keeps earned gear/story without manufacturing controls or actors");
        }
        Console.WriteLine("PASS real unstarted/partial/finished reset/layout/end, zero training rewards, retained explicit gear, checksum paused-menu return recovery and raw v1/v2 compatibility; NATIVE_UNVERIFIED");
    }
    static void DisabledStalePage(FrontierRuntime app, string cause)
    {
        var g=app.Game; g.mode="rangecontrols"; Draw(app); string before=JsonUtility.ToJson(g.save), stats=Stats(g);
        foreach(Rect r in new[]{Layout(0),Layout(1),Layout(2),Reset,Resume,Finish})
        {
            Check(GUI.buttonCalls.Exists(b=>Same(b.rect,r) && !b.enabled) && !Get<MenuNavigation>(app,"navigation").CanActivate(Id(app,r)),"Stale "+cause+" disables actual transaction and focus at "+r.x+","+r.y);
            GUI.queuedClick=r; Draw(app); Check(GUI.queuedClick.HasValue,"Disabled stale "+cause+" rejects actual mouse click"); GUI.queuedClick=null;
            Get<MenuNavigation>(app,"navigation").focused=Id(app,r); Key(app,KeyCode.Return); Draw(app);
            Get<MenuNavigation>(app,"navigation").focused=Id(app,r); Key(app,KeyCode.JoystickButton0); Draw(app);
        }
        Check(JsonUtility.ToJson(g.save)==before && Stats(g)==stats && g.mode=="rangecontrols","Stale "+cause+" mouse/forged-focus Enter/A cannot execute a return, reset, reward or stale target transaction");
        Key(app,KeyCode.Escape); Draw(app); Check(g.mode=="pause","Stale "+cause+" retains a real Escape route back to Pause");
    }
    static void BilingualLayoutAndStaleOwnershipAreHonest(FrontierRuntime app)
    {
        foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})foreach(int width in new[]{960,1280})
        {
            Prepare(app,english,large); Start(app); Screen.width=width; Screen.height=width*5/8; Call(app,"Resize");
            string suffix=(english?"en":"zh")+(large?"-large":"-normal")+"-"+width; Draw(app,"world-"+suffix);
            Rect worldEntry=new Rect(344,250,592,28); Check(GUI.buttonCalls.Exists(b=>Same(b.rect,worldEntry) && b.enabled) && Has(app.Game.L("训练场控制 · ESC","Range controls · ESC")),"Actual bilingual world HUD advertises explicit training controls");
            Check((bool)Call(app,"PointerOverInterface",new Point(worldEntry.x+10,worldEntry.y+10)),"New actual HUD entry protects mouse attacks from clicking through");
            Key(app,KeyCode.Escape); Draw(app,"pause-"+suffix); Check(Has(app.Game.L("试装训练场控制","Loadout range controls")),"Actual bilingual Pause renders its range-only entry"); Activate(app,Entry); Draw(app,"page-"+suffix);
            Rect[] rectangles={Layout(0),Layout(1),Layout(2),Reset,Resume,Finish};
            for(int i=0;i<rectangles.Length;i++)
            {
                Rect r=rectangles[i]; var button=GUI.buttonCalls.Find(b=>Same(b.rect,r)); Check(button!=null,"Every actual range-control button remains present");
                Check(button.enabled==(i!=0),"Current layout is the only unavailable live control");
                float scale=Get<float>(app,"scale"), x=Get<float>(app,"offsetX")+r.x*scale,y=Get<float>(app,"offsetY")+r.y*scale;
                Check(x>=0 && y>=0 && x+r.width*scale<=Screen.width+.1f && y+r.height*scale<=Screen.height+.1f,"Actual virtual control scales fully inside "+width+" display");
                for(int j=0;j<i;j++)Check(!Overlap(r,rectangles[j]),"Actual training controls never overlap across bilingual supporting text sizes");
            }
            string save=JsonUtility.ToJson(app.Game.save); Draw(app); Draw(app); Check(JsonUtility.ToJson(app.Game.save)==save,"Repeated actual bilingual rendering is read-only");
        }
        foreach(string context in new[]{"ordinary","basic","sparring"})
        {
            Prepare(app); var g=app.Game;
            if(context=="basic")g.StartPractice();
            else if(context=="sparring")
            {
                WorldObject mentor=Array.Find(g.Map.objects,o=>o.kind=="mentor"); Check(app.SetVisualReviewPosition(new Point(mentor.x,mentor.y-.7f)),"Sparring fixture uses original mentor");
                Key(app,KeyCode.E);Choose(app,"sparring_menu");Choose(app,"sparring_start");
            }
            Key(app,KeyCode.Escape); Draw(app); Check(g.mode=="pause" && !Has("Loadout range controls") && !GUI.buttonCalls.Exists(b=>Same(b.rect,Entry)),"Non-range "+context+" Pause has no misplaced range entry");
            DisabledStalePage(app,"non-range "+context);
        }
        foreach(string cause in new[]{"load","travel","new game","return replacement","new attempt","new review world"})
        {
            Prepare(app);Start(app);OpenControls(app);var g=app.Game;
            if(cause=="load") { SaveData copy=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(g.save));g.Load(copy);g.Start(); }
            else if(cause=="travel")g.Enter(1);
            else if(cause=="new game")g.NewGame(1);
            else if(cause=="return replacement")
            {
                var r=g.save.sparringReturn;g.save.sparringReturn=new SparringReturnRecord{x=r.x,y=r.y,health=r.health,energy=r.energy,heat=r.heat,cold=r.cold};
            }
            else if(cause=="new attempt") { g.Close();Check(g.ResetRange(),"Fixture deliberately begins another canonical range attempt"); }
            else { app.BeginVisualReview(0,0);app.VisualReviewPaused=false; }
            DisabledStalePage(app,cause);
        }
        Console.WriteLine("PASS actual bilingual normal/large 960/1280 HUD/Pause/page controls and captures="+captureCount+", scaled nonoverlapping controls, non-range exclusion and load/travel/new-game/return/attempt/world ownership rejection; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath, directory=Path.Combine(Path.GetTempPath(),"starfall-range-controls-"+Guid.NewGuid());
        Application.persistentDataPath=directory; captures=args.Length>0 && args[0]!="-" ? args[0] : null; if(captures!=null)Directory.CreateDirectory(captures);
        var app=new FrontierRuntime(); int failed=0; Action<FrontierRuntime>[] tests={KeyboardAndPadReachOriginalTransactions,BackAndResumeKeepAttemptAndWorldCombat,EveryAttemptStateAndSaveReturnStayCanonical,BilingualLayoutAndStaleOwnershipAreHonest};
        try
        {
            Call(app,"Awake"); Check(app.Game!=null,"Actual Awake loads complete campaign");
            foreach(var test in tests)try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}
            app.EndVisualReview(); Call(app,"OnDestroy");
        }
        finally { Application.persistentDataPath=previous; Input.Clear(); GUI.queuedClick=null; if(Directory.Exists(directory))Directory.Delete(directory,true); }
        Console.WriteLine("Runtime range controls: "+(tests.Length-failed)+"/"+tests.Length+" groups passed; captures="+captureCount+", keyboard Tab edges="+keyboardTabMoves+", actual pad stick edges="+padNavigationMoves+"; NATIVE_UNVERIFIED"); return failed==0?0:1;
    }
}
