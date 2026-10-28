// Real K / Spend / combat / SaveStore paths through offline Unity declarations.
// Command captures are not native Unity, GPU, hardware-input or play acceptance.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Starfall;
using UnityEngine;

public static class RuntimeTalentGrowthChecks
{
    private const BindingFlags All=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private static string captureFolder,capturePrefix;
    private static int captures;
    public sealed class Label { public string k,text; public float[] r; public int size; public int[] padding; }
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private static bool Near(float a,float b){return Math.Abs(a-b)<.003f;}
    private static bool Same(Rect a,Rect b){return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height);}
    private static object Call(object obj,string name,params object[] args){var m=obj.GetType().GetMethod(name,All);Check(m!=null,"Production method exists: "+name);return m.Invoke(obj,args);}
    private static object Helper(string name,params object[] args){return typeof(RuntimeTalentResetChecks).GetMethod(name,All).Invoke(null,args);}
    private static T Field<T>(object obj,string name){var f=obj.GetType().GetField(name,All);Check(f!=null,"Production field exists: "+name);return (T)f.GetValue(obj);}
    private static void Set(object obj,string name,object value){obj.GetType().GetField(name,All).SetValue(obj,value);}
    private static float Number(object obj,string name){return Convert.ToSingle(Field<object>(obj,name),CultureInfo.InvariantCulture);}
    private static Rect Spend(int slot){return new Rect(297+slot*318,475,266,29);}
    private static readonly Rect Reset=new Rect(279,704,938,25);
    private static void Tick(FrontierRuntime app){Call(app,"Update");Input.Clear();}
    private static void Key(FrontierRuntime app,KeyCode key){Input.keys.Add(key);Tick(app);}
    private static void Draw(FrontierRuntime app,string name=null)
    {
        GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        if(captureFolder!=null&&name!=null){var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();GUI.Export(captureFolder,capturePrefix+"-"+name);captures++;GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);}
    }
    private static void Click(FrontierRuntime app,Rect rect){GUI.queuedClick=rect;Draw(app);Check(!GUI.queuedClick.HasValue,"Actual enabled talent control consumes mouse click");}
    private static void Focus(FrontierRuntime app,Rect rect){Draw(app);Field<MenuNavigation>(app,"navigation").Select((string)Call(app,"ControlId",rect.x,rect.y,rect.width,rect.height));}
    private static void Prepare(FrontierRuntime app){Helper("Prepare",app);Check(app.Game.mode=="talents","Actual K opens original Talents page");Draw(app);}
    private static List<Label> Labels(){var labels=new List<Label>();foreach(string command in GUI.commands)if(command.StartsWith("{\"k\":\"text\"",StringComparison.Ordinal))labels.Add(JsonUtility.FromJson<Label>(command));return labels;}
    private static Label At(float x,float y,float width,float height){return Labels().Find(l=>l.r!=null&&l.r.Length==4&&Near(l.r[0],x)&&Near(l.r[1],y)&&Near(l.r[2],width)&&Near(l.r[3],height));}
    private static object Preview(GameCore game,int slot){return Call(game,"GetTalentGrowthPreview",slot);}
    private static void Expected(GameCore g,int slot,out float[] before,out float[] after)
    {
        int rank=g.save.talents[slot],next=Math.Min(5,rank+1);
        if(slot==0){float damage=g.WeaponContactDamage(g.save.weapon,g.Style),nextDamage;try{g.save.talents[0]=next;nextDamage=g.WeaponContactDamage(g.save.weapon,g.Style);}finally{g.save.talents[0]=rank;}before=new[]{damage,GameCore.Clamp(.06f+rank*.025f+(g.Relic(5)?.12f:0),0,1)};after=new[]{nextDamage,GameCore.Clamp(.06f+next*.025f+(g.Relic(5)?.12f:0),0,1)};}
        else if(slot==1){before=new[]{g.save.maxHealth,1-rank*.035f};after=new[]{g.save.maxHealth+(next-rank)*12,1-next*.035f};}
        else {before=new[]{g.Speed,.75f-rank*.06f};after=new[]{g.Speed+(next-rank)*.12f,.75f-next*.06f};}
    }
    private static void VisiblePair(Label label,float before,float after,bool percent,string message)
    {
        Check(label!=null,"Actual K draws numeric current/next values: "+message);
        var found=Regex.Matches(label.text,@"[-+]?(?:\d+(?:\.\d*)?|\.\d+)");
        Check(found.Count==2,"Visible growth value contains both current and next numbers: "+message+"; text="+label.text);
        float scale=percent?100:1;
        Check(Near(float.Parse(found[0].Value,CultureInfo.InvariantCulture),(float)Math.Round(before*scale,2,MidpointRounding.AwayFromZero))&&Near(float.Parse(found[1].Value,CultureInfo.InvariantCulture),(float)Math.Round(after*scale,2,MidpointRounding.AwayFromZero)),"Visible before/next numbers match live combat values: "+message+"; text="+label.text+"; expected="+before*scale+" -> "+after*scale);
        Check(label.text.Contains("→")&&(!percent||label.text.Contains("%")),"Growth values communicate current-to-next and percentage units: "+message);
    }
    private static void Cards(FrontierRuntime app)
    {
        var g=app.Game;
        // This semantic guard deliberately precedes every reference to the new
        // reflected API, so the same harness compiles against immutable old code.
        for(int i=0;i<3;i++)for(int row=0;row<2;row++)Check(At(302+i*318,row==0?354:408,256,27)!=null,"Real K card exposes both numeric current/next growth rows; slot="+i+" row="+row);
        for(int i=0;i<3;i++)
        {
            float[] before,after;Expected(g,i,out before,out after);object p=Preview(g,i);Check(p!=null,"Valid talent has immutable live comparison");
            Check(Field<int>(p,"talent")==i&&Field<int>(p,"rank")==g.save.talents[i]&&Field<int>(p,"nextRank")==Math.Min(5,g.save.talents[i]+1)&&Field<bool>(p,"canLearn")== (g.save.skillPoints>0&&g.save.talents[i]<5),"Preview identifies the actual slot, current/next rank and existing Spend availability");
            foreach(var f in p.GetType().GetFields(BindingFlags.Public|BindingFlags.Instance))Check(f.IsInitOnly,"Growth preview fields are immutable: "+f.Name);
            for(int row=0;row<2;row++)
            {
                object value=Field<object>(p,row==0?"first":"second");Check(value!=null&&Field<object>(value,"stat").GetType().IsEnum,"Growth values identify a typed stat");
                foreach(var f in value.GetType().GetFields(BindingFlags.Public|BindingFlags.Instance))Check(f.IsInitOnly,"Growth value fields are immutable: "+f.Name);
                Check(Near(Number(value,"before"),before[row])&&Near(Number(value,"after"),after[row]),"Core growth values match live current and projected build");
                Label caption=At(302+i*318,row==0?333:386,256,20);Check(caption!=null&&!string.IsNullOrWhiteSpace(caption.text),"Each numeric row has its own visible stat caption");
                VisiblePair(At(302+i*318,row==0?354:408,256,27),before[row],after[row],row==1&&i<2,"talent "+i+" stat "+row);
            }
            Label name=At(302+i*318,288,256,40);Check(name!=null&&Regex.IsMatch(name.text,g.save.talents[i]+@"\s*/\s*5"),"Original named talent card shows actual rank out of five");
            var button=GUI.buttonCalls.Find(b=>Same(b.rect,Spend(i)));Check(button!=null&&button.enabled==(g.save.skillPoints>0&&g.save.talents[i]<5),"Original Spend control retains exact geometry and rank/point availability");
            for(int pip=0;pip<5;pip++)Check(GUI.textureCalls.Exists(t=>Same(t.rect,new Rect(341+i*318+pip*37,442,26,5))),"Original five rank pips remain present");
        }
        foreach(Rect preserved in new[]{Reset,new Rect(302,560,885,35),new Rect(302,646,891,31)})Check(GUI.buttonCalls.Exists(b=>Same(b.rect,preserved)),"Original reset/technique controls keep authored bounds");
    }
    private static string Permanent(GameCore g){return g.save.weapon+"/"+g.save.technique+"/"+g.save.relicRank+"/"+string.Join(",",g.save.weaponStyles)+"/"+string.Join(",",g.save.mastery)+"/"+string.Join(",",g.save.relics)+"/"+JsonUtility.ToJson(g.save.battlePlans[0])+"/"+string.Join(",",g.save.inventory)+"/"+string.Join(",",g.save.flags)+"/"+g.save.stage;}
    public static void RealGrowthCardsAndThreeSpendInputs(FrontierRuntime app)
    {
        foreach(int slot in new[]{0,1,2})
        {
            Prepare(app);Cards(app);var g=app.Game;object old=Preview(g,slot);int rank=g.save.talents[slot],points=g.save.skillPoints,coins=g.save.coins;string permanent=Permanent(g);float max=g.save.maxHealth,health=g.save.health,energy=g.save.energy;
            if(slot==0)Click(app,Spend(slot));else{Focus(app,Spend(slot));Key(app,slot==1?KeyCode.Return:KeyCode.JoystickButton0);Draw(app);}
            Check(g.save.talents[slot]==rank+1&&g.save.skillPoints==points-1&&g.save.coins==coins&&Permanent(g)==permanent&&Near(g.save.energy,energy),"Mouse/Enter/pad A Spend buys exactly one existing rank for one point without changing equipment/story/economy");
            Check(Near(g.save.maxHealth,max+(slot==1?12:0))&&Near(g.save.health,health+(slot==1?12:0)),"Only Resolve preserves its original maximum/current +12 health transaction");Check(Field<int>(old,"rank")==rank,"Already returned comparison is unchanged after learning");Draw(app);Cards(app);
        }
        Prepare(app);var capped=app.Game;capped.save.talents=new[]{5,5,5};capped.save.skillPoints=4;Draw(app);Cards(app);string full=JsonUtility.ToJson(capped.save);foreach(int i in new[]{0,1,2}){GUI.queuedClick=Spend(i);Draw(app);GUI.queuedClick=null;Check(JsonUtility.ToJson(capped.save)==full,"Capped disabled Spend cannot consume an extra rank or point");}
        Prepare(app);app.Game.save.skillPoints=0;Draw(app);Cards(app);for(int i=0;i<3;i++){var p=Preview(app.Game,i);Check(Field<int>(p,"nextRank")==app.Game.save.talents[i]+1&&!Field<bool>(p,"canLearn"),"No-point page still shows future rank without enabling purchase");}Check(Preview(app.Game,-1)==null&&Preview(app.Game,3)==null&&Preview(app.Game,int.MaxValue)==null,"Invalid talent slots have no invented preview");
        Console.WriteLine("PASS real K numeric cards/ranks, mouse/Enter/pad A original Spend transactions, immutable typed comparisons, capped and no-point future values, original pips/reset/techniques; NATIVE_UNVERIFIED");
    }
    private static void World(FrontierRuntime app)
    {
        Key(app,KeyCode.Escape);var g=app.Game;Check(g.mode=="world","Original Escape closes K to gameplay");g.Enter(1);g.Close();g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();g.effects.Clear();Check(app.SetVisualReviewPosition(new Point(12,14)),"Outcome fixture has legal open world feet");Set(app,"camera",g.Position);g.aim=new Point(1,0);
    }
    private static float ReleasedArrow(FrontierRuntime app,out float critical)
    {
        Key(app,KeyCode.J);var g=app.Game;Check(g.playerAction!=null&&g.playerAction.kind=="attack"&&!g.playerAction.released,"Actual J accepts the bow preparation");critical=g.playerAction.impact.criticalChance;int frames=0;while(g.projectiles.Count==0&&frames++<50)Tick(app);Check(g.projectiles.Count==3,"Actual Flow bow releases the original three arrows");return g.projectiles[0].damage;
    }
    public static void ActualAttackHealthWalkAndDodgeMatch(FrontierRuntime app)
    {
        float lastDamage=0,lastCrit=0,lastSpeed=0,lastCooldown=0,lastHurt=0;
        for(int learned=0;learned<2;learned++)
        {
            Prepare(app);Cards(app);if(learned==1)Click(app,Spend(0));Draw(app);Cards(app);object edge=Preview(app.Game,0);float damage=Number(Field<object>(edge,"first"),"before"),crit=Number(Field<object>(edge,"second"),"before");World(app);float actualCrit,shot=ReleasedArrow(app,out actualCrit);Check(Near(shot,damage)&&Near(actualCrit,crit),"Displayed Edge damage/arrow and critical chance equal actual accepted impact and released Flow arrows");if(learned==1)Check(shot>lastDamage&&actualCrit>lastCrit,"Purchased Edge improves actual future arrows and captured critical chance");lastDamage=shot;lastCrit=actualCrit;
            Prepare(app);Cards(app);if(learned==1)Click(app,Spend(1));Draw(app);Cards(app);object resolve=Preview(app.Game,1);float hp=Number(Field<object>(resolve,"first"),"before"),factor=Number(Field<object>(resolve,"second"),"before");World(app);var g=app.Game;g.save.relics=new[]{-1,-1};g.save.difficulty=1;g.save.mealKind=0;g.save.mealUntil=0;g.guardTime=g.invulnerable=0;g.save.health=150;Call(g,"Hurt",20f,"environment",(Point?)null);float hurt=150-g.save.health;Check(Near(g.save.maxHealth,hp)&&Near(hurt,20*factor),"Resolve maximum health and talent-only incoming factor match the actual bounded Hurt transaction");if(learned==1)Check(hurt<lastHurt,"Purchased Resolve reduces actual otherwise unmitigated incoming damage");lastHurt=hurt;
            Prepare(app);Cards(app);if(learned==1)Click(app,Spend(2));Draw(app);Cards(app);object agility=Preview(app.Game,2);float speed=Number(Field<object>(agility,"first"),"before"),cooldown=Number(Field<object>(agility,"second"),"before");World(app);g=app.Game;Point feet=g.Position;Input.axes["Horizontal"]=1;Input.held.Add(KeyCode.D);Tick(app);Check(Near((g.Position-feet).Length,speed*Time.unscaledDeltaTime),"Displayed walk speed matches legal actual held-D world movement");g.save.energy=100;float energy=g.save.energy,cost=g.CurrentDodgeEnergyCost;Key(app,KeyCode.Space);Check(g.dashTime>0&&Near(g.dashCooldown,cooldown)&&Near(g.save.energy,energy-cost),"Displayed initial dodge cooldown matches actual Space and original paid energy cost");if(learned==1)Check(speed>lastSpeed&&g.dashCooldown<lastCooldown,"Purchased Agility improves actual walking and fresh dodge cooldown");lastSpeed=speed;lastCooldown=g.dashCooldown;
        }
        Prepare(app);Cards(app);var live=app.Game;string old=At(302,354,256,27).text;live.save.weapon=2;live.save.weaponStyles[2]=1;Draw(app);Cards(app);Check(At(302,354,256,27).text!=old,"Open K refreshes actual weapon/style contact damage");old=At(302,354,256,27).text;live.save.relics=new[]{1,5};live.save.health=live.save.maxHealth;live.save.foodUntil=live.save.totalTime+90;Draw(app);Cards(app);Check(At(302,354,256,27).text!=old,"Open K refreshes current relic, high-health and stew attack context");old=At(938,354,256,27).text;live.save.relics=new[]{2,-1};live.save.cold=80;live.save.mealKind=3;live.save.mealUntil=live.save.totalTime+100;Draw(app);Cards(app);Check(At(938,354,256,27).text!=old,"Open K refreshes current wind relic, cold and road-meal walking context");
        Console.WriteLine("PASS actual released Flow arrows/immutable criticals, bounded Hurt and +12 HP, legal held-D speed, paid Space cooldown, learned outcomes and live weapon/style/relic/food/health/cold/meal refresh; NATIVE_UNVERIFIED");
    }
    public static void ReadOnlyPaidActionLegacyAndOwners(FrontierRuntime app)
    {
        Prepare(app);Cards(app);var g=app.Game;string save=JsonUtility.ToJson(g.save);int revision=g.revision;float clock=Field<float>(app,"clock");var art=Field<PixelArt>(app,"art");int owned=Field<List<Texture2D>>(art,"owned").Count,cache=Field<Dictionary<string,Texture2D>>(art,"cache").Count;
        foreach(EventType e in new[]{EventType.Layout,EventType.Repaint}){Event.current.type=e;for(int n=0;n<4;n++){for(int i=-1;i<=3;i++)Preview(g,i);Draw(app);Cards(app);}}Event.current.type=EventType.Repaint;Check(JsonUtility.ToJson(g.save)==save&&g.revision==revision&&Near(Field<float>(app,"clock"),clock)&&owned==Field<List<Texture2D>>(art,"owned").Count&&cache==Field<Dictionary<string,Texture2D>>(art,"cache").Count,"Repeated real Layout/Repaint and preview queries preserve complete save/revision/clock and art ownership");
        Click(app,Reset);Check(Field<object>(app,"talentResetConfirmation")!=null&&JsonUtility.ToJson(g.save)==save,"Original reset still opens its own unpaid deliberate confirmation");Key(app,KeyCode.Escape);Draw(app);Check(Field<object>(app,"talentResetConfirmation")==null&&g.mode=="talents"&&JsonUtility.ToJson(g.save)==save,"Original reset cancellation retains K and the exact paid build");
        Prepare(app);Cards(app);g=app.Game;g.save.weaponStyles[1]=1;World(app);g.save.energy=100;float paidEnergy=g.save.energy;Key(app,KeyCode.J);var action=g.playerAction;Check(action!=null&&!action.released&&Near(g.save.energy,paidEnergy-4),"Actual Focused J pays original four energy before K");float damage=action.damage;var impact=action.impact;float chance=impact.criticalChance;Key(app,KeyCode.K);Draw(app);Cards(app);float elapsed=action.elapsed,time=g.save.totalTime,energyPaused=g.save.energy;for(int n=0;n<8;n++){Input.held.Add(KeyCode.J);Input.held.Add(KeyCode.D);Input.axes["Horizontal"]=1;Tick(app);Draw(app);Cards(app);}Check(ReferenceEquals(g.playerAction,action)&&Near(action.elapsed,elapsed)&&Near(g.save.totalTime,time)&&Near(g.save.energy,energyPaused)&&g.projectiles.Count==0,"Preview menu pauses the original paid action and ignores held attack/movement without extra spending");Click(app,Spend(0));Draw(app);Cards(app);Check(ReferenceEquals(g.playerAction,action)&&Near(action.damage,damage)&&ReferenceEquals(action.impact,impact)&&Near(impact.criticalChance,chance),"Learning future Edge preserves current paid action identity/damage/critical snapshot");Key(app,KeyCode.Escape);int ticks=0;while(g.projectiles.Count==0&&ticks++<50)Tick(app);Check(g.projectiles.Count>0&&Near(g.projectiles[0].damage,damage)&&ReferenceEquals(g.projectiles[0].impact,impact),"Original Focused projectile resumes with the same purchased action snapshot");
        foreach(int version in new[]{1,2})
        {
            Prepare(app);Cards(app);g=app.Game;g.save.version=version;File.WriteAllText(SaveStore.PathFor(1),JsonUtility.ToJson(g.save));string warning;SaveData raw=SaveStore.Read(1,out warning);Check(raw!=null&&warning==null,"Actual SaveStore reads raw legacy v"+version);g.Load(raw);g.Start();Key(app,KeyCode.K);Draw(app);Cards(app);string permanent=Permanent(g);Click(app,Spend(1));Draw(app);Cards(app);int rank=g.save.talents[1],points=g.save.skillPoints;g.PrepareSave();Check(SaveStore.Write(g.save,0),"Actual checksummed writer persists the paid legacy build");SaveData restored=SaveStore.Read(0,out warning);Check(restored!=null&&warning==null&&restored.talents[1]==rank&&restored.skillPoints==points,"Actual checksum read retains exact learned rank and remaining points");Check(!JsonUtility.ToJson(restored).Contains("TalentGrowth")&&!JsonUtility.ToJson(restored).Contains("nextRank"),"Growth presentation adds no transient preview to mature save JSON");g.Load(restored);g.Start();Key(app,KeyCode.K);Draw(app);Cards(app);Check(Permanent(g)==permanent,"Legacy/checksum roundtrip preserves existing story, equipment, mastery, inventory and plans");
        }
        Prepare(app);Cards(app);var oldGame=app.Game;var nextSave=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(oldGame.save));nextSave.talents=new[]{0,5,4};nextSave.skillPoints=0;oldGame.Load(nextSave);oldGame.Start();Key(app,KeyCode.K);Draw(app);Cards(app);Check(At(302,288,256,40).text.Contains("0"),"Load owner refreshes displayed growth instead of retaining old rank");var nextGame=new GameCore(oldGame.data,JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(nextSave)));nextGame.mode="talents";typeof(FrontierRuntime).GetProperty("Game").GetSetMethod(true).Invoke(app,new object[]{nextGame});Draw(app);Cards(app);Prepare(app);Cards(app);Check(app.Game.save.talents[0]==2&&app.Game.save.skillPoints==4,"A fresh review/new-game owner restores its own live growth and availability");
        Console.WriteLine("PASS complete preview/OnGUI purity, original reset cancellation, real paid Focused attack freeze/resume and immutable snapshots, raw v1/v2/checksum learned build persistence and save/game/review ownership; NATIVE_UNVERIFIED");
    }
    public static void BilingualBoundedGrowthCaptures(FrontierRuntime app)
    {
        foreach(string state in new[]{"normal","no-points","cap"})foreach(int width in new[]{960,1280})foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})
        {
            Prepare(app);Cards(app);var g=app.Game;if(state=="no-points")g.save.skillPoints=0;if(state=="cap")g.save.talents=new[]{5,5,5};g.save.english=english;g.save.largeText=large;Screen.width=width;Screen.height=width==960?600:800;Call(app,"Resize");Draw(app);Cards(app);string before=JsonUtility.ToJson(g.save);int revision=g.revision;Draw(app,state+"-"+(english?"en":"zh")+(large?"-large":"-normal")+"-"+width);Check(JsonUtility.ToJson(g.save)==before&&g.revision==revision,"Actual bilingual normal/large full K capture does not mutate the build");
            foreach(int i in new[]{0,1,2})foreach(int row in new[]{0,1}){var caption=At(302+i*318,row==0?333:386,256,20);var value=At(302+i*318,row==0?354:408,256,27);Check(caption.size==(large?14:13)&&value.size==(large?19:18),"Growth captions and numeric values use the intended readable normal/large fonts");}
        }
        Console.WriteLine("PASS 24 actual bilingual normal/large 960/1280 normal/no-point/capped K captures, every live stat row/rank/value and original Spend/pips/reset/technique bounds, read-only source command witnesses; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-talent-growth-"+Guid.NewGuid());Application.persistentDataPath=directory;captureFolder=args.Length>0&&args[0]!="-"?args[0]:null;capturePrefix=args.Length>1?args[1]:"qa-talent-growth";var app=new FrontierRuntime();int failed=0;
        try{Directory.CreateDirectory(directory);if(captureFolder!=null)Directory.CreateDirectory(captureFolder);Call(app,"Awake");foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{RealGrowthCardsAndThreeSpendInputs,ActualAttackHealthWalkAndDodgeMatch,ReadOnlyPaidActionLegacyAndOwners,BilingualBoundedGrowthCaptures})try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error));}app.EndVisualReview();Call(app,"OnDestroy");}
        finally{Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime talent growth: "+(4-failed)+"/4 groups passed; captures="+captures+"; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
