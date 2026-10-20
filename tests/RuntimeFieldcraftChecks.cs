// Real Update, OnGUI and SaveStore paths against offline Unity declarations.
// Native import, rendering, hardware input and play feel are NATIVE_UNVERIFIED.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeFieldcraftChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string captureFolder;
    private static SaveData earned;
    private static int nextId = 59100, liveFrames;
    private static void Check(bool value, string text) { if (!value) throw new Exception(text); }
    private static object Call(FrontierRuntime app, string name, params object[] args) { return typeof(FrontierRuntime).GetMethod(name, Private).Invoke(app, args); }
    private static void Set(FrontierRuntime app, string name, object value) { typeof(FrontierRuntime).GetField(name, Private).SetValue(app, value); }
    private static T Get<T>(FrontierRuntime app, string name) { return (T)typeof(FrontierRuntime).GetField(name, Private).GetValue(app); }
    private static void Update(FrontierRuntime app) { Call(app, "Update"); Input.keys.Clear(); liveFrames++; }
    private static void Frames(FrontierRuntime app, int count) { for (int i=0;i<count;i++) Update(app); }
    private static void Key(FrontierRuntime app, KeyCode key) { Input.keys.Add(key); Update(app); }
    private static string L(FrontierRuntime app, string zh, string en) { return app.Game.L(zh,en); }
    private static void Draw(FrontierRuntime app, string capture=null)
    {
        GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        if(captureFolder!=null&&capture!=null){var texts=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();GUI.Export(captureFolder,"qa-fieldcraft-"+capture);GUI.commands.AddRange(texts);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);}
    }
    private static bool Has(string text) { return GUI.commands.Exists(c=>c.Contains("\"text\":"+GUI.S(text))); }
    private static bool HasPart(string text) { return GUI.commands.Exists(c=>c.Contains(text)); }
    private static Rect TextBounds(string label)
    {
        foreach(string command in GUI.commands)if(command.Contains("\"text\":"+GUI.S(label)))
        {int a=command.IndexOf("\"r\":[",StringComparison.Ordinal)+5,b=command.IndexOf(']',a);string[] p=command.Substring(a,b-a).Split(',');return new Rect(float.Parse(p[0],CultureInfo.InvariantCulture),float.Parse(p[1],CultureInfo.InvariantCulture),float.Parse(p[2],CultureInfo.InvariantCulture),float.Parse(p[3],CultureInfo.InvariantCulture));}
        throw new Exception("Actual GUI lacks text: "+label);
    }
    private static void RowGlyphRoom(float rowHeight,int expectedRows)
    {
        int checkedRows=0;
        foreach(string command in GUI.commands)
        {
            if(!command.Contains("\"k\":\"text\""))continue;
            int rStart=command.IndexOf("\"r\":[",StringComparison.Ordinal)+5,rEnd=command.IndexOf(']',rStart);string[] rect=command.Substring(rStart,rEnd-rStart).Split(',');float x=float.Parse(rect[0],CultureInfo.InvariantCulture),h=float.Parse(rect[3],CultureInfo.InvariantCulture);
            if(Math.Abs(x-279)>.01f||Math.Abs(h-rowHeight)>.01f)continue;
            int padStart=command.IndexOf("\"padding\":[",StringComparison.Ordinal)+11,padEnd=command.IndexOf(']',padStart);string[] pad=command.Substring(padStart,padEnd-padStart).Split(',');int sizeStart=command.IndexOf("\"size\":",StringComparison.Ordinal)+7,sizeEnd=command.IndexOf(',',sizeStart);int size=int.Parse(command.Substring(sizeStart,sizeEnd-sizeStart),CultureInfo.InvariantCulture);
            Check(h-int.Parse(pad[2])-int.Parse(pad[3])>=size,"Every actual journal row reserves a full native-independent glyph height after vertical padding");checkedRows++;
        }
        Check(checkedRows==expectedRows,"Strict glyph-height check covers every actual quest row on the visible page");
    }
    private static void OrdinaryPadding(string label)
    {
        foreach(string command in GUI.commands)if(command.Contains("\"text\":"+GUI.S(label)))
        {Check(command.Contains("\"padding\":[6,6,4,4]"),"Scoped quest padding cannot leak to ordinary control "+label);return;}
        throw new Exception("Actual ordinary control not drawn: "+label);
    }
    private static bool Same(Rect a,Rect b){return a.x==b.x&&a.y==b.y&&a.width==b.width&&a.height==b.height;}
    private static bool Overlap(Rect a,Rect b){return a.x<b.x+b.width&&b.x<a.x+a.width&&a.y<b.y+b.height&&b.y<a.y+a.height;}
    private static void Click(FrontierRuntime app,string label)
    {Draw(app);Rect r=TextBounds(label);Check(GUI.buttonCalls.Exists(b=>Same(b.rect,r)&&b.enabled),"Actual enabled button exists: "+label);GUI.queuedClick=r;Draw(app);Check(!GUI.queuedClick.HasValue,"Actual button consumes its click: "+label);}
    private static void Position(FrontierRuntime app,Point point)
    {Check(app.Game.CanStand(point),"Fixture requires exact legal feet "+point.x+","+point.y);Check(app.SetVisualReviewPosition(point)&&(app.Game.Position-point).Length<.001f,"Fixture cannot silently repair feet");Set(app,"camera",app.Game.Position);}
    private static WorldObject Object(GameCore g,string kind){var o=Array.Find(g.Map.objects,x=>x.kind==kind);Check(o!=null,"Existing object retained: "+kind);return o;}
    private static void Interact(FrontierRuntime app,string kind)
    {var o=Object(app.Game,kind);Position(app,new Point(o.x,o.y-.7f));Check(app.Game.Nearest()==o,"Actual E targets "+kind);Key(app,KeyCode.E);}
    private static void Choose(FrontierRuntime app,string action,string capture=null)
    {
        var g=app.Game;Check(g.mode=="dialog"&&g.conversation!=null,"Numbered action belongs to real dialogue: "+action);int index=Array.FindIndex(g.conversation.choices,c=>c.action==action);Check(index>=0&&index<9,"Actual authored choice exposes "+action);
        var pages=DialogueLayout.Pages(g.L(g.conversation.text,g.conversation.textEn),g.save.largeText);int page=0;
        while(true){Draw(app,capture==null?null:capture+"-page"+page);Check(Has(pages[page]),"Actual dialogue renders complete page "+page);if((bool)Call(app,"DialogueChoicesVisible"))break;Key(app,KeyCode.Return);Check(++page<pages.Count,"Dialogue choices eventually become visible");}
        Check(page==pages.Count-1,"Actual numbers are visible only after the final page");Key(app,(KeyCode)((int)KeyCode.Alpha1+index));
    }
    private static void Original(GameCore g,params string[] actions)
    {Check(g.conversation!=null&&g.conversation.choices.Length>=actions.Length,"Mature choices retained");for(int i=0;i<actions.Length;i++)Check(g.conversation.choices[i].action==actions[i],"Original number remains "+actions[i]);}
    private static SaveData Copy(SaveData s){return JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s));}
    private static void Prepare(FrontierRuntime app,int profession=0,bool ready=true)
    {
        Input.Clear();GUI.queuedClick=null;Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;app.BeginVisualReview(profession,0);app.VisualReviewPaused=false;var g=app.Game;
        // Only mature chapter/cleared-room and crafting-resource fixtures are staged.
        // New acceptance, demonstrations and reward flags are earned via real input.
        g.enemies.Clear();g.save.flags.Remove("weapon_1");g.save.flags.Remove("weapon_2");g.save.companion=-1;if(ready&&!g.Has("core_1"))g.save.flags.Add("core_1");if(!ready)g.save.flags.Remove("core_1");
        g.save.weapon=0;g.save.weaponStyles=new int[3];g.save.relics=new[]{-1,-1};g.save.english=true;g.save.largeText=false;g.save.reducedMotion=true;g.save.effectsQuality=0;g.save.coins=121;g.save.shards=13;g.save.skillPoints=2;
        g.save.inventory[0]=20;g.save.inventory[1]=20;g.save.inventory[4]=20;
        Set(app,"applicationPaused",false);Set(app,"pointerSeen",false);Set(app,"controllerUsed",false);Set(app,"combatHudTime",0f);Set(app,"resumeTime",0f);Set(app,"selectedRegion",-1);Set(app,"journalTab",0);Call(app,"Resize");
    }
    private static void Accept(FrontierRuntime app,string suffix="")
    {Interact(app,"mentor");Original(app.Game,"practice","close","sparring_menu");Choose(app,"fieldcraft_offer","offer-menu"+suffix);Check(!app.Game.FieldcraftAccepted,"Reading offer does not accept");Choose(app,"fieldcraft_accept","offer"+suffix);Check(app.Game.FieldcraftAccepted&&app.Game.save.trackedQuest=="fieldcraft","Actual numbered acceptance tracks the optional field task");}
    private static void Aim(FrontierRuntime app,Point target)
    {Set(app,"camera",app.Game.Position);Point p=(Point)Call(app,"ScreenPoint",target);float scale=Get<float>(app,"scale");Input.mousePosition=new Vector3(Get<float>(app,"offsetX")+p.x*scale,Screen.height-Get<float>(app,"offsetY")-p.y*scale,0);}
    private static ActorAction Attack(FrontierRuntime app)
    {Input.held.Add(KeyCode.J);Update(app);Input.held.Clear();Check(app.Game.playerAction!=null&&app.Game.playerAction.kind=="attack","Actual J begins basic action");return app.Game.playerAction;}
    private static void WaitAction(FrontierRuntime app)
    {int frames=0;while((app.Game.playerAction!=null||app.Game.dashTime>0||app.Game.attackCooldown>0)&&frames++<200)Update(app);Check(frames<200&&!app.Game.Dying,"Live action completes without death");}
    private static void Equip(FrontierRuntime app,int weapon,int style=0)
    {
        if(app.Game.mode!="world")Key(app,KeyCode.Escape);Key(app,KeyCode.I);Check(app.Game.mode=="inventory","Actual I opens mature inventory");
        Click(app,L(app,new[]{"短剑","猎弓","星火杖"}[weapon],new[]{"Sword","Bow","Starfire staff"}[weapon]));Click(app,L(app,new[]{"均衡","蓄势","流转"}[style],new[]{"Balanced","Focused","Flow"}[style]));Click(app,L(app,"装备此打法","Equip style"));
        Check(app.Game.save.weapon==weapon&&app.Game.Style==style,"Original real equipment controls select weapon/style");Key(app,KeyCode.Escape);
    }
    private static void StageEncounter(FrontierRuntime app,params Point[] foes)
    {
        // Isolate an explicitly staged, uncompleted mature encounter. Each new
        // ordinary foe has live AI, ordinary starting HP and no stun/invulnerability.
        // A different weapon scenario begins with a fresh world-room fixture.
        var g=app.Game;g.Enter(1);g.enemies.Clear();g.Close();Position(app,new Point(12,14));
        while(g.invulnerable>0)Update(app);foreach(Point p in foes){Check(g.CanStand(p),"Staged live foe fits accepted collision");g.enemies.Add(new Enemy(++nextId,0,p.x,p.y,43){timer=1.43f});}
        Check(g.invulnerable<=0&&!g.practicing,"Positive proof uses mortal non-practice combat");Aim(app,foes[0]);
    }
    private static void Sword(FrontierRuntime app,string suffix="")
    {
        Equip(app,0);StageEncounter(app,new Point(13.1f,14));var g=app.Game;var e=g.enemies[0];int frames=0;
        while(!(e.state==1&&e.timer<=.055f)&&frames++<250)Update(app);Check(frames<250,"Natural live hunter announces its melee windup state="+e.state+",timer="+e.timer+",mode="+g.mode+",room="+g.save.room+",pos="+g.Position.x+","+g.Position.y+",foe="+e.x+","+e.y+",paused="+app.VisualReviewPaused);
        float hp=g.save.health;Input.axes["Vertical"]=1;Input.held.Add(KeyCode.W);Key(app,KeyCode.Space);Input.axes.Clear();Input.held.Clear();
        while(g.riposteTime<=0&&frames++<300)Update(app);Check(g.riposteTime>0&&g.save.health==hp,"Real Space crosses a genuine accepted incoming melee contact without manual invulnerability");
        Draw(app,"counter-earned"+suffix);Check(!g.FieldcraftRecorded(0),"Opening counter window alone cannot record sword demonstration");
        while(g.dashTime>0)Update(app);
        int approach=0;while((g.Position-e.Position).Length>1.25f&&approach++<80){Point d=g.DirectionTo(g.Position,e.Position);Input.axes["Horizontal"]=d.x;Input.axes["Vertical"]=-d.y;Update(app);}
        Input.axes.Clear();Aim(app,e.Position);Check(g.riposteTime>0,"Actual approach reaches sword before natural counter expiry");ActorAction a=Attack(app);Check(a.impact.counter&&g.riposteTime==0&&!g.FieldcraftRecorded(0),"Counter is captured/consumed on action start, while record waits for real contact");
        for(int i=0;i<50&&!g.FieldcraftRecorded(0);i++)Update(app);Check(a.released&&g.FieldcraftRecorded(0)&&e.hp<43,"Real counter J contacts a living hunter and earns sword record");WaitAction(app);
    }
    private static void Bow(FrontierRuntime app)
    {
        Equip(app,1,1);StageEncounter(app,new Point(15.4f,14),new Point(17.4f,14));var g=app.Game;Enemy[] foes=g.enemies.ToArray();float energy=g.save.energy;ActorAction a=Attack(app);
        Check(a.weapon==1&&a.style==1&&!a.impact.counter&&g.save.energy<energy,"Real focused basic bow retains its original four-energy cost");
        for(int i=0;i<90&&!g.FieldcraftRecorded(1);i++)Update(app);
        Check(g.FieldcraftRecorded(1)&&g.FieldcraftContactCount(1)>=2&&Array.TrueForAll(foes,e=>e.hp<43),"One real focused arrow hits two distinct moving live foes");WaitAction(app);
    }
    private static void Staff(FrontierRuntime app)
    {
        Equip(app,2);StageEncounter(app,new Point(15.8f,14),new Point(16.7f,13.55f),new Point(16.7f,14.45f));var g=app.Game;Enemy[] foes=g.enemies.ToArray();ActorAction a=Attack(app);Check(a.weapon==2&&a.kind=="attack","Actual J starts basic staff rather than class Q");
        for(int i=0;i<100&&!g.FieldcraftRecorded(2);i++)Update(app);
        Check(g.FieldcraftRecorded(2)&&g.FieldcraftContactCount(2)>=3&&Array.TrueForAll(foes,e=>e.hp<43),"One real explosion hits direct target and two distinct naturally moving live foes");WaitAction(app);
    }
    public static void ActualOfferCraftDemonstrateAndReport(FrontierRuntime app)
    {
        Prepare(app,0,false);var g=app.Game;Interact(app,"mentor");Original(g,"practice","close","sparring_menu");Choose(app,"fieldcraft_offer","locked");Check(!g.FieldcraftAccepted&&!g.AcceptFieldcraft(),"Locked offer cannot remotely accept");Key(app,KeyCode.Escape);
        g.save.flags.Add("core_1");int profession=g.save.profession,companion=g.save.companion,coins=g.save.coins,shards=g.save.shards,points=g.save.skillPoints,weapon=g.save.weapon;Accept(app);
        Check(g.save.profession==profession&&g.save.companion==companion&&g.save.weapon==weapon&&g.save.coins==coins&&g.save.shards==shards&&g.save.skillPoints==points,"Accepting neither auto-equips, changes class/companion nor spends currency");
        Interact(app,"forge");int wood=g.save.inventory[0],crystal=g.save.inventory[1],parts=g.save.inventory[4];Choose(app,"bow","craft-bow");Check(g.Has("weapon_1")&&g.save.inventory[0]==wood-4&&g.save.inventory[4]==parts-2,"Real old blacksmith bow purchase keeps timber4/parts2");
        Interact(app,"forge");parts=g.save.inventory[4];Choose(app,"staff","craft-staff");Check(g.Has("weapon_2")&&g.save.inventory[1]==crystal-4&&g.save.inventory[4]==parts-3,"Real old staff purchase keeps crystal4/parts3");
        Sword(app);Bow(app);Staff(app);Check(g.FieldcraftProgress==3&&g.FieldcraftDemonstrationsComplete&&!g.FieldcraftComplete,"All genuine techniques await manual Ada report");
        earned=Copy(g.save);g.Enter(0);g.enemies.Clear();g.Close();Interact(app,"mentor");Original(g,"practice","close","sparring_menu");Choose(app,"fieldcraft_offer","earned-report");coins=g.save.coins;shards=g.save.shards;points=g.save.skillPoints;Choose(app,"fieldcraft_report","report");
        Check(g.FieldcraftComplete&&g.save.coins==coins+40&&g.save.shards==shards+5&&g.save.skillPoints==points+1,"Actual manual report pays coins40/shards5/point1 once");
        Check(!g.FinishFieldcraft()&&g.save.coins==coins+40&&g.save.shards==shards+5&&g.save.skillPoints==points+1,"Repeated report cannot duplicate reward");
        Interact(app,"mentor");Original(g,"practice","close","sparring_menu");Choose(app,"practice","old-basic");Check(g.practicing&&!g.SparringActive&&g.enemies[0].id==90001,"Original Alpha1 basic lesson retained");g.EndPractice();
        Console.WriteLine("PASS actual Ada E/numbered unlock and acceptance, old craft costs/style controls, genuine Space counter J, one piercing arrow/two live targets, one staff explosion/three natural AI targets, manual exact once reward and original basic lesson; frames="+liveFrames+"; NATIVE_UNVERIFIED");
    }
    public static void ActualSkillPracticeAndSeparateActionsDoNotForgeRecords(FrontierRuntime app)
    {
        Prepare(app,2);Accept(app);var g=app.Game;g.save.flags.Add("weapon_2");Equip(app,2);StageEncounter(app,new Point(14,14),new Point(14.5f,13.8f),new Point(14.5f,14.2f));Aim(app,g.enemies[0].Position);Key(app,KeyCode.Q);ActorAction a=g.playerAction;Check(a!=null&&a.kind=="skill","Real Q starts genuine class skill");Frames(app,35);
        Check(a.released&&g.FieldcraftProgress==0&&g.enemies.TrueForAll(e=>e.hp<43),"Genuine multi-contact Q cannot masquerade as basic staff explosion");
        g.Enter(0);g.enemies.Clear();g.Close();Interact(app,"mentor");Choose(app,"practice");Aim(app,g.enemies[0].Position);Attack(app);Frames(app,40);Check(g.FieldcraftProgress==0,"Actual ordinary practice hit cannot earn new live-field evidence");g.EndPractice();
        g.save.flags.Add("weapon_1");Equip(app,1,1);StageEncounter(app,new Point(15.4f,14));Attack(app);Frames(app,70);Check(!g.FieldcraftRecorded(1)&&g.FieldcraftContactCount(1)==1,"One live focused-arrow contact is only partial evidence");
        Aim(app,g.enemies[0].Position);Attack(app);Frames(app,70);Check(!g.FieldcraftRecorded(1)&&g.FieldcraftContactCount(1)==1,"Two separate arrows against the same foe cannot aggregate into piercing proof");
        Console.WriteLine("PASS real multi-contact Q, actual safe-practice J, one live target and separate focused arrows cannot forge field demonstrations; NATIVE_UNVERIFIED");
    }
    public static void RealChecksumLegacyPauseAndTransientEvidence(FrontierRuntime app)
    {
        Check(earned!=null,"Real complete demonstration checkpoint exists");Prepare(app);var g=app.Game;g.Load(Copy(earned));g.Start();g.PrepareSave();Check(SaveStore.Write(g.save,0),"Actual checksum writer persists earned flags");string warning;var restored=SaveStore.Read(0,out warning);Check(restored!=null&&warning==null,"Actual checksum reader accepts earned field notes");
        g.Load(restored);g.Start();Check(g.FieldcraftProgress==3&&g.FieldcraftDemonstrationsComplete&&!g.FieldcraftComplete,"All actual earned records survive round trip without automatically claiming report");
        for(int i=0;i<3;i++)Check(g.FieldcraftContactCount(i)==i+1,"Completed records expose their earned goal after loading");
        foreach(int version in new[]{1,2})
        {
            var old=Copy(restored);old.version=version;old.flags.RemoveAll(f=>f.StartsWith("fieldcraft",StringComparison.Ordinal));File.WriteAllText(SaveStore.PathFor(1),JsonUtility.ToJson(old));var prior=SaveStore.Read(1,out warning);Check(prior!=null&&warning==null,"Actual raw v"+version+" reader accepts pre-feature save");g.Load(prior);g.Start();
            Check(!g.FieldcraftAccepted&&g.FieldcraftProgress==0&&!g.FieldcraftComplete&&g.Has("core_1")&&g.Has("weapon_1")&&g.Has("weapon_2")&&g.save.coins==old.coins&&g.save.inventory[0]==old.inventory[0],"Legacy flags-only migration invents no evidence and preserves story/gear/economy");
        }
        Prepare(app);g=app.Game;Accept(app);g.save.flags.Add("weapon_1");Equip(app,1,1);StageEncounter(app,new Point(15.4f,14));Attack(app);Frames(app,70);Check(g.FieldcraftContactCount(1)==1&&!g.FieldcraftRecorded(1),"One genuine partial arrow exists before saving");g.PrepareSave();Check(SaveStore.Write(g.save,2),"Production writer saves unfinished demonstration");var partial=SaveStore.Read(2,out warning);Check(partial!=null&&warning==null,"Actual checksum reader restores unfinished notes");g.Load(partial);g.Start();Check(g.FieldcraftContactCount(1)==0&&!g.FieldcraftRecorded(1),"Load retires partial live-action evidence without fabricating second contact");
        Prepare(app);g=app.Game;Accept(app);g.save.flags.Add("weapon_1");Equip(app,1,1);StageEncounter(app,new Point(16,14),new Point(18,14));ActorAction action=Attack(app);Key(app,KeyCode.Tab);float elapsed=action.elapsed,time=g.save.totalTime;Frames(app,20);Check(g.mode=="journal"&&action.elapsed==elapsed&&g.save.totalTime==time&&g.FieldcraftProgress==0,"Actual journal pauses pending basic action and evidence without fast-forwarding contacts");Key(app,KeyCode.Escape);
        for(int i=0;i<90&&!g.FieldcraftRecorded(1);i++)Update(app);Check(g.FieldcraftRecorded(1),"A genuinely paused/resumed action may still earn its real contacts");
        Console.WriteLine("PASS production SaveStore checksum and raw old v1/v2 migration, three real flags survive without auto-report, transient identity reset, real journal pause and resumed physical contacts; NATIVE_UNVERIFIED");
    }
    private static void CounterThreatWitness(FrontierRuntime app,string suffix)
    {
        var g=app.Game;g.enemies.Clear();Check(g.CanStand(new Point(22,14)),"Distant visual foe has legal feet");var foe=new Enemy(++nextId,0,22,14,43){state=1,timer=.3f,windupDuration=.65f,target=new Point(22,14)};foe.Face(new Point(0,-1));g.enemies.Add(foe);
        // Visual-only placement, distinct from the actual-input proof above.
        foreach(bool combat in new[]{false,true})
        {
            Set(app,"combatHudTime",combat?3f:0f);Check((bool)typeof(FrontierRuntime).GetProperty("CombatHud",Private).GetValue(app,null)==combat,"Visual fixture actually selects the requested exploration/combat HUD");Rect bounds=(Rect)Call(app,"FieldcraftCounterBounds");Check(bounds.y==(combat?89:102),"Distinct counter geometry matches the actual HUD mode");Point pixel=new Point(bounds.x+bounds.width/2,bounds.y+30);Set(app,"camera",new Point(foe.x-(pixel.x-640)/36f,foe.y-(pixel.y-390)/36f));Draw(app,"counter-overlap-"+(combat?"combat-":"explore-")+suffix);
            Check((bool)Call(app,"ThreatOver",bounds),"Actual actor/warning overlaps the new counter readout");var regions=(List<Rect>)Call(app,"HudRegions");Check(regions.Exists(r=>Same(r,bounds)),"Both counter bounds participate in the real threat witness regions");
            Check(GUI.textureCalls.Exists(c=>c.texture==Texture2D.whiteTexture&&Same(c.rect,bounds)&&Math.Abs(c.color.a-.12f)<.001f),"Counter fill fades to exact .12 opacity over real threats");
            GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"DrawHudThreatWitness");Check(GUI.textureCalls.Exists(c=>c.texture.name.Contains("outline_Monsters")),"The actual enemy silhouette remains visible above counter text");Check(GUI.textureCalls.FindAll(c=>c.rect.width==2).Count==33,"The complete thirty-three point melee warning survives counter overlap");
        }
    }
    public static void BilingualJournalMenusAndExactCounterClock(FrontierRuntime app)
    {
        foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})foreach(bool small in new[]{true,false})
        {
            Prepare(app);var g=app.Game;g.save.english=english;g.save.largeText=large;Screen.width=small?960:1920;Screen.height=small?600:1080;Call(app,"Resize");string suffix=(english?"en":"zh")+(large?"-large":"-normal")+(small?"-960":"-1920");Accept(app,"-"+suffix);
            Key(app,KeyCode.Tab);Draw(app);int quest=Array.IndexOf(GameCore.QuestIds,"fieldcraft");Check(quest==17&&GameCore.QuestIds.Length>=19&&GameCore.QuestIds[18]=="supper","The field quest retains eighteenth position and supper appends without renumbering when later stories add pages");float spacing=Math.Min(46,464f/18);
            const int pageSize=19;int pages=(GameCore.QuestIds.Length+pageSize-1)/pageSize,checkedRows=0;
            while(Get<int>(app,"selectedQuest")/pageSize>0){GUI.queuedClick=new Rect(650,699,258,26);Draw(app);Check(!GUI.queuedClick.HasValue,"Actual previous-page control returns to mature rows");}
            for(int page=0;page<pages;page++)
            {
                Draw(app);int count=Math.Min(pageSize,GameCore.QuestIds.Length-page*pageSize);RowGlyphRoom(spacing-7,count);
                for(int cell=0;cell<count;cell++){int i=page*pageSize+cell;Rect row=new Rect(279,226+cell*spacing-(cell==18?3:0),321,spacing-7);Check(GUI.buttonCalls.Exists(b=>Same(b.rect,row)&&b.enabled),"Every current quest has a visible enabled row on its own page: "+i);GUI.queuedClick=row;Draw(app);Check(!GUI.queuedClick.HasValue&&Get<int>(app,"selectedQuest")==i,"All actual journal rows remain clickable: "+i);checkedRows++;}
                if(page+1<pages){GUI.queuedClick=new Rect(927,699,263,26);Draw(app);Check(!GUI.queuedClick.HasValue&&Get<int>(app,"selectedQuest")==((page+1)*pageSize),"Actual next-page control reaches appended quests");}
            }
            Check(checkedRows==GameCore.QuestIds.Length,"Strict glyph and actual click checks cover all quests across every page");
            while(Get<int>(app,"selectedQuest")/pageSize>0){GUI.queuedClick=new Rect(650,699,258,26);Draw(app);Check(!GUI.queuedClick.HasValue,"Return through the real previous-page control");}
            GUI.queuedClick=new Rect(279,226+quest*spacing,321,spacing-7);Draw(app);Check(Get<int>(app,"selectedQuest")==quest,"Return to the original field-notes row after checking all appended rows");
            Draw(app,"journal-"+suffix);RowGlyphRoom(spacing-7,Math.Min(19,GameCore.QuestIds.Length));OrdinaryPadding(g.L("任务","Quests"));OrdinaryPadding(g.L("追踪任务","Track quest"));OrdinaryPadding(g.L("查看目标地图","Show destination"));var guide=g.GetQuest("fieldcraft");Check(Has(g.L("下一步：","Next: ")+guide.next),"Actual eighteenth row shows truthful next step");Rect reward=TextBounds(guide.reward);foreach(var step in guide.steps)Check(!Overlap(TextBounds((step.done?"✓  ":"○  ")+step.text),reward),"Every field objective remains above reward band");Key(app,KeyCode.Escape);
            // Isolated display fixtures only select which newly authored next-step
            // copy is visible. Actual records are earned in the first group.
            g.save.flags.Add("weapon_1");g.save.flags.Add("weapon_2");
            string[] displayRecords={"fieldcraft_sword","fieldcraft_bow","fieldcraft_staff"};
            for(int missing=0;missing<3;missing++)
            {
                foreach(string flag in displayRecords)g.save.flags.Remove(flag);for(int i=0;i<missing;i++)g.save.flags.Add(displayRecords[i]);
                Set(app,"combatHudTime",0f);Draw(app,"guide-missing"+missing+"-"+suffix);Check(Has(g.GuidanceHint())&&TextBounds(g.GuidanceHint()).height==65,"Actual exploration HUD draws missing weapon "+missing+" with the correct travel-prefix and authored hint");
            }
            foreach(string flag in displayRecords)g.save.flags.Remove(flag);
            // This separate display fixture isolates native-independent GUI truth.
            // Positive proof above earns the same window through actual Space.
            g.riposteTime=1.234f;g.toastTime=0;Set(app,"combatHudTime",0f);Draw(app,"counter-explore-"+suffix);Rect bounds=(Rect)Call(app,"FieldcraftCounterBounds");Check(bounds.width>0&&bounds.height>0&&HasPart(L(app,"闪避反击 1.3","COUNTER 1.3")),"Exploration counter card displays actual remaining 1.234-second timer");
            Check(!(bool)Call(app,"PointerOverInterface",new Point(bounds.x+5,bounds.y+5)),"Exploration read-only counter does not steal world aiming");Set(app,"combatHudTime",3f);Draw(app,"counter-combat-"+suffix);Check(HasPart(L(app,"闪避反击 1.3","COUNTER 1.3")),"Combat counter card uses the same real countdown");bounds=(Rect)Call(app,"FieldcraftCounterBounds");Check(!(bool)Call(app,"PointerOverInterface",new Point(bounds.x+5,bounds.y+5)),"Combat read-only counter does not steal world aiming");Check((bool)Call(app,"PointerOverInterface",new Point(440,740))&&(bool)Call(app,"PointerOverInterface",new Point(780,740)),"Original compact Q/dodge controls retain pointer blocking");
            CounterThreatWitness(app,suffix);Key(app,KeyCode.I);Draw(app,"counter-menu-hidden-"+suffix);OrdinaryPadding(g.L("装备此打法","Equip style"));Check(!HasPart(L(app,"闪避反击 1.3","COUNTER 1.3")),"Paused inventory cannot display a live counter opportunity");Key(app,KeyCode.Escape);Set(app,"applicationPaused",true);Draw(app,"counter-paused-hidden-"+suffix);Check(!HasPart(L(app,"闪避反击 1.3","COUNTER 1.3")),"Application pause hides counter opportunity");Set(app,"applicationPaused",false);
            app.VisualReviewPaused=true;Draw(app,"counter-review-hidden-"+suffix);Check(!HasPart(L(app,"闪避反击 1.3","COUNTER 1.3")),"Paused visual review hides live counter opportunity");app.VisualReviewPaused=false;
            g.riposteTime=0;Draw(app,"counter-expired-"+suffix);Check(!HasPart(L(app,"闪避反击 1.3","COUNTER 1.3")),"Expired live counter is not advertised");
        }
        Console.WriteLine("PASS all real journal rows across visible pages with strict glyph-height room and unchanged mature positions, complete offer pages, both languages normal/large at 960/1920, exact timer in both HUDs, read-only pointer aiming and menu/application/review/expiry hiding; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,folder=Path.Combine(Path.GetTempPath(),"starfall-fieldcraft-"+Guid.NewGuid());Application.persistentDataPath=folder;captureFolder=args.Length>0?args[0]:null;if(captureFolder!=null)Directory.CreateDirectory(captureFolder);var app=new FrontierRuntime();int failed=0;
        try{Call(app,"Awake");Check(app.Game!=null,"Actual Awake loads full mature campaign");foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{ActualOfferCraftDemonstrateAndReport,ActualSkillPracticeAndSeparateActionsDoNotForgeRecords,RealChecksumLegacyPauseAndTransientEvidence,BilingualJournalMenusAndExactCounterClock})try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}app.EndVisualReview();Call(app,"OnDestroy");}
        finally{Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;if(Directory.Exists(folder))Directory.Delete(folder,true);}
        Console.WriteLine("Runtime fieldcraft: "+(4-failed)+"/4 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
