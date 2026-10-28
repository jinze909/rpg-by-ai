// Actual production Update/OnGUI and SaveStore paths with offline declarations.
// These checks do not establish native Unity import, GPU rendering or hardware input.
using System;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeBountyRewardChecks
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly Rect Claim=new Rect(310,458,271,46),Track=new Rect(602,458,271,46),Abandon=new Rect(895,458,288,46),Accept=new Rect(999,207,192,43);
    static string captureFolder,capturePrefix;static int captures;
    sealed class Label {public string text=null;public float[] r=null;}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static object Call(object target,string name,params object[] args){var m=target.GetType().GetMethod(name,Private|BindingFlags.Public);Check(m!=null,"Production entry point exists: "+name);return m.Invoke(target,args);}
    static void Set(FrontierRuntime app,string name,object value){typeof(FrontierRuntime).GetField(name,Private).SetValue(app,value);}
    static T Get<T>(FrontierRuntime app,string name){return(T)typeof(FrontierRuntime).GetField(name,Private).GetValue(app);}
    static T Field<T>(object quote,string name){Check(quote!=null,"Active reward quote exists");var field=quote.GetType().GetField(name);Check(field!=null,"Readonly quote field exists: "+name);return(T)field.GetValue(quote);}
    static object Quote(GameCore g){return Call(g,"GetBountyRewardQuote");}
    static bool Near(float a,float b){return Math.Abs(a-b)<.003f;}
    static bool Same(Rect a,Rect b){return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height);}
    static void Update(FrontierRuntime app){Call(app,"Update");Input.keys.Clear();}
    static void Key(FrontierRuntime app,KeyCode key){Input.keys.Add(key);Update(app);}
    static void Draw(FrontierRuntime app,string capture=null)
    {
        GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        if(captureFolder!=null&&capture!=null){var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();GUI.Export(captureFolder,capturePrefix+"-"+capture);captures++;GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);}
    }
    static string At(float x,float y,float width,float height)
    {
        foreach(string command in GUI.commands){if(!command.Contains("\"k\":\"text\""))continue;var label=JsonUtility.FromJson<Label>(command);if(label.r!=null&&Near(label.r[0],x)&&Near(label.r[1],y)&&Near(label.r[2],width)&&Near(label.r[3],height))return label.text;}return null;
    }
    static void Enabled(Rect rect,bool enabled){Check(GUI.buttonCalls.Exists(b=>Same(b.rect,rect)&&b.enabled==enabled),"Original button geometry and truthful enabled state at "+rect.x+","+rect.y);}
    static void Activate(FrontierRuntime app,Rect rect,int device=0)
    {
        Draw(app);Enabled(rect,true);
        if(device==0){GUI.queuedClick=rect;Draw(app);Check(!GUI.queuedClick.HasValue,"Actual mouse click consumed");}
        else{string control=rect.x+":"+rect.y+":"+rect.width+":"+rect.height;var navigation=Get<MenuNavigation>(app,"navigation");navigation.Select(control);Check(navigation.CanActivate(control),"Actual focused action can activate");Key(app,device==1?KeyCode.Return:KeyCode.JoystickButton0);Draw(app);Check(Get<string>(app,"pressedControl")==control,"Actual Enter/pad A consumed by OnGUI");}
        Draw(app);
    }
    static void Prepare(FrontierRuntime app)
    {
        Input.Clear();GUI.queuedClick=null;GUI.enabled=true;GUI.color=Color.white;GUI.matrix=Matrix4x4.identity;Event.current.type=EventType.Repaint;Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;
        app.BeginVisualReview(1,0);app.VisualReviewPaused=false;var g=app.Game;g.enemies.Clear();g.projectiles.Clear();g.hazards.Clear();g.effects.Clear();g.fallen.Clear();
        g.save.companion=-1;g.save.english=true;g.save.largeText=false;g.save.showGuidance=false;g.save.reducedMotion=true;g.save.effectsQuality=0;g.save.bounty=-1;g.save.bountyProgress=0;g.save.bountyCycle=0;
        g.save.coins=100;g.save.shards=10;g.save.level=20;g.save.xp=0;g.save.skillPoints=3;g.save.frontierEnding=0;g.save.health=22;g.save.flags.Add("bounty_reward_mature_story");
        Set(app,"applicationPaused",false);Set(app,"pointerSeen",false);Set(app,"pointerAimActive",false);Set(app,"controllerUsed",false);Set(app,"arrivalTime",0f);Set(app,"resumeTime",0f);Set(app,"combatHudTime",0f);Call(app,"Resize");
    }
    static void Open(FrontierRuntime app){Key(app,KeyCode.N);Check(app.Game.mode=="bounties","Actual N opens original Contracts");Draw(app);}
    static void RewardText(FrontierRuntime app,bool active,int shards)
    {
        string text=active?At(310,292,873,22):At(366,239,585,27);
        Check(text!=null&&text.Contains("35")&&text.Contains("30")&&text.Contains(shards.ToString())&&(text.ToLowerInvariant().Contains("shard")||text.Contains("碎片")),"Actual acceptance/active card exposes exact 35 coins, 30 XP and "+shards+" shards");
    }
    static void PreviewAndOriginalClaimInputs(FrontierRuntime app)
    {
        foreach(int ending in new[]{0,3})for(int id=0;id<6;id++)
        {
            Prepare(app);var g=app.Game;g.save.frontierEnding=ending;g.save.bountyCycle=id;Open(app);int shards=ending==3?4:3;RewardText(app,false,shards);if(id==5&&!g.CanEnter(8)){Enabled(Accept,false);g.AcceptBounty(5);Check(g.save.bounty==-1,"Original final contract rejects acceptance until expedition unlock");g.save.frontierEnding=1;Draw(app);Enabled(Accept,true);}Activate(app,Accept,id%3);Check(g.save.bounty==id&&g.save.bountyProgress==0&&g.save.trackedQuest=="bounty","Actual Accept retains target and tracking");RewardText(app,true,shards);Enabled(Claim,false);
            object quote=Quote(g);Check(Field<int>(quote,"coinReward")==35&&Field<int>(quote,"experienceReward")==30&&Field<int>(quote,"shardReward")==shards&&!Field<bool>(quote,"complete")&&!Field<bool>(quote,"canClaim"),"Incomplete live quote has exact original rewards without premature claim");
            g.save.bountyProgress=GameCore.BountyTargets[id];Draw(app);Enabled(Claim,true);quote=Quote(g);Check(Field<bool>(quote,"complete")&&Field<bool>(quote,"canClaim")&&Field<long>(quote,"coinsAfter")==135&&Field<long>(quote,"shardsAfter")==10+shards,"Completed live quote projects exact balances");
            string balance=At(310,410,873,35);Check(balance!=null&&balance.Contains("100")&&balance.Contains("135")&&balance.Contains("10")&&balance.Contains((10+shards).ToString())&&balance.Contains("→"),"Actual completed card previews both current and resulting balances");
            Activate(app,Claim,id%3);Check(g.save.coins==135&&g.save.shards==10+shards&&g.save.xp==30&&g.save.skillPoints==3&&g.save.bounty==-1&&g.save.bountyProgress==0&&g.save.bountyCycle==id+1,"Mouse/Enter/pad A claims original rewards once and retires original contract");string paid=JsonUtility.ToJson(g.save);Check(!g.ClaimBounty()&&JsonUtility.ToJson(g.save)==paid&&g.Has("bounty_reward_mature_story"),"Repeated claim cannot pay again or change mature story");
        }
        Console.WriteLine("PASS all six original contracts in 3/4-shard endings, actual N/Accept/Claim mouse/Enter/pad A, incomplete disabled claim, exact reward/balance preview, original one-time payout and mature story; NATIVE_UNVERIFIED");
    }
    static void BlockedRewardRetainedAcrossSaveAndRetry(FrontierRuntime app)
    {
        for(int kind=0;kind<2;kind++)
        {
            Prepare(app);var g=app.Game;g.save.bounty=kind;g.save.bountyProgress=GameCore.BountyTargets[kind];g.save.frontierEnding=3;if(kind==0)g.save.coins=999980;else g.save.shards=99997;Open(app);
            Enabled(Claim,false);object quote=Quote(g);Check(Field<bool>(quote,"complete")&&!Field<bool>(quote,"canClaim"),"Ready reward blocked by persistent "+(kind==0?"coin":"shard")+" cap");Enabled(Track,true);Enabled(Abandon,true);Check(!string.IsNullOrEmpty(At(310,511,873,20)),"Blocked card has visible explanation");
            string ready=JsonUtility.ToJson(g.save);int cycle=g.save.bountyCycle;GUI.queuedClick=Claim;Draw(app);Check(GUI.queuedClick.HasValue&&JsonUtility.ToJson(g.save)==ready,"Disabled actual mouse claim retains ready contract and balances");GUI.queuedClick=null;foreach(KeyCode key in new[]{KeyCode.Return,KeyCode.JoystickButton0}){var navigation=Get<MenuNavigation>(app,"navigation");Check(!navigation.CanActivate("310:458:271:46"),"Disabled Claim is absent from actual focus activation list");navigation.focused="310:458:271:46";Key(app,key);Draw(app);Check(g.mode=="bounties"&&JsonUtility.ToJson(g.save)==ready,"Actual Enter/pad A cannot activate stale focus on disabled completed Claim");}Check(!g.ClaimBounty()&&JsonUtility.ToJson(g.save)==ready,"Direct stale/alternate claim rechecks capacity and preserves earned contract");
            g.PrepareSave();Check(SaveStore.Write(g.save,0),"Ready completed reward writes through actual checksum SaveStore");string warning;var saved=SaveStore.Read(0,out warning);Check(saved!=null&&warning==null,"Actual checksum reader accepts blocked contract");g.Load(saved);g.Start();g.Close();g.enemies.Clear();Check(g.save.bounty==kind&&g.save.bountyProgress==GameCore.BountyTargets[kind]&&g.save.bountyCycle==cycle,"Reload preserves ready contract progress and cycle");Open(app);Enabled(Claim,false);
            // The previous quote must never authorize a later payout after state changes.
            if(kind==0)g.save.coins=999964;else g.save.shards=99995;Draw(app);Enabled(Claim,true);int coins=g.save.coins,shards=g.save.shards;Activate(app,Claim,kind+1);Check(g.save.coins==coins+35&&g.save.shards==shards+4&&g.save.bounty==-1&&g.save.bountyCycle==cycle+1,"Freeing capacity enables exact once-only original reward after restoration");
        }
        Prepare(app);var game=app.Game;game.save.bounty=0;game.save.bountyProgress=6;game.save.coins=999964;Open(app);Enabled(Claim,true);game.save.coins=999999;Check(!game.ClaimBounty()&&game.save.bounty==0&&game.save.bountyProgress==6&&game.save.coins==999999,"Live claim rejects formerly affordable quote after balance increases");
        Console.WriteLine("PASS actual disabled Claim, checksum completed-contract persistence, coin/shard cap protection, fresh capacity recheck and retry after freeing space without losing progress, rotation or rewards; NATIVE_UNVERIFIED");
    }
    static void LevelsLegacyTrackingAndAbandon(FrontierRuntime app)
    {
        Prepare(app);var g=app.Game;g.save.bounty=0;g.save.bountyProgress=6;g.save.level=1;g.save.xp=39;g.save.skillPoints=98;Open(app);object quote=Quote(g);Check(Field<int>(quote,"levelAfter")==2&&Field<long>(quote,"experienceAfter")==29&&Field<long>(quote,"skillPointsAfter")==99&&Field<bool>(quote,"canClaim"),"Quote uses original 40 XP level threshold and exact skill-point result");Activate(app,Claim);Check(g.save.level==2&&g.save.xp==29&&g.save.skillPoints==99&&g.save.health==g.save.maxHealth,"Original AddXp still grants level, point and full heal at payment");
        Prepare(app);g=app.Game;g.save.bounty=0;g.save.bountyProgress=6;g.save.level=1;g.save.xp=39;g.save.skillPoints=99;Open(app);Enabled(Claim,false);Check(!g.ClaimBounty()&&g.save.level==1&&g.save.xp==39&&g.save.skillPoints==99&&g.save.bounty==0,"Level-up cannot consume ready contract while its skill point would be clipped on load");g.save.skillPoints=98;Draw(app);Activate(app,Claim);Check(g.save.skillPoints==99&&g.save.level==2,"Spending one point permits original level reward later");
        Prepare(app);g=app.Game;g.save.bounty=0;g.save.bountyProgress=6;g.save.level=30;g.save.xp=int.MaxValue;Open(app);Enabled(Claim,false);Check(!g.ClaimBounty()&&g.save.xp==int.MaxValue&&g.save.bounty==0,"Reward cannot overflow XP integer storage or consume ready contract");
        Prepare(app);g=app.Game;g.save.bounty=2;g.save.bountyProgress=3;g.save.trackedQuest="main";Open(app);int coins=g.save.coins,shards=g.save.shards,xp=g.save.xp;Activate(app,Track);Check(g.save.trackedQuest=="bounty"&&g.save.coins==coins&&g.save.shards==shards&&g.save.xp==xp&&g.save.bounty==2,"Original Track is available without consuming completed reward");
        g.PrepareSave();string raw=JsonUtility.ToJson(g.save),warning;foreach(int version in new[]{1,2}){var old=JsonUtility.FromJson<SaveData>(raw);old.version=version;File.WriteAllText(SaveStore.PathFor(1),JsonUtility.ToJson(old));var saved=SaveStore.Read(1,out warning);Check(saved!=null&&warning==null,"Actual legacy v"+version+" reader accepts original save");g.Load(saved);g.Start();g.Close();Check(g.Has("bounty_reward_mature_story")&&g.save.coins==coins&&g.save.shards==shards&&g.save.xp==xp,"Original legacy rewards/story retained");Check(version==1?g.save.bounty==-1&&g.save.bountyProgress==0:g.save.bounty==2&&g.save.bountyProgress==3,"Original v1 reset/v2 completed-contract migration remains unchanged");}
        Open(app);Activate(app,Abandon,2);Check(g.save.bounty==-1&&g.save.bountyProgress==0&&g.save.coins==coins&&g.save.shards==shards&&g.save.xp==xp,"Original explicit Abandon remains available and grants no reward");
        Console.WriteLine("PASS original XP rollover/point/full-heal result, saved point capacity protection and later retry, mature Track/Abandon inputs and original raw legacy v1/v2 migration without schema changes; NATIVE_UNVERIFIED");
    }
    static void BilingualLayoutsAndReadOnlyMenus(FrontierRuntime app)
    {
        foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})foreach(int width in new[]{960,1280})for(int state=0;state<3;state++)
        {
            Prepare(app);var g=app.Game;g.save.english=english;g.save.largeText=large;g.save.frontierEnding=3;if(state!=0){g.save.bounty=4;g.save.bountyProgress=4;}if(state==2)g.save.coins=999999;Screen.width=width;Screen.height=width==960?600:800;Call(app,"Resize");Open(app);RewardText(app,state!=0,4);if(state!=0){Enabled(Claim,state==1);Check(At(310,410,873,35)!=null&&At(310,511,873,20)!=null,"Live balances and status retain assigned text bounds");}
            string before=JsonUtility.ToJson(g.save);int revision=g.revision;float clock=Get<float>(app,"clock");foreach(EventType evt in new[]{EventType.Layout,EventType.Repaint}){Event.current.type=evt;for(int n=0;n<5;n++)Draw(app);}Check(JsonUtility.ToJson(g.save)==before&&g.revision==revision&&Get<float>(app,"clock")==clock,"Repeated actual Layout/Repaint is read-only");Event.current.type=EventType.Repaint;Draw(app,"layout-"+(english?"en":"zh")+(large?"-large":"-normal")+"-"+width+"-"+state);
        }
        if(captureFolder!=null)Check(captures==24,"Exactly 24 fresh bilingual size/state captures");
        Console.WriteLine("PASS 24 zh/en normal/large 960/1280 actual Contracts OnGUI captures for acceptance, ready and capped rewards, exact numeric fields and read-only Layout/Repaint; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-bounty-reward-"+Guid.NewGuid());Application.persistentDataPath=directory;captureFolder=args.Length>0&&args[0]!="-"?args[0]:null;capturePrefix=args.Length>1?args[1]:"qa-bounty-reward";if(captureFolder!=null)Directory.CreateDirectory(captureFolder);var app=new FrontierRuntime();int failed=0;
        try{Directory.CreateDirectory(directory);Call(app,"Awake");Check(app.Game!=null,"Actual Awake loads shipped campaign");foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{PreviewAndOriginalClaimInputs,BlockedRewardRetainedAcrossSaveAndRetry,LevelsLegacyTrackingAndAbandon,BilingualLayoutsAndReadOnlyMenus})try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}}
        finally{app.EndVisualReview();Call(app,"OnDestroy");Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;GUI.enabled=true;GUI.color=Color.white;GUI.matrix=Matrix4x4.identity;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime bounty reward: "+(4-failed)+"/4 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
