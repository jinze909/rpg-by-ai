// Actual Update/OnGUI/SaveStore paths using offline Unity declarations.
// These checks do not establish native import, GPU rendering or play feel.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeCombatFocusChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string captureFolder;
    private static int captures;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static bool Near(float a, float b, float tolerance=.003f) { return Math.Abs(a-b)<=tolerance; }
    private static bool Same(Rect a, Rect b) { return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height); }
    private static bool Same(Color a, Color b) { return Near(a.r,b.r)&&Near(a.g,b.g)&&Near(a.b,b.b)&&Near(a.a,b.a); }
    private static object Call(object obj, string name, params object[] args)
    { var method=obj.GetType().GetMethod(name,Private|BindingFlags.Public); Check(method!=null,"Production entry point exists: "+name); return method.Invoke(obj,args); }
    private static void Set(object obj, string name, object value)
    { var field=obj.GetType().GetField(name,Private); Check(field!=null,"Fixture field exists: "+name); field.SetValue(obj,value); }
    private static T Get<T>(object obj, string name) { return (T)obj.GetType().GetField(name,Private).GetValue(obj); }
    // Reflection permits the same harness to compile against the prior checkout;
    // a missing feature then fails its real input contract, not C# compilation.
    private static Enemy Target(GameCore g)
    { var property=typeof(GameCore).GetProperty("CombatFocusTarget"); return property==null?null:(Enemy)property.GetValue(g,null); }
    private static void Update(FrontierRuntime app) { Call(app,"Update"); Input.keys.Clear(); }
    private static void Frames(FrontierRuntime app,int count) { for(int i=0;i<count;i++)Update(app); }
    private static void Key(FrontierRuntime app,KeyCode key) { Input.keys.Add(key);Update(app); }
    private static void Pointer(FrontierRuntime app,Point point)
    { float scale=Get<float>(app,"scale"); Input.mousePosition=new Vector3(Get<float>(app,"offsetX")+point.x*scale,Screen.height-Get<float>(app,"offsetY")-point.y*scale,0); }
    private static Point ScreenPoint(FrontierRuntime app,Point point) { return (Point)Call(app,"ScreenPoint",point); }
    private static void Mouse(FrontierRuntime app,Point point) { Pointer(app,ScreenPoint(app,point)); }
    private static void Draw(FrontierRuntime app,string capture=null)
    {
        GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        if(captureFolder!=null&&capture!=null)
        {
            var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();
            GUI.Export(captureFolder,"qa-focus-"+capture);captures++;
            GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static Rect FocusButton(bool combat) { return combat?new Rect(1030,105,230,32):new Rect(1015,489,234,32); }
    private static void Click(FrontierRuntime app,Rect rect)
    { Draw(app);Check(GUI.buttonCalls.Exists(b=>Same(b.rect,rect)&&b.enabled),"Actual focus control is displayed and enabled");GUI.queuedClick=rect;Draw(app);Check(!GUI.queuedClick.HasValue,"Actual focus button consumes its click"); }
    private static void Prepare(FrontierRuntime app,int profession=1,int room=1)
    {
        Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;
        Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;Event.current.type=EventType.Repaint;
        app.BeginVisualReview(profession,room);app.VisualReviewPaused=false;var g=app.Game;
        g.enemies.Clear();g.effects.Clear();g.projectiles.Clear();g.hazards.Clear();g.fallen.Clear();g.save.companion=-1;
        g.save.english=true;g.save.largeText=false;g.save.showGuidance=false;g.save.reducedMotion=true;g.save.effectsQuality=0;
        g.save.x=12;g.save.y=14;g.save.energy=100;g.save.health=g.save.maxHealth;g.save.heat=g.save.cold=0;
        g.save.relics=new[]{-1,-1};g.save.technique=0;g.save.seed=1;g.save.totalTime=100;g.invulnerable=100;g.toastTime=0;
        Set(app,"camera",g.Position);Set(app,"applicationPaused",false);Set(app,"controllerUsed",false);Set(app,"pointerSeen",false);
        Set(app,"pointerAimActive",false);Set(app,"combatHudTime",0f);Set(app,"resumeTime",0f);Set(app,"arrivalTime",0f);
        Call(app,"Resize");Mouse(app,g.Position+new Point(4,0));
    }
    private static Enemy Enemy(FrontierRuntime app,float x=4,float y=0,int id=93001)
    { var p=app.Game.Position+new Point(x,y);var e=new Enemy(id,0,p.x,p.y,500,false){stun=100,timer=9999};app.Game.enemies.Add(e);return e; }
    private static Enemy Lock(FrontierRuntime app)
    { Enemy e=Enemy(app);Key(app,KeyCode.V);Check(ReferenceEquals(Target(app.Game),e),"Actual V Update acquires the live eligible target");return e; }
    private static void Released(FrontierRuntime app,ActorAction action)
    { for(int i=0;i<45&&!action.released;i++)Update(app);Check(action.released,"Actual simulation reaches the existing contact boundary"); }
    private static string SaveState(GameCore g) { return JsonUtility.ToJson(g.save); }

    private static void ActualKeyboardPadAndDefaultFreeAim(FrontierRuntime app)
    {
        Prepare(app);Enemy e=Enemy(app);Mouse(app,app.Game.Position+new Point(-4,0));Update(app);
        Check(Target(app.Game)==null&&app.Game.aim.x<-.999f,"New sessions retain desktop free aim until explicitly toggled");
        Key(app,KeyCode.V);Check(ReferenceEquals(Target(app.Game),e)&&app.Game.aim.x>.999f,"V explicitly overrides a contrary live pointer aim");
        Input.held.Add(KeyCode.V);Frames(app,65);Check(ReferenceEquals(Target(app.Game),e),"Held V never repeats the toggle");Input.held.Clear();
        Key(app,KeyCode.V);Check(Target(app.Game)==null&&app.Game.aim.x<-.999f,"A second fresh V releases to the original pointer aim");
        Input.keys.Add(KeyCode.JoystickButton9);Input.held.Add(KeyCode.JoystickButton9);Update(app);
        Check(ReferenceEquals(Target(app.Game),e)&&Get<bool>(app,"controllerUsed"),"Actual R3 press acquires and owns controller presentation");
        Frames(app,65);Check(ReferenceEquals(Target(app.Game),e),"Held R3 does not unlock or reacquire every frame");Input.held.Clear();
        Key(app,KeyCode.JoystickButton9);Check(Target(app.Game)==null,"A second R3 press releases the optional target");
        Prepare(app);Enemy east=Enemy(app,6,0,93002);Enemy(app,0,-2,93003);Mouse(app,app.Game.Position+new Point(4,0));Key(app,KeyCode.V);
        Check(ReferenceEquals(Target(app.Game),east),"Real acquisition weights current aim as well as distance");
        Prepare(app);Key(app,KeyCode.V);Check(Target(app.Game)==null&&app.Game.playerAction==null,"No eligible actor means V creates no lock or combat action");
        Console.WriteLine("PASS actual V/R3 acquire/release and held-edge behavior, default free aim, aim-weighted selection and no-target input; NATIVE_UNVERIFIED");
    }
    private static void BackpedalActionsAndPreparedContactStayImmutable(FrontierRuntime app)
    {
        for(int profession=0;profession<3;profession++)
        {
            Prepare(app,profession);var g=app.Game;Enemy e=Lock(app);Point before=g.Position;
            Input.axes["Horizontal"]=-1;Update(app);Input.axes.Clear();
            Check(g.Position.x<before.x-.05f&&g.aim.x>.999f,"Focus preserves independent backward movement for class "+profession);
            Mouse(app,g.Position+new Point(-4,0));Input.held.Add(KeyCode.J);Update(app);Input.held.Clear();
            ActorAction action=g.playerAction;Check(action!=null&&action.kind=="attack"&&action.direction.x>.999f&&action.target.HasValue,"Real J captures focus facing and position for class "+profession);
            Point direction=action.direction,target=action.target.Value;float cost=g.save.energy;
            e.y+=2;Update(app);Check(g.aim.y>.3f&&ReferenceEquals(g.playerAction,action),"Live target motion updates aim during preparation");
            Check((action.direction-direction).Length<.0001f&&(action.target.Value-target).Length<.0001f&&(g.VisualFacing-direction).Length<.0001f,"Accepted J keeps its prepared direction, target and full-body facing");
            Released(app,action);Check(g.save.energy>=cost,"Target tracking does not repeatedly charge the accepted attack");
            if(profession>0)
            {
                Check(g.projectiles.Count==1,"Original basic ranged contact still emits exactly one projectile");Projectile shot=g.projectiles[0];Point velocity=shot.velocity;
                e.y-=4;Mouse(app,g.Position+new Point(0,-4));Frames(app,3);
                Check(g.projectiles.Contains(shot)&&(shot.velocity-velocity).Length<.0001f,"Released projectile flight never homes toward the moved focus");
            }
        }
        for(int profession=0;profession<3;profession++)for(int technique=0;technique<2;technique++)
        {
            Prepare(app,profession);var g=app.Game;g.save.technique=technique;Enemy e=Lock(app);
            Mouse(app,g.Position+new Point(-4,0));Key(app,KeyCode.Q);ActorAction action=g.playerAction;
            Check(action!=null&&action.kind=="skill"&&action.direction.x>.999f&&Near(g.skillCooldown,3.5f)&&Near(g.save.energy,60),"Actual base/alternate Q uses focus with unchanged 40 energy and 3.5s cooldown");
            Point direction=action.direction,target=action.target.Value;e.y+=2;Frames(app,3);
            Check(g.aim.y>.3f&&(action.direction-direction).Length<.0001f&&(action.target.Value-target).Length<.0001f,"Each class/base/alternate Q keeps its accepted target snapshot");
            Released(app,action);
            if(profession==1)Check(g.projectiles.Count==(technique==0?5:1),"Archer Q retains original base fan or alternate piercing-arrow count");
            else Check(g.effects.Exists(f=>f.kind==(profession==2&&technique==1?"frost":"burst")),"Radial and frost Q retain original player-centered release behavior");
        }
        Console.WriteLine("PASS real J/backpedal for three classes, moving-target live aim, immutable prepared facing/target and nonhoming flight; all six actual Q techniques preserve costs/contact patterns; NATIVE_UNVERIFIED");
    }
    private static void SessionAndActorBoundariesDiscardFocus(FrontierRuntime app)
    {
        foreach(string boundary in new[]{"inventory","pause","application","focus-lost","travel","load","reused-id","death","target-death","range","occluded","removed"})
        {
            Prepare(app);var g=app.Game;Enemy e=Lock(app);
            if(boundary=="inventory"||boundary=="pause")Key(app,boundary=="inventory"?KeyCode.I:KeyCode.Escape);
            else if(boundary=="application")Call(app,"OnApplicationPause",true);
            else if(boundary=="focus-lost")Call(app,"OnApplicationFocus",false);
            else if(boundary=="travel")g.Enter(0);
            else if(boundary=="load"){g.Load(g.save);g.Close();}
            else if(boundary=="reused-id"){g.enemies.Remove(e);Enemy(app,4,0,e.id);Update(app);}
            else if(boundary=="death"){g.invulnerable=0;Call(g,"Hurt",10000f,"environment",null);Check(g.Dying,"Actual lethal damage begins death");}
            else if(boundary=="target-death"){e.hp=0;Update(app);}
            else if(boundary=="range"){e.x=g.Position.x+12.1f;Update(app);}
            else if(boundary=="occluded"){g.save.x=4;g.save.y=5;e.x=8;e.y=5;Update(app);}
            else {g.enemies.Remove(e);Update(app);}
            Check(Target(g)==null,"Actual "+boundary+" clears the selected actor");
            Draw(app);Check(!GUI.commands.Exists(c=>c.Contains("Release target")||c.Contains("解除锁定"))&&Marker(app,e).Count==0,"Invalid or inactive targets leave no stale release label or marker at "+boundary);
            if(boundary=="application")Call(app,"OnApplicationPause",false);
            if(!g.Dying){g.Close();if(boundary=="removed"||boundary=="reused-id"||boundary=="range"||boundary=="occluded"||boundary=="target-death"){g.save.x=12;g.save.y=14;g.enemies.Clear();g.enemies.Add(e);e.x=g.Position.x+4;e.y=g.Position.y;e.hp=e.maxHp;}Update(app);Check(Target(g)==null,"Returning from "+boundary+" never silently restores focus");}
        }
        foreach(string boundary in new[]{"reset","layout","end","clear"})
        {
            Prepare(app,1,0);var g=app.Game;WorldObject mentor=Array.Find(g.Map.objects,o=>o.kind=="mentor");Check(mentor!=null,"Original town mentor exists");
            g.save.x=mentor.x;g.save.y=mentor.y-.7f;Check(g.StartRange(0),"Actual mentor range starts");Mouse(app,g.Position+new Point(4,0));Key(app,KeyCode.V);
            Enemy previous=Target(g);Check(previous!=null,"Live range targets support explicit focus");
            if(boundary=="reset")Check(g.ResetRange(),"Real range reset accepts");else if(boundary=="layout")Check(g.ChangeRangeLayout(1),"Real range change accepts");else if(boundary=="end")g.EndPractice();else{g.enemies.Clear();Update(app);Check(g.RangeFinished,"Actual cleared range enters review");}
            Check(Target(g)==null,"Range "+boundary+" severs focus despite reused actor IDs");Update(app);Check(Target(g)==null,"Range "+boundary+" leaves no auto-acquisition");
        }
        Console.WriteLine("PASS menu/pause/application/focus loss, travel/load, reused actor IDs, death/range/LOS/removal and real range reset/layout/end/review discard focus without auto-reacquisition; NATIVE_UNVERIFIED");
    }
    private static void ActualHudClicksAndWorldPointerOwnership(FrontierRuntime app)
    {
        foreach(bool combat in new[]{false,true})
        {
            Prepare(app);var g=app.Game;Enemy e=Enemy(app,9);Set(app,"combatHudTime",combat?3f:0f);Rect rect=FocusButton(combat);Draw(app);
            Check(GUI.buttonCalls.Exists(b=>Same(b.rect,rect)&&b.enabled),"Only actual new focus control is registered in each HUD layout");
            Point inside=new Point(rect.x+12,rect.y+12),beside=new Point(rect.x-2,rect.y+12);
            Check((bool)Call(app,"PointerOverInterface",inside)&&!(bool)Call(app,"PointerOverInterface",beside),"Pointer blocker follows exact new control bounds, including adjacent world space");
            Pointer(app,inside);Input.buttons.Add(0);Update(app);Input.buttons.Clear();Check(g.playerAction==null,"Actual left mouse hold over focus control cannot also attack");
            Set(app,"combatHudTime",combat?3f:0f);Click(app,rect);Check(ReferenceEquals(Target(g),e),"Actual OnGUI focus click selects target");
            Set(app,"combatHudTime",combat?3f:0f);Click(app,rect);Check(Target(g)==null,"Actual OnGUI release click clears target");
            g.enemies.Clear();Update(app);Set(app,"combatHudTime",combat?3f:0f);Draw(app);
            Check(!GUI.buttonCalls.Exists(b=>Same(b.rect,rect))&&!(bool)Call(app,"PointerOverInterface",inside),"Invisible no-target focus control leaves its former world area clickable");
        }
        Prepare(app);Enemy target=Lock(app);Draw(app);Point marker=ScreenPoint(app,target.Position)+new Point(-20,-6);
        Check(!(bool)Call(app,"PointerOverInterface",marker),"Read-only target brackets are world-pointer transparent");Pointer(app,marker);Input.buttons.Add(0);Update(app);Input.buttons.Clear();
        Check(app.Game.playerAction!=null&&app.Game.playerAction.kind=="attack","Actual mouse hold through target brackets still launches J attack");
        Console.WriteLine("PASS actual acquisition/release OnGUI clicks in both HUDs, exact new control pointer bounds, no hidden blocker and marker click-through attacks; NATIVE_UNVERIFIED");
    }
    private static List<GUI.TextureCall> Marker(FrontierRuntime app,Enemy e)
    {
        Point p=ScreenPoint(app,e.Position);
        return GUI.textureCalls.FindAll(c=>c.texture==Texture2D.whiteTexture&&c.rect.x>=p.x-25&&c.rect.x+c.rect.width<=p.x+25&&c.rect.y>=p.y-11&&c.rect.y+c.rect.height<=p.y+16
            &&(Near(c.rect.width,9)&&(Near(c.rect.height,2)||Near(c.rect.height,5))||Near(c.rect.height,7)&&(Near(c.rect.width,2)||Near(c.rect.width,5)))
            &&(Same(c.color,PixelArt.C("#101a2b"))||Same(c.color,PixelArt.C("#7acabb"))));
    }
    private static void ButtonInk(Rect rect,bool english)
    {
        string geometry="\"r\":["+rect.x.ToString(CultureInfo.InvariantCulture)+","+rect.y.ToString(CultureInfo.InvariantCulture)+","+rect.width.ToString(CultureInfo.InvariantCulture)+","+rect.height.ToString(CultureInfo.InvariantCulture)+"]";
        string command=GUI.commands.Find(c=>c.Contains("\"k\":\"text\"")&&c.Contains(geometry));Check(command!=null,"Actual focus button text is recorded");
        Check(command.Contains(english?"Release target":"解除锁定"),"Actual focus state has the correct bilingual label");
        int start=command.IndexOf("\"padding\":[",StringComparison.Ordinal)+11,end=command.IndexOf(']',start);string[] padding=command.Substring(start,end-start).Split(',');
        int top=int.Parse(padding[2]),bottom=int.Parse(padding[3]);Check(rect.height-top-bottom>=18&&command.Contains("\"size\":16"),"Actual focus button padding reserves 18px Chinese glyph ink at the shipped 16px font size");
    }
    private static void MarkerVisibilityDangerPriorityAndBilingualCaptures(FrontierRuntime app)
    {
        foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})foreach(int width in new[]{960,1920})foreach(bool combat in new[]{false,true})
        {
            Prepare(app);var g=app.Game;Enemy e=Enemy(app,9);Key(app,KeyCode.V);Check(ReferenceEquals(Target(g),e),"Actual V selects the farther exploration target");g.save.english=english;g.save.largeText=large;Screen.width=width;Screen.height=width==960?600:1080;Call(app,"Resize");Set(app,"combatHudTime",combat?3f:0f);
            string suffix=(english?"en":"zh")+(large?"-large":"-normal")+"-"+width+(combat?"-combat":"-explore");
            string before=SaveState(g);Point direction=g.aim;float clock=Get<float>(app,"clock");
            Draw(app,suffix);Check(Marker(app,e).Count==16,"Low-FX reduced-motion actual HUD has eight static backed corner strokes");ButtonInk(FocusButton(combat),english);
            Point p=ScreenPoint(app,e.Position);Rect hp=new Rect(p.x-26,p.y-54,52,4);
            Check(GUI.textureCalls.Exists(c=>c.texture==Texture2D.whiteTexture&&Same(c.rect,hp)&&Same(c.color,PixelArt.C("#d58e79"))),"Focused full-health ordinary enemy keeps its original HP bar visible");
            var first=Marker(app,e);Set(app,"clock",clock+3);Draw(app);var second=Marker(app,e);Check(first.Count==second.Count,"Marker has no clock-driven pulse");for(int i=0;i<first.Count;i++)Check(Same(first[i].rect,second[i].rect)&&Same(first[i].color,second[i].color),"Static marker geometry and alpha are unchanged across visual time");Set(app,"clock",clock);
            Event.current.type=EventType.Layout;Draw(app);Event.current.type=EventType.Repaint;Check(SaveState(g)==before&&(g.aim-direction).Length<.0001f&&ReferenceEquals(Target(g),e),"Actual Layout/Repaint is save/simulation/selection read-only");
            foreach(string hidden in new[]{"menu","application","review"})
            {
                if(hidden=="menu")g.mode="pause";else if(hidden=="application")Set(app,"applicationPaused",true);else app.VisualReviewPaused=true;
                Draw(app);Check(Marker(app,e).Count==0,"Actual marker hides during "+hidden);
                if(hidden=="menu"){g.Close();Key(app,KeyCode.V);}else if(hidden=="application")Set(app,"applicationPaused",false);else app.VisualReviewPaused=false;
            }
        }
        Prepare(app);Enemy danger=Lock(app);danger.kind=2;danger.state=1;danger.target=danger.Position;danger.stun=0;danger.timer=.7f;danger.windupDuration=.9f;
        Set(app,"combatHudTime",3f);Set(app,"camera",danger.Position-new Point((1145-640)/36f,(121-390)/36f));Draw(app,"danger-priority");
        var marker=Marker(app,danger);Check(marker.Count==16&&(bool)Call(app,"ThreatOver",FocusButton(true)),"Actual target and warning overlap the new combat control");
        int markerIndex=GUI.textureCalls.IndexOf(marker[marker.Count-1]);int dangerIndex=GUI.textureCalls.FindLastIndex(c=>c.texture==Texture2D.whiteTexture&&Near(c.rect.height,3)&&Same(c.color,PixelArt.C("#df8a68")));
        Check(dangerIndex>markerIndex,"Actual foreground danger contour is submitted after focus brackets and overlapping HUD");
        Check(captureFolder==null||captures==17,"Exactly sixteen bilingual/size/resolution/HUD captures plus actual danger overlap were written");
        foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})foreach(bool pad in new[]{false,true})
        {
            Prepare(app);var g=app.Game;g.save.english=english;g.save.largeText=large;Key(app,KeyCode.F1);
            Check(g.mode=="help","Actual F1 opens the original field guide");Click(app,new Rect(279,177,160,66));
            if(pad)Key(app,KeyCode.JoystickButton9);
            Check(Get<bool>(app,"controllerUsed")==pad,"Actual keyboard/R3 input selects the matching controls help body");
            string before=SaveState(g);string suffix="help-controls-"+(pad?"pad-":"keyboard-")+(english?"en":"zh")+(large?"-large":"-normal");Draw(app,suffix);
            string binding=pad?(english?"R3: lock / release target":"R3：锁定 / 解除目标"):(english?"V: lock / release target":"V：锁定 / 解除目标");
            string body=GUI.commands.Find(c=>c.Contains("\"k\":\"text\"")&&c.Contains(binding));Check(body!=null,"Actual controls help renders the new "+(pad?"R3":"V")+" binding in both languages");
            string[] original=pad?(english?new[]{"Left stick: move","X: attack","A: dodge","Y: technique","B: talk, gather and operate","LB: stew","RB: companion command","VIEW: journal","START: pause","In menus: stick / D-pad moves focus, A selects, B returns, LB/RB switches menus.","Use stew and tea in Inventory.","Press B at a sealed route.","A turns dialogue pages, then selects a choice."}:new[]{"左摇杆：移动","X：攻击","A：闪避","Y：战技","B：交谈、采集与机关","LB：炖汤","RB：同伴指令","VIEW：手帐","START：暂停","菜单内用摇杆或方向键移动焦点，A确认、B返回，LB/RB切换菜单。","炖汤与凉茶也可在背包使用。","靠近封印按B开启旧路。","长对话用A翻页，再确认选项。"}):(english?new[]{"WASD / arrows: move","J / left click: attack","Space: dodge","Q: class technique","E: talk, gather and operate","F: stew","T: tea","Z: companion command","I inventory","G relics","K talents","C companions","Tab journal","M map","B projects","N contracts","F1 guide","Esc closes menus or pauses.","Space or Enter turns dialogue pages."}:new[]{"WASD / 方向键：移动","J / 鼠标左键：攻击","空格：闪避","Q：职业技能","E：交谈、采集与机关","F：炖汤","T：凉茶","Z：同伴指令","I背包","G遗物","K成长","C同伴","Tab任务","M地图","B建设","N悬赏","F1指南","Esc关闭菜单或暂停。","长对话可用空格或Enter翻页。"});
            foreach(string retained in original)Check(body.Contains(retained),"Original controls help retains "+retained);
            Check(body.Contains("\"size\":"+(large?19:18))&&body.Contains("\"padding\":[0,0,0,0]"),"Actual help body capture records its normal/large font and content padding");
            Event.current.type=EventType.Layout;Draw(app);Event.current.type=EventType.Repaint;Draw(app);Check(SaveState(g)==before&&g.mode=="help"&&Target(g)==null,"Reading actual controls help never casts, locks or alters campaign progress");
        }
        Check(captureFolder==null||captures==25,"Exactly seventeen world/HUD captures plus eight bilingual normal/large keyboard/pad help captures were written");
        Console.WriteLine("PASS static low-FX/reduced-motion brackets, full-health enemy bar, bilingual/normal-large 960/1920 font padding, render isolation, pause/menu/review hiding, foreground danger priority and actual F1 keyboard/pad help retains old controls plus V/R3; captures="+captures+"; NATIVE_UNVERIFIED");
    }
    private static void LegacySaveStoreAndProgressRemainCompatible(FrontierRuntime app)
    {
        Prepare(app);var g=app.Game;Enemy e=Enemy(app,9);Draw(app);string before=SaveState(g);int revision=g.revision;Rect rect=FocusButton(false);
        Click(app,rect);Check(ReferenceEquals(Target(g),e)&&SaveState(g)==before&&g.revision==revision,"Real GUI acquisition changes no SaveData, progress or revision");
        Click(app,rect);Check(Target(g)==null&&SaveState(g)==before&&g.revision==revision,"Real GUI release changes no SaveData, progress or revision");
        Click(app,rect);g.PrepareSave();string payload=SaveState(g);Check(!payload.ToLowerInvariant().Contains("combatfocus"),"Focus adds no fields to the existing SaveData payload");
        Check(SaveStore.Write(g.save,2),"Actual checksum SaveStore writes unchanged schema");string warning;SaveData loaded=SaveStore.Read(2,out warning);
        Check(loaded!=null&&warning==null&&!File.ReadAllText(SaveStore.PathFor(2)).ToLowerInvariant().Contains("combatfocus"),"Actual checksum envelope contains no transient selection");
        g.Load(loaded);g.Start();Check(Target(g)==null,"Actual checksummed load starts in free aim");
        foreach(int version in new[]{1,2})
        {
            Prepare(app);g=app.Game;Lock(app);string old="{\"version\":"+version+",\"profession\":1,\"room\":1,\"x\":12,\"y\":14,\"coins\":137,\"stage\":2,\"inventory\":[4,2,4,0,2,0,1,3],\"flags\":[\"intro\",\"focus-legacy-sentinel\"]}";
            File.WriteAllText(SaveStore.PathFor(0),old);loaded=SaveStore.Read(0,out warning);Check(loaded!=null&&warning==null,"Raw old v"+version+" save decodes without new fields");
            g.Load(loaded);g.Start();Check(Target(g)==null&&g.save.coins==137&&g.save.stage==2&&g.Has("focus-legacy-sentinel"),"Raw old v"+version+" preserves campaign progress and never restores focus");
        }
        foreach(FieldInfo field in typeof(SaveData).GetFields())Check(!field.Name.ToLowerInvariant().Contains("combatfocus"),"Transient selection does not expand public save schema");
        Console.WriteLine("PASS real focus GUI leaves SaveData/revision/progress unchanged, actual checksummed SaveStore and raw legacy v1/v2 loads retain campaign progress with free aim and no focus save fields; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        captureFolder=args.Length>0?args[0]:null;string previous=Application.persistentDataPath;
        string temporary=Path.Combine(Path.GetTempPath(),"starfall-focus-save-"+Guid.NewGuid());Application.persistentDataPath=temporary;Directory.CreateDirectory(temporary);
        var app=new FrontierRuntime();
        try
        {
            Call(app,"Awake");if(app.Game==null){Console.WriteLine("FAIL Awake cannot load shipped campaign");return 1;}
            Action<FrontierRuntime>[] tests={ActualKeyboardPadAndDefaultFreeAim,BackpedalActionsAndPreparedContactStayImmutable,SessionAndActorBoundariesDiscardFocus,ActualHudClicksAndWorldPointerOwnership,MarkerVisibilityDangerPriorityAndBilingualCaptures,LegacySaveStoreAndProgressRemainCompatible};
            int failures=0;foreach(var test in tests)try{test(app);}catch(Exception error){failures++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}
            Console.WriteLine("Runtime combat focus "+(tests.Length-failures)+"/"+tests.Length+"; NATIVE_UNVERIFIED");return failures==0?0:1;
        }
        finally{app.EndVisualReview();Call(app,"OnDestroy");Application.persistentDataPath=previous;if(Directory.Exists(temporary))Directory.Delete(temporary,true);}
    }
}
