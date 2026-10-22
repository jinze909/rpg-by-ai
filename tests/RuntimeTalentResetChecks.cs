// Actual K / Update / OnGUI / SaveStore contracts through offline Unity declarations.
// These command-stream captures do not replace native Editor / GPU / input QA.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeTalentResetChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly Rect Footer=new Rect(279,704,938,25);
    private static string captureFolder,capturePrefix;
    private static int captures;
    public sealed class Label { public string k,text; public float[] r; public int size; public int[] padding; }
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private static object Call(object obj,string name,params object[] args){var m=obj.GetType().GetMethod(name,Private|BindingFlags.Public);Check(m!=null,"Production method exists: "+name);return m.Invoke(obj,args);}
    private static void Set(object obj,string name,object value){var f=obj.GetType().GetField(name,Private);Check(f!=null,"Production field exists: "+name);f.SetValue(obj,value);}
    private static T Get<T>(object obj,string name){var f=obj.GetType().GetField(name,Private);Check(f!=null,"Production field exists: "+name);return(T)f.GetValue(obj);}
    private static object Pending(FrontierRuntime app){return Get<object>(app,"talentResetConfirmation");}
    private static bool Near(float a,float b){return Math.Abs(a-b)<.003f;}
    private static bool Same(Rect a,Rect b){return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height);}
    private static void Update(FrontierRuntime app){Call(app,"Update");Input.keys.Clear();}
    private static void Frames(FrontierRuntime app,int count){for(int i=0;i<count;i++)Update(app);}
    private static void Key(FrontierRuntime app,KeyCode key){Input.keys.Add(key);Update(app);}
    private static void Draw(FrontierRuntime app,string name=null)
    {
        GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        if(captureFolder!=null&&name!=null){var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();GUI.Export(captureFolder,capturePrefix+"-"+name);captures++;GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);}
    }
    private static string Id(FrontierRuntime app,Rect r){return(string)Call(app,"ControlId",r.x,r.y,r.width,r.height);}
    private static void Click(FrontierRuntime app,Rect r){GUI.queuedClick=r;Draw(app);Check(!GUI.queuedClick.HasValue,"Actual enabled mouse control consumes its click");}
    private static string Economy(GameCore g){return g.save.coins+"/"+g.save.skillPoints+"/"+string.Join(",",g.save.talents)+"/"+g.save.health.ToString(CultureInfo.InvariantCulture)+"/"+g.save.maxHealth.ToString(CultureInfo.InvariantCulture);}
    private static string Permanent(GameCore g){return g.save.weapon+"/"+g.save.technique+"/"+g.save.relicRank+"/"+string.Join(",",g.save.weaponStyles)+"/"+string.Join(",",g.save.mastery)+"/"+string.Join(",",g.save.relics)+"/"+JsonUtility.ToJson(g.save.battlePlans[0])+"/"+string.Join(",",g.save.inventory)+"/"+string.Join(",",g.save.flags)+"/"+g.save.stage;}
    private static void Prepare(FrontierRuntime app)
    {
        Input.Clear();GUI.queuedClick=null;GUI.enabled=true;GUI.color=Color.white;GUI.matrix=Matrix4x4.identity;Event.current.type=EventType.Repaint;Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;
        app.BeginVisualReview(1,0);app.VisualReviewPaused=false;var g=app.Game;g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();g.effects.Clear();g.fallen.Clear();g.save.companion=-1;g.save.english=true;g.save.largeText=false;g.save.showGuidance=false;g.save.reducedMotion=true;g.save.effectsQuality=0;
        g.save.upgrades=2;g.save.talents=new[]{2,3,1};g.save.skillPoints=4;g.save.coins=90;g.save.maxHealth=176;g.save.health=168;g.save.energy=67;g.save.weapon=1;g.save.weaponStyles=new[]{1,2,1};g.save.mastery=new[]{36,74,10};g.save.relics=new[]{0,2};g.save.relicRank=2;g.save.technique=1;if(!g.Has("technique_1"))g.save.flags.Add("technique_1");if(!g.Has("relic_0"))g.save.flags.Add("relic_0");if(!g.Has("relic_2"))g.save.flags.Add("relic_2");if(!g.Has("return_letter_complete"))g.save.flags.Add("return_letter_complete");g.save.battlePlans[0]=new BattlePlan{recorded=true,weapon=1,style=2,technique=1,relicFirst=0,relicSecond=2};g.toastTime=0;
        Set(app,"applicationPaused",false);Set(app,"pointerSeen",false);Set(app,"controllerUsed",false);Set(app,"resumeTime",0f);Call(app,"Resize");Key(app,KeyCode.K);Check(g.mode=="talents","Actual K opens original Talents page");Draw(app);
    }
    private static void Open(FrontierRuntime app,bool canPay=true)
    {
        string economy=Economy(app.Game);Click(app,Footer);Check(Economy(app.Game)==economy,"One actual footer click previews without spending coins or talent points");Draw(app);Check(Pending(app)!=null&&app.Game.mode=="talents","Confirmation is a true owned overlay while the mature Talents mode remains");
        var nav=Get<MenuNavigation>(app,"navigation");Check(nav.scope.Contains("/talent-reset/"),"Modal owns an isolated tokenized navigation scope");var controls=ModalButtons(app);Check(controls.Count==(canPay?2:1),"Only available modal actions own input, with Confirm registered only when payable");Check(nav.focused==Id(app,controls[0].rect),"Every newly opened confirmation starts with the left Cancel focused");
    }
    private static List<GUI.ButtonCall> ModalButtons(FrontierRuntime app)
    {
        var nav=Get<MenuNavigation>(app,"navigation");var result=GUI.buttonCalls.FindAll(b=>nav.CanActivate(Id(app,b.rect)));result.Sort((a,b)=>a.rect.x.CompareTo(b.rect.x));return result;
    }
    private static Rect Confirm(FrontierRuntime app){Draw(app);var buttons=ModalButtons(app);Check(buttons.Count==2,"Two modal controls are available");return buttons[1].rect;}
    private static void Deliberate(FrontierRuntime app,KeyCode key){Key(app,KeyCode.Tab);Draw(app);var buttons=ModalButtons(app);Check(Get<MenuNavigation>(app,"navigation").focused==Id(app,buttons[1].rect),"One deliberate Tab reaches the modal Confirm");Key(app,key);Draw(app);}
    private static void Refunded(FrontierRuntime app,string permanent,float energy)
    {
        var s=app.Game.save;Check(s.coins==70&&s.skillPoints==10&&s.talents[0]==0&&s.talents[1]==0&&s.talents[2]==0,"Exactly one accepted reset charges 20 coins and refunds all six ranks into existing four points");Check(s.health==140&&s.maxHealth==140&&Near(s.energy,energy),"Only original nonhealing health cap applies; energy is unchanged");Check(Permanent(app.Game)==permanent,"Technique unlock/selection, styles, mastery, relics, battle plan, story and inventory are retained\nBEFORE "+permanent+"\nAFTER "+Permanent(app.Game));Check(Pending(app)==null&&app.Game.mode=="talents","Accepted confirmation retires its token and stays on Talents");
    }
    public static void RealPreviewCancelDeliberateAndExactlyOnce(FrontierRuntime app)
    {
        foreach(KeyCode cancel in new[]{KeyCode.Return,KeyCode.JoystickButton0,KeyCode.Escape,KeyCode.JoystickButton1})
        {
            Prepare(app);string before=Economy(app.Game),permanent=Permanent(app.Game);Open(app);Key(app,cancel);Draw(app);Check(Pending(app)==null&&app.Game.mode=="talents"&&Economy(app.Game)==before&&Permanent(app.Game)==permanent,"Default Enter/pad A and explicit Esc/pad B cancel without modifying the build");
        }
        Prepare(app);string mouseBefore=Economy(app.Game),mousePermanent=Permanent(app.Game);Open(app);Draw(app);Click(app,ModalButtons(app)[0].rect);Check(Pending(app)==null&&app.Game.mode=="talents"&&Economy(app.Game)==mouseBefore&&Permanent(app.Game)==mousePermanent,"Actual mouse Cancel preserves the full build without charging");
        foreach(int input in new[]{0,1,2})
        {
            Prepare(app);string permanent=Permanent(app.Game);float energy=app.Game.save.energy;Open(app);Rect old=Confirm(app);if(input==0)Click(app,old);else Deliberate(app,input==1?KeyCode.Return:KeyCode.JoystickButton0);Refunded(app,permanent,energy);string paid=Economy(app.Game);GUI.queuedClick=old;Draw(app);GUI.queuedClick=null;Key(app,KeyCode.Return);Draw(app);Check(Economy(app.Game)==paid,"Old mouse rectangle and repeated Return cannot spend a retired confirmation twice");
        }
        Prepare(app);app.Game.save.skillPoints=95;string overflow=Economy(app.Game);Draw(app);Open(app,false);var unavailable=GUI.buttonCalls.Find(b=>Same(b.rect,new Rect(652,577,260,42)));Check(unavailable!=null&&!unavailable.enabled,"Point capacity overflow permits a reason preview but disables payment on the real modal");GUI.queuedClick=unavailable.rect;Draw(app);Check(GUI.queuedClick.HasValue,"Unavailable overflow payment rejects actual mouse activation");GUI.queuedClick=null;Check(Economy(app.Game)==overflow&&Pending(app)!=null,"Overflow cannot silently lose earned points or take coins");Key(app,KeyCode.Escape);Draw(app);Check(Pending(app)==null&&Economy(app.Game)==overflow,"Unavailable quote remains safely cancellable");
        Console.WriteLine("PASS actual K/footer nonspending preview, isolated Cancel-first Enter/pad A and Esc/pad B, deliberate mouse/Tab-Enter/Tab-pad confirmation, exact single charge/refund/health cap, permanent build retention and overflow rejection; NATIVE_UNVERIFIED");
    }
    public static void ModalParentOwnershipAndPauseGuards(FrontierRuntime app)
    {
        Prepare(app);Open(app);string economy=Economy(app.Game),permanent=Permanent(app.Game);Rect confirmation=Confirm(app);
        foreach(Rect parent in new[]{Footer,new Rect(297,475,266,29),new Rect(302,646,891,31),new Rect(48,180,174,43)}){GUI.queuedClick=parent;Draw(app);Check(GUI.queuedClick.HasValue,"Modal blocks parent mouse button even when its action was available");GUI.queuedClick=null;Check(Pending(app)!=null&&Economy(app.Game)==economy&&Permanent(app.Game)==permanent,"Blocked parent click cannot learn, reset, switch technique or leave the menu");}
        foreach(KeyCode key in new[]{KeyCode.I,KeyCode.K,KeyCode.M,KeyCode.G,KeyCode.C,KeyCode.B,KeyCode.N,KeyCode.F1,KeyCode.JoystickButton4,KeyCode.JoystickButton5}){Key(app,key);Draw(app);Check(Pending(app)!=null&&app.Game.mode=="talents"&&Economy(app.Game)==economy,"Modal blocks parent menu hotkeys and LB/RB switching");}
        foreach(bool review in new[]{false,true})
        {
            if(review)app.VisualReviewPaused=true;else Call(app,"OnApplicationPause",true);Draw(app);GUI.queuedClick=confirmation;Draw(app);Check(GUI.queuedClick.HasValue&&Economy(app.Game)==economy,"Paused applications/review disable the mouse confirmation even during OnGUI");GUI.queuedClick=null;Key(app,KeyCode.Tab);Key(app,KeyCode.Return);Draw(app);Check(Economy(app.Game)==economy,"Paused keyboard cannot confirm either");if(review)app.VisualReviewPaused=false;else Call(app,"OnApplicationPause",false);
        }
        Key(app,KeyCode.Escape);Draw(app);Check(Pending(app)==null&&app.Game.mode=="talents","Resume can still cancel the paused modal");
        Prepare(app);Open(app);string focused=Economy(app.Game);Rect retired=Confirm(app);Call(app,"OnApplicationFocus",false);Draw(app);Check(Pending(app)==null&&app.Game.mode=="talents"&&Economy(app.Game)==focused,"Actual application focus loss retires confirmation without changing the build");Call(app,"OnApplicationFocus",true);GUI.queuedClick=retired;Draw(app);GUI.queuedClick=null;Check(Pending(app)==null&&Economy(app.Game)==focused,"Refocus cannot pay through a retired mouse rectangle");
        foreach(string mutation in new[]{"coins","health","talents","skill-points","save-owner","world-generation","game-owner","mode","death"})
        {
            Prepare(app);Open(app);Rect stale=Confirm(app);GameCore original=app.Game;
            if(mutation=="coins")original.save.coins--;
            else if(mutation=="health")original.save.health--;
            else if(mutation=="talents")original.save.talents[0]++;
            else if(mutation=="skill-points")original.save.skillPoints++;
            else if(mutation=="save-owner"){original.Load(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(original.save)));original.mode="talents";}
            else if(mutation=="world-generation"){original.Enter(0);original.Close();original.mode="talents";}
            else if(mutation=="game-owner"){var next=new GameCore(original.data,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(original.save)));next.mode="talents";typeof(FrontierRuntime).GetProperty("Game").GetSetMethod(true).Invoke(app,new object[]{next});}
            else if(mutation=="mode")original.mode="inventory";
            else {original.mode="world";original.save.health=1;original.invulnerable=0;Call(original,"Hurt",999f,"environment",(Point?)null);original.mode="talents";}
            string before=Economy(app.Game),old=Economy(original);GUI.queuedClick=stale;Draw(app);GUI.queuedClick=null;Check(Economy(app.Game)==before&&Economy(original)==old,"Stale "+mutation+" cannot pay on behalf of either game/save owner");bool values=mutation=="coins"||mutation=="health"||mutation=="talents"||mutation=="skill-points";Check((Pending(app)!=null)==values,"Stale "+mutation+" preserves a disabled value-change preview or retires an invalid owner");if(values){Check(!GUI.buttonCalls.Find(b=>Same(b.rect,stale)).enabled,"Changed growth values disable payment before the click is processed");Key(app,KeyCode.Escape);Draw(app);Check(Pending(app)==null&&Economy(app.Game)==before,"Changed-value quote can be cancelled without payment");}
        }
        Console.WriteLine("PASS actual parent mouse/menu-key/LB/RB isolation, application/review paused confirmation gates, focus-loss cancellation, disabled stale value quotes, invalid save/game/world/mode/death owner retirement; NATIVE_UNVERIFIED");
    }
    public static void LegacySavesAndCapturedCombatRemainIntact(FrontierRuntime app)
    {
        for(int version=1;version<=2;version++)
        {
            Prepare(app);var g=app.Game;g.save.version=version;File.WriteAllText(SaveStore.PathFor(1),JsonUtility.ToJson(g.save));string warning;var old=SaveStore.Read(1,out warning);Check(old!=null&&warning==null,"Production SaveStore reads unchanged raw v"+version);g.Load(old);Check(g.save.version==2&&(version==1?g.save.relics[0]==-1&&g.save.relics[1]==-1:g.save.relics[0]==0&&g.save.relics[1]==2),"Existing v1 migration clears unsupported equipped expansion relics while v2 equipment is retained");g.Start();Key(app,KeyCode.K);Draw(app);string permanent=Permanent(g);float energy=g.save.energy;Open(app);Deliberate(app,KeyCode.Return);Refunded(app,permanent,energy);g.PrepareSave();Check(SaveStore.Write(g.save,0),"Production checksum writer accepts confirmed build");var saved=SaveStore.Read(0,out warning);Check(saved!=null&&warning==null&&saved.coins==70&&saved.skillPoints==10&&saved.talents[0]+saved.talents[1]+saved.talents[2]==0&&saved.health==140,"Production checksum read retains exact paid/refunded build");g.Load(saved);g.Start();Draw(app);Check(Permanent(g)==permanent&&Pending(app)==null,"Legacy story/build survives roundtrip without serializing a modal");string json=JsonUtility.ToJson(saved);Check(!json.Contains("talentReset")&&!json.Contains("refundedPoints")&&!json.Contains("reasonEn"),"Transient confirmation adds no fields to mature save JSON");
        }
        foreach(bool flying in new[]{false,true})
        {
            Prepare(app);var g=app.Game;Key(app,KeyCode.Escape);g.Enter(1);g.Close();g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();Check(app.SetVisualReviewPosition(new Point(12,14)),"Original room accepts legal combat feet");g.aim=new Point(1,0);g.save.seed=0;Set(app,"camera",g.Position);var enemy=new Enemy(67891,0,g.Position.x+4.5f,g.Position.y,999){timer=1000};g.enemies.Add(enemy);Key(app,KeyCode.J);var action=g.playerAction;Check(action!=null&&action.kind=="attack"&&!action.released,"Actual J starts the mature bow attack");
            if(flying){int frames=0;while(!action.released&&frames++<50)Update(app);Check(action.released&&g.projectiles.Count>0&&enemy.hp==999,"Actual delayed bow release is still flying before the menu opens");}
            var shot=g.projectiles.Count>0?g.projectiles[0]:null;float damage=action.damage;var impact=action.impact;float critical=impact.criticalChance;Key(app,KeyCode.K);Draw(app);float pausedAction=action.elapsed,pausedLife=shot==null?0:shot.life;float total=g.save.totalTime;Open(app);Frames(app,15);Check(g.save.totalTime==total&&action.elapsed==pausedAction&&(shot==null||shot.life==pausedLife),"Actual Talents confirmation pauses the current prepared/flying action without aging it");Deliberate(app,KeyCode.Return);Check(g.save.coins==70&&g.save.skillPoints==10&&ReferenceEquals(g.playerAction,action)&&action.damage==damage&&ReferenceEquals(action.impact,impact)&&impact.criticalChance==critical,"Confirmed talent reset retains the prepared action identity, damage and immutable critical snapshot");if(shot!=null)Check(ReferenceEquals(g.projectiles[0],shot)&&ReferenceEquals(shot.impact,impact),"Flying projectile keeps original damage/impact owner through the reset");Key(app,KeyCode.Escape);int ticks=0;while(enemy.hp==999&&ticks++<150)Update(app);Check(enemy.hp<999,"Original prepared/flying attack resumes and physically contacts after accepting the new future build");
        }
        Console.WriteLine("PASS production raw v1/v2 and checksummed SaveStore, exact paid build/story/equipment/plans persistence, absent transient schema, actual J preparation/flying shot pause and immutable captured damage/critical/owner through confirmation and later contact; NATIVE_UNVERIFIED");
    }
    public static void BilingualLayoutAndReadOnlyPresentation(FrontierRuntime app)
    {
        foreach(int width in new[]{960,1280,1920})foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})
        {
            Prepare(app);app.Game.save.english=english;app.Game.save.largeText=large;Screen.width=width;Screen.height=width==960?600:width==1280?800:1080;Call(app,"Resize");Draw(app);Open(app);CheckPreview(app);Draw(app,"preview-"+(english?"en":"zh")+(large?"-large":"-normal")+"-"+width);string before=JsonUtility.ToJson(app.Game.save);int revision=app.Game.revision;float clock=Get<float>(app,"clock");object pending=Pending(app);var art=Get<PixelArt>(app,"art");int textures=Get<List<Texture2D>>(art,"owned").Count,cache=Get<Dictionary<string,Texture2D>>(art,"cache").Count;
            foreach(EventType evt in new[]{EventType.Layout,EventType.Repaint})
            {
                Event.current.type=evt;GUI.color=new Color(.2f,.3f,.4f,.5f);GUI.enabled=false;GUI.matrix=Matrix4x4.TRS(new Vector3(3,5,0),Quaternion.Euler(0,0,10),new Vector3(1.4f,1.4f,1));var matrix=GUI.matrix;Draw(app);Check(JsonUtility.ToJson(app.Game.save)==before&&app.Game.revision==revision&&Get<float>(app,"clock")==clock&&ReferenceEquals(Pending(app),pending),"Actual repeated Layout/Repaint preserves complete save/revision/clock/quote identity");Check(!GUI.enabled&&Near(GUI.color.a,.5f)&&Near(GUI.matrix.m00,matrix.m00)&&Near(GUI.matrix.m03,matrix.m03),"Actual OnGUI restores incoming GUI enabled/color/matrix state");
            }
            Check(textures==Get<List<Texture2D>>(art,"owned").Count&&cache==Get<Dictionary<string,Texture2D>>(art,"cache").Count,"Repeated modal draws allocate no additional art texture or cached icons");GUI.color=Color.white;GUI.matrix=Matrix4x4.identity;GUI.enabled=true;Event.current.type=EventType.Repaint;Key(app,KeyCode.Escape);Draw(app);
        }
        Console.WriteLine("PASS actual bilingual normal/large 960/1280/1920 confirmation previews, complete refund/coin/health facts, modal button bounds, source-only command captures and read-only save/revision/clock/GUI/texture-cache rendering; NATIVE_UNVERIFIED");
    }
    private static void CheckPreview(FrontierRuntime app)
    {
        // Exact quote facts must be genuinely drawn, not only returned by Core.
        var labels=new List<Label>();foreach(string command in GUI.commands)if(command.StartsWith("{\"k\":\"text\"",StringComparison.Ordinal))labels.Add(JsonUtility.FromJson<Label>(command));
        string combined=string.Join("\n",labels.ConvertAll(l=>l.text).ToArray());var coins=labels.Find(l=>l.r!=null&&l.r.Length==4&&Near(l.r[0],368)&&Near(l.r[1],247));var refund=labels.Find(l=>l.r!=null&&l.r.Length==4&&Near(l.r[0],368)&&Near(l.r[1],289));var health=labels.Find(l=>l.r!=null&&l.r.Length==4&&Near(l.r[0],368)&&Near(l.r[1],385));Check(coins!=null&&coins.text.Contains("20")&&coins.text.Contains("90 → 70"),"The visible cost row quotes both20coins and90-to-70 balance");Check(refund!=null&&refund.text.Contains("6")&&refund.text.Contains("4 → 10"),"The visible refund row quotes six ranks and4-to-10 unused points");Check(health!=null&&health.text.Contains("176 → 140")&&health.text.Contains("168 → 140"),"The visible health row quotes exact maximum and current loss before payment");Check(combined.Contains(app.Game.L("天赋","talent"))||combined.Contains("Talents"),"Actual bilingual modal carries talent identity");
        var buttons=ModalButtons(app);Check(buttons.Count==2,"Modal registers precisely two available controls");foreach(var b in buttons)Check(b.rect.x>=24&&b.rect.y>=24&&b.rect.x+b.rect.width<=1256&&b.rect.y+b.rect.height<=776,"Both actual modal actions fit the authored menu canvas");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-talent-reset-"+Guid.NewGuid());Application.persistentDataPath=directory;captureFolder=args.Length>0&&args[0]!="-"?args[0]:null;capturePrefix=args.Length>1?args[1]:"qa-talent-reset";var app=new FrontierRuntime();int failed=0;
        try{Directory.CreateDirectory(directory);if(captureFolder!=null)Directory.CreateDirectory(captureFolder);Call(app,"Awake");foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{RealPreviewCancelDeliberateAndExactlyOnce,ModalParentOwnershipAndPauseGuards,LegacySavesAndCapturedCombatRemainIntact,BilingualLayoutAndReadOnlyPresentation})try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error));}app.EndVisualReview();Call(app,"OnDestroy");}
        finally{Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime talent-reset: "+(4-failed)+"/4 groups passed; captures="+captures+"; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
