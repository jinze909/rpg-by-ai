// Real production Update, OnGUI, nearest-object E/B and checksum SaveStore.
// Display positions and hostile HP are fixtures; captures are not native Unity evidence.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Starfall;
using UnityEngine;

public static class RuntimeInteractionHintChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string captureFolder;
    private static int captures;
    private sealed class Label { public string text = null; public float[] r = null, color = null; public int size = 0; public bool center = false; }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static bool Near(float a, float b, float tolerance=.003f) { return Math.Abs(a-b)<=tolerance; }
    private static bool Same(Rect a, Rect b) { return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height); }
    private static bool Inside(Rect a, Rect b) { return a.x>=b.x && a.y>=b.y && a.x+a.width<=b.x+b.width && a.y+a.height<=b.y+b.height; }
    private static float Area(Rect a, Rect b) { return Math.Max(0,Math.Min(a.x+a.width,b.x+b.width)-Math.Max(a.x,b.x))*Math.Max(0,Math.Min(a.y+a.height,b.y+b.height)-Math.Max(a.y,b.y)); }
    private static object Call(object target, string name, params object[] args)
    { var method=target.GetType().GetMethod(name,Private|BindingFlags.Public); Check(method!=null,"Production entry point exists: "+name);return method.Invoke(target,args); }
    private static void Set(object target, string name, object value)
    { var field=target.GetType().GetField(name,Private);Check(field!=null,"Fixture field exists: "+name);field.SetValue(target,value); }
    private static T Get<T>(object target,string name) { return (T)target.GetType().GetField(name,Private).GetValue(target); }
    private static string Hint(GameCore g, WorldObject target, bool english)
    {
        // Reflection keeps immutable historical production compilable: RED is a missing behavior assertion.
        var method=typeof(GameCore).GetMethod("InteractionHint",new[]{typeof(WorldObject),typeof(bool)});
        Check(method!=null && method.ReturnType==typeof(string),"Read-only production InteractionHint(WorldObject,bool) API exists");
        return (string)method.Invoke(g,new object[]{target,english});
    }
    private static void Update(FrontierRuntime app) { Call(app,"Update");Input.keys.Clear();Input.buttonDown.Clear(); }
    private static void Frames(FrontierRuntime app,int count) { for(int i=0;i<count;i++)Update(app); }
    private static void Key(FrontierRuntime app,KeyCode key) { Input.keys.Add(key);Update(app); }
    private static void ClearDraw() { GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear(); }
    private static void Draw(FrontierRuntime app,string capture=null)
    {
        ClearDraw();Call(app,"OnGUI");
        if(captureFolder!=null && capture!=null)
        {
            var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();
            GUI.Export(captureFolder,"qa-interaction-hint-"+capture);captures++;
            GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static bool Has(string text) { return GUI.commands.Exists(c=>c.Contains("\"text\":"+GUI.S(text))); }
    private static Label Text(string text)
    { string command=GUI.commands.Find(c=>c.Contains("\"text\":"+GUI.S(text)));Check(command!=null,"Actual OnGUI contains truthful text: "+text);return JsonUtility.FromJson<Label>(command); }
    private static Label TextAt(string text,Rect expected)
    {
        foreach(string command in GUI.commands)if(command.Contains("\"text\":"+GUI.S(text)))
        { var label=JsonUtility.FromJson<Label>(command);if(Same(LabelRect(label),expected))return label; }
        throw new Exception("Actual OnGUI contains expected existing text rectangle: "+text);
    }
    private static Rect LabelRect(Label label) { return new Rect(label.r[0],label.r[1],label.r[2],label.r[3]); }
    private static string Name(GameCore g, WorldObject target,bool pad=false) { return (pad?"B":"E")+"  "+g.L(target.name,target.en); }
    private static Rect Card(FrontierRuntime app,WorldObject target,bool pad=false)
    {
        var g=app.Game;string hint=Hint(g,target,g.save.english);Check(hint.Length>0,"The authored nearest target has a state hint");
        Label title=Text(Name(g,target,pad)),status=Text(hint);Rect card=new Rect(title.r[0]-8,title.r[1]-5,320,64);
        Check(Same(LabelRect(title),new Rect(card.x+8,card.y+5,304,24)) && title.size==(g.save.largeText?15:14) && title.center,"Full actual nearest-object name keeps its centered 14/15px first row");
        Check(Same(LabelRect(status),new Rect(card.x+8,card.y+33,304,24)) && status.size==(g.save.largeText?13:12) && status.center,"Truthful state has its own centered 12/13px second row");
        Check(GUI.textureCalls.Exists(c=>c.texture==Texture2D.whiteTexture && Same(c.rect,card)),"Actual renderer emits the complete 320x64 state panel");
        return card;
    }
    private static void Pointer(FrontierRuntime app,Point point)
    { float scale=Get<float>(app,"scale");Input.mousePosition=new Vector3(Get<float>(app,"offsetX")+point.x*scale,Screen.height-Get<float>(app,"offsetY")-point.y*scale,0); }
    private static void Anchor(FrontierRuntime app,WorldObject target,Point pixel)
    { Set(app,"camera",new Point(target.x-(pixel.x-640)/36f,target.y-(pixel.y-390)/36f)); }
    private static void Prepare(FrontierRuntime app,int room=1)
    {
        Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;
        Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;Event.current.type=EventType.Repaint;
        app.BeginVisualReview(0,room);app.VisualReviewPaused=false;var g=app.Game;
        g.enemies.Clear();g.projectiles.Clear();g.effects.Clear();g.hazards.Clear();g.fallen.Clear();g.save.companion=-1;
        g.save.english=true;g.save.largeText=false;g.save.showGuidance=false;g.save.reducedMotion=true;g.save.effectsQuality=0;
        g.save.totalTime=100;g.save.frontierEnding=0;g.save.level=30;g.save.xp=0;g.save.energy=100;g.save.health=g.save.maxHealth;
        g.save.foodUntil=g.save.teaUntil=g.save.mealUntil=0;g.save.plantedAt=-1;g.guardTime=g.riposteTime=0;g.toastTime=0;g.invulnerable=100;
        Set(app,"applicationPaused",false);Set(app,"pointerSeen",false);Set(app,"pointerAimActive",false);Set(app,"controllerUsed",false);
        Set(app,"combatHudTime",0f);Set(app,"resumeTime",0f);Set(app,"arrivalTime",0f);Call(app,"Resize");
    }
    private static WorldObject Object(GameCore g,string kind)
    { WorldObject target=Array.Find(g.Map.objects,o=>o.kind==kind);Check(target!=null,"Actual shipped object exists: "+kind);return target; }
    private static void Approach(FrontierRuntime app,WorldObject target)
    {
        var g=app.Game;bool found=false;
        for(float radius=.25f;!found && radius<1.75f;radius+=.25f)for(int direction=0;!found && direction<32;direction++)
        {
            double angle=direction*Math.PI/16;Point p=new Point(target.x+(float)Math.Cos(angle)*radius,target.y+(float)Math.Sin(angle)*radius);
            if(!g.CanStand(p))continue;g.save.x=p.x;g.save.y=p.y;found=ReferenceEquals(g.Nearest(),target);
        }
        Check(found,"Actual collision and Nearest permit approaching "+target.id);Set(app,"camera",g.Position);
        Set(app,"previousRoom",g.save.room);Set(app,"previousFloor",g.save.abyssFloor);Set(app,"arrivalTime",0f);
    }
    private static void Choose(FrontierRuntime app,string action)
    {
        var g=app.Game;Check(g.mode=="dialog" && g.conversation!=null,"Actual E opened the original dialogue");
        int index=Array.FindIndex(g.conversation.choices,c=>c.action==action);Check(index>=0 && index<9,"Original dialogue offers numeric action "+action);
        Draw(app);int pages=0;while(!(bool)Call(app,"DialogueChoicesVisible") && pages++<30){Key(app,KeyCode.Return);Draw(app);}
        Check((bool)Call(app,"DialogueChoicesVisible"),"Real Enter pagination exposes original choices");
        Key(app,(KeyCode)((int)KeyCode.Alpha1+index));Check(g.mode=="world","Actual numeric choice completes "+action);
    }
    private static void Show(FrontierRuntime app,WorldObject target,string capture)
    {
        bool english=app.Game.save.english;
        try
        {
            foreach(bool language in new[]{english,!english})
            { app.Game.save.english=language;Draw(app,capture+(language?"-en":"-zh"));Card(app,target,Get<bool>(app,"controllerUsed")); }
        }
        finally{app.Game.save.english=english;}
    }
    private static string Economy(GameCore g)
    { return g.save.coins+"/"+g.save.shards+"/"+g.save.xp+"/"+string.Join(",",Array.ConvertAll(g.save.inventory,v=>v.ToString()))+"/"+string.Join(",",Array.ConvertAll(g.save.mastery,v=>v.ToString())); }
    private static void ResourcesRestAndNearestInput(FrontierRuntime app)
    {
        Prepare(app);var g=app.Game;var resources=new List<WorldObject>();
        foreach(string kind in new[]{"herb","wood","ore"})
        {
            var target=Object(g,kind);resources.Add(target);Approach(app,target);string fresh=Hint(g,target,true);Show(app,target,"fresh-"+kind);
            int item=kind=="herb"?2:kind=="wood"?0:1,count=g.save.inventory[item],parts=g.save.inventory[4];
            Key(app,KeyCode.E);Check(g.Has("node_"+target.id) && g.save.inventory[item]==count+3 && g.save.inventory[4]==parts+(kind=="ore"?3:0),"Real Update E preserves original "+kind+" gathering quantity and ore parts");
            Check(Hint(g,target,true)!=fresh && ReferenceEquals(g.Nearest(),target),"Gathered node stays the exact E target with an updated hint");
            Show(app,target,"gathered-"+kind);string economy=Economy(g);Key(app,KeyCode.E);Check(Economy(g)==economy,"Repeated actual E never invents more gathered supplies");
        }
        g.Enter(0);g.enemies.Clear();Approach(app,Object(g,"rest"));float clock=g.save.totalTime;string balances=Economy(g);
        Key(app,KeyCode.E);Check(g.save.totalTime<=clock+.021f,"E only talks to the inn before choosing rest");Choose(app,"rest");
        Check(Near(g.save.totalTime,clock+45.02f,.03f) && Economy(g)==balances && !g.save.flags.Exists(f=>f.StartsWith("node_")),"Actual numeric inn rest retains 45 seconds, zero currency cost and renewal of all original resource flags");
        g.Enter(1);g.enemies.Clear();foreach(var target in resources){Approach(app,target);Check(Hint(g,target,true)=="Gather supplies","Actual rest returns every original resource hint to available");}
        foreach(KeyCode input in new[]{KeyCode.E,KeyCode.JoystickButton1})
        {
            Prepare(app);g=app.Game;var first=Object(g,"herb");var second=Object(g,"wood");Approach(app,first);Point feet=g.Position;
            float ax=first.x,ay=first.y,bx=second.x,by=second.y;
            try
            {
                // Two owned campaign references at the same reachable point exercise the mature strict-< tie rule.
                first.x=second.x=feet.x+.4f;first.y=second.y=feet.y;
                WorldObject expected=Array.IndexOf(g.Map.objects,first)<Array.IndexOf(g.Map.objects,second)?first:second;
                Check(ReferenceEquals(g.Nearest(),expected),"Existing authored object order still wins exact distance ties");
                Set(app,"controllerUsed",input==KeyCode.JoystickButton1);Show(app,expected,"nearest-tie-"+(input==KeyCode.E?"keyboard":"pad"));
                Key(app,input);Check(g.Has("node_"+expected.id) && !g.Has("node_"+(ReferenceEquals(expected,first)?second.id:first.id)),"Actual E/B acts on the same tied Nearest reference drawn in the prompt");
            }
            finally{first.x=ax;first.y=ay;second.x=bx;second.y=by;}
        }
        Console.WriteLine("PASS real Update E herb/timber/ore +3 and ore parts, depleted retry, real numeric 45-second free inn rest/renewal, actual same Nearest and exact-tie E/B behavior; NATIVE_UNVERIFIED");
    }
    private static void GardenSealsCachesAndSave(FrontierRuntime app)
    {
        Prepare(app,0);var g=app.Game;WorldObject garden=Object(g,"garden");Approach(app,garden);Show(app,garden,"garden-empty");
        int coins=g.save.coins,seeds=g.save.inventory[7];Key(app,KeyCode.E);Check(g.save.inventory[7]==seeds && g.save.plantedAt<0,"E talks to An without planting or harvesting");Choose(app,"seeds");
        Check(g.save.coins==coins-5 && g.save.inventory[7]==seeds+3,"Original numeric seed purchase keeps five-coin cost and three seeds");
        seeds=g.save.inventory[7];Key(app,KeyCode.E);Choose(app,"plant");Check(g.save.inventory[7]==seeds-1 && Near(g.save.plantedAt,g.save.totalTime),"Original numeric planting consumes exactly one seed");
        g.save.plantedAt=g.save.totalTime-36.9f;Show(app,garden,"garden-growing-ceil24");Check(Hint(g,garden,true)=="Talk · crops ready in 24s","Garden status uses ceiling of the original sixty-second growth clock");
        string economy=Economy(g);Key(app,KeyCode.E);Choose(app,"harvest");Check(Economy(g)==economy && g.save.plantedAt>=0,"Actual early harvest choice grants no invented reward");
        g.save.plantedAt=g.save.totalTime-60;Show(app,garden,"garden-ready-talk");economy=Economy(g);Key(app,KeyCode.E);Check(Economy(g)==economy && g.save.plantedAt>=0,"Ready garden E still only opens An's unchanged conversation");
        int herbs=g.save.inventory[2];seeds=g.save.inventory[7];coins=g.save.coins;int harvest=5+g.save.projects[0]*2;Choose(app,"harvest");
        Check(g.save.inventory[2]==herbs+harvest && g.save.inventory[7]==seeds+2 && g.save.coins==coins+8 && g.save.plantedAt<0,"Original numeric harvest retains mature herbs/project bonus, two seeds and eight coins");
        foreach(int power in new[]{1,2,3})
        {
            Prepare(app,power);g=app.Game;var seal=Object(g,"seal"+power);var cache=Object(g,"cache"+power);g.save.flags.Remove("core_"+power);
            Approach(app,cache);Show(app,cache,"cache-locked-"+power);economy=Economy(g);Key(app,KeyCode.E);Check(Economy(g)==economy && !g.Has("cache_"+power),"Actual locked cache refuses the original transaction");
            Approach(app,seal);Show(app,seal,"seal-required-"+power);Key(app,KeyCode.E);Check(!g.Has("seal_"+power) && Economy(g)==economy,"Actual E cannot release seal "+power+" without its original core power");
            g.save.flags.Add("core_"+power);Show(app,seal,"seal-openable-"+power);Key(app,KeyCode.E);Check(g.Has("seal_"+power) && Economy(g)==economy,"Actual E releases seal "+power+" with its original power and no new cost");Show(app,seal,"seal-open-"+power);
            Approach(app,cache);Show(app,cache,"cache-fresh-"+power);coins=g.save.coins;int shards=g.save.shards,stew=g.save.inventory[6],mastery=g.save.mastery[g.save.weapon];
            Key(app,KeyCode.E);Check(g.Has("cache_"+power) && g.save.coins==coins+30 && g.save.shards==shards+3 && g.save.inventory[6]==stew+1 && g.save.mastery[g.save.weapon]==Math.Min(100,mastery+8),"Actual cache E preserves one-time 30 coins, three shards, stew and eight mastery");Choose(app,"close");
            Show(app,cache,"cache-claimed-"+power);economy=Economy(g);Key(app,KeyCode.E);Check(g.conversation!=null && Economy(g)==economy,"Claimed-cache E rereads its real record without duplicate rewards");Choose(app,"close");
            g.PrepareSave();Check(SaveStore.Write(g.save,power-1),"Actual checksum SaveStore writes existing gathered/seal/cache progress");string warning;var loaded=SaveStore.Read(power-1,out warning);
            Check(loaded!=null && warning==null && loaded.flags.Contains("cache_"+power) && loaded.coins==g.save.coins,"Actual checksum SaveStore restores original claimed-cache state and balances");
            string raw=JsonUtility.ToJson(loaded);Check(!raw.Contains("InteractionHint") && !raw.Contains("interactionHint"),"Hint presentation never expands SaveData");g.Load(loaded);g.Start();g.enemies.Clear();Approach(app,cache);Show(app,cache,"cache-reloaded-"+power);
        }
        Console.WriteLine("PASS real An E dialogue and numeric purchase/plant/early-check/harvest, honest 24s ceiling and ready TALK, all three original core/seal/cache transactions, claimed reread and checksum saves without schema expansion; NATIVE_UNVERIFIED");
    }
    private static void LayoutsNamesAndOriginalGeneric(FrontierRuntime app)
    {
        Point[] anchors={new Point(640,390),new Point(172,245),new Point(20,20),new Point(1260,780),new Point(8,792),new Point(640,20)};
        foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})foreach(bool narrow in new[]{false,true})foreach(bool combat in new[]{false,true})
        {
            Prepare(app);var g=app.Game;var target=Object(g,"herb");Approach(app,target);g.save.english=english;g.save.largeText=large;
            Screen.width=narrow?960:1280;Screen.height=narrow?600:800;Call(app,"Resize");Set(app,"combatHudTime",combat?3f:0f);
            g.guardTime=g.riposteTime=1.2f;g.save.mealKind=2;g.save.mealUntil=g.save.totalTime+100;g.RestartTutorial();g.Notify("原有提示保留","Existing notification retained");
            Set(app,"resumeTime",2f);Set(app,"resumeBrief","Existing return brief");Set(app,"arrivalTime",2f);
            for(int placement=0;placement<anchors.Length;placement++)
            {
                Anchor(app,target,anchors[placement]);Draw(app,"layout-"+(english?"en":"zh")+(large?"-large":"-normal")+(narrow?"960":"1280")+(combat?"-combat":"-exploration")+"-"+placement);
                Rect bounds=Card(app,target);Check(Inside(bounds,new Rect(8,8,1264,784)),"State card respects eight-pixel virtual canvas margins at every staged edge");
                var regions=new List<Rect>((List<Rect>)Call(app,"HudRegions"));Check(regions.Exists(r=>Same(r,bounds)),"Actual state panel registers in existing foreground threat-witness regions");
                if(!combat)
                {
                    // These real exploration elements are drawn after the world prompt and lack HudRegions entries.
                    // Derive toast bounds from its emitted centered text and verify its real panel before scoring.
                    Rect text=LabelRect(Text(g.L("原有提示保留","Existing notification retained")));
                    Rect toast=new Rect(text.x-18,text.y-13,text.width+36,text.height+21);
                    Check(Same(toast,new Rect(382,132,516,67)) && GUI.textureCalls.Exists(c=>c.texture==Texture2D.whiteTexture && Same(c.rect,toast)),"Overlap oracle uses the actual emitted exploration toast panel");
                    if(!regions.Exists(r=>Same(r,toast)))regions.Add(toast);
                    regions.Add(LabelRect(TextAt(g.L(g.Map.name,g.Map.en),new Rect(406,28,468,36))));
                    regions.Add(LabelRect(TextAt(g.L(g.Map.subtitle,g.Map.suben),new Rect(412,69,456,25))));
                    string balance=g.save.coins+g.L(" 硬币  ·  "," coins  ·  ")+(string)Call(app,"WeaponName",g.save.weapon);
                    regions.Add(LabelRect(TextAt(balance,new Rect(25,725,302,24))));
                    regions.Add(LabelRect(TextAt(g.L("J / 左键攻击 · E 互动","J / click attack · E interact"),new Rect(25,752,302,23))));
                }
                float actual=0;foreach(Rect region in regions)if(!Same(region,bounds))actual+=Area(bounds,region);
                Point anchor=(Point)Call(app,"ScreenPoint",new Point(target.x,target.y));float minimum=float.MaxValue;
                for(int option=0;option<4;option++)
                {
                    float x=option==2?anchor.x-344:option==3?anchor.x+24:anchor.x-160;
                    float y=option==0?anchor.y+14:option==1?anchor.y-128:anchor.y-32;
                    Rect candidate=new Rect(Math.Max(8,Math.Min(952,x)),Math.Max(8,Math.Min(728,y)),320,64);float score=0;
                    foreach(Rect region in regions)if(!Same(region,bounds))score+=Area(candidate,region);minimum=Math.Min(minimum,score);
                }
                Check(actual<=minimum+.01f,"Actual state card minimizes overlap with controls, statuses, toast, tutorial, arrival and resume at "+anchors[placement].x+","+anchors[placement].y+" "+(combat?"combat":"exploration")+" actual="+actual+" min="+minimum);
                Rect center=new Rect(480,368,320,64);float centerOverlap=0;
                foreach(Rect region in regions)if(!Same(region,bounds))centerOverlap+=Area(center,region);
                if(centerOverlap==0)Check(actual==0,"A clear center fallback keeps the actual state card separate from every emitted HUD region at "+anchors[placement].x+","+anchors[placement].y+" "+(combat?"combat":"exploration")+" overlap="+actual);
                Check(Has(g.L("原有提示保留","Existing notification retained")) && Has("Existing return brief") && Has(g.RoadMealStatus),"Original toast, resume and road meal remain intact alongside the state hint");
            }
        }
        foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})foreach(bool pad in new[]{false,true})
        {
            Prepare(app,0);var g=app.Game;var resident=Object(g,"guild");Approach(app,resident);g.save.english=english;g.save.largeText=large;Set(app,"controllerUsed",pad);
            Check(Hint(g,resident,english)=="","Ordinary residents keep the original generic prompt");Draw(app,"generic-"+(english?"en":"zh")+(large?"-large":"-normal")+(pad?"-pad":"-keyboard"));
            Point s=(Point)Call(app,"ScreenPoint",new Point(resident.x,resident.y));Rect expected=new Rect(s.x-125,s.y+14,250,35);Label label=Text(Name(g,resident,pad));
            Check(Same(LabelRect(label),new Rect(s.x-117,s.y+18,234,27)) && label.size==(large?15:14) && label.center,"Generic NPC prompt preserves pixel-exact original name geometry, font and centering");
            Check(GUI.textureCalls.Exists(c=>c.texture==Texture2D.whiteTexture && Same(c.rect,expected) && Near(c.color.a,.94f)),"Generic NPC prompt keeps the exact original 250x35 panel and opacity");
        }
        Prepare(app);var game=app.Game;var herb=Object(game,"herb");Approach(app,herb);string heading=Name(game,herb);var foe=new Enemy(59401,0,game.Position.x+2,game.Position.y,80){stun=100,timer=9999};game.enemies.Add(foe);
        Draw(app);Check(!Has(heading),"Unsafe proximity preserves the original SafeTravel suppression even with combat HUD");game.enemies.Clear();game.mode="inventory";Draw(app);Check(!Has(heading),"Menus preserve the original world-only interaction prompt condition");
        Console.WriteLine("PASS actual zh/en normal/large 960/1280 state/name rows in both HUDs, center/corners and eight-pixel canvas margins, minimum controls/status/toast/tutorial/arrival/resume overlap, exact original generic NPC E/B geometry and mature SafeTravel/world visibility; NATIVE_UNVERIFIED");
    }
    private static void Append(StringBuilder text,object value,HashSet<object> seen)
    {
        if(value==null){text.Append("null;");return;}Type type=value.GetType();
        if(type.IsPrimitive||type.IsEnum||value is string||value is decimal){text.Append(Convert.ToString(value,CultureInfo.InvariantCulture)).Append(';');return;}
        if(!type.IsValueType && !seen.Add(value)){text.Append("ref;");return;}var sequence=value as IEnumerable;
        if(sequence!=null){foreach(object item in sequence)Append(text,item,seen);text.Append("end;");return;}
        var fields=type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);Array.Sort(fields,(a,b)=>string.CompareOrdinal(a.Name,b.Name));
        foreach(var field in fields){text.Append(field.Name).Append('=');Append(text,field.GetValue(value),seen);}
    }
    private static string State(GameCore g) { var text=new StringBuilder();Append(text,g,new HashSet<object>());return text.ToString(); }
    private static void ReadOnlyMouseAndWitness(FrontierRuntime app)
    {
        Prepare(app);var g=app.Game;var target=Object(g,"herb");Approach(app,target);
        var victim=new Enemy(59402,0,g.Position.x+1.2f,g.Position.y,1){stun=100,timer=9999};g.enemies.Add(victim);
        Pointer(app,(Point)Call(app,"ScreenPoint",victim.Position));Set(app,"pointerAimActive",true);Key(app,KeyCode.Q);
        int ticks=0;while(g.enemies.Contains(victim)&&ticks++<130)Update(app);Check(!g.enemies.Contains(victim) && g.RecentCombatLoot!=null,"A real Q release prepares an existing combat-loot receipt beside the resource");
        Draw(app);g.PrepareSave();Check(SaveStore.Write(g.save,0),"Actual checksum save exists before read-only passes");string disk=File.ReadAllText(SaveStore.PathFor(0));string state=State(g);float clock=Get<float>(app,"clock");object receipt=g.RecentCombatLoot;
        var matrix=Matrix4x4.TRS(new Vector3(13,21,0),Quaternion.Euler(0,0,11),new Vector3(1.25f,1.25f,1));var tint=new Color(.3f,.4f,.5f,.6f);
        foreach(EventType evt in new[]{EventType.Layout,EventType.Repaint,EventType.Layout,EventType.Repaint})
        {
            Event.current.type=evt;GUI.matrix=matrix;GUI.color=tint;GUI.enabled=false;Draw(app);Card(app,target);
            Check(State(g)==state && Get<float>(app,"clock")==clock && ReferenceEquals(g.RecentCombatLoot,receipt),"Actual repeated Layout/Repaint changes no Core/save/receipt identity or presentation clock");
            Check(File.ReadAllText(SaveStore.PathFor(0))==disk,"Drawing the state card never touches the checksum save on disk");
            Check(GUI.matrix.Equals(matrix)&&GUI.color.Equals(tint)&&!GUI.enabled,"Actual OnGUI restores incoming matrix, tint and disabled state");
        }
        Event.current.type=EventType.Repaint;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;
        foreach(bool combat in new[]{false,true})foreach(bool fresh in new[]{false,true})
        {
            Prepare(app);g=app.Game;target=Object(g,"herb");Approach(app,target);Set(app,"combatHudTime",combat?3f:0f);Draw(app);Rect bounds=Card(app,target);
            Point pixel=new Point(bounds.x+50,bounds.y+44),desired=(pixel-(Point)Call(app,"ScreenPoint",g.Position)).Normalized;
            Check(!(bool)Call(app,"PointerOverInterface",pixel),"Read-only state card passes pointers to the world in either HUD");Pointer(app,pixel);if(fresh)Input.buttonDown.Add(0);else Input.buttons.Add(0);Update(app);Input.Clear();
            Check(g.playerAction!=null && g.playerAction.kind=="attack" && g.playerAction.direction.x*desired.x+g.playerAction.direction.y*desired.y>.99f,"Actual "+(fresh?"fresh":"held")+" mouse over the state row starts and aims the original world attack");
            Prepare(app);g=app.Game;target=Object(g,"herb");Approach(app,target);Set(app,"combatHudTime",combat?3f:0f);Draw(app);Point slot=combat?new Point(440,740):new Point(364,710);
            Check((bool)Call(app,"PointerOverInterface",slot),"Existing Q control remains pointer protected");Pointer(app,slot);if(fresh)Input.buttonDown.Add(0);else Input.buttons.Add(0);Update(app);Input.Clear();Check(g.playerAction==null,"Actual fresh/held mouse on the protected Q control never leaks a world attack");
        }
        foreach(bool combat in new[]{false,true})
        {
            Prepare(app);g=app.Game;target=Object(g,"herb");Approach(app,target);Set(app,"combatHudTime",combat?3f:0f);Anchor(app,target,new Point(1260,390));Draw(app);Rect bounds=Card(app,target);
            Point s=(Point)Call(app,"ScreenPoint",new Point(target.x,target.y));Point pixel=new Point(bounds.x+4,bounds.y+bounds.height-3);
            Point body=new Point(target.x+(pixel.x-s.x)/36f,target.y+(pixel.y-s.y)/36f);Point warning=new Point(target.x+(bounds.x+160-s.x)/36f,target.y+(bounds.y+32-s.y)/36f);
            var enemy=new Enemy(59403,0,body.x,body.y,80){state=1,timer=.3f,windupDuration=.65f,target=warning};enemy.Face(warning-body);g.enemies.Add(enemy);
            Check(g.SafeTravel && (enemy.Position-g.Position).Length>=8,"Far hostile fixture keeps actual nearest prompt safe and requested HUD unchanged");Draw(app,"threat-witness-"+(combat?"combat":"exploration"));bounds=Card(app,target);
            Check((bool)Call(app,"ThreatOver",bounds),"Actual world actor/warning overlaps the registered state card");int fill=GUI.textureCalls.FindIndex(c=>c.texture==Texture2D.whiteTexture && Same(c.rect,bounds));
            Check(fill>=0 && Near(GUI.textureCalls[fill].color.a,.12f),"Threatened state panel uses the existing exact .12 opacity cap");
            Check(GUI.textureCalls.FindLastIndex(c=>c.texture.name.Contains("outline_Monsters"))>fill,"Existing foreground actor outline is drawn after the state-card fill");
            ClearDraw();Call(app,"DrawHudThreatWitness");Check(GUI.textureCalls.Exists(c=>c.texture.name.Contains("outline_Monsters")) && GUI.textureCalls.Exists(c=>c.rect.width==2),"Shared foreground witness retains the actual actor and attack warning over the new card");
        }
        Console.WriteLine("PASS repeated real Layout/Repaint full Core/save/receipt/clock and disk read-only behavior, GUI restoration, actual fresh/held click-through in both HUDs with protected Q controls, .12 threat opacity and existing foreground actor/telegraph witness; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-interaction-hints-"+Guid.NewGuid());Application.persistentDataPath=directory;
        captureFolder=args.Length>0 && args[0]!="-"?args[0]:null;if(captureFolder!=null)Directory.CreateDirectory(captureFolder);var app=new FrontierRuntime();int failed=0;
        try
        {
            Call(app,"Awake");Check(app.Game!=null,"Actual Awake loads shipped campaign");
            foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{ResourcesRestAndNearestInput,GardenSealsCachesAndSave,LayoutsNamesAndOriginalGeneric,ReadOnlyMouseAndWitness})
                try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}
        }
        finally{app.EndVisualReview();Call(app,"OnDestroy");Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime interaction hints: "+(4-failed)+"/4 groups passed; captures="+captures+"; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
