// Shipped Update/E/dialogue/OnGUI/SaveStore evidence under offline Unity declarations.
// This does not validate native Editor import, GPU/font rendering, pad hardware or play feel.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeWaterGardenChecks
{
    const BindingFlags All=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static string captures,prefix; static int captureCount;
    static void Check(bool value,string text) { if(!value)throw new Exception(text); }
    static bool Near(float a,float b) { return Math.Abs(a-b)<.006f; }
    static bool Same(Rect a,Rect b) { return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height); }
    static object Call(object obj,string name,params object[] args) { var method=obj.GetType().GetMethod(name,All);Check(method!=null,"Shipped method exists: "+name);return method.Invoke(obj,args); }
    static void Set(object obj,string name,object value) { var field=obj.GetType().GetField(name,All);Check(field!=null,"Fixture field exists: "+name);field.SetValue(obj,value); }
    static T Get<T>(object obj,string name) { return (T)obj.GetType().GetField(name,All).GetValue(obj); }
    static bool State(GameCore g,string name) { var property=g.GetType().GetProperty("WaterGarden"+name,All);Check(property!=null,"Earned town follow-up exposes stage "+name);return (bool)property.GetValue(g,null); }
    static bool Api(GameCore g,string name) { return (bool)Call(g,name); }
    static void Update(FrontierRuntime app) { Call(app,"Update");Input.keys.Clear(); }
    static void Key(FrontierRuntime app,KeyCode key) { Input.keys.Add(key);Update(app); }
    static bool Text(string value) { return GUI.commands.Exists(c=>c.Contains("\"k\":\"text\"")&&c.Contains(value)); }
    static void Draw(FrontierRuntime app,string name=null)
    {
        GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        if(captures!=null&&name!=null)
        { var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();GUI.Export(captures,prefix+"-"+name);GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);captureCount++; }
    }
    static void Pages(FrontierRuntime app,string name=null)
    {
        for(int page=0;page<30;page++)
        {
            Draw(app,name==null?null:name+"-page"+page);
            if((bool)Call(app,"DialogueChoicesVisible"))
            {
                int count=app.Game.conversation.choices.Length;
                for(int i=0;i<count;i++)
                { Rect rect=ChoiceRect(i,count);Check(GUI.buttonCalls.Exists(b=>Same(b.rect,rect))&&rect.y+rect.height<=686,"Every actual numbered choice remains visible inside the dialogue card"); }
                return;
            }
            Key(app,KeyCode.Return);
        }
        Check(false,"Actual narrative pagination finishes before selecting a choice");
    }
    static Rect ChoiceRect(int index,int count)
    { int columns=count>2?2:Math.Max(1,count);float width=(832-(columns-1)*14)/(float)columns;return new Rect(320+index%columns*(width+14),539+index/columns*45,width,37); }
    static void Choose(FrontierRuntime app,string action,int input=1,string capture=null)
    {
        Check(app.Game.mode=="dialog"&&app.Game.conversation!=null,"Choice belongs to actual open dialogue: "+action);
        int index=Array.FindIndex(app.Game.conversation.choices,c=>c.action==action),count=app.Game.conversation.choices.Length;
        Check(index>=0&&index<9,"Actual earned NPC dialogue exposes "+action);Pages(app,capture);Rect rect=ChoiceRect(index,count);
        if(input==0) { Check(GUI.buttonCalls.Exists(b=>Same(b.rect,rect)&&b.enabled),"Actual mouse choice is visible and enabled");GUI.queuedClick=rect;Draw(app);Check(!GUI.queuedClick.HasValue,"Production OnGUI consumes the visible choice click"); }
        else if(input==1)Key(app,(KeyCode)((int)KeyCode.Alpha1+index));
        else
        {
            var nav=Get<MenuNavigation>(app,"navigation");string id=(string)Call(app,"ControlId",rect.x,rect.y,rect.width,rect.height);int keys=0;
            while(nav.focused!=id&&keys++<32){Key(app,KeyCode.Tab);Draw(app);}Check(nav.focused==id&&nav.CanActivate(id),"Real focus navigation reaches the enabled quest choice");Key(app,KeyCode.JoystickButton0);Draw(app);
        }
    }
    static void Click(FrontierRuntime app,Rect rect)
    { Draw(app);Check(GUI.buttonCalls.Exists(b=>Same(b.rect,rect)&&b.enabled),"Actual journal/map control is enabled");GUI.queuedClick=rect;Draw(app);Check(!GUI.queuedClick.HasValue,"Actual journal/map control consumes its click"); }
    static WorldObject Object(GameCore g,string kind)
    { var obj=Array.Find(g.Map.objects,o=>o.kind==kind);Check(obj!=null,"Authored canonical object exists: "+kind);return obj; }
    static void Position(FrontierRuntime app,Point point)
    { Check(app.Game.CanStand(point)&&app.SetVisualReviewPosition(point)&&(app.Game.Position-point).Length<.001f,"Fixture uses exact full-footprint walkable ground at "+point.x+","+point.y);Set(app,"camera",app.Game.Position); }
    static void Beside(FrontierRuntime app,string kind)
    {
        var obj=Object(app.Game,kind);bool found=false;
        foreach(Point offset in new[]{new Point(0,-.7f),new Point(0,.7f),new Point(-.7f,0),new Point(.7f,0),new Point(0,-1.2f),new Point(0,1.2f)})
        { Point p=new Point(obj.x,obj.y)+offset;if(!app.Game.CanStand(p))continue;Position(app,p);if(ReferenceEquals(app.Game.Nearest(),obj)){found=true;break;} }
        Check(found,"Actual nearest E target is canonical "+kind);
    }
    static void Interact(FrontierRuntime app,string kind,bool pad=false)
    { Beside(app,kind);Key(app,pad?KeyCode.JoystickButton1:KeyCode.E);Check(app.Game.mode=="dialog","Real E/B opens the authored "+kind+" conversation"); }
    static void Prefix(GameCore g,params string[] actions)
    { Check(g.conversation!=null&&g.conversation.choices.Length>=actions.Length,"Mature choices remain present");for(int i=0;i<actions.Length;i++)Check(g.conversation.choices[i].action==actions[i],"Mature numbered choice retained: "+actions[i]); }
    static void Prepare(FrontierRuntime app,bool ready=true,bool english=true,bool large=false,int width=1280)
    {
        Input.Clear();GUI.queuedClick=null;GUI.enabled=true;GUI.color=Color.white;GUI.matrix=Matrix4x4.identity;Event.current.type=EventType.Repaint;Screen.width=width;Screen.height=width==960?600:800;Time.unscaledDeltaTime=.02f;
        app.BeginVisualReview(0,0);app.VisualReviewPaused=false;var g=app.Game;g.enemies.Clear();g.projectiles.Clear();g.hazards.Clear();g.effects.Clear();g.fallen.Clear();
        g.save.flags.RemoveAll(f=>f.StartsWith("watergarden_")||f.StartsWith("ally_")||f.StartsWith("supper_"));if(ready){g.save.flags.Add("ally_1");g.save.flags.Add("ally_3");}
        g.save.english=english;g.save.largeText=large;g.save.showGuidance=true;g.save.reducedMotion=true;g.save.effectsQuality=0;g.save.companion=-1;g.save.plantedAt=-1;g.save.projects[0]=2;g.save.frontierEnding=0;g.save.inventory[7]=6;g.save.tea=3;
        Set(app,"applicationPaused",false);Set(app,"controllerUsed",false);Set(app,"pointerSeen",false);Set(app,"resumeTime",0f);Set(app,"arrivalTime",0f);Set(app,"combatHudTime",0f);Set(app,"journalTab",0);Set(app,"selectedQuest",0);Set(app,"selectedRegion",-1);Set(app,"dialogParent",null);Call(app,"Resize");
    }
    static void Accept(FrontierRuntime app,int input=1,string capture=null)
    { Interact(app,"forge");Prefix(app.Game,"upgrade","bow","staff","close","forge_quotes");Choose(app,"watergarden_offer",input);Choose(app,"watergarden_accept",input,capture);Check(State(app.Game,"Accepted")&&app.Game.mode=="world","Actual earned Noll offer accepts and closes to the world"); }
    static void DrawWater(FrontierRuntime app,int input=1,string capture=null)
    { Interact(app,"waterDraw");Choose(app,"watergarden_draw",input,capture);Check(State(app.Game,"Drawn")&&app.Game.mode=="world","Actual independent well action draws the repaired pump's water"); }
    static void Water(FrontierRuntime app,int input=1,string capture=null)
    { Interact(app,"waterBed");Choose(app,"watergarden_water",input,capture);Check(State(app.Game,"Watered")&&app.Game.mode=="world","Actual demonstration bed consumes the ordered watering step"); }
    static void Report(FrontierRuntime app,int input=1,string capture=null)
    { Interact(app,"garden");Prefix(app.Game,"plant","seeds","close");Choose(app,"watergarden_offer",input);Choose(app,"watergarden_finish",input,capture); }
    static void Walk(FrontierRuntime app,string kind)
    {
        var g=app.Game;WorldObject obj=Object(g,kind);List<Point> path=g.InteractionPath(obj);Check(path.Count>0,"Guide destination has an actual interaction path");int frames=0;
        while(!ReferenceEquals(g.Nearest(),obj)&&frames++<5000)
        {
            while(path.Count>0&&(path[0]-g.Position).Length<.14f)path.RemoveAt(0);Check(path.Count>0,"A real walking approach remains until E owns the intended target");Point move=(path[0]-g.Position).Normalized;
            Input.axes["Horizontal"]=move.x;Input.axes["Vertical"]=-move.y;Input.held.Clear();if(move.x>.05f)Input.held.Add(KeyCode.D);if(move.x<-.05f)Input.held.Add(KeyCode.A);if(move.y>.05f)Input.held.Add(KeyCode.S);if(move.y<-.05f)Input.held.Add(KeyCode.W);Update(app);Check(g.CanStand(g.Position),"Actual held walking retains full-footprint collision");
        }
        Input.axes.Clear();Input.held.Clear();Check(frames>0&&frames<5000&&ReferenceEquals(g.Nearest(),obj),"Actual Update/WASD route reaches canonical "+kind);Set(app,"camera",g.Position);
    }
    static void Guide(GameCore g,string kind)
    { Point? target=g.NavigationTarget();WorldObject obj=Object(g,kind);Check(target.HasValue&&(target.Value-new Point(obj.x,obj.y)).Length<.001f,"Tracked quest guide owns next canonical "+kind); }

    static void ActualQuestJournalAndWalking(FrontierRuntime app)
    {
        Prepare(app);var g=app.Game;int[] inventory=(int[])g.save.inventory.Clone();int tea=g.save.tea,coins=g.save.coins;float planted=g.save.plantedAt;Accept(app);
        Key(app,KeyCode.Tab);Draw(app);Check(g.mode=="journal","Actual Tab opens the journal");int index=Array.IndexOf(GameCore.QuestIds,"watergarden");Check(index==21&&GameCore.QuestIds[20]=="riverlesson"&&GameCore.QuestIds[19]=="returnletter","Town follow-up appends after all mature stories");
        Click(app,new Rect(927,699,263,26));float spacing=464f/18;Click(app,new Rect(279,226+spacing*2,321,spacing-7));Draw(app);Check(Get<int>(app,"selectedQuest")==index&&Text(g.GetQuest("watergarden").title),"Actual second-page row selects the new complete guide");
        Click(app,new Rect(650,647,258,31));Check(g.save.trackedQuest=="watergarden","Actual tracking retains appended quest ID");Click(app,new Rect(927,647,263,31));Check(g.mode=="map"&&Get<int>(app,"selectedRegion")==0,"Actual destination map stays in the authored town");Key(app,KeyCode.Escape);if(g.mode!="world")Key(app,KeyCode.Escape);
        Guide(g,"waterDraw");Update(app);Check(Get<List<Point>>(app,"route").Count>0,"Actual world Update creates the destination route");Walk(app,"waterDraw");Key(app,KeyCode.E);Choose(app,"watergarden_draw");Guide(g,"waterBed");Walk(app,"waterBed");Key(app,KeyCode.E);Choose(app,"watergarden_water");Guide(g,"garden");Walk(app,"garden");Key(app,KeyCode.E);Choose(app,"watergarden_offer");Choose(app,"watergarden_finish");
        Check(State(g,"Complete")&&g.GetQuest("watergarden").complete&&g.save.inventory[7]==inventory[7]+3&&g.save.tea==tea+1,"Actual three town approaches finish once for three seeds and one tea");
        for(int i=0;i<inventory.Length;i++)if(i!=7)Check(g.save.inventory[i]==inventory[i],"No duplicate original parts/herbs/cooked reward at index "+i);Check(g.save.coins==coins&&g.save.plantedAt==planted,"The follow-up neither charges coins nor fabricates mature crop growth");
        Interact(app,"garden");Choose(app,"watergarden_offer");Check(!Array.Exists(g.conversation.choices,c=>c.action=="watergarden_finish"),"Completed reread cannot offer another reward");Choose(app,"close");Check(g.save.inventory[7]==inventory[7]+3&&g.save.tea==tea+1,"Completed reread pays no second reward");
        Console.WriteLine("PASS real E/numeric acceptance, second-page journal track/map, held-WASD draw/bed/An routes, exact once-only reward and no duplicate original payment; NATIVE_UNVERIFIED");
    }

    static void EarnedGatesAndMatureTown(FrontierRuntime app)
    {
        foreach(string earned in new[]{"none","ally_1","ally_3"})
        {
            Prepare(app,false);var g=app.Game;if(earned!="none")g.save.flags.Add(earned);Interact(app,"forge");Check(!Array.Exists(g.conversation.choices,c=>c.action=="watergarden_offer"),"Noll never offers the follow-up before both original commissions: "+earned);Choose(app,"close");
            foreach(WorldObject obj in g.Map.objects)if(obj.kind=="waterDraw"||obj.kind=="waterBed")Check(!g.IsObjectPresent(obj),"Locked demonstration points remain absent");
        }
        Prepare(app,false);var game=app.Game;game.save.inventory[2]=10;game.save.inventory[4]=9;int coins=game.save.coins,stew=game.save.inventory[6],skills=game.save.skillPoints;
        Interact(app,"garden");Prefix(game,"ally1","plant","seeds","close");Choose(app,"ally1");Interact(app,"forge");Prefix(game,"upgrade","bow","staff","ally3","close");Choose(app,"ally3");
        Check(game.Has("ally_1")&&game.Has("ally_3")&&game.save.inventory[2]==4&&game.save.inventory[4]==4&&game.save.coins==coins+80&&game.save.inventory[6]==stew+2&&game.save.skillPoints==skills+2,"Both original commissions retain exact costs and all rewards");
        Accept(app);Position(app,new Point(18,11));Check(ReferenceEquals(game.Nearest(),Object(game,"routes")),"New draw point never steals Wayfinder's original E at the well");Position(app,new Point(17,7));Check(ReferenceEquals(game.Nearest(),Object(game,"garden")),"New demonstration bed never steals An's original E");
        foreach(string method in new[]{"AcceptWaterGarden","DrawGardenWater","WaterGardenBed","FinishWaterGarden"})
        { Position(app,new Point(23,15));string save=JsonUtility.ToJson(game.save);Check(!Api(game,method)&&JsonUtility.ToJson(game.save)==save,"Direct remote API cannot advance or pay: "+method); }
        DrawWater(app);Water(app);Report(app);Check(State(game,"Complete"),"Preserved original commissions flow into the earned optional task");
        int seeds=game.save.inventory[7],herbs=game.save.inventory[2],gold=game.save.coins;Interact(app,"garden");Choose(app,"plant");float planted=game.save.plantedAt;Check(planted>=0&&game.save.inventory[7]==seeds-1,"Original An planting still consumes exactly one seed");
        game.save.totalTime=planted+59;Interact(app,"garden");Choose(app,"harvest");Check(game.save.plantedAt==planted&&game.save.inventory[2]==herbs,"The optional test bed cannot shorten mature sixty-second growth");game.save.totalTime=planted+60;Interact(app,"garden");Choose(app,"harvest");Check(game.save.plantedAt<0&&game.save.inventory[2]==herbs+9&&game.save.inventory[7]==seeds+1&&game.save.coins==gold+8,"Original project-enhanced harvest still gives nine herbs, two seeds and eight coins");
        Console.WriteLine("PASS earned two-commission gates, exact original commission costs/rewards, original Wayfinder/An E ownership, remote API rejection and unchanged sixty-second/project-enhanced gardening; NATIVE_UNVERIFIED");
    }

    static void Reload(FrontierRuntime app,int slot)
    {
        var g=app.Game;g.PrepareSave();Check(SaveStore.Write(g.save,slot),"Actual SaveStore writes stage envelope");string warning;SaveData save=SaveStore.Read(slot,out warning);Check(save!=null&&warning==null,"Actual SaveStore reads stage envelope");g.Load(save);g.Start();g.enemies.Clear();Set(app,"camera",g.Position);
    }
    static void SaveStagesCapsAndReadOnly(FrontierRuntime app)
    {
        Prepare(app);var g=app.Game;Accept(app);Reload(app,0);Check(State(g,"Accepted")&&!State(g,"Drawn")&&g.save.trackedQuest=="watergarden","Accepted envelope preserves only its earned stage and tracking");DrawWater(app);
        GameCore original=Get<GameCore>(app,"reviewOriginal");try{Set(app,"reviewOriginal",null);Set(app,"saveTimer",13f);Update(app);Check(File.Exists(SaveStore.PathName)&&Near(Get<float>(app,"saveTimer"),0),"Actual Update autosave writes drawn-water stage");}finally{Set(app,"reviewOriginal",original);}
        string warning;SaveData drawn=SaveStore.Read(out warning);Check(drawn!=null&&warning==null,"Actual autosave stage envelope is readable");g.Load(drawn);g.Start();g.enemies.Clear();Check(State(g,"Drawn")&&!State(g,"Watered"),"Autosave reload retains the carried water without advancing the bed");Water(app);Reload(app,2);Check(State(g,"Watered")&&!State(g,"Complete"),"Watered envelope still owes An's actual report");
        foreach(bool seedCap in new[]{true,false})
        { g.save.inventory[7]=seedCap?9997:9996;g.save.tea=seedCap?998:999;int seeds=g.save.inventory[7],tea=g.save.tea;Report(app);Check(!State(g,"Complete")&&g.save.inventory[7]==seeds&&g.save.tea==tea,"Either full seed or tea storage rejects both reward and completion atomically");if(g.mode=="dialog")Choose(app,"close"); }
        g.save.inventory[7]=9996;g.save.tea=998;Report(app);Check(State(g,"Complete")&&g.save.inventory[7]==9999&&g.save.tea==999,"Exact remaining capacity accepts the complete reward once");Reload(app,2);Check(State(g,"Complete")&&g.save.inventory[7]==9999&&g.save.tea==999,"Completed envelope never drops or repeats the reward");
        foreach(int version in new[]{1,2})
        {
            Prepare(app);g=app.Game;g.PrepareSave();SaveData old=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(g.save));old.version=version;File.WriteAllText(SaveStore.PathFor(2),JsonUtility.ToJson(old));SaveData read=SaveStore.Read(2,out warning);Check(read!=null&&warning==null,"Raw legacy v"+version+" remains readable");g.Load(read);g.Start();g.enemies.Clear();Check(g.Has("ally_1")&&g.Has("ally_3")&&State(g,"Ready")&&!State(g,"Accepted")&&!State(g,"Drawn")&&!State(g,"Watered")&&!State(g,"Complete"),"Legacy commissions unlock only the offer, never fabricated water progress");
        }
        Prepare(app);Accept(app);Interact(app,"waterDraw");Pages(app);g=app.Game;string snapshot=JsonUtility.ToJson(g.save);int revision=g.revision;float time=Get<float>(app,"clock");var art=Get<PixelArt>(app,"art");Draw(app);int textures=Get<List<Texture2D>>(art,"owned").Count;
        foreach(EventType type in new[]{EventType.Layout,EventType.Repaint}){Event.current.type=type;Draw(app);Check(snapshot==JsonUtility.ToJson(g.save)&&g.revision==revision&&Get<float>(app,"clock")==time,"Repeated actual OnGUI stays read-only for save/progress/clock");}Check(textures==Get<List<Texture2D>>(art,"owned").Count,"Repeated reading allocates no new art textures");
        Console.WriteLine("PASS actual autosave/staged/completed envelopes, raw legacy v1/v2, independent atomic seed/tea capacity refusals and exact-cap retry, read-only Layout/Repaint; NATIVE_UNVERIFIED");
    }

    static void Marker(FrontierRuntime app,string kind,string capture)
    {
        Beside(app,kind);var g=app.Game;WorldObject obj=Object(g,kind);g.save.explored[0]=16777215;Draw(app,capture);Check(g.DiscoveredLandmarks().Contains(obj),"Accepted current point participates in actual discovered landmarks");Point p=(Point)Call(app,"ScreenPoint",new Point(obj.x,obj.y));
        if(kind=="waterDraw")
        { Check(GUI.textureCalls.Exists(t=>Same(t.rect,new Rect(p.x-11,p.y-25,22,24)))&&GUI.textureCalls.Exists(t=>Same(t.rect,new Rect(p.x-8,p.y-22,16,5))),"Actual well cue draws its bucket outline and five-pixel clean-water surface"); }
        else Check(GUI.textureCalls.Exists(t=>Same(t.rect,new Rect(p.x-22,p.y-10,44,15)))&&GUI.textureCalls.Exists(t=>Same(t.rect,new Rect(p.x-19,p.y-8,38,10))),"Actual unwatered trial bed draws its separate outline and soil");
        const float cell=6;Check(GUI.textureCalls.Exists(t=>Same(t.rect,new Rect(1024+obj.x*cell-1,42+obj.y*cell-1,3,3))),"Actual town minimap contains the live canonical quest interaction");
        Key(app,KeyCode.M);Draw(app);Check(GUI.textureCalls.Exists(t=>Same(t.rect,new Rect(888+obj.x*8.4f-1,264+obj.y*8.4f-1,3,3))),"Actual town local map contains the discovered live interaction");Key(app,KeyCode.Escape);
    }
    static void BilingualLayoutsAndMarkers(FrontierRuntime app)
    {
        foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})foreach(int width in new[]{960,1280})
        {
            Prepare(app,true,english,large,width);var g=app.Game;string suffix=(english?"en":"zh")+(large?"-large":"-normal")+"-"+width;int input=width==960?0:2;Accept(app,input,"offer-"+suffix);Marker(app,"waterDraw","well-"+suffix);DrawWater(app,input,"draw-"+suffix);Marker(app,"waterBed","bed-"+suffix);Water(app,input,"water-"+suffix);Report(app,input,"report-"+suffix);Check(State(g,"Complete"),"Actual mouse/pad report completes both localized accessible layouts");
            g.toastTime=0;Beside(app,"garden");Draw(app,"watered-world-"+suffix);WorldObject bed=Object(g,"waterBed");Point bedPoint=(Point)Call(app,"ScreenPoint",new Point(bed.x,bed.y));Check(!g.IsObjectPresent(bed)&&GUI.textureCalls.Exists(t=>Same(t.rect,new Rect(bedPoint.x-16,bedPoint.y,32,2))),"Retired interaction leaves its exact persistent watered-bed cue in the world");Interact(app,"garden");Choose(app,"watergarden_offer",input);Pages(app,"complete-"+suffix);Check(!Array.Exists(g.conversation.choices,c=>c.action=="watergarden_finish"),"Completed localized review never exposes a payout");Choose(app,"close",input);
            Position(app,new Point(18,11));Draw(app);Check(ReferenceEquals(g.Nearest(),Object(g,"routes")),"Completed rendering retains the mature Wayfinder interaction");
        }
        Console.WriteLine("PASS actual bilingual normal/large 960/1280 paginated offers/actions/reports, mouse/pad activation, independent world/minimap/local-map markers and persistent watered-world captures; NATIVE_UNVERIFIED");
    }

    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,temp=Path.Combine(Path.GetTempPath(),"starfall-water-garden-"+Guid.NewGuid());Application.persistentDataPath=temp;captures=args.Length>0&&args[0]!="-"?args[0]:null;prefix=args.Length>1?args[1]:"qa-water-garden";int failed=0;var app=new FrontierRuntime();
        try
        { Directory.CreateDirectory(temp);if(captures!=null)Directory.CreateDirectory(captures);Call(app,"Awake");foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{ActualQuestJournalAndWalking,EarnedGatesAndMatureTown,SaveStagesCapsAndReadOnly,BilingualLayoutsAndMarkers})try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error));}app.EndVisualReview();Call(app,"OnDestroy"); }
        finally{Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;GUI.enabled=true;GUI.color=Color.white;GUI.matrix=Matrix4x4.identity;Event.current.type=EventType.Repaint;if(Directory.Exists(temp))Directory.Delete(temp,true);}
        Console.WriteLine("Runtime water-garden: "+(4-failed)+"/4 groups passed; captures="+captureCount+"; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
