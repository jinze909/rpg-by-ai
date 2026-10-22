// Real production Update, OnGUI, combat releases and checksum SaveStore.
// Enemy HP/positions and display-only camera/timers are fixtures, not native evidence.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Starfall;
using UnityEngine;

public static class RuntimeCombatLootChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string captureFolder;
    private static int captures, enemyId = 59100;
    private sealed class Label { public string text = null; public float[] r = null, color = null; public int size = 0; }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static bool Near(float a, float b, float tolerance = .003f) { return Math.Abs(a-b) <= tolerance; }
    private static bool Same(Rect a, Rect b) { return Near(a.x,b.x) && Near(a.y,b.y) && Near(a.width,b.width) && Near(a.height,b.height); }
    private static bool Overlap(Rect a, Rect b) { return a.x < b.x+b.width && b.x < a.x+a.width && a.y < b.y+b.height && b.y < a.y+a.height; }
    private static bool Inside(Rect a, Rect b) { return a.x >= b.x && a.y >= b.y && a.x+a.width <= b.x+b.width && a.y+a.height <= b.y+b.height; }
    private static object Call(object target, string name, params object[] args)
    { var method=target.GetType().GetMethod(name,Private|BindingFlags.Public); Check(method!=null,"Production entry point exists: "+name); return method.Invoke(target,args); }
    private static void Set(object target, string name, object value)
    { var field=target.GetType().GetField(name,Private); Check(field!=null,"Fixture field exists: "+name); field.SetValue(target,value); }
    private static T Get<T>(object target, string name) { return (T)target.GetType().GetField(name,Private).GetValue(target); }
    private static object Public(object target, string name)
    { var property=target.GetType().GetProperty(name); Check(property!=null && !property.CanWrite,"Read-only production loot API exists: "+name); return property.GetValue(target,null); }
    private static object Receipt(GameCore g) { return Public(g,"RecentCombatLoot"); }
    private static float Remaining(GameCore g) { return (float)Public(g,"CombatLootTimeRemaining"); }
    private static T Field<T>(object receipt, string name)
    { Check(receipt!=null,"A real combat transaction produced a receipt"); var field=receipt.GetType().GetField(name); Check(field!=null && field.IsInitOnly,"Receipt field is immutable: "+name); return (T)field.GetValue(receipt); }
    private static long Item(object receipt, int item) { return (long)Call(receipt,"ItemCount",item); }
    private static bool Visible(FrontierRuntime app)
    { var property=typeof(FrontierRuntime).GetProperty("CombatLootVisible",Private); Check(property!=null,"Live loot presentation exists"); return (bool)property.GetValue(app,null); }
    private static Rect Bounds(FrontierRuntime app) { return (Rect)Call(app,"CombatLootBounds"); }
    private static void Update(FrontierRuntime app) { Call(app,"Update"); Input.keys.Clear(); Input.buttonDown.Clear(); }
    private static void Frames(FrontierRuntime app, int frames) { for(int i=0;i<frames;i++)Update(app); }
    private static void Key(FrontierRuntime app, KeyCode key) { Input.keys.Add(key); Update(app); }
    private static void ClearDraw() { GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); }
    private static void Draw(FrontierRuntime app, string capture = null)
    {
        ClearDraw(); Call(app,"OnGUI");
        if(captureFolder!=null && capture!=null)
        {
            var commands=GUI.commands.ToArray(); var textures=GUI.textureCalls.ToArray(); var buttons=GUI.buttonCalls.ToArray();
            GUI.Export(captureFolder,"qa-combat-loot-"+capture); captures++;
            GUI.commands.AddRange(commands); GUI.textureCalls.AddRange(textures); GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static bool Has(string text) { return GUI.commands.Exists(c=>c.Contains("\"text\":"+GUI.S(text))); }
    private static bool HasPart(string text) { return GUI.commands.Exists(c=>c.Contains("\"k\":\"text\"") && c.Contains(text)); }
    private static string Header(GameCore g, long coins, long xp, bool unbanked=false)
    { return g.L(unbanked?"未结算":"战利品",unbanked?"UNBANKED":"LOOT")+"  +"+coins.ToString(CultureInfo.InvariantCulture)+g.L(" 硬币"," coins")+" · +"+xp.ToString(CultureInfo.InvariantCulture)+" XP"; }
    private static Label Text(string text)
    { string command=GUI.commands.Find(c=>c.Contains("\"text\":"+GUI.S(text))); Check(command!=null,"Actual OnGUI contains truthful text: "+text); return JsonUtility.FromJson<Label>(command); }
    private static Rect LabelRect(Label label) { return new Rect(label.r[0],label.r[1],label.r[2],label.r[3]); }
    private static void Pointer(FrontierRuntime app, Point point)
    { float scale=Get<float>(app,"scale"); Input.mousePosition=new Vector3(Get<float>(app,"offsetX")+point.x*scale,Screen.height-Get<float>(app,"offsetY")-point.y*scale,0); }
    private static void Aim(FrontierRuntime app, Point world) { Pointer(app,(Point)Call(app,"ScreenPoint",world)); Set(app,"pointerAimActive",true); }
    private static Point ClearFoot(GameCore g)
    {
        for(int y=4;y<g.Map.rows.Length-4;y++)for(int x=4;x<g.Map.rows[0].Length-4;x++)
        {
            Point p=new Point(x+.5f,y+.5f); bool clear=g.CanStand(p);
            for(int dir=0;clear&&dir<8;dir++)for(float step=.25f;clear&&step<=2.5f;step+=.25f)
                clear=g.CanStand(p+new Point((float)Math.Cos(dir*Math.PI/4),(float)Math.Sin(dir*Math.PI/4))*step);
            if(clear)return p;
        }
        throw new Exception("A real shipped clear combat footprint exists");
    }
    private static void Prepare(FrontierRuntime app, int profession=0)
    {
        Input.Clear(); GUI.queuedClick=null; GUI.matrix=Matrix4x4.identity; GUI.color=Color.white; GUI.enabled=true;
        Screen.width=1280; Screen.height=800; Time.unscaledDeltaTime=.02f; Event.current.type=EventType.Repaint;
        app.BeginVisualReview(profession,0); app.VisualReviewPaused=false; var g=app.Game;
        g.enemies.Clear(); g.projectiles.Clear(); g.effects.Clear(); g.hazards.Clear(); g.fallen.Clear();
        g.save.companion=-1; g.save.english=true; g.save.largeText=false; g.save.showGuidance=false;
        g.save.reducedMotion=true; g.save.effectsQuality=0; g.save.energy=100; g.save.health=g.save.maxHealth;
        g.save.relics=new[]{-1,-1}; g.save.talents=new int[3]; g.save.totalTime=100;
        g.save.foodUntil=g.save.teaUntil=g.save.mealUntil=0; g.save.technique=0; g.save.level=30; g.save.xp=0;
        g.save.frontierEnding=0; g.toastTime=0; g.guardTime=0; g.invulnerable=100;
        Check(app.SetVisualReviewPosition(ClearFoot(g)),"Staged combat feet use actual campaign collision");
        Set(app,"applicationPaused",false); Set(app,"pointerSeen",false); Set(app,"pointerAimActive",false); Set(app,"controllerUsed",false);
        Set(app,"combatHudTime",0f); Set(app,"resumeTime",0f); Set(app,"arrivalTime",0f); Set(app,"camera",g.Position); Call(app,"Resize"); Aim(app,g.Position+new Point(2,0));
    }
    private static Enemy Target(GameCore g, int kind=0, bool boss=false, float offset=1.3f)
    {
        Point p=g.Position+new Point(offset,0); Check(g.CanStand(p),"Staged victim stands on actual ground");
        var enemy=new Enemy(++enemyId,kind,p.x,p.y,1,boss){stun=100,timer=9999}; g.enemies.Add(enemy); return enemy;
    }
    private static void WaitForKill(FrontierRuntime app, Enemy enemy, int limit=130)
    { int frame=0; while(app.Game.enemies.Contains(enemy)&&frame++<limit)Update(app); Check(!app.Game.enemies.Contains(enemy),"Real accepted action kills its staged low-HP target"); }
    private static object Kill(FrontierRuntime app, string route="Q", int kind=0, bool boss=false)
    {
        var g=app.Game; Enemy enemy=Target(g,kind,boss); Aim(app,enemy.Position);
        if(route=="companion")
        {
            if(!g.Has("companion_1"))g.save.flags.Add("companion_1"); g.save.companion=1; g.save.relationships[1]=3;
            g.companionPosition=g.Position; Set(g,"companionAttack",100f); g.companionCooldown=0; Key(app,KeyCode.Z);
            Check(g.companionAction!=null && !g.companionAction.released,"Actual Z starts a prepared companion command");
        }
        else
        {
            if(route=="frost") { g.save.technique=1; if(!g.Has("technique_2"))g.save.flags.Add("technique_2"); }
            Key(app,route=="J"?KeyCode.J:KeyCode.Q);
            Check(g.playerAction!=null && !g.playerAction.released,"Actual J/Q starts a prepared player action");
        }
        Check(Receipt(g)==null,"Prepared attacks cannot invent early rewards"); WaitForKill(app,enemy); return Receipt(g);
    }
    private static List<GUI.TextureCall> Icons(Rect bounds)
    { return GUI.textureCalls.FindAll(c=>c.texture.name=="resource:Frontier/Art/Icons" && Inside(c.rect,bounds)); }
    private static Rect IconUv(int frame) { return new Rect(frame%4/4f,1-(frame/4+1)/4f,.25f,.25f); }
    private static void Exact(object receipt, long coins, long xp, long shards=0, bool unbanked=false)
    { Check(Field<long>(receipt,"coins")==coins && Field<long>(receipt,"xp")==xp && Field<long>(receipt,"shards")==shards && Field<bool>(receipt,"unbanked")==unbanked,"Receipt equals the rewards actually committed to the mature transaction"); }
    private static void ActualTransactionsAndMaterials(FrontierRuntime app)
    {
        foreach(string route in new[]{"J","Q","companion","frost"})
        {
            Prepare(app,route=="frost"?2:0); var g=app.Game; int coins=g.save.coins, xp=g.save.xp, herb=g.save.inventory[2], kills=g.save.kills[0];
            object receipt=Kill(app,route); Exact(receipt,6,12); Check(Item(receipt,2)==1 && Item(receipt,-1)==0 && Item(receipt,100)==0,"Actual "+route+" kill credits exactly one herb and safe out-of-range reads");
            Check(g.save.coins==coins+6 && g.save.xp==xp+12 && g.save.inventory[2]==herb+1 && g.save.kills[0]==kills+1,"Actual "+route+" release retains original coins, XP, inventory and kill credit");
            Check(Remaining(g)>2.9f && Remaining(g)<=3,"Real reward opens only the three-second simulation window");
            Draw(app,"actual-"+route); Text(Header(g,6,12)); Text("Moonherb ×1"); Check(Icons(Bounds(app)).Count==1 && Same(Icons(Bounds(app))[0].uv,IconUv(6)),"Real herb receipt draws its authored herb icon");
            Check(g.effects.Exists(e=>e.kind=="loot"&&e.variant==2),"Mature herb world effect remains truthful");
        }
        int[] kinds={0,1,2,3}, items={2,1,4,5}, frames={6,5,8,9};
        for(int i=0;i<kinds.Length;i++)
        {
            Prepare(app); var g=app.Game; int coins=g.save.coins, xp=g.save.xp, inventory=g.save.inventory[items[i]];
            object receipt=Kill(app,"Q",kinds[i],i==3); Exact(receipt,i==3?70:6,i==3?80:12);
            Check(Item(receipt,items[i])==1 && g.save.inventory[items[i]]==inventory+1 && g.save.coins==coins+(i==3?70:6) && g.save.xp==xp+(i==3?80:12),"Each ordinary archetype/Boss keeps its original item index and economy");
            Draw(app,"material-"+items[i]); Text(g.data.items[items[i]].en+" ×1"); Check(Icons(Bounds(app)).Count==1 && Same(Icons(Bounds(app))[0].uv,IconUv(frames[i])),"Actual single-item row retains the corresponding crystal/herb/gear/relic icon");
        }
        Prepare(app); var game=app.Game; var targets=new List<Enemy>();
        for(int i=0;i<4;i++)targets.Add(Target(game,kinds[i],i==3,1+i*.3f));
        Key(app,KeyCode.Q); Check(Receipt(game)==null,"Area preparation produces no early aggregate"); foreach(var target in targets)WaitForKill(app,target);
        object aggregate=Receipt(game); Exact(aggregate,88,116); foreach(int item in items)Check(Item(aggregate,item)==1,"A real multi-kill aggregates each distinct drop exactly once");
        Draw(app,"actual-four-material-aggregate"); Text(Header(game,88,116)); var icons=Icons(Bounds(app)); Check(icons.Count==4,"All four distinct rewards fit the real second row");
        int[] orderedFrames={5,6,8,9}; for(int i=0;i<4;i++)Check(Same(icons[i].uv,IconUv(orderedFrames[i])) && Near(icons[i].rect.x,32+70*i),"Four compact authored icons retain stable crystal/herb/gear/relic order");
        Check(GUI.commands.FindAll(c=>c.Contains("\"text\":\"×1\"")).Count==4,"Every aggregated material retains its readable exact count");
        Console.WriteLine("PASS actual J/Q/Z-companion/persistent Frost kills, original 6 coins/12 XP and 70 coins/80 XP Boss transactions, all four item identities/authored icons and real four-material aggregation; NATIVE_UNVERIFIED");
    }
    private static void TwoHerbs(FrontierRuntime app)
    { var g=app.Game; var one=Target(g); var two=Target(g,0,false,1.7f); Key(app,KeyCode.Q); WaitForKill(app,one); WaitForKill(app,two); Exact(Receipt(g),12,24); Check(Item(Receipt(g),2)==2,"Actual area kill counts two herbs"); }
    private static void Hidden(FrontierRuntime app, string reason)
    { Draw(app); Check(!Visible(app) && !HasPart("LOOT  +") && !HasPart("UNBANKED  +") && !HasPart("战利品  +") && !HasPart("未结算  +"),reason); }
    private static void LayoutsInputThreatAndFade(FrontierRuntime app)
    {
        foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})foreach(bool narrow in new[]{false,true})foreach(bool combat in new[]{false,true})
        {
            Prepare(app); TwoHerbs(app); var g=app.Game; g.fallen.Clear(); g.save.english=english; g.save.largeText=large;
            Screen.width=narrow?960:1280; Screen.height=narrow?600:800; Call(app,"Resize");
            g.guardTime=1.234f; g.riposteTime=1.234f; g.save.mealKind=2; g.save.mealUntil=g.save.totalTime+100; g.RestartTutorial();
            g.Notify("原有提示保留","Existing notification retained"); Set(app,"combatHudTime",combat?3f:0f); Set(app,"resumeTime",2f); Set(app,"resumeBrief","Existing return brief");
            Draw(app,"layout-"+(english?"en":"zh")+(large?"-large":"-normal")+(narrow?"960":"1280")+(combat?"-combat":"-exploration"));
            Rect bounds=Bounds(app); Check(Same(bounds,new Rect(24,418,298,48)),"Both real HUDs reserve the same fixed 298×48 loot card");
            Label heading=Text(Header(g,12,24)), item=Text(g.L(g.data.items[2].name,g.data.items[2].en)+" ×2");
            Check(heading.size==(large?13:12) && item.size==(large?13:12) && Inside(LabelRect(heading),bounds) && Inside(LabelRect(item),bounds),"Bilingual currency/full material rows stay inside the card with real 12/13px sizing");
            Check(Icons(bounds).Count==1 && Same(Icons(bounds)[0].uv,IconUv(6)),"One material keeps its full localized name and exact authored icon");
            Check(HasPart(g.L("防护 ","GUARD ")) && HasPart(g.L("闪避反击 ","COUNTER ")) && Has(g.RoadMealStatus) && Has(g.L("原有提示保留","Existing notification retained")),"Existing Guard, counter, meal and toast remain visible together");
            Check(Has("Existing return brief") && GUI.textureCalls.Exists(c=>c.texture==Texture2D.whiteTexture && Same(c.rect,new Rect(352,205,576,98))),"Actual mature resume text and full panel coexist with loot");
            foreach(Rect other in new[]{(Rect)Call(app,"GuardStatusBounds"),(Rect)Call(app,"FieldcraftCounterBounds"),(Rect)Call(app,"RoadMealHudBounds",combat),new Rect(24,474,298,204),new Rect(352,205,576,98),new Rect(383,612,514,64)})
                Check(!Overlap(bounds,other),"Loot stays separate from Guard, counter, road meal, onboarding, resume and actor-warning panels");
            if(!combat){Check(HasPart(g.L("冒险入门 ","ONBOARDING ")),"Actual exploration onboarding remains visible below loot");Check(!Overlap(bounds,new Rect(24,226,298,64)),"Loot stays below the actual exploration toast fill");}
            Check(((List<Rect>)Call(app,"HudRegions")).Exists(r=>Same(r,bounds)),"Loot participates in the actual HUD threat-witness regions");
            Check(!(bool)Call(app,"PointerOverInterface",new Point(bounds.x+40,bounds.y+20)),"Read-only loot card does not block world pointers");
        }
        foreach(bool combat in new[]{false,true})foreach(bool fresh in new[]{false,true})
        {
            Prepare(app); Kill(app); Frames(app,40); var g=app.Game; g.fallen.Clear(); Set(app,"combatHudTime",combat?3f:0f); Draw(app); Rect bounds=Bounds(app);
            Point pixel=new Point(bounds.x+50,bounds.y+22), desired=(pixel-(Point)Call(app,"ScreenPoint",g.Position)).Normalized;
            Pointer(app,pixel); if(fresh)Input.buttonDown.Add(0);else Input.buttons.Add(0); Update(app); Input.Clear();
            Check(g.playerAction!=null && g.playerAction.kind=="attack" && g.playerAction.direction.x*desired.x+g.playerAction.direction.y*desired.y>.99f,"Actual "+(fresh?"fresh":"held")+" mouse over loot launches and aims its world attack in either HUD");
            Prepare(app); Kill(app); Frames(app,40); g=app.Game; g.fallen.Clear(); Set(app,"combatHudTime",combat?3f:0f); Draw(app);
            Point slot=combat?new Point(440,740):new Point(364,710); Check((bool)Call(app,"PointerOverInterface",slot),"Existing Q slot remains pointer protected");
            Pointer(app,slot); if(fresh)Input.buttonDown.Add(0);else Input.buttons.Add(0); Update(app); Input.Clear(); Check(g.playerAction==null,"Actual fresh/held mouse over protected Q control never leaks a basic attack");
        }
        foreach(bool combat in new[]{false,true})
        {
            Prepare(app); Kill(app); var g=app.Game; g.fallen.Clear(); Set(app,"combatHudTime",combat?3f:0f); Rect bounds=Bounds(app);
            // Display-only camera/enemy warning fixture. Reward came from the earlier actual kill.
            var enemy=new Enemy(++enemyId,0,22,14,80){state=1,timer=.3f,windupDuration=.65f,target=new Point(22,14)}; enemy.Face(new Point(0,-1)); g.enemies.Add(enemy);
            Point pixel=new Point(bounds.x+bounds.width/2,bounds.y+30); Set(app,"camera",new Point(enemy.x-(pixel.x-640)/36f,enemy.y-(pixel.y-390)/36f));
            Draw(app,"threat-"+(combat?"combat":"exploration")); Check((bool)Call(app,"ThreatOver",bounds),"The actual enemy/warning envelope overlaps loot");
            int fill=GUI.textureCalls.FindIndex(c=>c.texture==Texture2D.whiteTexture && Same(c.rect,bounds)); Check(fill>=0 && Near(GUI.textureCalls[fill].color.a,.12f,.001f),"Threatened loot background is capped at exactly .12 opacity");
            Check(GUI.textureCalls.FindLastIndex(c=>c.texture.name.Contains("outline_Monsters"))>fill,"Actual actor outline remains visible after loot fill");
            ClearDraw(); Call(app,"DrawHudThreatWitness"); Check(GUI.textureCalls.Exists(c=>c.texture.name.Contains("outline_Monsters")) && GUI.textureCalls.FindAll(c=>c.rect.width==2).Count==33,"Shared witness preserves the actual actor and complete thirty-three-point warning above loot");
            Set(g,"combatLootRemaining",.175f); ClearDraw(); Call(app,"DrawCombatLootHud");
            Check(GUI.textureCalls.Exists(c=>c.texture==Texture2D.whiteTexture && Same(c.rect,bounds) && Near(c.color.a,.06f)),"Final-window opacity multiplies the .12 threat cap by the same honest fade");
        }
        Prepare(app); Kill(app); var game=app.Game; Draw(app); ClearDraw(); Call(app,"DrawCombatLootHud");
        var fullTextures=GUI.textureCalls.ToArray(); var fullText=new List<Label>(); foreach(string command in GUI.commands)if(command.Contains("\"k\":\"text\""))fullText.Add(JsonUtility.FromJson<Label>(command));
        // Display-only exact late-window fixture; real Update still owns removal.
        Set(game,"combatLootRemaining",.175f); Draw(app,"half-fade"); ClearDraw(); Call(app,"DrawCombatLootHud");
        Check(GUI.textureCalls.Count==fullTextures.Length,"Fade keeps all receipt geometry stable");
        for(int i=0;i<fullTextures.Length;i++)Check(Same(GUI.textureCalls[i].rect,fullTextures[i].rect) && Near(GUI.textureCalls[i].color.a,fullTextures[i].color.a*.5f),"Final .35s fades every background, frame and material icon together");
        foreach(Label old in fullText)Check(Near(Text(old.text).color[3],old.color[3]*.5f),"Final .35s fades every text row together");
        Frames(app,10); Hidden(app,"Actual final simulation ticks expire the entire card"); Check(Receipt(game)==null && Remaining(game)==0,"Expired receipt leaves no readable transient reward");
        foreach(bool english in new[]{false,true})
        {
            Prepare(app); Kill(app); game=app.Game; game.save.english=english; game.save.largeText=true; Screen.width=960; Screen.height=600; Call(app,"Resize");
            // Display-only overflow fixture uses the real immutable receipt constructor.
            // Actual rewards, owner and generation still originate from the earlier kill.
            object receipt=Receipt(game); var constructor=receipt.GetType().GetConstructors(Private)[0]; object capped=null;
            foreach(int index in new[]{1,2,4,5})capped=constructor.Invoke(new object[]{capped,10001,10002,index,10003,0,false});
            Set(game,"combatLootReceipt",capped); Draw(app,"capped-four-materials-"+(english?"en":"zh")+"-large960");
            Text(game.L("战利品","LOOT")+"  +9999+"+game.L(" 硬币"," coins")+" · +9999+ XP");
            Check(GUI.commands.FindAll(c=>c.Contains("\"text\":\"×9999+\"")).Count==4 && Icons(Bounds(app)).Count==4,"Display-only huge aggregate retains all four distinct positive materials with honest lower-bound counts");
            foreach(string command in GUI.commands)if(command.Contains("\"text\":\"×9999+\""))Check(JsonUtility.FromJson<Label>(command).size==13,"Huge material counts honor actual large-text sizing");
        }
        Console.WriteLine("PASS zh/en normal1280/large960 in both HUDs, full-name ×2 rows, Guard/counter/meal/onboarding/toast/resume/warning separation, true fresh/held click-through and protected Q controls, .12 threat fade, complete actor/warning witnesses and synchronized final .35s alpha; NATIVE_UNVERIFIED");
    }
    private static void Append(StringBuilder text, object value, HashSet<object> seen)
    {
        if(value==null){text.Append("null;");return;} Type type=value.GetType();
        if(type.IsPrimitive || type.IsEnum || value is string || value is decimal){text.Append(Convert.ToString(value,CultureInfo.InvariantCulture)).Append(';');return;}
        if(!type.IsValueType && !seen.Add(value)){text.Append("ref;");return;} var sequence=value as IEnumerable;
        if(sequence!=null){foreach(object item in sequence)Append(text,item,seen);text.Append("end;");return;}
        var fields=type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic); Array.Sort(fields,(a,b)=>string.CompareOrdinal(a.Name,b.Name));
        foreach(var field in fields){text.Append(field.Name).Append('=');Append(text,field.GetValue(value),seen);}
    }
    private static string State(GameCore g) { var text=new StringBuilder(); Append(text,g,new HashSet<object>()); return text.ToString(); }
    private static void ReadOnlySaveAndLifecycle(FrontierRuntime app)
    {
        Prepare(app); Kill(app); var g=app.Game; Draw(app); string state=State(g); float clock=Get<float>(app,"clock");
        var matrix=Matrix4x4.TRS(new Vector3(13,21,0),Quaternion.Euler(0,0,11),new Vector3(1.25f,1.25f,1)); var tint=new Color(.3f,.4f,.5f,.6f);
        foreach(EventType evt in new[]{EventType.Layout,EventType.Repaint,EventType.Layout,EventType.Repaint})
        {
            Event.current.type=evt; GUI.matrix=matrix; GUI.color=tint; GUI.enabled=false; Draw(app); Text(Header(g,6,12));
            Check(State(g)==state && Get<float>(app,"clock")==clock,"Repeated actual OnGUI Layout/Repaint mutates no Core, SaveData, receipt or presentation clock");
            Check(GUI.matrix.Equals(matrix) && GUI.color.Equals(tint) && !GUI.enabled,"Actual OnGUI restores incoming matrix, color and enabled state");
        }
        Event.current.type=EventType.Repaint; GUI.matrix=Matrix4x4.identity; GUI.color=Color.white; GUI.enabled=true;
        Key(app,KeyCode.I); float left=Remaining(g); Frames(app,20); Hidden(app,"Inventory hides transient loot"); Check(Remaining(g)==left,"Inventory freezes actual receipt lifetime"); Key(app,KeyCode.Escape);
        Call(app,"OnApplicationPause",true); left=Remaining(g); Frames(app,10); Hidden(app,"Application pause hides loot"); Check(Remaining(g)==left,"Application pause freezes receipt lifetime"); Call(app,"OnApplicationPause",false);
        app.VisualReviewPaused=true; Frames(app,10); Hidden(app,"Frozen review hides loot"); Check(Remaining(g)==left,"Frozen review does not consume receipt lifetime"); app.VisualReviewPaused=false;
        g.hitStop=.1f; left=Remaining(g); Frames(app,3); Draw(app); Check(Visible(app) && Remaining(g)==left,"Mature hit stop freezes loot clock while retaining truthful card");
        g.hitStop=0; Frames(app,10); Check(Near(Remaining(g),left-.2f),"Only actual unpaused world ticks advance the receipt window");
        g.PrepareSave(); Check(SaveStore.Write(g.save,0),"Actual checksum SaveStore writes the original save schema"); string warning; SaveData saved=SaveStore.Read(0,out warning);
        Check(saved!=null && warning==null,"Actual checksum SaveStore reads its saved envelope"); string raw=JsonUtility.ToJson(saved);
        Check(!raw.Contains("CombatLoot") && !raw.Contains("combatLoot"),"Loot receipts and clocks never expand SaveData");
        foreach(int version in new[]{1,2})
        {
            SaveData old=JsonUtility.FromJson<SaveData>(raw); old.version=version; File.WriteAllText(SaveStore.PathFor(1),JsonUtility.ToJson(old));
            SaveData loaded=SaveStore.Read(1,out warning); Check(loaded!=null && warning==null,"Actual raw legacy v"+version+" loads without receipt fields"); g.Load(loaded); g.Start();
            Check(Receipt(g)==null && Remaining(g)==0 && g.save.coins==old.coins,"Actual legacy load preserves balances and clears transient loot"); Hidden(app,"Legacy saves invent no stale receipt");
        }
        foreach(string boundary in new[]{"travel","load","death","practice","range","sparring","owner","room"})
        {
            Prepare(app); Kill(app); g=app.Game;
            if(boundary=="travel")g.Enter(1);
            else if(boundary=="load"){g.PrepareSave();Check(SaveStore.Write(g.save,2),"Actual checksum writes before load boundary");var loaded=SaveStore.Read(2,out warning);Check(loaded!=null && warning==null,"Actual checksum reads before load boundary");g.Load(loaded);g.Start();}
            else if(boundary=="death"){g.invulnerable=0;Call(g,"Hurt",10000f,"environment",null);Check(g.Dying,"Actual lethal incoming damage begins death");}
            else if(boundary=="practice"){g.StartPractice();Check(g.practicing,"Actual original basic practice starts");}
            else if(boundary=="range" || boundary=="sparring")
            {
                WorldObject mentor=Array.Find(g.Map.objects,o=>o.kind=="mentor"); Check(mentor!=null && app.SetVisualReviewPosition(new Point(mentor.x,mentor.y-.7f)),"Real mentor proximity satisfies mature training prerequisites");
                Check(boundary=="range"?g.StartRange(0):g.StartSparring(),"Actual safe "+boundary+" starts");
            }
            else if(boundary=="owner")g.save=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(g.save));
            else g.save.room=1;
            Check(Receipt(g)==null && Remaining(g)==0,"Actual "+boundary+" boundary hides the old reward immediately"); Hidden(app,"No transient card crosses "+boundary+" boundary");
        }
        Prepare(app); Kill(app); Frames(app,151); Check(Receipt(app.Game)==null && Remaining(app.Game)==0,"Actual three-second simulation window naturally expires"); Hidden(app,"Natural expiry removes the card entirely");
        Console.WriteLine("PASS full Core/save/receipt read-only Layout/Repaint, GUI restoration, inventory/application/review/hit-stop clocks, real checksum and v1/v2 legacy paths, travel/load/death/basic/range/sparring/wrong-owner/wrong-room boundaries and natural expiry; NATIVE_UNVERIFIED");
    }
    private static void StartAbyss(FrontierRuntime app, int risk)
    {
        Prepare(app); var g=app.Game; g.save.frontierEnding=1; g.StartAbyss(risk); Check(g.InAbyss && g.save.abyssRisk==risk,"Actual expedition entry accepts requested mature risk");
        g.enemies.Clear(); g.projectiles.Clear(); g.effects.Clear(); g.hazards.Clear(); g.fallen.Clear(); g.invulnerable=100; g.toastTime=0;
        Set(app,"camera",g.Position); Call(app,"Resize"); Aim(app,g.Position+new Point(2,0));
    }
    private static void AbyssUnbankedBankAndHonesty(FrontierRuntime app)
    {
        foreach(int risk in new[]{0,1,2})foreach(bool english in new[]{false,true})
        {
            StartAbyss(app,risk); var g=app.Game; g.save.english=english; int coins=g.save.coins,xp=g.save.xp,shards=g.save.shards; int[] inventory=(int[])g.save.inventory.Clone();
            object receipt=Kill(app); Exact(receipt,8+risk*3,14,1+risk,true);
            Check(g.save.coins==coins && g.save.xp==xp && g.save.shards==shards && g.save.runCoins==8+risk*3 && g.save.runXp==14 && g.save.runShards==1+risk,"Actual Abyss kill changes only existing unbanked run balances");
            for(int i=0;i<inventory.Length;i++)Check(g.save.inventory[i]==inventory[i] && Item(receipt,i)==0,"Actual Abyss kill invents no inventory-material award");
            Check(g.effects.Exists(e=>e.kind=="loot"&&e.variant==5),"Actual Abyss world loot effect honestly shows shards");
            Draw(app,"abyss-risk"+risk+(english?"-en":"-zh")); Text(Header(g,8+risk*3,14,true)); Text("+"+(1+risk)+g.L(" 碎片 · 到东门结算"," shards · bank at east gate")); Check(Icons(Bounds(app)).Count==0,"Unbanked receipt has no misleading inventory material icon");
            WorldObject gate=Array.Find(g.Map.objects,o=>o.kind=="abyssGate"); Check(gate!=null,"Real seeded expedition retains the east banking gate");
            g.Talk(gate); Check(g.conversation!=null,"Actual cleared-floor gate presents banking choices"); int bank=Array.FindIndex(g.conversation.choices,c=>c.action=="abyssBank"); Check(bank>=0,"Actual gate offers mature bank/extract action");g.Choose(bank);
            Check(!g.InAbyss && g.save.coins==coins+8+risk*3 && g.save.xp==xp+14 && g.save.shards==shards+1+risk && g.save.runCoins==0 && g.save.runXp==0 && g.save.runShards==0,"Real gate bank commits original balances exactly once and clears run balances");
            Check(Receipt(g)==null && Remaining(g)==0,"Actual banking clears the now-obsolete UNBANKED receipt"); Hidden(app,"Banking/extraction cannot retain an unbanked card");
        }
        foreach(int risk in new[]{0,2})
        {
            StartAbyss(app,risk); var g=app.Game; int coins=g.save.coins,xp=g.save.xp,shards=g.save.shards;
            object receipt=Kill(app,"Q",3,true); Exact(receipt,70,100,5+risk,true);
            Check(g.save.coins==coins && g.save.xp==xp && g.save.shards==shards && g.save.runCoins==70 && g.save.runXp==100 && g.save.runShards==5+risk && Item(receipt,5)==0,"Actual Abyss Boss awards only original 70/100/(5+risk) unbanked currencies");
            Draw(app,"abyss-fallen-boss-risk"+risk); Text("Guardian defeated · loot UNBANKED"); Check(!HasPart("loot secured") && !HasPart("Loot secured"),"Fallen Abyss Boss never calls unbanked loot secured");
            Check(Has(Header(g,70,100,true)) && Has("+"+(5+risk)+" shards · bank at east gate"),"Fallen Boss coexists with exact unbanked card");
            // Direct exploration renderer fixture exercises its separate mature fallen-Boss sentence.
            ClearDraw(); Call(app,"DrawExplorationHud"); Text("Loot UNBANKED · bank at the east gate"); Check(!HasPart("Loot secured"),"Exploration fallen-Boss guidance also states the banking requirement honestly");
        }
        Console.WriteLine("PASS actual Abyss risk0/1/2 unbanked 8+3risk coins/14 XP/1+risk shards, bilingual banking guidance and no inventory fiction, real east-gate banking/extraction clears receipt, real Boss 70/100/5+risk and honest fallen-Boss labels; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-combat-loot-"+Guid.NewGuid()); Application.persistentDataPath=directory;
        captureFolder=args.Length>0 && args[0]!="-"?args[0]:null; if(captureFolder!=null)Directory.CreateDirectory(captureFolder); var app=new FrontierRuntime(); int failed=0;
        try
        {
            Call(app,"Awake"); Check(app.Game!=null,"Actual Awake loads the shipped campaign");
            foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{ActualTransactionsAndMaterials,LayoutsInputThreatAndFade,ReadOnlySaveAndLifecycle,AbyssUnbankedBankAndHonesty})
                try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}
        }
        finally{app.EndVisualReview();Call(app,"OnDestroy");Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime combat loot: "+(4-failed)+"/4 groups passed; captures="+captures+"; NATIVE_UNVERIFIED"); return failed==0?0:1;
    }
}
