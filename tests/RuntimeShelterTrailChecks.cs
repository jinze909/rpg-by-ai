// Shipped Update, OnGUI, climate, map collision and SaveStore, with offline
// Unity declarations. Native import, rendering and play feel are NATIVE_UNVERIFIED.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeShelterTrailChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Point[] Sources = { new Point(6,14), new Point(19,11), new Point(24,6) };
    private static readonly Point[] Marks = { new Point(12,13), new Point(21,8), new Point(18,9) };
    private static readonly Point[] Destinations = { new Point(19,11), new Point(24,6), new Point(15,6) };
    private static string captureFolder;
    private static SaveData earnedRoute, earnedPartial;
    private static int walkFrames, acceptedWindHits, dodges;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static object Call(FrontierRuntime app, string method, params object[] args)
    { var m=typeof(FrontierRuntime).GetMethod(method, Private); Check(m!=null,"Actual runtime contains "+method); return m.Invoke(app,args); }
    private static void Set(FrontierRuntime app, string field, object value)
    { typeof(FrontierRuntime).GetField(field, Private).SetValue(app,value); }
    private static T Get<T>(FrontierRuntime app, string field)
    { return (T)typeof(FrontierRuntime).GetField(field, Private).GetValue(app); }
    private static void Update(FrontierRuntime app) { Call(app,"Update"); Input.keys.Clear(); }
    private static void Key(FrontierRuntime app, KeyCode key) { Input.keys.Add(key); Update(app); }
    private static void Draw(FrontierRuntime app, string capture=null)
    {
        GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); Call(app,"OnGUI");
        if(captureFolder!=null&&capture!=null)
        {
            var texts=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();
            GUI.Export(captureFolder,"qa-shelter-"+capture);GUI.commands.AddRange(texts);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static bool HasText(string value) { return GUI.commands.Exists(c=>c.Contains("\"text\":"+GUI.S(value))); }
    private sealed class RecordedText { public string text=null; public float[] r=null; }
    private static Rect TextBounds(string value,bool prefix=false)
    {
        foreach(string command in GUI.commands)
        {
            if(!command.Contains("\"k\":\"text\""))continue;
            var text=JsonUtility.FromJson<RecordedText>(command);
            if(text.text!=null&&(prefix?text.text.StartsWith(value,StringComparison.Ordinal):text.text==value))return new Rect(text.r[0],text.r[1],text.r[2],text.r[3]);
        }
        throw new Exception("Actual GUI contains text bounds: "+value);
    }
    private static bool Overlap(Rect a,Rect b)
    {return a.x<b.x+b.width&&a.x+a.width>b.x&&a.y<b.y+b.height&&a.y+a.height>b.y;}
    private static void Position(FrontierRuntime app, Point point)
    {
        Check(app.Game.CanStand(point),"Explicit fixture uses exact legal feet "+point.x+","+point.y);
        Check(app.SetVisualReviewPosition(point)&&(app.Game.Position-point).Length<.001f,"Fixture staging cannot silently repair an invalid footprint");Set(app,"camera",app.Game.Position);
    }
    private static WorldObject Object(GameCore g,string kind)
    { var o=Array.Find(g.Map.objects,x=>x.kind==kind);Check(o!=null,"Original or authored interaction remains "+kind);return o; }
    private static WorldObject Brazier(GameCore g,int index)
    {var o=Array.Find(g.Map.objects,x=>x.id=="brazier"+index);Check(o!=null,"Mature fire remains "+index);return o;}
    private static void Interact(FrontierRuntime app, WorldObject obj, bool stage=false)
    {
        if(stage)Position(app,new Point(obj.x,obj.y-.7f));
        Check(app.Game.Nearest()==obj,"Actual nearest E targets "+obj.kind+":"+obj.id);Key(app,KeyCode.E);
    }
    private static void Choose(FrontierRuntime app,string action,string capture=null)
    {
        var g=app.Game;Check(g.mode=="dialog"&&g.conversation!=null,"Real dialogue owns numeric "+action);
        int index=Array.FindIndex(g.conversation.choices,c=>c.action==action);Check(index>=0&&index<9,"Authored numeric choice exists "+action);
        var pages=DialogueLayout.Pages(g.L(g.conversation.text,g.conversation.textEn),g.save.largeText);int page=0;
        do{Draw(app,capture==null?null:capture+"-page"+page);Check(HasText(pages[page]),"Actual GUI renders every complete page before "+action);if((bool)Call(app,"DialogueChoicesVisible"))break;Key(app,KeyCode.Return);page++;}while(page<pages.Count);
        Check(page==pages.Count-1&&(bool)Call(app,"DialogueChoicesVisible"),"Numbers require final narrative page");Key(app,(KeyCode)((int)KeyCode.Alpha1+index));
    }
    private static void Original(GameCore g,params string[] actions)
    { Check(g.conversation!=null&&g.conversation.choices.Length>=actions.Length,"Mature numbered choices remain");for(int i=0;i<actions.Length;i++)Check(g.conversation.choices[i].action==actions[i],"Original action remains number "+(i+1)+": "+actions[i]); }
    private static void Prepare(FrontierRuntime app,bool light=true)
    {
        Input.Clear();GUI.queuedClick=null;Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;
        app.BeginVisualReview(0,7);app.VisualReviewPaused=false;var g=app.Game;
        // Fixture only the already earned old mountain chapter and available old
        // timber. New acceptance, marks, arrivals, ending and weather are real.
        foreach(var f in new[]{"mountain_report","personal_2","companion_2","boss_7"})if(!g.Has(f))g.save.flags.Add(f);
        g.enemies.Clear();g.save.inventory[0]=12;g.save.difficulty=1;g.save.companion=0;g.save.english=true;g.save.largeText=false;g.save.effectsQuality=0;g.save.reducedMotion=true;
        Set(app,"applicationPaused",false);Set(app,"pointerSeen",false);Set(app,"controllerUsed",false);Set(app,"combatHudTime",0f);Set(app,"resumeTime",0f);Set(app,"journalTab",0);Set(app,"selectedRegion",-1);Call(app,"Resize");
        if(light)for(int i=0;i<3;i++){Interact(app,Brazier(g,i),true);Original(g,"warm:brazier"+i,"close");Choose(app,"warm:brazier"+i);Check(g.Has("warm_brazier"+i),"Original numeric lighting paid timber at fire "+i);}
    }
    private static void Accept(FrontierRuntime app)
    {
        Interact(app,Object(app.Game,"keeper"),true);Original(app.Game,"gift2","join2","close");Choose(app,"shelter_offer","sen-offer-menu");
        Check(!app.Game.ShelterAccepted&&app.Game.save.companion==0,"Physical offer cannot silently accept or swap Mira");Choose(app,"shelter_accept","sen-offer");
        Check(app.Game.ShelterAccepted&&app.Game.save.trackedQuest=="shelter"&&app.Game.save.companion==0,"Real acceptance records and tracks only the optional route");
    }
    private static void Movement(FrontierRuntime app,Point movement)
    {
        Input.axes["Horizontal"]=movement.x;Input.axes["Vertical"]=-movement.y;Input.held.Clear();
        if(movement.x>.05f)Input.held.Add(KeyCode.D);if(movement.x<-.05f)Input.held.Add(KeyCode.A);if(movement.y>.05f)Input.held.Add(KeyCode.S);if(movement.y<-.05f)Input.held.Add(KeyCode.W);
    }
    private static void Walk(FrontierRuntime app,Point destination,string label)
    {
        var g=app.Game;int frames=0;float before=g.save.totalTime;
        while((g.Position-destination).Length>.17f&&frames++<5000)
        {Check(!g.Dying&&g.mode=="world"&&g.save.room==7,"Real traversal survives "+label);Movement(app,g.DirectionTo(g.Position,destination));Update(app);Check(g.CanStand(g.Position),"Map collision retains legal feet "+label);}
        Input.axes.Clear();Input.held.Clear();Check(frames<5000&&(frames==0||g.save.totalTime>before),"Held WASD reaches "+label+" without feet writes");walkFrames+=frames;
    }
    private static void StartLeg(FrontierRuntime app,int index,bool stage=false,string capture=null)
    {
        if(stage)Position(app,new Point(Sources[index].x,Sources[index].y-.7f));else Walk(app,new Point(Sources[index].x,Sources[index].y-.7f),"leg "+index+" start");
        Interact(app,Brazier(app.Game,index));Original(app.Game,"close","close");Choose(app,"shelter_start:"+index,capture);
        Check(app.Game.ShelterLeg==index,"Manual fire choice starts exactly leg "+index);
    }
    private static void Mark(FrontierRuntime app,int index,string capture=null)
    {
        Walk(app,new Point(Marks[index].x,Marks[index].y-.7f),"survey mark "+index);
        var obj=Object(app.Game,"shelterMark"+index);Interact(app,obj);Choose(app,"shelter_mark:"+index,capture);
        Check(app.Game.Has("shelter_mark_"+index),"Actual E and number records mark "+index);
    }
    private static void Arrival(FrontierRuntime app,int index,string capture=null)
    {
        Walk(app,new Point(Destinations[index].x,Destinations[index].y-.7f),"leg "+index+" destination");
        var obj=index==2?Object(app.Game,"keeper"):Brazier(app.Game,index+1);Interact(app,obj);
        if(index==2)Original(app.Game,"gift2","join2","close");else Original(app.Game,"close","close");
        Choose(app,"shelter_arrival:"+index,capture);Check(app.Game.Has("shelter_leg_"+index)&&app.Game.ShelterLeg<0,"Manual destination confirms and stops leg "+index);
    }
    private static SaveData Copy(SaveData save) {return JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));}
    private static SaveData RoundTrip(GameCore g,int slot)
    {g.PrepareSave();Check(SaveStore.Write(g.save,slot),"Actual SaveStore envelope writes route");string warning;var v=SaveStore.Read(slot,out warning);Check(v!=null&&warning==null,"Actual checksum/JSON restores route");return v;}
    private static void WaitWind(FrontierRuntime app,float maximum=1.4f)
    {
        var g=app.Game;int frames=0;while((!g.ShelterWindWarning||g.ShelterWindTimer>maximum)&&frames++<700){Update(app);Check(!g.Dying,"Reading natural wind survives ordinary damage");}
        Check(frames<700&&g.ShelterWindWarning,"Weather naturally announces a fixed lane");
    }
    private static void WindGeometry(FrontierRuntime app,string capture)
    {
        var g=app.Game;Check(g.ShelterWindWarning,"Renderer inspected a naturally prepared lane");Draw(app,capture);
        Rect status=TextBounds(g.L("横风已锁定 ","Crosswind locked "),true);
        if(g.toastTime>0)Check(!Overlap(status,TextBounds(g.toast)),"Actual locked-wind countdown and current toast remain in separate text bands");
        Rect bounds=(Rect)Call(app,"ShelterWindBounds");Point a=(Point)Call(app,"ScreenPoint",g.ShelterWindFrom),b=(Point)Call(app,"ScreenPoint",g.ShelterWindTo);float edge=.6f*36+3;
        Check(Math.Abs(bounds.x-(Math.Min(a.x,b.x)-edge))<.02f&&Math.Abs(bounds.y-(Math.Min(a.y,b.y)-edge))<.02f&&Math.Abs(bounds.width-(Math.Abs(a.x-b.x)+edge*2))<.02f&&Math.Abs(bounds.height-(Math.Abs(a.y-b.y)+edge*2))<.02f,"HUD bounds cover exact terrain-clipped wind lane and .6 contact radius");
        Check(GUI.textureCalls.Exists(t=>t.texture.name=="white"&&(t.rect.width>40||t.rect.height>40)&&Math.Min(t.rect.width,t.rect.height)<=3),"Minimal/reduced-motion still draws actual announced wind boundary");
        Point from=g.ShelterWindFrom,to=g.ShelterWindTo,center=g.ShelterWindCenter;
        // Presentation-only camera boundary fixture: an actual prepared lane is
        // placed behind the old left climate panel, without changing its world
        // capture or combat timer. The production HUD must repaint its boundary.
        Point camera=Get<Point>(app,"camera");
        bool horizontal=Math.Abs(to.x-from.x)>Math.Abs(to.y-from.y);
        Set(app,"camera",center+new Point((640-(horizontal?450:200))/36f,(400-(horizontal?180:400))/36f));
        Rect climate=new Rect(24,145,298,60),player=(Rect)Call(app,"PlayerHudBounds");
        Check(!(player.x<climate.x+climate.width&&player.x+player.width>climate.x&&player.y<climate.y+climate.height&&player.y+player.height>climate.y),"Presentation fixture isolates the wind from player/HUD overlap");
        Check((bool)Call(app,"ThreatOver",new Rect(24,145,298,60)),"A prepared wind behind the climate HUD triggers protected-panel fading");
        GUI.commands.Clear();GUI.textureCalls.Clear();Call(app,"DrawHud");
        Check(GUI.textureCalls.Exists(t=>t.rect.x==24&&t.rect.y==145&&t.rect.width==298&&t.rect.height==60&&Math.Abs(t.color.a-.12f)<.001f),"Actual climate panel fades to .12 alpha over the prepared wind alone");
        GUI.commands.Clear();GUI.textureCalls.Clear();Call(app,"DrawHudThreatWitness");
        Check(GUI.textureCalls.Exists(t=>Math.Abs(t.color.a-.96f)<.01f&&t.color.r>.93f&&t.color.g>.70f&&t.color.g<.73f&&t.color.b>.44f&&t.color.b<.48f),"Actual HUD repaint retains the distinct gold wind boundary above protected panels");
        Draw(app,"hud-witness-"+capture);Set(app,"camera",camera);
        Movement(app,new Point(0,0));for(int i=0;i<5;i++)Update(app);
        Check((g.ShelterWindFrom-from).Length<.001f&&(g.ShelterWindTo-to).Length<.001f&&(g.ShelterWindCenter-center).Length<.001f,"Prepared geometry is captured, not recentered while timer runs");
        Input.axes.Clear();Input.held.Clear();
    }
    public static void RealRouteInputClimateAndManualProgress(FrontierRuntime app)
    {
        Prepare(app);var g=app.Game;Check(g.ShelterReady&&!g.ShelterAccepted,"Earned original ending unlocks optional trial");
        g.CompanionStory(2);Original(g,"gift2","join2","close");Check(Array.FindIndex(g.conversation.choices,c=>c.action=="shelter_offer")<0,"Roster story cannot remotely accept physical keeper offer");g.Close();Accept(app);
        Walk(app,new Point(6,13.3f),"first fire");StartLeg(app,0,false,"first-leg-start");
        Walk(app,new Point(12,12.3f),"first roadside mark");WaitWind(app);WindGeometry(app,"wind-first-minimal");
        Point locked=g.ShelterWindCenter;Walk(app,locked,"naturally captured center");WaitWind(app);float health=g.save.health,cold=g.save.cold;
        int frames=0;while(g.ShelterWindWarning&&frames++<100)Update(app);
        Check(!g.Dying&&Math.Abs(health-g.save.health-8)<.03f&&g.save.cold-cold>7&&g.save.cold-cold<9,"Actual environmental contact costs exactly eight HP and adds seven cold only on accepted damage");acceptedWindHits++;
        WaitWind(app,.12f);health=g.save.health;cold=g.save.cold;float energy=g.save.energy;
        Movement(app,(g.ShelterWindTo-g.ShelterWindFrom).x!=0?new Point(0,1):new Point(1,0));Key(app,KeyCode.Space);dodges++;
        for(int i=0;i<15;i++)Update(app);Input.axes.Clear();Input.held.Clear();
        Check(g.save.health==health&&g.save.cold-cold<2&&g.save.energy<energy,"Real timed Space preserves mature dodge window and avoids weather HP/cold bonus");
        Mark(app,0,"mark-first");earnedPartial=Copy(RoundTrip(g,0));Arrival(app,0,"arrival-first");
        for(int i=1;i<3;i++){StartLeg(app,i,false,"start-leg"+i);Mark(app,i,"mark"+i);Arrival(app,i,"arrival"+i);}
        Check(!g.ShelterReported&&g.ShelterLeg<0,"Three genuine recorded walks leave publication manual");earnedRoute=Copy(RoundTrip(g,1));
        Check(g.enemies.Count==0&&g.Has("boss_7"),"Optional route spawns no foes and does not replace the old guardian");
        Console.WriteLine("PASS real keeper E/numbers and original choices, paid lighting, three manual WASD/record/arrival legs, naturally locked warning, accepted eight-HP/seven-cold contact and real Space avoidance; walk frames="+walkFrames+", contacts="+acceptedWindHits+", dodges="+dodges+"; NATIVE_UNVERIFIED");
    }
    private static void Report(FrontierRuntime app,bool upper,string capture)
    {
        Interact(app,Object(app.Game,"keeper"),true);Original(app.Game,"gift2","join2","close");Choose(app,"shelter_offer",capture+"-keeper");Choose(app,upper?"shelter_upper":"shelter_lower",capture+"-choice");
    }
    public static void BothEndingRewardsAndRealColdRoutes(FrontierRuntime app)
    {
        Check(earnedRoute!=null,"Prior real route supplies earned report fixture");
        foreach(bool upper in new[]{false,true})
        {
            Prepare(app);var g=app.Game;g.Load(Copy(earnedRoute));g.Start();int coins=g.save.coins,shards=g.save.shards,bond=g.save.relationships[2];int tea=g.save.tea;
            Report(app,upper,upper?"upper-ending":"lower-ending");Check(g.ShelterReported&&g.ShelterComplete&&g.save.coins==coins+35&&g.save.shards==shards+4&&g.save.relationships[2]==Math.Min(5,bond+1),"Either real ending pays exact once settlement");
            Check(g.save.tea==tea&&g.save.companion==0,"Ending neither creates tea nor swaps selected Mira");Check(!g.FinishShelterTrail(upper)&&g.save.coins==coins+35&&g.save.shards==shards+4,"Repeated settlement cannot duplicate reward");
            // Explicit spatial boundary fixtures inspect real climate ticks, never
            // invent route progress or award flags. Both points are outside fire.
            Position(app,upper?new Point(21,8):new Point(12,13));Check(g.ShelterColdProtected,"Chosen surveyed corridor actually protects the current feet");
            float cold=g.save.cold;for(int i=0;i<50;i++)Update(app);Check(Math.Abs(g.save.cold-cold)<.001f,"Sheltered corridor halts accumulation without manufacturing cooling");
            Position(app,upper?new Point(12,13):new Point(21,8));Check(!g.ShelterColdProtected,"Unchosen corridor retains ordinary exposure");cold=g.save.cold;for(int i=0;i<50;i++)Update(app);Check(Math.Abs(g.save.cold-cold-1.1f)<.03f,"Unchosen corridor keeps original 1.1-per-second cold");
            Position(app,new Point(6,13.3f));cold=g.save.cold;for(int i=0;i<25;i++)Update(app);Check(g.save.cold<=Math.Max(0,cold-3.95f),"Existing lit fire still cools at eight per second");
            Interact(app,Object(g,"keeper"),true);Original(g,"gift2","join2","close");Choose(app,"close","keeper-original-after"+(upper?"-upper":"-lower"));
            Check(g.save.mountainChoice==earnedRoute.mountainChoice&&g.Has("mountain_report")&&g.Has("personal_2"),"Sen route preserves original faction ending and personal story");
        }
        Console.WriteLine("PASS both real paged ending choices, exact once rewards, unchanged companion/tea/chapter, chosen corridor halt versus unchosen 1.1 cold and retained brazier cooling; NATIVE_UNVERIFIED");
    }
    public static void JournalWarningBilingualAndOriginalInteractions(FrontierRuntime app)
    {
        foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})foreach(bool minimal in new[]{false,true})
        {
            Prepare(app);var g=app.Game;g.Load(Copy(earnedRoute));g.Start();g.save.english=english;g.save.largeText=large;g.save.effectsQuality=minimal?0:2;g.save.reducedMotion=minimal;g.toastTime=0;
            Screen.width=minimal?960:1920;Screen.height=minimal?600:1080;Call(app,"Resize");Set(app,"camera",g.Position);
            string suffix=(english?"-en":"-zh")+(large?"-large":"-normal")+(minimal?"-960-minimal":"-1920-rich");
            Key(app,KeyCode.Tab);Draw(app);int quest=Array.IndexOf(GameCore.QuestIds,"shelter");float spacing=Math.Min(46,464f/18);
            string[] matureQuests={"main","shore","ally1","ally2","ally3","personal0","personal1","personal2","bounty","secrets","watch","signal","archive","reach","homeward","commission","shelter"};
            Check(quest==16&&GameCore.QuestIds.Length>=matureQuests.Length,"Shelter remains the seventeenth row after all sixteen mature quests");for(int i=0;i<matureQuests.Length;i++)Check(GameCore.QuestIds[i]==matureQuests[i],"Appended optional quests cannot renumber mature quest "+matureQuests[i]);GUI.queuedClick=new Rect(279,226+quest*spacing,321,spacing-7);Draw(app,"journal"+suffix);Check(!GUI.queuedClick.HasValue,"Actual seventeenth row accepts its click");Draw(app,"objective"+suffix);
            Check(Get<int>(app,"selectedQuest")==16&&HasText(g.L("下一步：","Next: ")+g.GetQuest("shelter").next),"Actual journal displays earned publication objective");
            var guide=g.GetQuest("shelter");Rect reward=TextBounds(guide.reward);
            foreach(var step in guide.steps)Check(!Overlap(TextBounds((step.done?"✓  ":"○  ")+step.text),reward),"Each actual progress step stays clear of the fixed reward band");
            Key(app,KeyCode.Escape);Report(app,english,"settlement"+suffix);Draw(app,"finished"+suffix);
            Interact(app,Object(g,"campfire"),true);Original(g,"fieldrest","tea","drinktea","companions","close","kitchen");Choose(app,"close","original-camp"+suffix);
            foreach(string kind in new[]{"bell0","bell1","bell2"}){int id=int.Parse(kind.Substring(4)),tone=g.save.bells[id];Interact(app,Object(g,kind),true);Check(g.mode=="world"&&g.save.bells[id]==(tone+1)%4,"Original E immediately rotates the mature bell "+kind);Draw(app,"original-"+kind+suffix);}
        }
        foreach(bool english in new[]{false,true})foreach(bool minimal in new[]{false,true})
        {
            Prepare(app);app.Game.save.english=english;app.Game.save.largeText=true;app.Game.save.effectsQuality=minimal?0:2;app.Game.save.reducedMotion=minimal;Screen.width=minimal?960:1920;Screen.height=minimal?600:1080;Call(app,"Resize");Accept(app);StartLeg(app,0,true);
            Walk(app,new Point(12,12.3f),"weather readout");WaitWind(app);WindGeometry(app,"warning"+(english?"-en":"-zh")+(minimal?"-960-minimal":"-1920-rich"));
        }
        Console.WriteLine("PASS actual seventeenth journal row, complete paged publication dialogues, three retained bell puzzles and six mature camp numbers, Chinese/English normal/large 960/1920 minimal/rich naturally captured wind GUI; NATIVE_UNVERIFIED");
    }
    public static void PartialSavePauseTravelDeathAndLegacy(FrontierRuntime app)
    {
        Check(earnedPartial!=null,"Prior actual first-mark walk supplies partial save fixture");Prepare(app);var g=app.Game;g.Load(Copy(earnedPartial));g.Start();
        Check(g.ShelterAccepted&&g.Has("shelter_mark_0")&&g.ShelterLeg<0&&!g.ShelterWindWarning&&!g.Has("shelter_leg_0"),"Production checksum round trip preserves marker but cancels unfinished volatile weather");StartLeg(app,0,true);Walk(app,new Point(12,12.3f),"resumed weather");WaitWind(app);
        Key(app,KeyCode.Escape);float time=g.save.totalTime,timer=g.ShelterWindTimer,cold=g.save.cold;for(int i=0;i<25;i++)Update(app);Check(g.mode=="pause"&&g.save.totalTime==time&&g.ShelterWindTimer==timer&&g.save.cold==cold,"Actual pause freezes weather and climate");Key(app,KeyCode.Escape);
        Call(app,"OnApplicationPause",true);time=g.save.totalTime;timer=g.ShelterWindTimer;for(int i=0;i<25;i++)Update(app);Check(g.save.totalTime==time&&g.ShelterWindTimer==timer,"Actual application pause freezes warning");Call(app,"OnApplicationPause",false);
        g.Enter(0);Check(g.ShelterAccepted&&g.Has("shelter_mark_0")&&g.ShelterLeg<0&&!g.ShelterWindWarning,"Travel retains mark while cancelling live attempt");g.Enter(7);g.enemies.Clear();Check(g.ShelterLeg<0,"Returning never automatically starts a storm");StartLeg(app,0,true);Walk(app,new Point(12,12.3f),"death boundary");WaitWind(app);
        // Explicit one-HP negative fixture tests the mature death system. Positive
        // route traversal above used ordinary HP, energy and damage throughout.
        g.save.health=1;int frames=0;while(!g.Dying&&frames++<700)Update(app);Check(g.Dying&&g.Has("recovery_pending"),"Real environmental contact enters existing death recovery");var death=RoundTrip(g,2);g.Load(death);g.Start();Check(g.save.room==0&&g.ShelterLeg<0&&!g.ShelterWindWarning&&g.ShelterAccepted&&g.Has("shelter_mark_0")&&!g.ShelterReported,"Death recovery preserves earned investigation and cannot award or auto restart trail");
        for(int version=1;version<=2;version++)
        {
            var legacy=new SaveData{version=version,room=7,stage=3,x=5,y=14,coins=137,kills=new[]{1,2,3,4,5,6,7,8,9,10,11,12},explored=new[]{11,12,13,14,15,16,17,18,19,20,21,22},flags=new List<string>{"intro","desert_report","mountain_report","personal_2","companion_2","homeward_complete","commission_reported","commission_water_first","signal_complete","sparring_complete"},battlePlans=new[]{new BattlePlan{recorded=true,weapon=0,relicFirst=-1,relicSecond=-1}}};
            File.WriteAllText(SaveStore.PathFor(2),JsonUtility.ToJson(legacy));string warning;var saved=SaveStore.Read(2,out warning);Check(saved!=null&&warning==null,"Actual legacy v"+version+" raw JSON reads");g.Load(saved);g.Start();Check(g.save.room==7&&g.save.coins==137&&!g.ShelterAccepted&&!g.ShelterComplete&&g.ShelterLeg<0,"Old JSON invents no trail or volatile weather");
            for(int i=0;i<12;i++)Check(g.save.kills[i]==i+1&&g.save.explored[i]==i+11,"All mature map counter prefixes remain");Check(g.Has("personal_2")&&g.Has("mountain_report")&&g.Has("homeward_complete")&&g.Has("commission_water_first")&&g.save.battlePlans[0].recorded,"Legacy stories and recorded battle plans remain intact");Draw(app,"legacy-v"+version);
        }
        Console.WriteLine("PASS earned-marker SaveStore/checksum with cancelled weather, manual restart, actual menu/application pause, travel/death recovery and v1/v2 story/map/battle-plan compatibility; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-shelter-check-"+Guid.NewGuid());Application.persistentDataPath=directory;captureFolder=args.Length>0?args[0]:null;if(captureFolder!=null)Directory.CreateDirectory(captureFolder);
        var app=new FrontierRuntime();int failed=0;
        try
        {
            Call(app,"Awake");Check(app.Game!=null,"Production Awake loads full campaign");
            foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[] {RealRouteInputClimateAndManualProgress,BothEndingRewardsAndRealColdRoutes,JournalWarningBilingualAndOriginalInteractions,PartialSavePauseTravelDeathAndLegacy})
                try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}
            app.EndVisualReview();Call(app,"OnDestroy");
        }
        finally{Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime Sen shelter trail: "+(4-failed)+"/4 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
