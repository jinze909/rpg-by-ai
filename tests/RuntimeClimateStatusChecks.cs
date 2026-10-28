// Actual shipped Update/OnGUI and SaveStore paths using offline Unity declarations.
// Spatial/earned-story fixtures are explicit; native input/GPU/play feel remain unverified.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Starfall;
using UnityEngine;

public static class RuntimeClimateStatusChecks
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static string folder,prefix;
    sealed class Label { public string text=null; public float[] r=null,color=null; public int size=0; }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static bool Near(float a,float b,float tolerance=.004f){return Math.Abs(a-b)<=tolerance;}
    static object Call(object obj,string name,params object[] args){var m=obj.GetType().GetMethod(name,Private|BindingFlags.Public);Check(m!=null,"Production entry point exists: "+name);return m.Invoke(obj,args);}
    static void Set(object obj,string name,object value){obj.GetType().GetField(name,Private|BindingFlags.Public).SetValue(obj,value);}
    static T Get<T>(object obj,string name){return (T)obj.GetType().GetField(name,Private|BindingFlags.Public).GetValue(obj);}
    static void Update(FrontierRuntime app){Call(app,"Update");Input.keys.Clear();Input.buttonDown.Clear();}
    static void Frames(FrontierRuntime app,int n){for(int i=0;i<n;i++)Update(app);}
    static void Key(FrontierRuntime app,KeyCode key){Input.keys.Add(key);Update(app);}
    static void Draw(FrontierRuntime app,string capture=null)
    {
        GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        if(folder!=null&&capture!=null){var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();GUI.Export(folder,prefix+"-"+capture);GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);}
    }
    static void Pointer(FrontierRuntime app,Point point){float scale=Get<float>(app,"scale");Input.mousePosition=new Vector3(Get<float>(app,"offsetX")+point.x*scale,Screen.height-Get<float>(app,"offsetY")-point.y*scale,0);}
    static void Position(FrontierRuntime app,Point point){Check(app.Game.CanStand(point)&&app.SetVisualReviewPosition(point),"Explicit fixture uses legal full-footprint footing "+point.x+","+point.y);Set(app,"camera",app.Game.Position);}
    static void Prepare(FrontierRuntime app,int room=6)
    {
        Input.Clear();GUI.queuedClick=null;GUI.enabled=true;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;Event.current.type=EventType.Repaint;Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;
        app.BeginVisualReview(0,room);app.VisualReviewPaused=false;var g=app.Game;g.enemies.Clear();g.projectiles.Clear();g.hazards.Clear();g.effects.Clear();g.fallen.Clear();g.save.companion=-1;g.save.english=true;g.save.largeText=false;g.save.showGuidance=false;g.save.reducedMotion=true;g.save.effectsQuality=0;g.save.talents=new int[3];g.save.relics=new[]{-1,-1};g.save.energy=100;g.save.health=g.save.maxHealth;g.save.totalTime=100;g.save.heat=g.save.cold=40;g.save.foodUntil=g.save.teaUntil=g.save.mealUntil=0;g.save.tea=8;g.save.inventory[6]=8;g.save.inventory[0]=10;g.hitStop=g.toastTime=g.invulnerable=g.playerHitTime=0;
        g.save.flags.RemoveAll(f=>f.StartsWith("warm_")||f.StartsWith("shelter")||f=="mountain_report"||f=="personal_2"||f=="companion_2");
        Set(app,"applicationPaused",false);Set(app,"pointerSeen",false);Set(app,"pointerAimActive",false);Set(app,"controllerUsed",false);Set(app,"combatHudTime",0f);Set(app,"arrivalTime",0f);Set(app,"resumeTime",0f);Call(app,"Resize");Position(app,room==6?new Point(14,17.3f):new Point(12,13));Pointer(app,new Point(900,390));
    }
    static object Reading(GameCore g){return Call(g,"GetClimateReading");}
    static void Read(GameCore g,string protection,float rate)
    {object r=Reading(g);Check(r!=null&&Get<bool>(r,"cold")== (g.save.room==7)&&Get<string>(r,"protection")==protection&&Near(Get<float>(r,"rate"),rate),"Live readonly climate source/rate matches actual surroundings: "+protection+"/"+rate);Check(Near(Get<float>(r,"amount"),g.save.room==7?g.save.cold:g.save.heat),"Reading uses current actual climate balance");}
    static Label AssertHud(FrontierRuntime app)
    {
        var g=app.Game;string text=g.Conditions();Check(text.Split('\n').Length==2,"Actual exploration climate states current protection and threshold or active harm warning on two lines: "+text);
        string command=GUI.commands.Find(c=>c.Contains("\"text\":"+GUI.S(text)));Check(command!=null,"Actual OnGUI exposes authoritative climate Conditions");var label=JsonUtility.FromJson<Label>(command);Check(Near(label.r[0],39)&&Near(label.r[1],156)&&Near(label.r[2],271)&&Near(label.r[3],42),"Existing climate card text geometry is unchanged");Check(label.size==(g.save.largeText?14:13),"Original large-text policy is respected");
        Color color=Get<Color>(app,g.save.room==7?g.save.cold>70?"gold":"muted":g.save.heat>=85?"gold":"muted");Check(Near(label.color[0],color.r)&&Near(label.color[1],color.g)&&Near(label.color[2],color.b),"Danger tint matches original strict cold70 / inclusive heat85 thresholds");return label;
    }
    static void Walk(FrontierRuntime app,Point goal)
    {
        var g=app.Game;List<Point> path=g.NavigationPath(goal);Check(path.Count>0,"Original full-footprint navigation reaches climate source");int frames=0;
        while((g.Position-goal).Length>.12f&&frames++<1200){while(path.Count>0&&(path[0]-g.Position).Length<.12f)path.RemoveAt(0);Point next=path.Count>0?path[0]:goal,move=(next-g.Position).Normalized,before=g.Position;float speed=g.Speed;Input.axes["Horizontal"]=move.x;Input.axes["Vertical"]=-move.y;Input.held.Clear();if(move.x>.05f)Input.held.Add(KeyCode.D);if(move.x<-.05f)Input.held.Add(KeyCode.A);if(move.y>.05f)Input.held.Add(KeyCode.S);if(move.y<-.05f)Input.held.Add(KeyCode.W);Update(app);Check(g.CanStand(g.Position)&&(g.Position-before).Length<=speed*.0201f+.001f,"Actual WASD climate traversal keeps original speed and full-footprint collision");}
        Input.Clear();Check(frames<1200,"Actual WASD reaches climate source");Set(app,"camera",g.Position);
    }
    static void Choice(FrontierRuntime app,string action)
    {var g=app.Game;Check(g.mode=="dialog"&&g.conversation!=null,"Actual E opens original climate-service dialogue");int i=Array.FindIndex(g.conversation.choices,c=>c.action==action);Check(i>=0&&i<9,"Original numbered climate action exists: "+action);Draw(app);for(int n=0;!(bool)Call(app,"DialogueChoicesVisible")&&n<20;n++){Key(app,KeyCode.Return);Draw(app);}Check((bool)Call(app,"DialogueChoicesVisible"),"Read original dialogue before choosing climate action");Key(app,(KeyCode)((int)KeyCode.Alpha1+i));Draw(app);}
    static void RatesSourcesAndRealInteractions(FrontierRuntime app)
    {
        Prepare(app);var g=app.Game;Draw(app);AssertHud(app);Read(g,"none",1.3f);float before=g.save.heat;Frames(app,50);Check(Near(g.save.heat-before,1.3f,.015f),"Actual desert Update retains original +1.3/s");
        Walk(app,new Point(9.5f,17));Read(g,"shade",-10);before=g.save.heat;Frames(app,10);Check(Near(g.save.heat-before,-2,.015f),"Real walk into oasis shade retains original -10/s cooling");Walk(app,new Point(12,17));Read(g,"none",1.3f);
        Position(app,new Point(14,17.3f));Key(app,KeyCode.E);Choice(app,"race");Read(g,"race",-10);before=g.save.heat;Frames(app,10);Check(g.racing&&Near(g.save.heat-before,-2,.015f),"Real first launch retains original race cooling");
        Prepare(app,7);g=app.Game;Read(g,"none",1.1f);before=g.save.cold;Frames(app,50);Check(Near(g.save.cold-before,1.1f,.015f),"Actual mountain Update retains original +1.1/s");Position(app,new Point(6,13.3f));Read(g,"none",1.1f);Key(app,KeyCode.E);int wood=g.save.inventory[0];Choice(app,"warm:brazier0");Check(g.Has("warm_brazier0")&&g.save.inventory[0]==wood-2,"Real E/numeric lighting retains two timber cost and existing warm flag");g.Close();Read(g,"brazier",-8);before=g.save.cold;Frames(app,10);Check(Near(g.save.cold-before,-1.6f,.015f),"Real lit fire retains original -8/s warming");Walk(app,new Point(12,13));Read(g,"none",1.1f);
        foreach(string flag in new[]{"mountain_report","personal_2","companion_2","shelter_accepted","shelter_leg_0","shelter_leg_1","shelter_leg_2","sheltered_reported","sheltered_lower"})if(!g.Has(flag))g.save.flags.Add(flag);
        // Explicit earned-story fixture: report rewards/story interactions are independently covered by RuntimeShelterTrailChecks.
        Check(g.ShelterColdProtected,"Earned lower corridor protects actual current legal feet");g.save.cold=73;Read(g,"shelter",0);Frames(app,50);Check(Near(g.save.cold,73)&&g.Speed<4.5f,"Shelter halts accumulation without pretending to remove retained cold or slow");
        int tea=g.save.tea;Key(app,KeyCode.T);Draw(app);AssertHud(app);Read(g,"tea",-8);Check(g.save.tea==tea-1&&g.save.cold==0&&g.save.heat==0&&Near(g.save.teaUntil-g.save.totalTime,180),"Real T keeps one tea/reset/full180s and overrides shelter heating source");
        Prepare(app);g=app.Game;tea=g.save.tea;Key(app,KeyCode.T);Read(g,"tea",-10);Check(g.save.tea==tea-1&&g.save.heat==0,"Real desert T names tea cooling");
        Console.WriteLine("PASS actual Update/WASD oasis enter/exit, +1.3/+1.1/-10/-8 rates, E/numeric two-timber lit fire, first race launch, earned-corridor hold with retained cold/slow and real T one-tea180s reset/source priority; NATIVE_UNVERIFIED");
    }
    static void ThresholdsMovementAndHarm(FrontierRuntime app)
    {
        Prepare(app,7);var g=app.Game;Draw(app);AssertHud(app);g.save.cold=70;Read(g,"none",1.1f);Check(!Get<bool>(Reading(g),"slowed")&&Near(g.Speed,4.5f),"Exactly70 cold has no movement slow");g.save.cold=70.001f;Check(Get<bool>(Reading(g),"slowed")&&Near(g.Speed,3.95f),"Strictly over70 retains original .55 movement penalty");
        foreach(float cold in new[]{69f,71f}){g.save.cold=cold;g.save.energy=10;Position(app,new Point(12,13));Point p=g.Position;Input.axes["Horizontal"]=1;Input.held.Add(KeyCode.D);Update(app);Input.Clear();Check(Near((g.Position-p).Length,(cold>70?3.95f:4.5f)*.02f,.002f)&&Near(g.save.energy-10,(cold>70?10:14)*.02f,.002f),"Actual held movement and energy recovery retain cold70 penalties");}
        foreach(int room in new[]{6,7})foreach(bool harmful in new[]{false,true})
        {
            Prepare(app,room);g=app.Game;float value=room==6?harmful?85:84.96f:harmful?90:89.97f;if(room==6)g.save.heat=value;else g.save.cold=value;float health=g.save.health;Set(g,"climateTimer",.001f);g.invulnerable=0;Draw(app);AssertHud(app);Check(Get<bool>(Reading(g),"harmful")==harmful,"Reading matches original inclusive85/90 harm threshold");Update(app);Check(Near(health-g.save.health,harmful?4:0),"Actual original one-second climate timer keeps exactly4 environmental damage at inclusive85/90");if(harmful){float after=g.save.health;Frames(app,20);Check(g.save.health==after,"Climate feedback never adds extra damage ticks");}
        }
        Console.WriteLine("PASS strict cold>70 .55 walk and -4/s recovery penalty, actual held-WASD distance/energy, inclusive heat85/cold90 original4 damage and unchanged pulse timing; NATIVE_UNVERIFIED");
    }
    static void PausedClockAndLegacySaves(FrontierRuntime app)
    {
        Prepare(app,7);var g=app.Game;Draw(app);AssertHud(app);Key(app,KeyCode.I);float cold=g.save.cold,time=g.save.totalTime;Frames(app,15);Check(g.mode=="inventory"&&g.save.cold==cold&&g.save.totalTime==time,"Actual inventory freezes original climate/world clocks");Key(app,KeyCode.Escape);Call(app,"OnApplicationPause",true);cold=g.save.cold;time=g.save.totalTime;Frames(app,10);Check(g.save.cold==cold&&g.save.totalTime==time,"Application pause cannot tick climate");Call(app,"OnApplicationPause",false);app.VisualReviewPaused=true;Frames(app,10);Check(g.save.cold==cold&&g.save.totalTime==time,"Paused review cannot tick climate");app.VisualReviewPaused=false;g.hitStop=.12f;Frames(app,3);Check(g.save.cold==cold&&g.save.totalTime==time,"Actual hit-stop freezes original climate pulse");g.hitStop=0;Update(app);Check(g.save.cold>cold,"Original climate resumes after hit-stop");
        g.save.cold=72.5f;g.save.heat=21.25f;g.save.teaUntil=g.save.totalTime+150;g.save.coins=137;g.save.inventory[0]=9;g.PrepareSave();Check(SaveStore.Write(g.save,0),"Original checksum writer accepts climate save");string warning;SaveData saved=SaveStore.Read(0,out warning);Check(saved!=null&&warning==null,"Original checksum reader accepts climate save");string raw=JsonUtility.ToJson(saved);Check(!raw.Contains("ClimateReading")&&!raw.Contains("ClimateWarning"),"Derived feedback adds no persisted fields");
        foreach(int version in new[]{1,2}){SaveData old=JsonUtility.FromJson<SaveData>(raw);old.version=version;File.WriteAllText(SaveStore.PathFor(1),JsonUtility.ToJson(old));SaveData loaded=SaveStore.Read(1,out warning);Check(loaded!=null&&warning==null,"Actual raw legacy v"+version+" reads");g.Load(loaded);g.Start();g.enemies.Clear();g.hazards.Clear();Draw(app);AssertHud(app);Read(g,"tea",-8);Check(g.save.cold==72.5f&&g.save.heat==21.25f&&g.save.teaUntil==saved.teaUntil&&g.save.coins==137&&g.save.inventory[0]==9,"Legacy v"+version+" preserves exact climate/deadline/economy without new fields");}
        g.Enter(0);Check(Reading(g)==null,"Outside climate regions no exposure snapshot is invented");
        Console.WriteLine("PASS real inventory/application/review/hit-stop freeze and resume, original checksum save, raw v1/v2 exact climate/deadline/economy restoration and unchanged schema/outside-room absence; NATIVE_UNVERIFIED");
    }
    static void Append(StringBuilder s,object value,HashSet<object> seen){if(value==null){s.Append("null;");return;}Type type=value.GetType();if(type.IsPrimitive||type.IsEnum||value is string||value is decimal){s.Append(Convert.ToString(value,CultureInfo.InvariantCulture)).Append(';');return;}if(!type.IsValueType&&!seen.Add(value)){s.Append("ref;");return;}var sequence=value as IEnumerable;if(sequence!=null){foreach(object item in sequence)Append(s,item,seen);s.Append("end;");return;}var fields=type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);Array.Sort(fields,(a,b)=>string.CompareOrdinal(a.Name,b.Name));foreach(var field in fields){s.Append(field.Name).Append('=');Append(s,field.GetValue(value),seen);}}
    static string State(GameCore g){var s=new StringBuilder();Append(s,g,new HashSet<object>());return s.ToString();}
    static string Difference(string before,string after){int i=0;while(i<before.Length&&i<after.Length&&before[i]==after[i])i++;int start=Math.Max(0,i-90);return " first change at "+i+" before="+before.Substring(start,Math.Min(220,before.Length-start))+" after="+after.Substring(start,Math.Min(220,after.Length-start));}
    static void BilingualLayoutsPurityAndInput(FrontierRuntime app)
    {
        Prepare(app);Draw(app);AssertHud(app);
        foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})foreach(int width in new[]{960,1280})foreach(int state in new[]{0,1,2,3})
        {
            Prepare(app,state<2?6:7);var g=app.Game;g.save.english=english;g.save.largeText=large;g.save.heat=state==1?85:40;g.save.cold=state==2?90:73;g.save.foodUntil=g.save.totalTime+60;
            if(state==0)Position(app,new Point(9.5f,17));if(state==3){foreach(string flag in new[]{"mountain_report","personal_2","companion_2","shelter_accepted","shelter_leg_0","shelter_leg_1","shelter_leg_2","sheltered_reported","sheltered_lower"})if(!g.Has(flag))g.save.flags.Add(flag);Check(g.ShelterColdProtected,"Layout uses genuine earned corridor geometry");}
            Screen.width=width;Screen.height=width*5/8;Call(app,"Resize");Draw(app,"layout-"+(english?"en":"zh")+(large?"-large":"-normal")+"-"+width+"-"+state);AssertHud(app);Check(GUI.commands.Exists(c=>c.Contains(english?"STEW BOOST ":"炖汤强化 ")),"Existing provision status coexists with climate status");Check(!GUI.buttonCalls.Exists(b=>b.rect.x==24&&b.rect.y==145),"Climate text remains passive");
        }
        // The original world renderer fills its light-ray scratch buffer on its first draw.
        // Warm that unchanged renderer before observing all Core state, including paid actions.
        Prepare(app);Draw(app);var game=app.Game;Key(app,KeyCode.J);ActorAction action=game.playerAction;Check(action!=null&&!action.released,"Actual J prepares paid original attack");game.skillCooldown=.12f;game.Skill(new Point(1,0));string before=State(game);float clock=Get<float>(app,"clock");var matrix=Matrix4x4.TRS(new Vector3(13,21,0),Quaternion.Euler(0,0,11),new Vector3(1.25f,1.25f,1));var tint=new Color(.3f,.4f,.5f,.6f);
        foreach(EventType evt in new[]{EventType.Layout,EventType.Repaint}){Event.current.type=evt;GUI.matrix=matrix;GUI.color=tint;GUI.enabled=false;Draw(app);Check(State(game)==before&&Get<float>(app,"clock")==clock&&ReferenceEquals(action,game.playerAction),"Actual repeated Layout/Repaint never mutates climate/save/queues/paid-action or clock"+Difference(before,State(game)));Check(GUI.matrix.Equals(matrix)&&GUI.color.Equals(tint)&&!GUI.enabled,"OnGUI restores caller state");}
        Event.current.type=EventType.Repaint;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Prepare(app);game=app.Game;Draw(app);Point panel=new Point(100,170);Check((bool)Call(app,"PointerOverInterface",panel),"Original climate-panel pointer protection remains");Pointer(app,panel);Input.buttons.Add(0);Update(app);Input.Clear();Check(game.playerAction==null,"Passive climate panel retains original mouse attack block");Pointer(app,new Point(900,390));Input.buttons.Add(0);Update(app);Input.Clear();Check(game.playerAction!=null&&game.playerAction.kind=="attack","Mouse attack outside unchanged panel remains available");Set(app,"combatHudTime",3f);Draw(app);Check(!GUI.commands.Exists(c=>c.Contains("\"text\":"+GUI.S(game.Conditions()))),"New exploration feedback does not replace compact combat climate");
        Console.WriteLine("PASS32 actual zh/en normal/large960/1280 shade/heat-harm/cold-harm/shelter+provision captures, original bounds/font/tint, passive panel pointer rules/world attack and pure GUI with paid action/queues; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-climate-status-"+Guid.NewGuid());Application.persistentDataPath=directory;folder=args.Length>0&&args[0]!="-"?args[0]:null;prefix=args.Length>1?args[1]:"qa-climate-status";if(folder!=null)Directory.CreateDirectory(folder);var app=new FrontierRuntime();int failed=0;
        try{Call(app,"Awake");Check(app.Game!=null,"Actual Awake loads shipped campaign");foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{RatesSourcesAndRealInteractions,ThresholdsMovementAndHarm,PausedClockAndLegacySaves,BilingualLayoutsPurityAndInput})try{test(app);}catch(Exception e){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(e.InnerException??e).Message);}}
        finally{app.EndVisualReview();Call(app,"OnDestroy");Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;GUI.enabled=true;GUI.color=Color.white;GUI.matrix=Matrix4x4.identity;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime climate status: "+(4-failed)+"/4 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
