// Executes real Update/OnGUI and SaveStore; no native Editor or GPU acceptance.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Starfall;
using UnityEngine;

public static class RuntimeFrostOverlapChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static string captureFolder,capturePrefix="qa-frost-overlap";
    private static int captures;
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static bool Near(float a,float b,float tolerance=.006f){return Math.Abs(a-b)<=tolerance;}
    private static bool Same(Rect a,Rect b){return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height);}
    private static object Call(object target,string name,params object[] args){var method=target.GetType().GetMethod(name,Private|BindingFlags.Public);Check(method!=null,"Actual production entry exists: "+name);return method.Invoke(target,args);}
    private static T Get<T>(object target,string name){return (T)target.GetType().GetField(name,Private|BindingFlags.Public).GetValue(target);}
    private static void Set(object target,string name,object value){target.GetType().GetField(name,Private|BindingFlags.Public).SetValue(target,value);}
    private static void Update(FrontierRuntime app){Call(app,"Update");Input.keys.Clear();Input.buttonDown.Clear();}
    private static void Frames(FrontierRuntime app,int count){for(int i=0;i<count;i++)Update(app);}
    private static void Key(FrontierRuntime app,KeyCode key){Input.keys.Add(key);Update(app);}
    private static void Pointer(FrontierRuntime app,Point point){float scale=Get<float>(app,"scale");Input.mousePosition=new Vector3(Get<float>(app,"offsetX")+point.x*scale,Screen.height-Get<float>(app,"offsetY")-point.y*scale,0);}
    private static void Draw(FrontierRuntime app,string capture=null)
    {
        GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        if(captureFolder!=null&&capture!=null){var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();GUI.Export(captureFolder,capturePrefix+"-"+capture);captures++;GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);}
    }
    private static void Click(FrontierRuntime app,Rect rect){Draw(app);Check(GUI.buttonCalls.Exists(b=>Same(b.rect,rect)&&b.enabled),"Actual mouse action slot is enabled");GUI.queuedClick=rect;Draw(app);Check(!GUI.queuedClick.HasValue,"Actual OnGUI consumes displayed action click");}
    private static void Position(FrontierRuntime app,Point point){Check(app.Game.CanStand(point)&&app.SetVisualReviewPosition(point)&&(app.Game.Position-point).Length<.001f,"Exact legal authored terrain fixture");Set(app,"camera",point);}
    private static void Prepare(FrontierRuntime app)
    {
        Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;
        app.BeginVisualReview(2,0);app.VisualReviewPaused=false;var g=app.Game;g.enemies.Clear();g.projectiles.Clear();g.hazards.Clear();g.effects.Clear();g.fallen.Clear();
        foreach(string flag in new[]{"companion_2","technique_2","frost_overlap_mature_story"})if(!g.Has(flag))g.save.flags.Add(flag);
        g.save.english=true;g.save.showGuidance=false;g.save.largeText=false;g.save.reducedMotion=true;g.save.effectsQuality=0;g.save.companion=2;g.save.relationships=new[]{3,3,3};g.save.frontierEnding=0;g.save.technique=1;g.save.weapon=2;g.save.weaponStyles=new int[3];g.save.talents=new int[3];g.save.relics=new[]{-1,-1};g.save.health=g.save.maxHealth;g.save.energy=100;g.save.seed=1;g.save.totalTime=100;g.save.foodUntil=g.save.teaUntil=g.save.mealUntil=0;g.save.heat=g.save.cold=0;g.invulnerable=1000;g.toastTime=0;g.skillCooldown=g.companionCooldown=0;Set(g,"companionAttack",1000f);
        Position(app,new Point(12,14));Set(app,"applicationPaused",false);Set(app,"pointerSeen",false);Set(app,"pointerAimActive",false);Set(app,"controllerUsed",false);Set(app,"combatHudTime",0f);Set(app,"resumeTime",0f);Set(app,"arrivalTime",0f);Call(app,"Resize");Call(app,"ResetControllerAim",false);Pointer(app,new Point(900,390));
    }
    private static Enemy Foe(GameCore g,int id,Point point){Check(g.CanStand(point),"Durable test opponent uses legal authored ground");var e=new Enemy(id,0,point.x,point.y,9999){stun=1000,timer=1000,staggerRecovery=1000};g.enemies.Add(e);return e;}
    private static object Head(GameCore g){return Get<object>(g,"frost");}
    private static object Tail(GameCore g){object field=Head(g);Check(field!=null,"Real release creates a frost field");var next=field.GetType().GetField("next");while(next!=null&&next.GetValue(field)!=null)field=next.GetValue(field);return field;}
    private static int Pulses(object field){return Get<int>(field,"pulses");}
    private static float Age(object field){return Get<float>(field,"age");}
    private static ActorAction Start(FrontierRuntime app,bool skill,int device)
    {
        var g=app.Game;float energy=g.save.energy;
        if(device==2){Set(app,"combatHudTime",0f);bool compact=(bool)typeof(FrontierRuntime).GetProperty("CombatHud",Private).GetValue(app,null);Click(app,compact?(skill?new Rect(430,732,78,50):new Rect(685,732,78,50)):(skill?new Rect(354,699,109,75):new Rect(702,699,109,75)));}
        else Key(app,skill?(device==0?KeyCode.Q:KeyCode.JoystickButton3):(device==0?KeyCode.Z:KeyCode.JoystickButton5));
        var action=skill?g.playerAction:g.companionAction;Check(action!=null&&!action.released&&action.kind==(skill?"skill":"command"),"Actual "+(skill?"Q/Y/mouse skill":"Z/RB/mouse command")+" accepts original preparation");
        Check(Near(action.contact,skill?.28f:.2f)&&Near(action.duration,skill?.62f:.52f),"Original contact and recovery timing remains captured");
        Check(Near(g.save.energy,skill?energy-40:Math.Min(100,energy+(device==2?0:.36f))),"Original Q charges 40 energy once; command charges no energy (including the input frame's regeneration)");
        Check(Near(skill?g.skillCooldown:g.companionCooldown,skill?3.5f:18f),"Original 3.5/18-second cooldown is charged exactly once");
        Check(action.impact!=null&&action.impact.playerOwned==skill&&action.impact.weapon==2&&action.impact.masteryWeapon==2,"Actual release keeps independently captured player/companion ownership and weapon mastery");
        return action;
    }
    private static object Cast(FrontierRuntime app,bool skill,int device)
    {
        ActorAction action=Start(app,skill,device);for(int i=0;i<30&&!action.released;i++)Update(app);Check(action.released,"Original action reaches its contact through real Update");object field=Tail(app.Game);
        Check(ReferenceEquals(Get<ImpactProfile>(field,"impact"),action.impact)&&Near(Get<float>(field,"damage"),action.damage*.4f),"Each field keeps the releasing action's original .4 base damage and immutable impact");return field;
    }
    private static float Expected(float damage,int hits,int seed)
    {float total=0;for(int i=0;i<hits;i++){seed=(int)(((long)seed*1103515245+12345)&0x7fffffff);total+=damage*(seed/2147483647f<.06f?1.7f:1);}return total;}
    private static void AssertComplete(object first,object second)
    {Check(Pulses(first)==5&&Pulses(second)==5,"Both original releases must complete all five contacts independently; actual="+Pulses(first)+"+"+Pulses(second));}
    private static void RealInputsBothCastOrders(FrontierRuntime app)
    {
        foreach(int device in new[]{0,1,2})foreach(bool qFirst in new[]{true,false})
        {
            Prepare(app);var g=app.Game;Enemy e=Foe(g,61501,new Point(13,14));float damage=g.Damage*.4f;object first=Cast(app,qFirst,device);Frames(app,45);int firstBefore=Pulses(first);Check(firstBefore>=2&&firstBefore<5,"First accepted field is genuinely in flight before another cast");
            object second=Cast(app,!qFirst,device);Frames(app,225);AssertComplete(first,second);Check(Head(g)==null,"Completed releases retire naturally without residual field");Check(Near(9999-e.hp,Expected(damage,10,1),.025f),"Ten actual original contacts retain their seeded critical and total damage; actual="+(9999-e.hp));
            Check(Get<ImpactProfile>(first,"impact").playerOwned==qFirst&&Get<ImpactProfile>(second,"impact").playerOwned!=qFirst,"Cast order cannot replace either captured owner");Check(g.projectiles.Count==0&&g.enemies.Contains(e)&&g.save.kills[0]==0,"Overlap creates no extra projectiles, kills or rewards for a living opponent");
            Draw(app,"completed-"+device+"-"+qFirst);
        }
        Console.WriteLine("PASS real Q/Y/mouse skills and Z/RB/mouse commands in both cast orders, ten original pulse contacts, seeded total damage, captured owners, unchanged cost/contact/recovery/cooldowns and natural retirement; NATIVE_UNVERIFIED");
    }
    private static void DistinctCentersAndWallSight(FrontierRuntime app)
    {
        Prepare(app);var g=app.Game;Point a=new Point(12,14),b=new Point(22,14);Enemy firstFoe=Foe(g,61502,a+new Point(1,0)),secondFoe=Foe(g,61503,b+new Point(1,0));object first=Cast(app,true,0);Position(app,b);object second=Cast(app,false,1);
        Check((Get<Point>(first,"center")-a).Length<.001f&&(Get<Point>(second,"center")-b).Length<.001f,"Each actual cast fixes its own release center instead of following the moving caster");Frames(app,225);AssertComplete(first,second);float baseDamage=g.Damage*.4f;
        Check(firstFoe.hp<9999&&secondFoe.hp<9999&&9999-firstFoe.hp>=baseDamage*5-.02f&&9999-secondFoe.hp>=baseDamage*5-.02f&&9999-firstFoe.hp<=baseDamage*5*1.7f+.02f&&9999-secondFoe.hp<=baseDamage*5*1.7f+.02f,"Each non-overlapping center delivers only its own five local contacts");
        Prepare(app);g=app.Game;Point origin=new Point(),blocked=new Point();bool found=false;
        for(float y=3;!found&&y<21;y+=.5f)for(float x=3;!found&&x<33;x+=.5f){Point p=new Point(x,y);if(!g.CanStand(p))continue;for(int direction=0;!found&&direction<16;direction++){double angle=direction*Math.PI/8;Point q=p+new Point((float)Math.Cos(angle),(float)Math.Sin(angle))*3.5f;if(g.CanStand(q)&&!(bool)Call(g,"ClearAttackPath",p,q)){origin=p;blocked=q;found=true;}}}
        Check(found,"Authored collision supplies a legal opposite-side wall fixture");Position(app,origin);Enemy behind=Foe(g,61504,blocked);first=Cast(app,true,0);second=Cast(app,false,0);Frames(app,225);AssertComplete(first,second);Check(behind.hp==9999,"All ten original field contacts retain wall line-of-sight exclusion");
        Console.WriteLine("PASS independent fixed release centers and disjoint local recipients, plus ten actual contacts blocked by unmodified authored terrain sight; NATIVE_UNVERIFIED");
    }
    private static void Append(StringBuilder text,object value,HashSet<object> seen)
    {if(value==null){text.Append("null;");return;}Type type=value.GetType();if(type.IsPrimitive||type.IsEnum||value is string||value is decimal){text.Append(Convert.ToString(value,CultureInfo.InvariantCulture)).Append(';');return;}if(!type.IsValueType&&!seen.Add(value)){text.Append("ref;");return;}var seq=value as IEnumerable;if(seq!=null){foreach(object item in seq)Append(text,item,seen);text.Append("end;");return;}var fields=type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);Array.Sort(fields,(a,b)=>string.CompareOrdinal(a.Name,b.Name));foreach(var field in fields){text.Append(field.Name).Append('=');Append(text,field.GetValue(value),seen);}}
    private static string State(GameCore g){var result=new StringBuilder();Append(result,g,new HashSet<object>());return result.ToString();}
    private static void ActualGroundAt(FrontierRuntime app,Point center)
    {
        Point pixel=(Point)Call(app,"ScreenPoint",center);
        Check(GUI.textureCalls.Exists(c=>c.texture.name=="resource:Frontier/Art/Effects"&&Same(c.uv,new Rect(.5f,.5f,.25f,.25f))&&Near(c.rect.x+c.rect.width/2,pixel.x)&&Near(c.rect.y+c.rect.height/2,pixel.y)&&c.color.a>0),"Actual OnGUI draws authored frost frame 6 centered on its own release foot");
        Check(GUI.textureCalls.Exists(c=>c.texture==Texture2D.whiteTexture&&Same(c.rect,new Rect(pixel.x+144-2,pixel.y-2,4,4))&&c.color.a>0),"Actual original four-unit ground ring remains centered on each field, even at FX quality zero");
    }
    private static void PausePresentationAndIndependentExpiry(FrontierRuntime app)
    {
        foreach(bool reduced in new[]{false,true})foreach(int quality in new[]{0,1,2})
        {
            Prepare(app);var g=app.Game;g.save.reducedMotion=reduced;g.save.effectsQuality=quality;Point a=g.Position;object first=Cast(app,true,0);Frames(app,45);Position(app,a+new Point(3,0));object second=Cast(app,false,0);float age1=Age(first),age2=Age(second),world=g.save.totalTime;
            string snapshot=State(g);foreach(EventType type in new[]{EventType.Layout,EventType.Repaint}){Event.current.type=type;Draw(app,"two-centers-"+reduced+"-"+quality+"-"+type);ActualGroundAt(app,a);ActualGroundAt(app,a+new Point(3,0));Check(State(g)==snapshot,"Actual OnGUI is fully read-only with two live fields, all save/action/owner state");}Event.current.type=EventType.Repaint;
            Check(g.effects.Exists(e=>e.kind=="frost"&&(e.position-a).Length<.001f)&&g.effects.Exists(e=>e.kind=="frost"&&(e.position-(a+new Point(3,0))).Length<.001f),"Both actual centers retain their original ground-effect pulses");
            Key(app,KeyCode.I);age1=Age(first);age2=Age(second);world=g.save.totalTime;Frames(app,20);Draw(app);Check(Age(first)==age1&&Age(second)==age2&&g.save.totalTime==world,"Inventory pauses both original field clocks");Key(app,KeyCode.Escape);
            Call(app,"OnApplicationPause",true);age1=Age(first);age2=Age(second);world=g.save.totalTime;Frames(app,20);Draw(app);Check(Age(first)==age1&&Age(second)==age2&&g.save.totalTime==world,"Application pause freezes both casts and world");Call(app,"OnApplicationPause",false);
            app.VisualReviewPaused=true;Frames(app,20);Check(Age(first)==age1&&Age(second)==age2,"Review pause freezes both casts");app.VisualReviewPaused=false;
            for(int i=0;i<250&&Age(first)<4;i++)Update(app);Check(Pulses(first)==5&&Near(Age(first),4)&&Age(second)<4&&Head(g)!=null,"Older field retires at original four-second age without expiring the younger field");Frames(app,100);AssertComplete(first,second);Check(Head(g)==null,"Younger field independently reaches its own four-second expiry");
        }
        Console.WriteLine("PASS read-only actual Layout/Repaint with two visible ground centers, inventory/application/review pause, all effects-quality and reduced-motion policies, and independent four-second field expiry; NATIVE_UNVERIFIED");
    }
    private static void BoundariesAndLegacySave(FrontierRuntime app)
    {
        foreach(string boundary in new[]{"travel","checksum","legacy1","legacy2","death","training","newgame"})
        {
            Prepare(app);var g=app.Game;Cast(app,true,0);Cast(app,false,1);Check(Head(g)!=null,"Boundary begins with genuine active overlapping fields");g.save.coins=73;g.save.inventory[0]=11;g.PrepareSave();string raw=JsonUtility.ToJson(g.save);Check(!raw.Contains("FrostField")&&!raw.Contains("frostGeneration")&&!raw.Contains("playerAction"),"Persistent save schema contains no transient field chain");
            if(boundary=="travel")g.Enter(1);
            else if(boundary=="death"){g.invulnerable=0;Call(g,"Hurt",10000f,"environment",null);Check(g.Dying,"Actual lethal damage enters original death flow");Frames(app,60);Check(!g.Dying&&g.save.room==0,"Actual death/recovery returns home");}
            else if(boundary=="training"){g.StartPractice();Check(g.practicing,"Actual training starts");}
            else if(boundary=="newgame"){g.NewGame(2);g.Close();}
            else{string warning;SaveData loaded;if(boundary=="checksum"){Check(SaveStore.Write(g.save,0),"Actual checksum writer succeeds");loaded=SaveStore.Read(0,out warning);}else{var legacy=JsonUtility.FromJson<SaveData>(raw);legacy.version=boundary=="legacy1"?1:2;File.WriteAllText(SaveStore.PathFor(1),JsonUtility.ToJson(legacy));loaded=SaveStore.Read(1,out warning);}Check(loaded!=null&&warning==null,"Actual checksum/raw legacy save reader succeeds");g.Load(loaded);g.Start();Check(g.save.coins==73&&g.save.inventory[0]==11&&g.Has("frost_overlap_mature_story"),"Loading keeps original mature story/economy while discarding only transient casts");}
            Check(Head(g)==null,"Session boundary clears all active old fields: "+boundary);g.enemies.Clear();g.effects.Clear();g.projectiles.Clear();g.hazards.Clear();Set(g,"companionAttack",1000f);g.save.companion=-1;Enemy witness=Foe(g,61505,g.Position);int coins=g.save.coins,xp=g.save.xp,kills=g.save.kills[g.save.room];Frames(app,220);Draw(app);
            Check(witness.hp==9999&&!g.effects.Exists(e=>e.kind=="frost")&&Head(g)==null,"Old fields cannot damage the new-session witness or emit stale ground pulses: "+boundary);Check(g.save.coins==coins&&g.save.xp==xp&&g.save.kills[g.save.room]==kills,"Cleared old casts grant no phantom kill/reward: "+boundary);
        }
        Console.WriteLine("PASS actual travel, checksum/raw legacy v1/v2 loading, lethal recovery, practice and new-game boundaries discard both old casts without stale damage/rewards or save-schema/story/economy loss; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-frost-overlap-"+Guid.NewGuid());Application.persistentDataPath=directory;captureFolder=args.Length>0&&args[0]!="-"?args[0]:null;if(args.Length>1)capturePrefix=args[1];if(captureFolder!=null)Directory.CreateDirectory(captureFolder);var app=new FrontierRuntime();int failed=0;
        try{Call(app,"Awake");Check(app.Game!=null,"Actual Awake loads shipped campaign");foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{RealInputsBothCastOrders,DistinctCentersAndWallSight,PausePresentationAndIndependentExpiry,BoundariesAndLegacySave})try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}}
        finally{app.EndVisualReview();Call(app,"OnDestroy");Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime frost overlap: "+(4-failed)+"/4 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
