// Real authored E interactions, numbered/mouse/pad dialogue and SaveStore.
// Offline Unity declarations cannot establish native font/GPU or hardware feel.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeSupplyPreparationChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static string captures;
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
    static object Quote(GameCore g,int recipe,int quantity) { return Call(g,"GetSupplyBatchQuote",recipe,quantity); }
    static int Maximum(GameCore g,int recipe) { return (int)Call(g,"MaximumSupplyBatch",recipe); }
    static bool Enabled(GameCore g,string action) { return (bool)Call(g,"DialogueChoiceEnabled",action); }
    static bool Craft(GameCore g,int recipe,int quantity) { return (bool)Call(g,"CraftSupplyBatch",recipe,quantity); }
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
            GUI.Export(captures,"qa-supply-preparation-"+capture); GUI.commands.AddRange(commands); GUI.textureCalls.AddRange(textures); GUI.buttonCalls.AddRange(buttons); captureCount++;
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
        Check(false,"Actual dialogue pagination terminates before selecting a supply");
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
            Check(nav.focused==id && nav.CanActivate(id),"Actual Tab focus reaches the intended enabled supply choice");
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
        g.save.projects[1]=0; g.save.totalTime=100; g.save.health=57; g.save.energy=41; g.save.foodUntil=160; g.save.teaUntil=190; g.save.mealKind=2; g.save.mealUntil=230;
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
    static void Open(FrontierRuntime app,string kind="cook",int input=1)
    { Interact(app,kind); Choose(app,"supply_prepare",input); Check(app.Game.mode=="dialog","Actual appended choice opens supply preparation"); Pages(app); CheckBatchActions(app.Game); }
    static void CheckBatchActions(GameCore g)
    {
        Check(g.conversation!=null && g.conversation.choices.Length==7,"Preparation retains exactly seven numbered choices");
        string[] expected={"supply_batch_0_1","supply_batch_0_5","supply_batch_0_"+Maximum(g,0),"supply_batch_1_1","supply_batch_1_5","supply_batch_1_"+Maximum(g,1),"close"};
        for(int i=0;i<expected.Length;i++)Check(g.conversation.choices[i].action==expected[i],"Actual supply row retains recipe/quantity/order, including snapshot maximum: "+i);
    }
    static void ExactPayment(GameCore g,SaveData before,int recipe,int quantity)
    {
        var expected=Copy(before); expected.inventory[2]-=2*quantity;
        if(recipe==0) { expected.inventory[3]-=quantity; expected.inventory[6]+=quantity; }
        else { expected.inventory[1]-=quantity; expected.tea+=quantity; }
        Check(JsonUtility.ToJson(g.save)==JsonUtility.ToJson(expected),"Exactly the quoted ingredients and output change; all health, energy, time, meal/buff timers, money, history and other save fields remain exact");
    }
    static void OriginalHostChoicesAndSingleRecipes(FrontierRuntime app)
    {
        string[] cook={"cook","buymeal","buywood","tea","close","kitchen"};
        foreach(int room in new[]{0,9})foreach(bool supper in new[]{false,true})
        {
            Prepare(app,room); var g=app.Game; if(supper)foreach(string flag in new[]{"ally_1","ally_2","ally_3"})g.save.flags.Add(flag);
            Interact(app,"cook"); for(int i=0;i<cook.Length;i++)Check(g.conversation.choices[i].action==cook[i],"All six original chef action numbers remain unchanged");
            int appended=6; if(supper)Check(g.conversation.choices[appended++].action=="supper_menu","The existing optional supper action retains its original number");
            Check(Index(app,"supply_prepare")==appended,"New supply entry follows every original chef/supper choice");
            Choose(app,"kitchen"); Check(g.conversation.choices[0].action=="kitchen_cook_1","Original numbered road-kitchen entry still opens original recipes"); Choose(app,"close");
            Open(app); Choose(app,"close"); Interact(app,"cook"); Pages(app); SaveData before=Copy(g.save); Choose(app,"cook"); ExactPayment(g,before,0,1); Check(g.mode=="world","Original single stew still closes the chef dialogue");
            Interact(app,"cook"); Pages(app); before=Copy(g.save); Choose(app,"tea"); ExactPayment(g,before,1,1); Check(g.mode=="world","Original single tea retains its original fourth choice and close behavior");
            if(supper) { Interact(app,"cook"); Choose(app,"supper_menu"); Check(g.conversation!=null && Array.Exists(g.conversation.choices,c=>c.action=="supper_accept"),"Original supper entry still reaches its earned optional story"); Choose(app,"close"); }
        }
        string[] camp={"fieldrest","tea","drinktea","companions","close","kitchen"};
        foreach(int room in new[]{5,6,GameCore.ReachRoom})
        {
            Prepare(app,room); var g=app.Game; string optional=null;
            if(room==6) { foreach(string flag in new[]{"desert_report","personal_1","companion_1","commission_accepted","commission_tested","commission_reported","commission_caravan"})if(!g.Has(flag))g.save.flags.Add(flag); optional="commission_supply"; }
            if(room==GameCore.ReachRoom) { foreach(string flag in new[]{"core_1","reach_complete","personal_0","companion_0","homeward_accept"})if(!g.Has(flag))g.save.flags.Add(flag); optional="homeward_camp"; }
            Interact(app,"campfire"); for(int i=0;i<camp.Length;i++)Check(g.conversation.choices[i].action==camp[i],"All original camp choice numbers remain unchanged");
            int appended=6; if(optional!=null)Check(g.conversation.choices[appended++].action==optional,"Original Homeward/Commission camp extension retains its number");
            Check(Index(app,"supply_prepare")==appended,"Supply preparation appends after mature camp story choices"); Choose(app,"supply_prepare"); Pages(app); CheckBatchActions(g);
            Check(!Member<bool>(Quote(g,0,1),"available") && Member<bool>(Quote(g,1,1),"available"),"Unbuilt camp permits original tea ingredients while stew remains a chef recipe");
            g.enemies.Add(new Enemy(92641,0,g.Position.x+1,g.Position.y,50)); Check(!g.SafeTravel,"The ordinary nearby enemy really prevents safe travel");
            Check(Member<bool>(Quote(g,1,1),"available"),"Camp tea batch does not acquire road-kitchen waystation or SafeTravel restrictions");
            SaveData before=Copy(g.save); Choose(app,"supply_batch_1_1"); ExactPayment(g,before,1,1); Check(g.mode=="dialog","Camp brewing refreshes the real supply dialogue without consuming tea");
        }
        Console.WriteLine("PASS actual E at both chefs and three camps; all original single/road-kitchen/supper/Homeward/Commission choice indices, exact single recipes and original unrestricted camp tea; NATIVE_UNVERIFIED");
    }
    static void AllSevenChoicesAcrossMouseNumberAndPad(FrontierRuntime app)
    {
        for(int input=0;input<3;input++)for(int index=0;index<7;index++)
        {
            Prepare(app); Open(app,"cook",input); var g=app.Game; string action=g.conversation.choices[index].action; SaveData before=Copy(g.save); Conversation previous=g.conversation;
            if(index==6) { Choose(app,action,input); Check(g.mode=="world" && JsonUtility.ToJson(g.save)==JsonUtility.ToJson(before),"Seventh choice closes with no payment through every actual input"); continue; }
            int recipe=index/3, quantity=index%3==0?1:index%3==1?5:Maximum(g,recipe); object q=Quote(g,recipe,quantity);
            Check(Member<bool>(q,"available") && Member<int>(q,"recipe")==recipe && Member<int>(q,"quantity")==quantity && Member<int>(q,"herbs")==quantity*2 && Member<int>(q,"fish")== (recipe==0?quantity:0) && Member<int>(q,"crystals")== (recipe==1?quantity:0),"Live quote identifies the exact selected recipe, amount and every ingredient");
            Choose(app,action,input); ExactPayment(g,before,recipe,quantity); Check(g.mode=="dialog" && g.conversation!=previous,"Each paid batch refreshes the real dialogue in place"); Pages(app); CheckBatchActions(g);
        }
        Prepare(app); Open(app); var game=app.Game; SaveData first=Copy(game.save); Choose(app,"supply_batch_0_5"); ExactPayment(game,first,0,5); Pages(app); CheckBatchActions(game);
        int maximum=Maximum(game,0); SaveData second=Copy(game.save); Choose(app,"supply_batch_0_"+maximum,2); ExactPayment(game,second,0,maximum); Pages(app); CheckBatchActions(game);
        int tea=Maximum(game,1); SaveData third=Copy(game.save); Choose(app,"supply_batch_1_"+tea,0); ExactPayment(game,third,1,tea); Pages(app); CheckBatchActions(game);
        Check(Maximum(game,0)==0 && Maximum(game,1)==0,"The refreshed shared-herb balance prevents a second maximum from inventing ingredients");
        Console.WriteLine("PASS all seven real choices through actual mouse, numeric keys and Tab/pad A; exact 1/5/max ingredient/output transactions and repeated refreshed shared-stock preparation without healing, time or buffs; NATIVE_UNVERIFIED");
    }
    static void FreshDisabledQuotesAndFocus(FrontierRuntime app)
    {
        foreach(int recipe in new[]{0,1})for(int mask=1;mask<4;mask++)
        {
            Prepare(app); Open(app); var g=app.Game; g.save.inventory[2]=(mask&1)!=0?2:10;
            if(recipe==0)g.save.inventory[3]=(mask&2)!=0?1:5; else g.save.inventory[1]=(mask&2)!=0?1:5;
            object q=Quote(g,recipe,5); string reason=Member<string>(q,"reasonEn");
            Check(!Member<bool>(q,"available") && Member<int>(q,"maxQuantity")==Maximum(g,recipe),"Current short stock invalidates the existing five-unit choice with a fresh maximum");
            Check(((mask&1)==0 || reason.Contains("8") && reason.ToLowerInvariant().Contains("herb")) && ((mask&2)==0 || reason.Contains("4") && reason.ToLowerInvariant().Contains(recipe==0?"fish":"crystal")),"The quote explains every exact current ingredient deficit");
            Pages(app); int index=recipe*3+1; string action=g.conversation.choices[index].action; Draw(app);
            Check(!Enabled(g,action) && GUI.buttonCalls.Exists(b=>Same(b.rect,ChoiceRect(index)) && !b.enabled),"The original existing row is disabled by the current quote");
            string before=JsonUtility.ToJson(g.save); Conversation previous=g.conversation; int revision=g.revision;
            GUI.queuedClick=ChoiceRect(index); Draw(app); Check(GUI.queuedClick.HasValue,"Disabled mouse control cannot consume a click"); GUI.queuedClick=null;
            Key(app,(KeyCode)((int)KeyCode.Alpha1+index)); Check(g.mode=="dialog" && g.conversation==previous,"A disabled numbered action keeps the current dialogue open");
            var nav=Get<MenuNavigation>(app,"navigation"); string id=ChoiceId(app,index); Check(!nav.CanActivate(id),"Disabled quote is excluded from real focus activation");
            foreach(KeyCode key in new[]{KeyCode.Return,KeyCode.JoystickButton0}) { nav.focused=id; Key(app,key); Draw(app); }
            for(int i=0;i<9;i++) { Key(app,KeyCode.Tab); Draw(app); Check(nav.focused!=id,"Tab never visits disabled supply rows"); }
            Check(!Craft(g,recipe,5) && g.mode=="dialog" && JsonUtility.ToJson(g.save)==before && g.revision==revision,"Direct and all actual unavailable inputs cannot partly charge, close or mutate saves");
        }
        Prepare(app); Open(app); var game=app.Game; string stale=game.conversation.choices[2].action; Check(stale=="supply_batch_0_12","The displayed maximum captures a numeric twelve rather than a mutable max token");
        game.save.inventory[3]=11; string unchanged=JsonUtility.ToJson(game.save); Key(app,KeyCode.Alpha3);
        Check(game.mode=="dialog" && JsonUtility.ToJson(game.save)==unchanged && !Enabled(game,stale),"A stale maximum is revalidated by actual numeric input and cannot silently shrink or charge a different quantity");
        Prepare(app); Open(app); game=app.Game; game.save.inventory[1]=game.save.inventory[2]=game.save.inventory[3]=0; Draw(app);
        Check(Get<MenuNavigation>(app,"navigation").focused==ChoiceId(app,6),"When every recipe is unavailable the initial preferred focus is the enabled close choice");
        Prepare(app,5); Open(app,"campfire"); Draw(app); Check(Get<MenuNavigation>(app,"navigation").focused==ChoiceId(app,3),"At a camp initial preferred focus skips every unavailable stew choice and selects actual tea");
        Prepare(app); Open(app); game=app.Game; game.save.x=3; game.save.y=3; unchanged=JsonUtility.ToJson(game.save); Key(app,KeyCode.Alpha1);
        Check(game.mode=="dialog" && JsonUtility.ToJson(game.save)==unchanged && !Craft(game,0,1),"Leaving the actual nearest host while reading rejects a stale batch without charging ingredients");
        Console.WriteLine("PASS live exact deficits and maxima, disabled actual mouse/numeric/Enter/pad/Tab, first enabled preferred focus, immutable max amount and fresh nearest-host validation without partial spending; NATIVE_UNVERIFIED");
    }
    static void CapsLegacyAndBilingualReadOnlyPresentation(FrontierRuntime app)
    {
        Prepare(app); Open(app); var g=app.Game; g.save.inventory[6]=9998; g.save.tea=998; Pages(app); Choose(app,"close"); Open(app);
        Check(Maximum(g,0)==1 && Maximum(g,1)==1,"Both maximum batches respect the exact remaining stew9999/tea999 output capacity");
        SaveData before=Copy(g.save); Choose(app,"supply_batch_0_1"); ExactPayment(g,before,0,1); Pages(app); before=Copy(g.save); Choose(app,"supply_batch_1_1"); ExactPayment(g,before,1,1); Pages(app); CheckBatchActions(g);
        Check(g.save.inventory[6]==9999 && g.save.tea==999 && Maximum(g,0)==0 && Maximum(g,1)==0,"Maximum stock exposes no phantom output capacity");
        Choose(app,"close"); before=Copy(g.save);
        Check(!Member<bool>(Quote(g,0,1),"available") && !Member<bool>(Quote(g,1,1),"available") && !Craft(g,0,1) && !Craft(g,1,1) && JsonUtility.ToJson(g.save)==JsonUtility.ToJson(before),"Direct world-mode full-stock transactions cannot waste ingredients or overflow");
        g.save.flags.Add("supply_runtime_mature_story"); g.PrepareSave(); Check(SaveStore.Write(g.save,0),"Actual checksum envelope writes paid prepared supplies"); string warning; SaveData loaded=SaveStore.Read(0,out warning);
        Check(loaded!=null && warning==null && loaded.inventory[6]==9999 && loaded.tea==999 && loaded.flags.Contains("supply_runtime_mature_story"),"Actual checksummed SaveStore retains cap quantities and unrelated earned story");
        string raw=JsonUtility.ToJson(loaded); Check(!raw.Contains("maxQuantity") && !raw.Contains("reasonZh"),"Read-only quotes add no serialized save schema");
        foreach(int version in new[]{1,2})
        {
            loaded.version=version; File.WriteAllText(SaveStore.PathFor(1),JsonUtility.ToJson(loaded)); SaveData legacy=SaveStore.Read(1,out warning); Check(legacy!=null && warning==null,"Production SaveStore accepts raw legacy v"+version);
            g.Load(legacy); g.Start(); g.Close(); Open(app); Check(g.save.inventory[6]==9999 && g.save.tea==999 && g.Has("supply_runtime_mature_story") && Maximum(g,0)==0 && Maximum(g,1)==0,"Legacy reload preserves genuine capped stock and story without new supplies"); Choose(app,"close");
        }
        foreach(string kind in new[]{"cook","campfire"})foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})foreach(string state in new[]{"ready","short","full","one-space"})
        {
            Prepare(app,kind=="cook"?0:5); g=app.Game; g.save.english=english; g.save.largeText=large; Screen.width=large?960:1280; Screen.height=large?600:800; Call(app,"Resize");
            if(state=="short") { g.save.inventory[1]=0; g.save.inventory[2]=1; g.save.inventory[3]=0; }
            if(state=="full" || state=="one-space") { g.save.inventory[6]=state=="full"?9999:9998; g.save.tea=state=="full"?999:998; }
            Open(app,kind); CheckBatchActions(g); Pages(app);
            foreach(int recipe in new[]{0,1})
            {
                object quote=Quote(g,recipe,5); string reason=Member<string>(quote,english?"reasonEn":"reasonZh");
                Check(reason.Length==0 || g.L(g.conversation.text,g.conversation.textEn).Contains(reason),"The actual localized preparation narrative includes its exact current five-unit quote reason");
            }
            for(int i=0;i<7;i++)
            {
                var choice=g.conversation.choices[i]; Rect rect=ChoiceRect(i); var button=GUI.buttonCalls.Find(b=>Same(b.rect,rect));
                Check(button!=null && button.enabled==Enabled(g,choice.action),"Every displayed batch button agrees with its current authoritative availability");
                Check(Text((i+1)+"  "+g.L(choice.label,choice.en)),"Actual final page draws each complete localized quantity/cost label");
                Check(GUI.textureCalls.Exists(t=>t.rect.x==88 && t.rect.y==180 && t.rect.width==1104 && t.rect.y+t.rect.height>=rect.y+rect.height),"The actual dialogue card encloses all seven rows including close");
            }
            string unchangedSave=JsonUtility.ToJson(g.save); int revision=g.revision; var art=Get<PixelArt>(app,"art"); int owned=Get<List<Texture2D>>(art,"owned").Count, cache=Get<Dictionary<string,Texture2D>>(art,"cache").Count;
            for(int i=0;i<12;i++)Draw(app);
            Check(JsonUtility.ToJson(g.save)==unchangedSave && g.revision==revision && owned==Get<List<Texture2D>>(art,"owned").Count && cache==Get<Dictionary<string,Texture2D>>(art,"cache").Count,"Repeated quote and dialogue rendering is read-only and allocates no art textures");
            string name=kind+"-"+state+(english?"-en":"-zh")+(large?"-large960":"-normal1280");
            // Reopen via actual E to capture every narrative page, not merely the final choices.
            Choose(app,"close"); Interact(app,kind); Choose(app,"supply_prepare"); Pages(app,name);
        }
        Console.WriteLine("PASS exact full-stock boundaries, checksum and raw v1/v2 SaveStore, no schema addition, and 32 actual bilingual normal1280/large960 host/state presentations with all seven controls/read-only art caches; captures="+captureCount+"; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,temporary=Path.Combine(Path.GetTempPath(),"starfall-supply-preparation-"+Guid.NewGuid()); Application.persistentDataPath=temporary;
        captures=args.Length>0 && args[0]!="-"?args[0]:null; var app=new FrontierRuntime(); int failed=0;
        try
        {
            Directory.CreateDirectory(temporary); if(captures!=null)Directory.CreateDirectory(captures); Call(app,"Awake"); Check(app.Game!=null,"Actual Awake loads the unmodified campaign");
            foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[] { OriginalHostChoicesAndSingleRecipes,AllSevenChoicesAcrossMouseNumberAndPad,FreshDisabledQuotesAndFocus,CapsLegacyAndBilingualReadOnlyPresentation })
                try { test(app); } catch(Exception error) { failed++; Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error)); }
            app.EndVisualReview(); Call(app,"OnDestroy");
        }
        finally { Application.persistentDataPath=previous; Input.Clear(); GUI.queuedClick=null; GUI.enabled=true; GUI.color=Color.white; GUI.matrix=Matrix4x4.identity; if(Directory.Exists(temporary))Directory.Delete(temporary,true); }
        Console.WriteLine("Runtime supply preparation: "+(4-failed)+"/4 groups passed; NATIVE_UNVERIFIED"); return failed==0?0:1;
    }
}
