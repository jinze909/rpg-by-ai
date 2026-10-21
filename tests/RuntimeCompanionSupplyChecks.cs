// Real journal/map/input/SaveStore against offline Unity declarations.
// Native import, GPU output and physical controls remain NATIVE_UNVERIFIED.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeCompanionSupplyChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static string captures;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static object Call(FrontierRuntime app, string method, params object[] args)
    { return typeof(FrontierRuntime).GetMethod(method, Private).Invoke(app, args); }
    static void Set(FrontierRuntime app, string field, object value)
    { typeof(FrontierRuntime).GetField(field, Private).SetValue(app, value); }
    static T Get<T>(FrontierRuntime app, string field)
    { return (T)typeof(FrontierRuntime).GetField(field, Private).GetValue(app); }
    static void Update(FrontierRuntime app) { Call(app, "Update"); Input.keys.Clear(); }
    static void Key(FrontierRuntime app, KeyCode key) { Input.keys.Add(key); Update(app); }
    static bool Same(Rect a, Rect b)
    { return Math.Abs(a.x-b.x)<.01f && Math.Abs(a.y-b.y)<.01f && Math.Abs(a.width-b.width)<.01f && Math.Abs(a.height-b.height)<.01f; }
    static bool Text(string text) { return GUI.commands.Exists(c => c.Contains("\"text\":" + GUI.S(text))); }
    static void Draw(FrontierRuntime app, string capture = null)
    {
        GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); Call(app, "OnGUI");
        if (captures != null && capture != null)
        {
            var text=GUI.commands.ToArray(); var textures=GUI.textureCalls.ToArray(); var buttons=GUI.buttonCalls.ToArray();
            GUI.Export(captures, "qa-companion-supply-" + capture); GUI.commands.AddRange(text); GUI.textureCalls.AddRange(textures); GUI.buttonCalls.AddRange(buttons);
        }
    }
    static void Click(FrontierRuntime app, Rect rectangle)
    {
        Draw(app); Check(GUI.buttonCalls.Exists(b => Same(b.rect,rectangle) && b.enabled), "Actual displayed button is enabled: " + rectangle.x + "," + rectangle.y);
        GUI.queuedClick=rectangle; Draw(app); Check(!GUI.queuedClick.HasValue, "Production control consumes its actual click"); Draw(app);
    }
    static void Prepare(FrontierRuntime app, int room = 1)
    {
        Input.Clear(); GUI.queuedClick=null; GUI.enabled=true; GUI.color=Color.white; GUI.matrix=Matrix4x4.identity;
        Screen.width=1280; Screen.height=800; Time.unscaledDeltaTime=.02f; Event.current.type=EventType.Repaint;
        app.BeginVisualReview(0,room); app.VisualReviewPaused=false; var g=app.Game; g.enemies.Clear(); g.projectiles.Clear();
        foreach (string flag in new[]{"companion_1","companion_2","mountain_report","companion_supply_mature_story"}) if(!g.Has(flag))g.save.flags.Add(flag);
        g.save.flags.RemoveAll(f=>f.StartsWith("landmark_pin:") || f=="personal_1" || f=="personal_2" || f=="lore_1" || f.StartsWith("node_"));
        g.save.english=true; g.save.largeText=false; g.save.showGuidance=true; g.save.companion=-1; g.save.reducedMotion=true; g.save.effectsQuality=0;
        g.save.inventory[1]=g.save.inventory[2]=g.save.inventory[4]=1; g.save.trackedQuest="main"; g.save.explored[room]=16777215;
        Set(app,"applicationPaused",false); Set(app,"pointerSeen",false); Set(app,"controllerUsed",false); Set(app,"arrivalTime",0f); Set(app,"resumeTime",0f); Set(app,"combatHudTime",0f);
        Set(app,"journalTab",0); Set(app,"selectedQuest",0); Call(app,"Resize");
    }
    static WorldObject Object(GameCore g, int room, string kind)
    { var o=Array.Find(g.data.maps[room].objects,x=>x.kind==kind); Check(o!=null,"Authored room retains " + kind); return o; }
    static WorldObject GuidedNode(GameCore g, string kind)
    {
        var q=g.CurrentGuide; var node=Array.Find(g.data.maps[q.room].objects,o=>o.kind==kind && (new Point(o.x,o.y)-q.target).Length<.001f);
        Check(node!=null && !g.Has("node_"+node.id),"Personal material shortage guides an unspent authored " + kind + " node, not the companion handoff");
        return node;
    }
    static void Journal(FrontierRuntime app, string id)
    {
        Check(app.Game.mode=="world","Actual journal opens from world"); Key(app,KeyCode.Tab); Check(app.Game.mode=="journal","Real Tab opens the mature journal");
        int row=Array.IndexOf(GameCore.QuestIds,id); Check(row>=0 && row<19,"Original personal story retains its original first-page row");
        float spacing=Math.Min(46,464f/Math.Min(GameCore.QuestIds.Length,18)); Click(app,new Rect(279,226+row*spacing,321,spacing-7));
        Check(Get<int>(app,"selectedQuest")==row && Text(app.Game.GetQuest(id).title),"Actual original journal row selects its complete personal story");
    }
    static void Track(FrontierRuntime app, string id)
    {
        Journal(app,id); Click(app,new Rect(650,647,258,31)); Check(app.Game.save.trackedQuest==id,"Only real Track quest confirms the story");
        Key(app,KeyCode.Escape); Check(app.Game.mode=="world","Journal returns to world");
    }
    static void Travel(FrontierRuntime app, int room)
    {
        Key(app,KeyCode.M); Set(app,"selectedRegion",room); Click(app,new Rect(888,616,306,47));
        Check(app.Game.save.room==room && app.Game.mode=="world","Actual original map fast travel reaches the selected accessible region");
        app.Game.enemies.Clear(); Set(app,"camera",app.Game.Position); Set(app,"arrivalTime",0f);
    }
    static void Interact(FrontierRuntime app, WorldObject target)
    {
        var g=app.Game; bool found=false;
        foreach(Point offset in new[]{new Point(0,-1),new Point(0,1),new Point(-1,0),new Point(1,0),new Point(0,-.5f),new Point(0,.5f)})
        {
            Point point=new Point(target.x,target.y)+offset;
            if(!g.CanStand(point))continue;
            Check(app.SetVisualReviewPosition(point) && (g.Position-point).Length<.001f,"Interaction fixture uses unmodified legal terrain");
            if(g.Nearest()==target){found=true;break;}
        }
        Check(found,"The authored guide destination is the actual nearest E interaction"); Set(app,"camera",g.Position); Key(app,KeyCode.E);
    }
    static void Choose(FrontierRuntime app, string action)
    {
        var c=app.Game.conversation; Check(c!=null && app.Game.mode=="dialog","Real E offers the original dialogue");
        int index=Array.FindIndex(c.choices,x=>x.action==action); Check(index>=0 && index<9,"Original intended transaction remains available");
        Draw(app); Key(app,(KeyCode)((int)KeyCode.Alpha1+index)); Check(app.Game.mode=="world","Actual numbered original dialogue choice returns to world");
    }
    static void CheckReward(GameCore g, int id, int coins, int shards, int points, int bond)
    {
        Check(g.Has("personal_"+id) && g.save.coins==coins+35 && g.save.shards==shards+3 && g.save.skillPoints==points+1 && g.save.relationships[id]==Math.Min(5,bond+2),
            "Original handoff keeps exactly +35 coins, +3 shards, +1 point, +2 capped bond and one completion flag");
        Check(g.CurrentGuide.complete && g.CurrentGuide.steps[0].done && g.CurrentGuide.steps[0].text.Contains("4/4"),"Completed story retains fulfilled material history after its payment");
    }
    static void IvoGatherAndOriginalHandoff(FrontierRuntime app)
    {
        Prepare(app); var g=app.Game; Track(app,"personal1"); var ore=GuidedNode(g,"ore");
        Check(g.CurrentGuide.room==1 && g.CurrentGuide.steps[0].text=="Parts 1/4 · Crystals 1/4","Real tracked Ivo journal reports both exact current requirements");
        Update(app); Check(Get<List<Point>>(app,"route").Count>0,"Actual world route cache draws an actionable walking route to the unspent ore");
        Check(g.NavigationTarget().HasValue && (g.NavigationTarget().Value-new Point(ore.x,ore.y)).Length<.001f,"Gold quest target is the actual gathering destination");
        Draw(app,"ivo-gather-hud-en"); Interact(app,ore);
        Check(g.Has("node_"+ore.id) && g.save.inventory[1]==4 && g.save.inventory[4]==4 && !g.Has("personal_1"),"Real E grants original ore +3 crystals/+3 parts and never completes the companion story automatically");
        var handoff=g.CurrentGuide; var ivo=Object(g,6,"tinker");
        Check(handoff.room==6 && (handoff.target-new Point(ivo.x,ivo.y)).Length<.001f && handoff.next=="Talk to your companion and deliver","Actual threshold restores Ivo's original handoff destination");
        Journal(app,"personal1"); Draw(app,"ivo-ready-journal-en"); Key(app,KeyCode.Escape); Travel(app,6); Interact(app,Object(g,6,"tinker"));
        int coins=g.save.coins,shards=g.save.shards,points=g.save.skillPoints,bond=g.save.relationships[1]; Choose(app,"personal1");
        Check(g.save.inventory[1]==0 && g.save.inventory[4]==0,"Ivo's original transaction spends exactly four of each material"); CheckReward(g,1,coins,shards,points,bond);
        Console.WriteLine("PASS real Tab/select/Track, actual route cache and E ore yield, automatic NPC retarget, original map travel and numbered Ivo handoff with exact original payment/rewards; NATIVE_UNVERIFIED");
    }
    static void SenRecordBeforeHerbsAndOriginalHandoff(FrontierRuntime app)
    {
        Prepare(app); var g=app.Game; Track(app,"personal2"); var lore=Object(g,1,"lore");
        Check(g.CurrentGuide.room==1 && (g.CurrentGuide.target-new Point(lore.x,lore.y)).Length<.001f && !g.Has("lore_1"),"Sen still leads to the original forest record before material collection");
        Interact(app,lore); Check(g.Has("lore_1") && g.mode=="dialog","Actual E discovers the mature forest story record"); Choose(app,"close");
        var herb=GuidedNode(g,"herb"); Check(g.CurrentGuide.steps[0].text=="Forest record and herbs 1/4","After the record, live journal retains Sen's actual remaining herb count");
        Draw(app,"sen-gather-hud-en"); Interact(app,herb);
        Check(g.save.inventory[2]==4 && g.Has("node_"+herb.id) && !g.Has("personal_2"),"Actual herb interaction retains +3 yield and manual delivery");
        var sen=Object(g,7,"keeper"); Check(g.CurrentGuide.room==7 && (g.CurrentGuide.target-new Point(sen.x,sen.y)).Length<.001f,"Four herbs plus the existing record return navigation to Sen");
        Travel(app,7); Interact(app,Object(g,7,"keeper")); int coins=g.save.coins,shards=g.save.shards,points=g.save.skillPoints,bond=g.save.relationships[2]; Choose(app,"personal2");
        Check(g.save.inventory[2]==0 && g.Has("lore_1"),"Sen spends four herbs while retaining the permanent discovered record"); CheckReward(g,2,coins,shards,points,bond);
        Console.WriteLine("PASS actual Sen record-first E/read/close, live herb target and E yield, automatic keeper retarget and unchanged paid personal story rewards/history; NATIVE_UNVERIFIED");
    }
    static void DepletedNodesRestAndSaveRestoration(FrontierRuntime app)
    {
        Prepare(app); var g=app.Game; Track(app,"personal1"); g.save.inventory[1]=g.save.inventory[4]=0;
        foreach(var map in g.data.maps)foreach(var node in map.objects)if(node.kind=="ore" && !g.Has("node_"+node.id))g.save.flags.Add("node_"+node.id);
        Check(g.CurrentGuide.room==0 && g.CurrentGuide.next.Contains("rest"),"A real global depletion routes to the original inn instead of a spent node or unfunded Ivo handoff");
        Travel(app,0); var bed=Object(g,0,"rest"); Interact(app,bed); Choose(app,"rest");
        Check(g.save.inventory[1]==0 && g.save.inventory[4]==0 && !g.Has("personal_1"),"Actual inn refresh does not invent materials, companion payment or rewards"); var nodeAfterRest=GuidedNode(g,"ore");
        Check(g.CurrentGuide.room==1 && !g.Has("node_"+nodeAfterRest.id),"After the existing rest transaction, personal navigation resumes at a fresh real ore node");
        g.PrepareSave(); Check(SaveStore.Write(g.save,0),"Actual checksum envelope writes the tracked personal story"); string warning; var loaded=SaveStore.Read(0,out warning);
        Check(loaded!=null && warning==null,"Actual SaveStore checksum reader accepts the refreshed story state"); g.Load(loaded); g.Start(); g.Close();
        Check(g.save.trackedQuest=="personal1" && g.Has("companion_supply_mature_story") && g.Has("companion_1") && !g.Has("personal_1") && g.save.inventory[1]==0 && g.save.inventory[4]==0,"Real reload retains story/companion ownership/inventory without granting completion"); GuidedNode(g,"ore");
        foreach(int version in new[]{1,2})
        {
            var legacy=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(g.save)); legacy.version=version; legacy.flags.Add("return_letter_complete");
            File.WriteAllText(SaveStore.PathFor(1),JsonUtility.ToJson(legacy)); loaded=SaveStore.Read(1,out warning); Check(loaded!=null && warning==null,"Production SaveStore reads raw legacy v"+version); g.Load(loaded); g.Start(); g.Close();
            Check(g.save.trackedQuest=="personal1" && g.Has("return_letter_complete") && g.save.inventory[1]==0 && g.save.inventory[4]==0,"Legacy reload retains unrelated earned story and exact unpaid inventory"); GuidedNode(g,"ore");
        }
        Console.WriteLine("PASS all real ore nodes depleted, actual inn dialogue rest/refresh, no free stock/reward and checksum plus raw v1/v2 SaveStore restoration with original mature story; NATIVE_UNVERIFIED");
    }
    static void BilingualJournalReadOnlyAndWaypointOwnership(FrontierRuntime app)
    {
        foreach(string quest in new[]{"personal1","personal2"})foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})
        {
            Prepare(app); var g=app.Game; g.save.english=english; g.save.largeText=large; if(quest=="personal2")g.save.flags.Add("lore_1");
            Screen.width=large?960:1280; Screen.height=large?600:800; Call(app,"Resize"); Track(app,quest); Journal(app,quest);
            QuestGuide q=g.CurrentGuide; Check(Text(g.L("下一步：","Next: ")+q.next) && Text("○  "+q.steps[0].text),"Actual bilingual journal draws exact next source and live requirement counts");
            string before=JsonUtility.ToJson(g.save); int revision=g.revision; for(int i=0;i<20;i++)Draw(app);
            Check(before==JsonUtility.ToJson(g.save) && revision==g.revision,"Twenty actual journal redraws cannot consume supplies, mutate saves or advance story");
            Draw(app,(quest=="personal1"?"ivo":"sen")+(english?"-en":"-zh")+(large?"-large960":"-normal1280")); Key(app,KeyCode.Escape);
        }
        Prepare(app,0); var game=app.Game; Track(app,"personal1"); game.save.explored[0]=16777215; var cook=Object(game,0,"cook");
        Key(app,KeyCode.M); Click(app,new Rect(913,692,304,30)); Check(game.mode=="landmarks","Actual map retains original optional waypoint menu");
        int index=game.DiscoveredLandmarks().IndexOf(cook); Check(index>=0 && index<6,"Original discovered cook occupies its known first waypoint page"); Click(app,new Rect(279,225+index*64,535,54)); Click(app,new Rect(866,572,351,42));
        Key(app,KeyCode.Escape); Key(app,KeyCode.Escape);
        Check(game.PinnedLandmark==cook && game.save.trackedQuest=="personal1" && game.CurrentGuide.room==1 && game.NavigationTarget().HasValue && (game.NavigationTarget().Value-new Point(cook.x,cook.y)).Length<.001f,
            "Material-source guidance never clears or steals the player's explicit local pin or replaces the tracked personal story");
        Console.WriteLine("PASS eight actual bilingual normal/large journal captures with live shortages and read-only redraws; original M/waypoint selection remains owner of world navigation without replacing personal quest tracking; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,temporary=Path.Combine(Path.GetTempPath(),"starfall-companion-supply-"+Guid.NewGuid()); Application.persistentDataPath=temporary;
        captures=args.Length>0 && args[0]!="-"?args[0]:null; var app=new FrontierRuntime(); int failed=0;
        try
        {
            Directory.CreateDirectory(temporary); if(captures!=null)Directory.CreateDirectory(captures); Call(app,"Awake"); Check(app.Game!=null,"Actual Awake loads original campaign");
            foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[] { IvoGatherAndOriginalHandoff, SenRecordBeforeHerbsAndOriginalHandoff, DepletedNodesRestAndSaveRestoration, BilingualJournalReadOnlyAndWaypointOwnership })
                try { test(app); } catch(Exception error) { failed++; Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error)); }
            app.EndVisualReview(); Call(app,"OnDestroy");
        }
        finally { Application.persistentDataPath=previous; Input.Clear(); GUI.queuedClick=null; GUI.enabled=true; GUI.color=Color.white; GUI.matrix=Matrix4x4.identity; if(Directory.Exists(temporary))Directory.Delete(temporary,true); }
        Console.WriteLine("Runtime companion supplies: "+(4-failed)+"/4 groups passed; NATIVE_UNVERIFIED"); return failed==0?0:1;
    }
}
