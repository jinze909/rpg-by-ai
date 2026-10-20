// Production journal input, OnGUI and SaveStore paths with offline Unity declarations.
// Native font rendering, controller hardware and Player play feel remain unverified.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeJournalPaginationChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string captureFolder;
    private static readonly Rect Previous = new Rect(650,699,258,26), Next = new Rect(927,699,263,26);
    private static void Check(bool value,string message) { if(!value)throw new Exception(message); }
    private static bool Near(float a,float b) { return Math.Abs(a-b)<.005f; }
    private static bool Same(Rect a,Rect b) { return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height); }
    private static object Call(object obj,string name,params object[] args) { return obj.GetType().GetMethod(name,Private|BindingFlags.Public).Invoke(obj,args); }
    private static void Set(object obj,string name,object value) { obj.GetType().GetField(name,Private).SetValue(obj,value); }
    private static T Get<T>(object obj,string name) { return (T)obj.GetType().GetField(name,Private).GetValue(obj); }
    private static void Update(FrontierRuntime app) { Call(app,"Update");Input.keys.Clear(); }
    private static void Key(FrontierRuntime app,KeyCode key) { Input.keys.Add(key);Update(app); }
    private static void Draw(FrontierRuntime app,string capture=null)
    {
        GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        if(captureFolder!=null&&capture!=null)
        {
            var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();
            GUI.Export(captureFolder,"qa-journal-pagination-"+capture);
            GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static void Click(FrontierRuntime app,Rect rect)
    {
        Draw(app);Check(GUI.buttonCalls.Exists(b=>Same(b.rect,rect)&&b.enabled),"The selected journal control is actually visible and enabled");
        GUI.queuedClick=rect;Draw(app);Check(!GUI.queuedClick.HasValue,"The journal control consumes its actual click");
    }
    private static void Focus(FrontierRuntime app,Rect rect)
    {
        Draw(app);string id=(string)Call(app,"ControlId",rect.x,rect.y,rect.width,rect.height);var navigation=Get<MenuNavigation>(app,"navigation");
        int steps=0;while(navigation.focused!=id&&steps++<60){Key(app,KeyCode.Tab);Draw(app);}
        Check(navigation.focused==id&&navigation.CanActivate(id),"Actual Tab navigation reaches an enabled control on the current page");
    }
    private static void Prepare(FrontierRuntime app,bool english=true,bool large=false)
    {
        Input.Clear();GUI.queuedClick=null;GUI.enabled=true;GUI.color=Color.white;GUI.matrix=Matrix4x4.identity;
        Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;Event.current.type=EventType.Repaint;
        app.BeginVisualReview(0,0);app.VisualReviewPaused=false;var g=app.Game;
        g.enemies.Clear();g.effects.Clear();g.hazards.Clear();g.projectiles.Clear();g.save.companion=-1;
        g.save.english=english;g.save.largeText=large;g.save.reducedMotion=true;g.save.effectsQuality=0;
        foreach(string flag in new[]{"archive_accept","archive_drain","archive_vent","archive_wave","archive_record_0","archive_record_1","archive_record_2","archive_defeated_74000","archive_defeated_74001","archive_defeated_74002","archive_complete","archive_family","harbor_report"})
            if(!g.Has(flag))g.save.flags.Add(flag);
        Set(app,"selectedQuest",0);Set(app,"journalTab",0);Set(app,"applicationPaused",false);Set(app,"controllerUsed",false);Set(app,"pointerSeen",false);
        Set(app,"resumeTime",0f);Set(app,"arrivalTime",0f);Set(app,"combatHudTime",0f);Call(app,"Resize");Key(app,KeyCode.Tab);
        Check(g.mode=="journal","Actual world Tab opens the mature journal");
    }
    private static Rect Row(int cell) { float spacing=464f/18;return new Rect(279,226+cell*spacing-(cell==18?3:0),321,spacing-7); }
    private static List<GUI.ButtonCall> Rows() { return GUI.buttonCalls.FindAll(b=>Near(b.rect.x,279)&&Near(b.rect.width,321)); }
    private static bool HasText(string text) { return GUI.commands.Exists(c=>c.Contains("\"text\":"+GUI.S(text))); }
    private static void MatureRowsAndLocalizedPages(FrontierRuntime app)
    {
        string[] mature={"main","shore","ally1","ally2","ally3","personal0","personal1","personal2","bounty","secrets","watch","signal","archive","reach","homeward","commission","shelter","fieldcraft","supper"};
        Check(GameCore.QuestIds.Length==20,"The new return letter appends exactly one complete quest");
        for(int i=0;i<mature.Length;i++)Check(GameCore.QuestIds[i]==mature[i],"Existing quest ordering is preserved: "+mature[i]);
        Check(GameCore.QuestIds[19]=="returnletter","The return letter occupies the next journal page");
        for(int language=0;language<2;language++)for(int large=0;large<2;large++)
        {
            Prepare(app,language==1,large==1);var g=app.Game;string suffix=(language==1?"en":"zh")+(large==1?"-large":"");
            string before=JsonUtility.ToJson(g.save);Draw(app,"first-"+suffix);Check(before==JsonUtility.ToJson(g.save),"Journal layout does not advance story/save state");
            var rows=Rows();Check(rows.Count==19,"The first page displays all nineteen mature quest rows");
            for(int i=0;i<19;i++)Check(Same(rows[i].rect,Row(i)),"The exact mature row rectangle survives pagination: "+i);
            Check(Row(18).y+Row(18).height<730&&GUI.buttonCalls.Exists(b=>Same(b.rect,Next)&&b.enabled)&&GUI.buttonCalls.Exists(b=>Same(b.rect,Previous)&&!b.enabled),"Original last row and separate page controls fit above the unchanged footer");
            Click(app,Row(11));Draw(app);Check(Get<int>(app,"selectedQuest")==11&&HasText(g.GetQuest("signal").title),"The established Warden row stays visible, selectable and correctly bound");
            Click(app,Next);Draw(app,"second-"+suffix);Check(Get<int>(app,"selectedQuest")==19&&Rows().Count==1&&Same(Rows()[0].rect,Row(0)),"Next page selects its first real quest without compressing rows");
            Check(HasText(g.GetQuest("returnletter").title)&&GUI.buttonCalls.Exists(b=>Same(b.rect,Previous)&&b.enabled)&&GUI.buttonCalls.Exists(b=>Same(b.rect,Next)&&!b.enabled),"Second-page details and enabled/disabled bounds match the selected quest");
            foreach(var b in GUI.buttonCalls)if(Same(b.rect,Previous)||Same(b.rect,Next))
                Check(b.rect.y>=690&&b.rect.y+b.rect.height<730&&b.rect.x>=625,"Page controls stay below the detail card, beside no quest rows and above Back");
            Click(app,Previous);Draw(app);Check(Get<int>(app,"selectedQuest")==0&&Rows().Count==19,"Previous page restores all mature quests with its first entry selected");
            Click(app,Row(18));Draw(app);Check(Get<int>(app,"selectedQuest")==18,"Selecting the final mature row is not reset by drawing the first page");
            Draw(app);Check(Get<int>(app,"selectedQuest")==18,"An explicit selection stays selected across subsequent repaint");
        }
        Console.WriteLine("PASS exact nineteen mature rectangles/order, preserved Warden selection, two pages and four localized/font-size layouts; NATIVE_UNVERIFIED");
    }
    private static void TrackingDestinationAndRememberedSelection(FrontierRuntime app)
    {
        Prepare(app);var g=app.Game;Click(app,Next);Draw(app);QuestGuide q=g.GetQuest("returnletter");
        Check(!q.locked&&!q.complete,"Authentic completed archive and harbor report unlock the new follow-up");
        Click(app,new Rect(650,647,258,31));Check(g.save.trackedQuest=="returnletter"&&g.CurrentGuide.id==q.id,"Actual second-page tracking targets the new real quest");
        Click(app,new Rect(927,647,263,31));Check(g.mode=="map"&&Get<int>(app,"selectedRegion")==q.room,"Actual Show destination opens the correct authored region");
        Key(app,KeyCode.Escape);Key(app,KeyCode.Tab);Draw(app);
        Check(g.mode=="journal"&&Get<int>(app,"selectedQuest")==19&&Rows().Count==1,"Closing and reopening the journal remembers its selected second-page quest");
        Key(app,KeyCode.Escape);Update(app);Point? target=g.NavigationTarget();
        Check(target.HasValue&&(target.Value-q.target).Length<.005f,"World navigation follows the selected follow-up's actual next interaction");
        g.PrepareSave();Check(SaveStore.Write(g.save,2),"Actual checksummed save writes the tracked new quest");string warning;SaveData loaded=SaveStore.Read(2,out warning);
        Check(loaded!=null&&warning==null,"Actual checksummed save reads the tracked new quest");g.Load(loaded);g.Start();
        Check(g.save.trackedQuest=="returnletter"&&g.CurrentGuide.id==q.id,"Guidance sanitization retains the new tracked ID after save/load");
        Set(app,"selectedQuest",int.MaxValue);Call(app,"OpenMenu","journal");Draw(app);Check(Get<int>(app,"selectedQuest")==GameCore.QuestIds.Length-1,"Oversized stale journal selection is clamped before indexing");
        Set(app,"selectedQuest",int.MinValue);Draw(app);Check(Get<int>(app,"selectedQuest")==0,"Negative stale journal selection is clamped before indexing");
        Console.WriteLine("PASS real second-page track/map/world route, remembered selection, checksummed save/load and stale-index bounds; NATIVE_UNVERIFIED");
    }
    private static void KeyboardAndControllerConfirmVisibleControls(FrontierRuntime app)
    {
        Prepare(app);Focus(app,Next);Key(app,KeyCode.Return);Draw(app);Draw(app);
        Check(Get<int>(app,"selectedQuest")==19&&Rows().Count==1,"Actual keyboard focus and Enter activate the visible next page");
        Rect track=new Rect(650,647,258,31);Focus(app,track);Key(app,KeyCode.JoystickButton0);Draw(app);
        Check(app.Game.save.trackedQuest=="returnletter"&&Get<bool>(app,"controllerUsed"),"Actual gamepad A confirms the second-page tracking control");
        Focus(app,Previous);Key(app,KeyCode.JoystickButton0);Draw(app);Draw(app);
        Check(Get<int>(app,"selectedQuest")==0&&Rows().Count==19,"Gamepad confirmation returns to the real first-page controls");
        Console.WriteLine("PASS actual Tab/Enter and gamepad-A control registration across both journal pages; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        captureFolder=args.Length>0?args[0]:null;string previous=Application.persistentDataPath;
        string temporary=Path.Combine(Path.GetTempPath(),"starfall-journal-pagination-"+Guid.NewGuid());Application.persistentDataPath=temporary;Directory.CreateDirectory(temporary);var app=new FrontierRuntime();
        try
        {
            Call(app,"Awake");Check(app.Game!=null,"Awake loads the shipped campaign");
            Action<FrontierRuntime>[] tests={MatureRowsAndLocalizedPages,TrackingDestinationAndRememberedSelection,KeyboardAndControllerConfirmVisibleControls};int failures=0;
            foreach(var test in tests)try{test(app);}catch(Exception error){failures++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}
            Console.WriteLine("Runtime journal pagination "+(tests.Length-failures)+"/"+tests.Length+"; NATIVE_UNVERIFIED");return failures==0?0:1;
        }
        finally{app.EndVisualReview();Call(app,"OnDestroy");Application.persistentDataPath=previous;if(Directory.Exists(temporary))Directory.Delete(temporary,true);}
    }
}
