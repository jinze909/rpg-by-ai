// Real E / numbered / mouse / pad blacksmith interaction through shipped Update and OnGUI.
// Offline command streams are not native Editor / GPU / hardware acceptance.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;
public static class RuntimeForgeQuoteChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static string captures, capturePrefix;
    static int captureCount;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static object Call(object target, string name, params object[] args)
    {
        MethodInfo method=target.GetType().GetMethod(name,Private|BindingFlags.Public);
        Check(method!=null,"Production method exists: "+name); return method.Invoke(target,args);
    }
    static void Set(FrontierRuntime app,string field,object value) { typeof(FrontierRuntime).GetField(field,Private).SetValue(app,value); }
    static T Get<T>(object target,string field) { return (T)target.GetType().GetField(field,Private).GetValue(target); }
    static T Member<T>(object target,string name)
    {
        var field=target.GetType().GetField(name); if(field!=null)return (T)field.GetValue(target);
        var property=target.GetType().GetProperty(name); Check(property!=null,"Quote member exists: "+name); return (T)property.GetValue(target,null);
    }
    static object Quote(GameCore g,int recipe) { return Call(g,"GetForgeQuote",recipe); }
    static bool Enabled(GameCore g,string action) { return (bool)Call(g,"DialogueChoiceEnabled",action); }
    static SaveData Copy(SaveData save) { return JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save)); }
    static void Update(FrontierRuntime app) { Call(app,"Update"); Input.keys.Clear(); }
    static void Key(FrontierRuntime app,KeyCode key) { Input.keys.Add(key); Update(app); }
    static bool Same(Rect a,Rect b) { return a.x==b.x && a.y==b.y && a.width==b.width && a.height==b.height; }
    static bool Text(string value) { return GUI.commands.Exists(c=>c.Contains("\"text\":"+GUI.S(value))); }
    static Rect ChoiceRect(int index,int count=7)
    { int columns=count>2?2:Math.Max(1,count); float width=(832-(columns-1)*14)/(float)columns; return new Rect(320+index%columns*(width+14),539+index/columns*45,width,37); }
    static string ChoiceId(FrontierRuntime app,int index,int count=7)
    { Rect r=ChoiceRect(index,count); return (string)Call(app,"ControlId",r.x,r.y,r.width,r.height); }
    static void Draw(FrontierRuntime app,string capture=null)
    {
        GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); Call(app,"OnGUI");
        if(capture!=null && captures!=null)
        {
            var commands=GUI.commands.ToArray(); var textures=GUI.textureCalls.ToArray(); var buttons=GUI.buttonCalls.ToArray();
            GUI.Export(captures,capturePrefix+"-"+capture); GUI.commands.AddRange(commands); GUI.textureCalls.AddRange(textures); GUI.buttonCalls.AddRange(buttons); captureCount++;
        }
    }
    static void Pages(FrontierRuntime app,string capture=null)
    {
        int page=0;
        do
        {
            Draw(app,capture==null?null:capture+"-page"+page);
            page++;
            if((bool)Call(app,"DialogueChoicesVisible"))return;
            Key(app,KeyCode.Return);
        } while(page<30);
        Check(false,"Actual dialogue pagination terminates before selecting a recipe");
    }
    static int Index(FrontierRuntime app,string action)
    { Check(app.Game.mode=="dialog" && app.Game.conversation!=null,"A choice belongs to a real current dialogue"); int index=Array.FindIndex(app.Game.conversation.choices,c=>c.action==action); Check(index>=0 && index<9,"Actual numbered action exists: "+action); return index; }
    static void Choose(FrontierRuntime app,string action,int input=1)
    {
        Pages(app); int index=Index(app,action), count=app.Game.conversation.choices.Length; Rect rect=ChoiceRect(index,count);
        Check(Enabled(app.Game,action),"The intended actual dialogue action is available: "+action);
        if(input==0)
        {
            Check(GUI.buttonCalls.Exists(b=>Same(b.rect,rect) && b.enabled),"The real mouse control is enabled"); GUI.queuedClick=rect; Draw(app); Check(!GUI.queuedClick.HasValue,"The actual displayed button consumes its click");
        }
        else if(input==1) Key(app,(KeyCode)((int)KeyCode.Alpha1+index));
        else
        {
            var nav=Get<MenuNavigation>(app,"navigation"); string id=ChoiceId(app,index,count); int countKeys=0;
            while(nav.focused!=id && countKeys++<32) { Key(app,KeyCode.Tab); Draw(app); }
            Check(nav.focused==id && nav.CanActivate(id),"Actual Tab focus reaches the intended enabled forge choice");
            Key(app,KeyCode.JoystickButton0); Draw(app);
        }
    }
    static void Prepare(FrontierRuntime app,int room=0)
    {
        Input.Clear(); GUI.queuedClick=null; GUI.enabled=true; GUI.color=Color.white; GUI.matrix=Matrix4x4.identity;
        Screen.width=1280; Screen.height=800; Time.unscaledDeltaTime=.02f; Event.current.type=EventType.Repaint;
        app.BeginVisualReview(0,room); app.VisualReviewPaused=false; var g=app.Game; g.enemies.Clear(); g.projectiles.Clear(); g.hazards.Clear();
        g.save.flags.RemoveAll(f=>f.StartsWith("supper_") || f.StartsWith("ally_") || f.StartsWith("homeward_") || f.StartsWith("commission_"));
        g.save.companion=-1; g.save.english=true; g.save.largeText=false; g.save.showGuidance=false; g.save.reducedMotion=true; g.save.effectsQuality=0;
        g.save.inventory[0]=19; g.save.inventory[1]=17; g.save.inventory[2]=50; g.save.inventory[3]=12; g.save.inventory[4]=13; g.save.inventory[6]=3; g.save.tea=4;
        g.save.upgrades=2; g.save.maxHealth=160; g.save.flags.RemoveAll(f=>f=="weapon_1" || f=="weapon_2"); g.save.weapon=0; g.save.projects[1]=0; g.save.totalTime=100; g.save.health=57; g.save.energy=41; g.save.foodUntil=160; g.save.teaUntil=190; g.save.mealKind=2; g.save.mealUntil=230;
        Set(app,"applicationPaused",false); Set(app,"pointerSeen",false); Set(app,"controllerUsed",false); Set(app,"arrivalTime",0f); Set(app,"resumeTime",0f); Set(app,"combatHudTime",0f); Set(app,"dialogParent",null); Call(app,"Resize");
    }
    static WorldObject Interact(FrontierRuntime app,string kind)
    {
        var g=app.Game; var host=Array.Find(g.Map.objects,o=>o.kind==kind); Check(host!=null,"The original authored host remains: "+kind); bool found=false;
        foreach(Point offset in new[]{new Point(0,-1),new Point(0,1),new Point(-1,0),new Point(1,0),new Point(.5f,.5f),new Point(0,-.5f)})
        {
            Point p=new Point(host.x,host.y)+offset; if(!g.CanStand(p))continue;
            Check(app.SetVisualReviewPosition(p) && (g.Position-p).Length<.001f,"Fixture staging uses legal unchanged terrain");
            if(g.Nearest()==host) { found=true; break; }
        }
        Check(found,"The actual nearest E target is precisely the authored "+kind); Set(app,"camera",g.Position); Key(app,KeyCode.E); Check(g.mode=="dialog","Actual E opens the host dialogue"); return host;
    }
    static void Open(FrontierRuntime app,int input=1)
    {
        Interact(app,"forge"); Choose(app,"forge_quotes",input);
        Check(app.Game.mode=="dialog" && app.Game.conversation.choices.Length==4,"Actual appended choice opens exactly four quote actions");
        string[] actions={"upgrade","bow","staff","forge_back"}; for(int i=0;i<4;i++)Check(app.Game.conversation.choices[i].action==actions[i],"Original recipe positions retained inside quote page"); Pages(app);
    }
    static void OriginalHostAndTransactions(FrontierRuntime app)
    {
        foreach(bool supper in new[]{false,true})
        {
            Prepare(app); var g=app.Game;
            if(supper)foreach(string flag in new[]{"ally_1","ally_2","ally_3"})g.save.flags.Add(flag);
            Interact(app,"forge");
            string[] original=supper?new[]{"upgrade","bow","staff","close","supper_prep_2","forge_quotes"}:new[]{"upgrade","bow","staff","ally3","close","forge_quotes"};
            Check(g.conversation.choices.Length==original.Length,"Quote entry adds to every original earned host choice");
            for(int i=0;i<original.Length;i++)Check(g.conversation.choices[i].action==original[i],"Original numbered forge/commission/supper action retained: "+i);
            Choose(app,"forge_quotes"); Pages(app); Choose(app,"forge_back"); Pages(app);
            for(int i=0;i<original.Length;i++)Check(g.conversation.choices[i].action==original[i],"Back to Noll restores the full original conversation and action order");
            if(supper)
            {
                Choose(app,"supper_prep_2"); Check(g.mode=="world"&&!g.Has("supper_prep_2"),"Original supper contribution still requires its original acceptance");
                g.save.flags.Add("supper_accept"); Interact(app,"forge"); Choose(app,"supper_prep_2");
                Check(g.mode=="dialog"&&Array.Exists(g.conversation.choices,c=>c.action=="supper_help_2"),"Original Noll supper dialogue remains reachable"); Choose(app,"supper_help_2"); Check(g.Has("supper_prep_2"),"Original earned contribution still completes");
            }
            else
            {
                var before=Copy(g.save); Choose(app,"ally3");
                Check(g.Has("ally_3")&&g.save.inventory[4]==before.inventory[4]-5&&g.save.coins==before.coins+40&&g.save.skillPoints==before.skillPoints+1&&g.save.inventory[6]==before.inventory[6]+1,"Original five-part pump commission retains all original rewards");
            }
        }
        for(int input=0;input<3;input++)for(int recipe=0;recipe<3;recipe++)
        {
            Prepare(app); Open(app,input); var g=app.Game; var before=Copy(g.save); var q=Quote(g,recipe); string action=new[]{"upgrade","bow","staff"}[recipe];
            Check(Member<bool>(q,"available"),"Stocked recipe is genuinely payable"); Choose(app,action,input);
            Check(g.mode=="world","Actual paid original recipe retains close-on-success behavior");
            Check(g.save.inventory[0]==before.inventory[0]-Member<int>(q,"wood")&&g.save.inventory[1]==before.inventory[1]-Member<int>(q,"crystals")&&g.save.inventory[4]==before.inventory[4]-Member<int>(q,"parts"),"Exact quoted original materials are spent once through mouse/numeric/pad");
            Check(g.save.coins==before.coins&&g.save.totalTime==before.totalTime&&g.save.energy==before.energy,"No money, world time or energy is charged by workshop transactions");
            if(recipe==0)Check(g.save.upgrades==3&&g.save.maxHealth==180&&g.save.health==180,"Original upgrade gains one tier, +20 maximum health and genuine full healing");
            else Check(g.save.weapon==recipe&&g.Has("weapon_"+recipe)&&g.save.health==before.health&&g.save.maxHealth==before.maxHealth,"Original newly crafted bow/staff unlocks and equips without healing");
            int[] paid=(int[])g.save.inventory.Clone(); int tier=g.save.upgrades,weapon=g.save.weapon; Key(app,(KeyCode)((int)KeyCode.Alpha1+recipe)); Draw(app); Check(string.Join(",",g.save.inventory)==string.Join(",",paid)&&g.save.upgrades==tier&&g.save.weapon==weapon,"Repeated retired numeric choice cannot pay a closed dialogue again; ordinary world regeneration may resume");
        }
        Console.WriteLine("PASS real E original recipe order, Noll commission and supper, quoted pages/back, mouse/numeric/pad exact original transactions and healing, no repeated charge; NATIVE_UNVERIFIED");
    }
    static void DisabledFreshnessAndFreeEquip(FrontierRuntime app)
    {
        foreach(string state in new[]{"short","cap"})foreach(int input in new[]{0,1,2})
        {
            Prepare(app); Open(app); var g=app.Game; var original=g.conversation;
            if(state=="short")g.save.inventory[0]=g.save.inventory[1]=g.save.inventory[4]=0; else g.save.upgrades=5;
            Draw(app); Check(!Enabled(g,"upgrade"),"Open dialogue rechecks current stock/tier rather than trusting its original label");
            var rect=ChoiceRect(0,4); Check(GUI.buttonCalls.Exists(b=>Same(b.rect,rect)&&!b.enabled),"Current unavailable quote disables the actual drawn recipe control");
            string before=JsonUtility.ToJson(g.save); int revision=g.revision;
            if(input==0){GUI.queuedClick=rect; Draw(app); Check(GUI.queuedClick.HasValue,"Disabled mouse cannot activate"); GUI.queuedClick=null;}
            else if(input==1)Key(app,KeyCode.Alpha1);
            else {var nav=Get<MenuNavigation>(app,"navigation"); string id=ChoiceId(app,0,4); Check(!nav.CanActivate(id),"Disabled recipe has no controller activation target"); nav.focused=id; Key(app,KeyCode.JoystickButton0); Draw(app);}
            Check(g.mode=="dialog"&&ReferenceEquals(original,g.conversation)&&JsonUtility.ToJson(g.save)==before&&g.revision==revision,"Unavailable input leaves dialogue, inventory, health, time and revision exact");
            g.Choose(0); Check(g.mode=="dialog"&&JsonUtility.ToJson(g.save)==before,"Authoritative Core rechecks disabled original recipe before closing");
        }
        Prepare(app); Open(app); var live=app.Game; live.save.inventory[0]=live.save.inventory[1]=live.save.inventory[4]=0; Draw(app); Check(!Enabled(live,"upgrade"),"Live shortage is disabled");
        live.save.inventory[0]=3; live.save.inventory[1]=2; live.save.inventory[4]=2; Draw(app); Check(Enabled(live,"upgrade"),"Same original conversation becomes payable once exact stock arrives"); Choose(app,"upgrade"); Check(live.save.upgrades==3&&live.save.inventory[0]==0&&live.save.inventory[1]==0&&live.save.inventory[4]==0,"Fresh re-enabled row consumes actual new stock exactly");
        foreach(int weapon in new[]{1,2})foreach(int input in new[]{0,1,2})
        {
            Prepare(app); var g=app.Game; g.save.flags.Add("weapon_"+weapon); g.save.inventory[0]=g.save.inventory[1]=g.save.inventory[4]=0;
            Open(app,input); object q=Quote(g,weapon); Check(Member<bool>(q,"available")&&Member<bool>(q,"owned")&&Member<int>(q,"wood")==0&&Member<int>(q,"crystals")==0&&Member<int>(q,"parts")==0,"Owned bow/staff honestly quotes free equipment with zero materials");
            g.Close(); g.aim=new Point(1,0); g.Attack(g.aim); Check(g.playerAction!=null,"Genuine accepted sword action exists before equipping");
            Check((bool)Call(g,"ShowForgeQuotes"),"Same nearby live smith can reopen read-only quotes while action is pending"); Pages(app);
            var expected=Copy(g.save); expected.weapon=weapon; Choose(app,weapon==1?"bow":"staff",input);
            Check(g.mode=="world"&&g.playerAction==null&&g.save.weapon==weapon,"Actual free equipment retains mature Equip/CancelAction behavior");
            Check(JsonUtility.ToJson(g.save)==JsonUtility.ToJson(expected),"Free owned equipment changes only weapon, never materials, healing or story");
        }
        Console.WriteLine("PASS live disabled shortage/tier5 mouse/numeric/pad/direct decisions retain dialogue and stock, same-row refill re-enables, owned bow/staff genuinely equip free and retain original CancelAction; NATIVE_UNVERIFIED");
    }
    static void GuardsLegacyAndCombat(FrontierRuntime app)
    {
        foreach(string state in new[]{"remote","inventory","practice","abyss","dying"})
        {
            Prepare(app); var g=app.Game; Interact(app,"forge"); g.Close();
            if(state=="remote"){g.save.x=3;g.save.y=3;} else if(state=="inventory")g.mode="inventory"; else if(state=="practice")g.practicing=true; else if(state=="abyss"){g.save.room=8;g.save.abyssFloor=1;} else g.deathTime=.5f;
            string save=JsonUtility.ToJson(g.save); string mode=g.mode; Check(!(bool)Call(g,"ShowForgeQuotes")&&JsonUtility.ToJson(g.save)==save&&g.mode==mode,"Public review rejects "+state+" without altering save/mode");
        }
        for(int version=1;version<=2;version++)
        {
            Prepare(app); var g=app.Game; Interact(app,"forge"); g.Close(); foreach(string flag in new[]{"core_1","relic_1","relic_4","return_letter_complete","archive_complete","weapon_1","weapon_2","technique_0"})if(!g.Has(flag))g.save.flags.Add(flag);
            g.save.weaponStyles=new[]{2,1,2};g.save.mastery=new[]{23,64,87};g.save.relics=new[]{1,4};g.save.relicRank=2;g.save.technique=1;
            g.mode="plans"; Check(g.SaveBattlePlan(0),"Earned original battle plan is recorded"); g.Close(); g.PrepareSave(); g.save.version=version;
            File.WriteAllText(SaveStore.PathFor(1),JsonUtility.ToJson(g.save)); string warning; var raw=SaveStore.Read(1,out warning); Check(raw!=null&&warning==null,"Production SaveStore reads raw v"+version); g.Load(raw); g.Start();
            Check(version==1?g.save.relics[0]==-1&&g.save.relics[1]==-1:g.save.relics[0]==1&&g.save.relics[1]==4,"Original v1 equipment migration and v2 retained equipment are respected");
            Open(app); var migrated=Copy(g.save); Choose(app,"upgrade"); g.PrepareSave(); Check(SaveStore.Write(g.save,0),"Actual checksum writer persists a paid quoted upgrade"); var loaded=SaveStore.Read(0,out warning);
            Check(loaded!=null&&warning==null&&loaded.upgrades==3&&loaded.health==180&&loaded.maxHealth==180&&loaded.inventory[0]==migrated.inventory[0]-3&&loaded.inventory[1]==migrated.inventory[1]-2&&loaded.inventory[4]==migrated.inventory[4]-2,"Raw legacy followed by production checksum keeps exact paid upgrade");
            Check(loaded.flags.Contains("return_letter_complete")&&loaded.flags.Contains("archive_complete")&&loaded.relics[0]==migrated.relics[0]&&loaded.relics[1]==migrated.relics[1]&&loaded.mastery[2]==87&&loaded.weaponStyles[2]==2&&loaded.technique==1&&loaded.battlePlans[0].relicFirst==1,"Mature story/equipment/style/mastery/technique and archived plan survive");
            string json=JsonUtility.ToJson(loaded); Check(!json.Contains("ForgeQuote")&&!json.Contains("reasonEn")&&!json.Contains("damageBonusAfter"),"Read-only quotes add no serialized schema");
        }
        foreach(bool flying in new[]{false,true})
        {
            Prepare(app); var g=app.Game; g.save.flags.Add("weapon_1"); g.Equip(1); Interact(app,"forge"); g.Close(); g.aim=new Point(1,0); g.Attack(g.aim); var action=g.playerAction; Check(action!=null&&!action.released,"Genuine bow windup exists at the original forge");
            if(flying){int ticks=0;while(!action.released&&ticks++<50)g.Tick(.01f,new Point());Check(action.released&&g.projectiles.Count>0,"Genuine bow release is flying before review");}
            var shot=g.projectiles.Count>0?g.projectiles[0]:null; var impact=action.impact; float damage=action.damage;
            Check((bool)Call(g,"ShowForgeQuotes"),"Actual near smith permits a nonspending review"); Pages(app); string before=JsonUtility.ToJson(g.save); int revision=g.revision; float elapsed=action.elapsed,total=g.save.totalTime,life=shot==null?0:shot.life;
            for(int i=0;i<20;i++){for(int recipe=0;recipe<3;recipe++)Quote(g,recipe); Update(app); Draw(app);}
            Check(JsonUtility.ToJson(g.save)==before&&g.revision==revision&&g.save.totalTime==total&&ReferenceEquals(g.playerAction,action)&&ReferenceEquals(action.impact,impact)&&action.damage==damage&&action.elapsed==elapsed,"Reading and actual menu frames never heal, age, cancel or recapture prepared damage/impact");
            if(shot!=null)Check(ReferenceEquals(g.projectiles[0],shot)&&ReferenceEquals(shot.impact,impact)&&shot.life==life,"Flying accepted projectile owner/impact/lifetime survives review unchanged");
            Choose(app,"forge_back"); Check(ReferenceEquals(g.playerAction,action)&&g.save.health==57,"Returning to Noll still does not heal or cancel the accepted attack");
        }
        Console.WriteLine("PASS near-live world/dialogue guards, raw v1/v2 and checksum SaveStore/mature progression without new schema, actual prepared and flying bow identity/damage/impact/clock preserved by read-only quotes; NATIVE_UNVERIFIED");
    }
    public sealed class Label { public string k,text; public float[] r; public int size; public int[] padding; }
    static void LayoutAndReadOnly(FrontierRuntime app)
    {
        int paginated=0;
        foreach(int width in new[]{960,1280,1920})foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})
        {
            Prepare(app); var g=app.Game; g.save.english=english;g.save.largeText=large;Screen.width=width;Screen.height=width==960?600:width==1280?800:1080;Call(app,"Resize");
            Interact(app,"forge"); Choose(app,"forge_quotes"); var pages=DialogueLayout.Pages(g.L(g.conversation.text,g.conversation.textEn),large); Check(pages.Count>=1,"Actual quote narrative has an existing layout page");if(pages.Count>1)paginated++;
            int page=0; string combined="";
            do
            {
                Draw(app); Check(Get<int>(app,"dialoguePage")==page,"Page state advances exactly once with real Enter");
                var labels=new List<Label>();foreach(string command in GUI.commands)if(command.StartsWith("{\"k\":\"text\"",StringComparison.Ordinal))labels.Add(JsonUtility.FromJson<Label>(command));
                string segment=pages[page]; Check(labels.Exists(l=>l.text==segment),"Actual GUI contains every complete authored page, not a replacement summary");combined+=segment;
                bool last=page==pages.Count-1;for(int i=0;i<4;i++)Check(GUI.buttonCalls.Exists(b=>Same(b.rect,ChoiceRect(i,4)))==last,"Recipe choices appear only after reading the actual final page");
                string name=(english?"en":"zh")+(large?"-large":"-normal")+"-"+width+"-page"+page; Draw(app,name);
                if(last)break; Key(app,KeyCode.Alpha1);Check(g.mode=="dialog"&&g.save.upgrades==2,"Early numbered recipe cannot bypass unread pages");Key(app,KeyCode.Return); page++;
            }while(page<20);
            Check(page==pages.Count-1&&combined==string.Join("",pages.ToArray()),"All actual authored quote pages are reachable without loss");
            Check(combined.Contains("19")&&combined.Contains("17")&&combined.Contains("13")&&combined.Contains("2 → 3/5")&&combined.Contains("+8 → +12")&&combined.Contains("160 → 180")&&combined.Contains("57")&&combined.Contains("180"),"Visible text includes exact stock, level/damage bonuses and original max/current health change");
            for(int i=0;i<4;i++){var b=GUI.buttonCalls.Find(v=>Same(v.rect,ChoiceRect(i,4)));Check(b!=null&&b.enabled&&b.rect.y+b.rect.height<=686,"All four authored bilingual choices remain enabled and inside the dialogue card");}
            string saved=JsonUtility.ToJson(g.save);int revision=g.revision;float clock=Get<float>(app,"clock");var art=Get<PixelArt>(app,"art");int textures=Get<List<Texture2D>>(art,"owned").Count,cache=Get<Dictionary<string,Texture2D>>(art,"cache").Count;
            foreach(EventType evt in new[]{EventType.Layout,EventType.Repaint})
            {
                Event.current.type=evt;GUI.color=new Color(.2f,.3f,.4f,.5f);GUI.enabled=false;GUI.matrix=Matrix4x4.TRS(new Vector3(3,5,0),Quaternion.Euler(0,0,10),new Vector3(1.4f,1.4f,1));var matrix=GUI.matrix;Draw(app);
                Check(JsonUtility.ToJson(g.save)==saved&&g.revision==revision&&Get<float>(app,"clock")==clock,"Repeated actual quote Layout/Repaint is read-only for save/revision/clock");Check(!GUI.enabled&&Math.Abs(GUI.color.a-.5f)<.001f&&Math.Abs(GUI.matrix.m03-matrix.m03)<.001f,"OnGUI restores incoming GUI enabled/color/matrix");
            }
            Check(textures==Get<List<Texture2D>>(art,"owned").Count&&cache==Get<Dictionary<string,Texture2D>>(art,"cache").Count,"Reading allocates no extra art textures or icons");
        }
        Check(paginated>0,"At least one actual bilingual/large quote uses genuine multi-page presentation");
        Console.WriteLine("PASS twelve actual bilingual normal/large 960/1280/1920 fully paginated quotes with stock/cost/health facts and final controls, read-only Layout/Repaint/GUI/art state; captures="+captureCount+"; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,temporary=Path.Combine(Path.GetTempPath(),"starfall-forge-quote-"+Guid.NewGuid());Application.persistentDataPath=temporary;
        captures=args.Length>0&&args[0]!="-"?args[0]:null;capturePrefix=args.Length>1?args[1]:"qa-forge-quote";var app=new FrontierRuntime();int failed=0;
        try
        {
            Directory.CreateDirectory(temporary);if(captures!=null)Directory.CreateDirectory(captures);Call(app,"Awake");
            foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{OriginalHostAndTransactions,DisabledFreshnessAndFreeEquip,GuardsLegacyAndCombat,LayoutAndReadOnly})try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error));}
            app.EndVisualReview();Call(app,"OnDestroy");
        }
        finally{Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;GUI.enabled=true;GUI.color=Color.white;GUI.matrix=Matrix4x4.identity;if(Directory.Exists(temporary))Directory.Delete(temporary,true);}
        Console.WriteLine("Runtime forge-quote: "+(4-failed)+"/4 groups passed; captures="+captureCount+"; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
