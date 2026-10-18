// Real Update, OnGUI and SaveStore paths with offline Unity declarations.
// Fixtures stage camera/HP/input; native import/render/play feel is unverified.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeCompanionTacticsChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string captureFolder;
    private static int captures;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static bool Near(float a, float b, float tolerance = .003f) { return Math.Abs(a - b) <= tolerance; }
    private static object Call(object obj, string name, params object[] args)
    { var method = obj.GetType().GetMethod(name, Private | BindingFlags.Public); Check(method != null, "Production entry point exists: " + name); return method.Invoke(obj, args); }
    private static void Set(object obj, string name, object value)
    { var field = obj.GetType().GetField(name, Private); Check(field != null, "Fixture field exists: " + name); field.SetValue(obj, value); }
    private static T Get<T>(object obj, string name)
    { var field = obj.GetType().GetField(name, Private); Check(field != null, "Fixture field exists: " + name); return (T)field.GetValue(obj); }
    private static object Public(object obj, string name)
    { var property = obj.GetType().GetProperty(name); Check(property != null, "Shipped read-only companion API exists: " + name); return property.GetValue(obj, null); }
    private static T Field<T>(object obj, string name)
    { Check(obj != null, "A genuine contact receipt/info exists before reading " + name); var field = obj.GetType().GetField(name); Check(field != null && field.IsInitOnly, "Published command data is immutable: " + name); return (T)field.GetValue(obj); }
    private static object Receipt(GameCore g) { return Public(g, "LastCompanionReceipt"); }
    private static float Feedback(GameCore g) { return (float)Public(g, "CompanionFeedbackTime"); }
    private static bool Same(Rect a, Rect b) { return Near(a.x,b.x) && Near(a.y,b.y) && Near(a.width,b.width) && Near(a.height,b.height); }
    private static bool Overlap(Rect a, Rect b) { return a.x < b.x+b.width && b.x < a.x+a.width && a.y < b.y+b.height && b.y < a.y+a.height; }
    private static void Update(FrontierRuntime app) { Call(app, "Update"); Input.keys.Clear(); }
    private static void Frames(FrontierRuntime app, int count) { for (int i=0;i<count;i++) Update(app); }
    private static void Key(FrontierRuntime app, KeyCode key) { Input.keys.Add(key); Update(app); }
    private static void Draw(FrontierRuntime app, string capture = null)
    {
        GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); Call(app, "OnGUI");
        if (captureFolder != null && capture != null)
        {
            var commands = GUI.commands.ToArray(); var textures = GUI.textureCalls.ToArray(); var buttons = GUI.buttonCalls.ToArray();
            GUI.Export(captureFolder, "qa-companion-"+capture); captures++;
            GUI.commands.AddRange(commands); GUI.textureCalls.AddRange(textures); GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static bool Has(string text) { return GUI.commands.Exists(c=>c.Contains("\"text\":"+GUI.S(text))); }
    private static bool ContainsText(string text) { return GUI.commands.Exists(c=>c.Contains("\"k\":\"text\"") && c.Contains(text)); }
    private static void Click(FrontierRuntime app, Rect rect)
    { Draw(app); Check(GUI.buttonCalls.Exists(b=>Same(b.rect,rect)&&b.enabled), "Displayed actual control is enabled at "+rect.x+","+rect.y); GUI.queuedClick=rect; Draw(app); Check(!GUI.queuedClick.HasValue,"Actual GUI consumes its displayed click"); }
    private static void CheckNewCommandButtonInk(Rect rect)
    {
        // ButtonCall holds the shared GUIStyle reference, whose local padding
        // has already been restored after drawing. The Label recording captures
        // the real per-call padding before that restoration, like actual ink.
        string geometry="\"r\":["+rect.x.ToString(CultureInfo.InvariantCulture)+","+rect.y.ToString(CultureInfo.InvariantCulture)+","+rect.width.ToString(CultureInfo.InvariantCulture)+","+rect.height.ToString(CultureInfo.InvariantCulture)+"]";
        string command=GUI.commands.Find(c=>c.Contains("\"k\":\"text\"") && c.Contains(geometry));
        Check(command!=null,"New command/back button records its actual text and content padding");
        int start=command.IndexOf("\"padding\":[",StringComparison.Ordinal)+11,end=command.IndexOf(']',start);
        string[] padding=command.Substring(start,end-start).Split(',');
        int top=int.Parse(padding[2],CultureInfo.InvariantCulture),bottom=int.Parse(padding[3],CultureInfo.InvariantCulture);
        // Shipped Noto Sans SC at the unchanged 16px button size has 18px
        // actual ink for 指令：贯穿三箭 / 指令：寒星领域. The older wrapped-line
        // tolerance allowed a 17px content box and could miss a clipped row.
        Check(rect.height-top-bottom>=18,"New 25px command/back button reserves all 18px of shipped Chinese glyph ink, actual content="+(rect.height-top-bottom));
    }
    private static void Pointer(FrontierRuntime app, Point point)
    { float scale=Get<float>(app,"scale"); Input.mousePosition=new Vector3(Get<float>(app,"offsetX")+point.x*scale,Screen.height-Get<float>(app,"offsetY")-point.y*scale,0); }
    private static void Prepare(FrontierRuntime app, int companion=0)
    {
        Input.Clear(); GUI.queuedClick=null; GUI.matrix=Matrix4x4.identity; GUI.color=Color.white; GUI.enabled=true;
        Screen.width=1280; Screen.height=800; Time.unscaledDeltaTime=.02f; Event.current.type=EventType.Repaint;
        app.BeginVisualReview(0,0); app.VisualReviewPaused=false; var g=app.Game; g.enemies.Clear(); g.effects.Clear(); g.projectiles.Clear(); g.hazards.Clear(); g.fallen.Clear();
        foreach(string flag in new[]{"companion_0","companion_1","companion_2","companion_tactics_mature_story"}) if(!g.Has(flag))g.save.flags.Add(flag);
        g.save.english=true; g.save.largeText=false; g.save.showGuidance=false; g.save.reducedMotion=true; g.save.effectsQuality=0;
        g.save.x=12; g.save.y=14; g.save.health=g.save.maxHealth; g.save.energy=100; g.save.relationships=new[]{3,3,3}; g.save.frontierEnding=0;
        g.save.companion=companion; g.companionCooldown=0; g.companionPosition=g.Position+new Point(-1,0); g.companionAction=null;
        g.save.heat=g.save.cold=0; g.save.seed=1; g.toastTime=0; g.invulnerable=100; g.save.totalTime=100;
        Set(app,"camera",g.Position); Set(app,"applicationPaused",false); Set(app,"controllerUsed",false); Set(app,"pointerSeen",false);
        Set(app,"combatHudTime",0f); Set(app,"resumeTime",0f); Set(app,"arrivalTime",0f); Call(app,"Resize"); Pointer(app,new Point(880,390));
    }
    private static Rect CommandSlot(bool combat) { return combat ? new Rect(685,732,78,50) : new Rect(702,699,109,75); }
    private static Rect Notes(int id) { return new Rect(279+id*318,704,302,25); }
    private static Rect Tab(int id) { return new Rect(279+id*318,174,302,36); }
    private static void Command(FrontierRuntime app, int input)
    { if(input==0)Key(app,KeyCode.Z); else if(input==1)Key(app,KeyCode.JoystickButton5); else Click(app,CommandSlot((bool)typeof(FrontierRuntime).GetProperty("CombatHud",Private).GetValue(app,null))); }
    private static object Release(FrontierRuntime app, int input=0)
    {
        Command(app,input); Check(app.Game.companionAction!=null && app.Game.companionAction.kind=="command" && Near(app.Game.companionAction.contact,.2f),"Real command starts original .2s preparation");
        Check(Receipt(app.Game)==null && Feedback(app.Game)==0,"Accepted input alone creates no misleading contact receipt");
        Frames(app,9); Check(app.Game.companionAction!=null && !app.Game.companionAction.released && Receipt(app.Game)==null,"Nine real .02s simulation frames have no early contact");
        Frames(app,1); Check(app.Game.companionAction!=null && app.Game.companionAction.released && Feedback(app.Game)>2.15f,"Tenth real .02s frame releases once with genuine feedback");
        return Receipt(app.Game);
    }
    private static void ActualInputsAndReceipts(FrontierRuntime app)
    {
        for(int id=0;id<3;id++)for(int input=0;input<3;input++)
        {
            Prepare(app,id); var g=app.Game; g.save.health=g.save.maxHealth-3; float damage=g.Damage; Point center=g.Position;
            Enemy controlled=null;
            if(id==2){controlled=new Enemy(85001,0,center.x+1,center.y,500,false);controlled.timer=100;g.enemies.Add(controlled);}
            object receipt=Release(app,input); Check(Field<int>(receipt,"id")==id && Field<int>(receipt,"bond")==3,"Receipt identifies accepted companion and cast bond");
            Check(Near(g.companionCooldown,17.8f),"Original 18-second command cooldown counts preparation exactly once");
            if(id==0)
            {
                Check(Near(Field<float>(receipt,"actualHeal"),3) && Near(Field<float>(receipt,"actualEnergy"),0),"Mira's real receipt reports clamped missing HP and zero actual energy gain at full energy");
                Check(Near(g.save.health,g.save.maxHealth) && g.guardTime>2.9f && Field<int>(receipt,"arrowCount")==0,"Original actual healing/three-second guard emits no invented arrows");
            }
            else if(id==1)
            {
                Check(Field<int>(receipt,"arrowCount")==3 && g.projectiles.Count==3,"Ivo's actual release creates exactly three existing piercing arrows");
                Check(Near(Field<float>(receipt,"arrowBaseDamage"),damage*.95f) && g.projectiles.TrueForAll(p=>p.friendly && p.piercing && !p.explosive && Near(p.damage,damage*.95f)),"Receipt and actual three-arrow base damage retain .8 + bond*.05 and existing projectile type");
            }
            else
            {
                Check(Field<int>(receipt,"controlCount")==1 && Near(controlled.stun,.45f) && g.effects.Exists(e=>e.kind=="frost"),"Sen reports only a genuine nearby controlled enemy; real first frost contact applies original .45s control (count="+Field<int>(receipt,"controlCount")+", stun="+controlled.stun+")");
                Check(Near(Field<float>(receipt,"frostPulseDamage"),damage*.4f) && (Field<Point>(receipt,"center")-center).Length<.001f,"Receipt reports original .4 base damage and player-centered field");
            }
            object before=Receipt(g); float cooldown=g.companionCooldown; Command(app,0);
            Check(ReferenceEquals(before,Receipt(g)) && g.companionCooldown<cooldown,"Rejected cooldown command cannot replace a true receipt or renew cooldown");
        }
        Prepare(app,0); app.Game.save.frontierEnding=3; Release(app); Check(Near(app.Game.companionCooldown,11.8f),"Shared-governance covenant retains original 12-second command cooldown");
        Prepare(app,0); Command(app,0); Frames(app,9); app.Game.save.health=app.Game.save.maxHealth-2; app.Game.save.energy=98;
        Frames(app,1); object partial=Receipt(app.Game);
        Check(Near(Field<float>(partial,"actualHeal"),2) && Near(Field<float>(partial,"actualEnergy"),1.72f),"Mira's actual energy receipt measures the partial cap after the original .28 frame regeneration instead of promising nominal +15");
        Console.WriteLine("PASS real Z/RB/mouse commands for all three companions, .2s contact boundary, actual clamped healing, piercing arrows, frost/control and original 18/12s cooldowns; NATIVE_UNVERIFIED");
    }
    private static string Progress(GameCore g)
    { return JsonUtility.ToJson(g.save)+"/"+g.companionCooldown+"/"+g.projectiles.Count+"/"+g.effects.Count+"/"+(g.companionAction==null?"none":g.companionAction.kind+g.companionAction.elapsed); }
    private static void NotesPreserveExistingCompanionControls(FrontierRuntime app)
    {
        Prepare(app,0); var g=app.Game; g.save.english=false; g.save.flags.Remove("companion_2"); Key(app,KeyCode.C); Check(g.mode=="companions","Original C opens companions"); Draw(app);
        for(int id=0;id<3;id++)
        {
            Check(GUI.buttonCalls.Exists(b=>Same(b.rect,new Rect(296+318*id,536,268,34))),"Original invite control keeps exact coordinates");
            Check(GUI.buttonCalls.Exists(b=>Same(b.rect,new Rect(296+318*id,583,268,32))),"Original story/meal control keeps exact coordinates");
            CheckNewCommandButtonInk(Notes(id));
        }
        Check(GUI.buttonCalls.Exists(b=>Same(b.rect,new Rect(279,652,938,38))),"Original travel-alone control retains exact bounds");
        string before=Progress(g); Click(app,Notes(2)); Check(g.mode=="companionnotes" && Get<int>(app,"selectedCompanionNote")==2,"Appended notes button previews the selected locked companion");
        Draw(app); CheckNewCommandButtonInk(new Rect(929,704,288,25));
        Check(Progress(g)==before && !g.Has("companion_2") && g.save.companion==0,"Reading a locked companion neither recruits, equips nor casts");
        for(int id=0;id<3;id++){Click(app,Tab(id));Check(Get<int>(app,"selectedCompanionNote")==id && Progress(g)==before,"Three real note tabs are entirely read-only");}
        Key(app,KeyCode.Z); Draw(app); Check(g.companionAction==null && g.companionCooldown==0,"Z in read-only notes cannot cast");
        Key(app,KeyCode.Escape); Check(g.mode=="companions","Escape returns notes to their companion parent"); Click(app,Notes(1)); Key(app,KeyCode.JoystickButton1); Check(g.mode=="companions","B returns notes to companions");
        Click(app,Notes(0)); Key(app,KeyCode.JoystickButton4); Check(g.mode=="talents","LB from notes uses companion menu's preceding category");
        Key(app,KeyCode.C); Click(app,Notes(1)); Key(app,KeyCode.JoystickButton5); Check(g.mode=="journal" && g.companionAction==null,"RB from notes navigates to journal instead of commanding");
        Check(g.Has("companion_tactics_mature_story") && !g.Has("companion_2"),"Existing story state and locked recruitment stay unchanged through navigation");
        Console.WriteLine("PASS original C/invite/story/travel-alone geometry, new command/back rows retain 18px Chinese ink, read-only locked preview/tabs, Escape/B parent and LB/RB navigation; NATIVE_UNVERIFIED");
    }
    private static void PauseAndSessionBoundaries(FrontierRuntime app)
    {
        Prepare(app); var g=app.Game; Release(app); float left=Feedback(g); Key(app,KeyCode.C); left=Feedback(g); float cooldown=g.companionCooldown; float world=g.save.totalTime;
        Click(app,Notes(0)); Frames(app,40); Check(Near(Feedback(g),left) && Near(g.companionCooldown,cooldown) && Near(g.save.totalTime,world),"Reading notes freezes feedback, cooldown and world simulation");
        Key(app,KeyCode.Escape); Key(app,KeyCode.Escape); Call(app,"OnApplicationPause",true); Frames(app,40);
        Check(Near(Feedback(g),left) && Near(g.companionCooldown,cooldown),"Application pause freezes true receipt lifetime without renewing it");
        Call(app,"OnApplicationPause",false); Frames(app,120); Check(Feedback(g)==0 && Receipt(g)!=null,"Feedback expires after simulation time but latest real receipt remains available in notes");
        foreach(string boundary in new[]{"travel","load","death","practice","select"})
        {
            Prepare(app); g=app.Game; Release(app);
            if(boundary=="travel")g.Enter(1);
            else if(boundary=="load")
            {
                g.PrepareSave(); Check(SaveStore.Write(g.save,2),"Actual checksum SaveStore accepts unchanged save schema"); string issue; SaveData loaded=SaveStore.Read(2,out issue); Check(loaded!=null && issue==null,"Checksum envelope reads without receipt save fields");
                string json=JsonUtility.ToJson(loaded); Check(!json.Contains("CompanionFeedbackTime")&&!json.Contains("LastCompanionReceipt"),"Receipt/session feedback does not enter legacy SaveData");g.Load(loaded);g.Start();
            }
            else if(boundary=="death"){g.invulnerable=0;Call(g,"Hurt",10000f,"environment",null);Check(g.Dying,"Real incoming damage begins mature death/recovery");}
            else if(boundary=="practice"){g.StartPractice();Check(g.practicing,"Original basic practice starts");}
            else g.SelectCompanion(1);
            Check(Receipt(g)==null && Feedback(g)==0,"Actual "+boundary+" boundary clears stale receipt immediately");
            Check(!(bool)typeof(FrontierRuntime).GetProperty("CompanionFeedbackVisible",Private).GetValue(app,null),"No stale world ribbon crosses actual "+boundary+" boundary");
        }
        Prepare(app,1); Command(app,0); Frames(app,4); Key(app,KeyCode.C); Frames(app,30); Check(Receipt(app.Game)==null,"Pausing a .08s preparation invents no early receipt");
        Key(app,KeyCode.Escape); Frames(app,6); Check(Receipt(app.Game)!=null,"The same unchanged preparation releases at its resumed real contact");
        Console.WriteLine("PASS notes/application pause freeze contact and receipt clocks, real travel/checksummed load/death/basic-practice/companion change clear transient state, no SaveData expansion; NATIVE_UNVERIFIED");
    }
    private static Rect Ribbon(bool combat) { return combat?new Rect(430,690,420,32):new Rect(342,653,596,33); }
    private static List<GUI.TextureCall> RibbonPanels(Rect bounds)
    { return GUI.textureCalls.FindAll(c=>c.texture==Texture2D.whiteTexture && Same(c.rect,bounds)); }
    private static void FeedbackPointerThreatAndPolicies(FrontierRuntime app)
    {
        foreach(bool combat in new[]{false,true})foreach(bool reduced in new[]{false,true})foreach(int fx in new[]{0,2})
        {
            Prepare(app); var g=app.Game; g.save.reducedMotion=reduced; g.save.effectsQuality=fx; Command(app,0); Set(app,"combatHudTime",combat?3f:0f); Draw(app);
            Rect bounds=Ribbon(combat); Check(RibbonPanels(bounds).Count>0,"Real preparing command displays short ribbon in both HUD layouts under every motion/FX policy");
            Check(!(bool)Call(app,"PointerOverInterface",new Point(bounds.x+10,bounds.y+10)),"Read-only ribbon passes world pointer attacks through");
            Check(((List<Rect>)Call(app,"HudRegions")).Exists(r=>Same(r,bounds)),"Ribbon participates in shared threat witness bounds");
            Pointer(app,new Point(bounds.x+30,bounds.y+15)); Input.buttons.Add(0); Update(app); Input.buttons.Clear(); Check(g.playerAction!=null,"Actual mouse hold over read-only ribbon still launches world J attack");
            Set(app,"camera",new Point(g.save.x,g.save.y-(705-390)/36f)); Draw(app); Check((bool)Call(app,"ThreatOver",bounds),"Visible player under ribbon is a true HUD threat overlap");
            Check(RibbonPanels(bounds).Exists(p=>p.color.a<=.13f),"Threat overlap reduces actual ribbon backdrop to .12 alpha");
            int panelIndex=GUI.textureCalls.FindIndex(p=>p.texture==Texture2D.whiteTexture&&Same(p.rect,bounds));
            Check(GUI.textureCalls.FindLastIndex(p=>p.texture.name.Contains("Player"))>panelIndex,"Actual hero witness remains after the overlapping ribbon; last="+GUI.textureCalls[GUI.textureCalls.Count-1].texture.name);
            g.Notify("原有提示保留","Existing notification retained"); Draw(app); Check(ContainsText("Existing notification retained"),"Command feedback never replaces mature toast content");
            Check(!Overlap(bounds,new Rect(20,108,260,62)),"Short command feedback is outside mature combat notification bounds");
            Call(app,"OnApplicationPause",true); Draw(app); Check(RibbonPanels(bounds).Count==0,"Paused application hides ribbon without implying active simulation");Call(app,"OnApplicationPause",false);
        }
        Prepare(app); Draw(app); Check(RibbonPanels(Ribbon(false)).Count==0,"No command and no actual receipt means no ribbon"); Release(app); Frames(app,120); Draw(app); Check(RibbonPanels(Ribbon(false)).Count==0,"Expired receipt has no lingering world panel");
        Console.WriteLine("PASS preparing/contact ribbon in both HUD layouts, low/full FX and reduced motion, true click-through attack, .12 threat backdrop with post-HUD hero witness, preserved toast and pause/expiry hiding; NATIVE_UNVERIFIED");
    }
    private static void BilingualLayoutCapturesAndReadOnlyDraw(FrontierRuntime app)
    {
        foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})foreach(int width in new[]{960,1920})
        {
            Prepare(app); var g=app.Game; g.save.english=english;g.save.largeText=large; Screen.width=width;Screen.height=width==960?600:1080;Call(app,"Resize");
            string suffix=(english?"en":"zh")+(large?"-large":"-normal")+"-"+width; Key(app,KeyCode.C);Draw(app,"menu-"+suffix);
            for(int id=0;id<3;id++)
            {
                if(g.mode=="companions")Click(app,Notes(id));else Click(app,Tab(id));
                string before=Progress(g); int selected=Get<int>(app,"selectedCompanionNote");
                foreach(EventType evt in new[]{EventType.Layout,EventType.Repaint}){Event.current.type=evt;Draw(app);Check(Progress(g)==before && Get<int>(app,"selectedCompanionNote")==selected,"Actual Layout/Repaint of notes is wholly simulation/save read-only");}
                Draw(app,"notes-"+id+"-"+suffix); Check(GUI.buttonCalls.TrueForAll(b=>!b.enabled||!Same(b.rect,CommandSlot(false))),"Notes page has no gameplay command control");
            }
            Event.current.type=EventType.Repaint; Key(app,KeyCode.Escape);Key(app,KeyCode.Escape); Command(app,0);Draw(app,"prepare-"+suffix); Frames(app,10);Draw(app,"receipt-"+suffix);
            Key(app,KeyCode.C);Click(app,Notes(0));Draw(app,"record-"+suffix);
        }
        Check(captureFolder==null || captures==56,"Exactly 56 actual bilingual/menu/tab/prepare/contact/receipt captures were written");
        Console.WriteLine("PASS actual bilingual normal/large text at 960/1920, three note tabs, world preparation/contact and saved latest receipt, read-only Layout/Repaint; captures="+captures+"; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        captureFolder=args.Length>0?args[0]:null;string previous=Application.persistentDataPath;
        string temporary=Path.Combine(Path.GetTempPath(),"starfall-companion-save-"+Guid.NewGuid());Application.persistentDataPath=temporary;
        var app=new FrontierRuntime();
        try
        {
            Call(app,"Awake");if(app.Game==null){Console.WriteLine("FAIL Awake cannot load shipped campaign");return 1;}
            int failures=0; Action<FrontierRuntime>[] tests={ActualInputsAndReceipts,NotesPreserveExistingCompanionControls,PauseAndSessionBoundaries,FeedbackPointerThreatAndPolicies,BilingualLayoutCapturesAndReadOnlyDraw};
            foreach(var test in tests)try{test(app);}catch(Exception error){failures++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}
            Console.WriteLine("Runtime companion tactics "+(tests.Length-failures)+"/"+tests.Length+"; NATIVE_UNVERIFIED");return failures==0?0:1;
        }
        finally{app.EndVisualReview();Call(app,"OnDestroy");Application.persistentDataPath=previous;if(Directory.Exists(temporary))Directory.Delete(temporary,true);}
    }
}
