// Full production Update, OnGUI, combat/cache reward and SaveStore paths.
// Offline declarations do not establish native Unity/fonts/GPU/hardware evidence.
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeWeaponMasteryChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static string captures;
    static int captureCount, fillWitnesses;
    sealed class LabelCommand { public string text; public float[] r = null; public int size = 0; }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static object Call(object target, string method, params object[] args)
    {
        MethodInfo found = target.GetType().GetMethod(method, Private | BindingFlags.Public);
        Check(found != null, "Production method exists: " + method); return found.Invoke(target, args);
    }
    static void Set(FrontierRuntime app, string field, object value)
    { typeof(FrontierRuntime).GetField(field, Private).SetValue(app, value); }
    static T Get<T>(FrontierRuntime app, string field)
    { return (T)typeof(FrontierRuntime).GetField(field, Private).GetValue(app); }
    static T Member<T>(object value, string field)
    {
        FieldInfo found = value.GetType().GetField(field);
        Check(found != null && found.IsInitOnly, "Mastery snapshot has immutable public readonly field: " + field);
        return (T)found.GetValue(value);
    }
    static object Progress(GameCore g, int weapon) { return Call(g, "GetWeaponMasteryProgress", weapon); }
    static string Summary(GameCore g, int weapon) { return (string)Call(g, "WeaponMasteryProgressText", weapon); }
    static bool Same(Rect a, Rect b) { return a.x == b.x && a.y == b.y && a.width == b.width && a.height == b.height; }
    static string RectKey(Rect r)
    { return "\"r\":[" + r.x.ToString(CultureInfo.InvariantCulture) + "," + r.y.ToString(CultureInfo.InvariantCulture) + "," + r.width.ToString(CultureInfo.InvariantCulture) + "," + r.height.ToString(CultureInfo.InvariantCulture) + "]"; }
    static LabelCommand Label(Rect rect)
    {
        string command = GUI.commands.Find(c => c.Contains("\"k\":\"text\"") && c.Contains(RectKey(rect)));
        Check(command != null, "Actual OnGUI exposes mastery text at " + RectKey(rect));
        return JsonUtility.FromJson<LabelCommand>(command);
    }
    static string L(FrontierRuntime app, string zh, string en) { return app.Game.L(zh, en); }
    static string Weapon(FrontierRuntime app, int weapon)
    { return L(app, new[] { "短剑", "猎弓", "星火杖" }[weapon], new[] { "Sword", "Bow", "Starfire staff" }[weapon]); }
    static void Draw(FrontierRuntime app, string capture = null)
    {
        GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); Call(app, "OnGUI");
        if (capture != null && captures != null)
        {
            var commands = GUI.commands.ToArray(); var textures = GUI.textureCalls.ToArray(); var buttons = GUI.buttonCalls.ToArray();
            GUI.Export(captures, "qa-weapon-mastery-" + capture); GUI.commands.AddRange(commands);
            GUI.textureCalls.AddRange(textures); GUI.buttonCalls.AddRange(buttons); captureCount++;
        }
    }
    static void Update(FrontierRuntime app) { Call(app, "Update"); Input.keys.Clear(); }
    static void Key(FrontierRuntime app, KeyCode key) { Input.keys.Add(key); Update(app); }
    static void Frames(FrontierRuntime app, int count) { for (int i = 0; i < count; i++) Update(app); }
    static Rect Button(FrontierRuntime app, string label, bool enabled = true)
    {
        Draw(app);
        foreach (string command in GUI.commands)
        {
            if (!command.Contains("\"k\":\"text\"") || !command.Contains("\"text\":" + GUI.S(label))) continue;
            LabelCommand c = JsonUtility.FromJson<LabelCommand>(command); Rect rect = new Rect(c.r[0], c.r[1], c.r[2], c.r[3]);
            var button = GUI.buttonCalls.Find(b => Same(b.rect, rect));
            if (button == null) continue;
            Check(button.enabled == enabled, "The real displayed button retains its gate: " + label); return rect;
        }
        throw new Exception("The actual GUI exposes no button labelled " + label);
    }
    static void Click(FrontierRuntime app, string label)
    {
        GUI.queuedClick = Button(app, label); Draw(app);
        Check(!GUI.queuedClick.HasValue, "Actual displayed control consumes the click: " + label);
    }
    static void Prepare(FrontierRuntime app, int profession = 0, int room = 0)
    {
        Input.Clear(); GUI.queuedClick = null; GUI.enabled = true; GUI.color = Color.white; GUI.matrix = Matrix4x4.identity;
        Screen.width = 1280; Screen.height = 800; Time.unscaledDeltaTime = .02f; Event.current.type = EventType.Repaint;
        app.BeginVisualReview(profession, room); app.VisualReviewPaused = false; var g = app.Game;
        g.enemies.Clear(); g.projectiles.Clear(); g.hazards.Clear(); g.save.companion = -1;
        g.save.flags.Remove("core_1"); g.save.flags.Remove("weapon_2"); g.save.flags.Remove("technique_" + profession);
        g.save.flags.Remove("cache_" + room); g.save.weapon = 0; g.save.weaponStyles = new int[3]; g.save.mastery = new int[3];
        g.save.technique = 0; g.save.level = 5; g.save.xp = 9; g.save.skillPoints = 3; g.save.coins = 313; g.save.shards = 17;
        g.save.health = 58; g.save.energy = 47; g.save.relics = new[] { -1, -1 }; g.save.talents = new[] { 1, 2, 1 };
        g.save.english = true; g.save.largeText = false; g.save.reducedMotion = true; g.save.effectsQuality = 0;
        g.save.totalTime = 100; g.save.foodUntil = g.save.teaUntil = g.save.mealUntil = 0; g.save.showGuidance = false;
        g.invulnerable = 100; g.toastTime = 0; Set(app, "applicationPaused", false); Set(app, "pointerSeen", false);
        Set(app, "controllerUsed", false); Set(app, "combatHudTime", 0f); Set(app, "resumeTime", 0f); Set(app, "arrivalTime", 0f);
        Set(app, "inspectedWeapon", 0); Set(app, "inspectedStyle", 0); Call(app, "Resize");
    }
    static void Inventory(FrontierRuntime app)
    { if (app.Game.mode != "inventory") Key(app, KeyCode.I); Check(app.Game.mode == "inventory", "Actual I opens the original inventory"); Draw(app); }
    static void Select(FrontierRuntime app, int weapon, int style = 0)
    {
        Inventory(app); Click(app, Weapon(app, weapon));
        ComparedInSameDraw(app, weapon, app.Game.save.weaponStyles[weapon]);
        Click(app, L(app, new[] { "均衡", "蓄势", "流转" }[style], new[] { "Balanced", "Focused", "Flow" }[style]));
        Check(Get<int>(app, "inspectedWeapon") == weapon && Get<int>(app, "inspectedStyle") == style, "Actual existing controls inspect the requested weapon and style");
        ComparedInSameDraw(app, weapon, style);
    }
    static void ComparedInSameDraw(FrontierRuntime app, int weapon, int style)
    {
        string shown = Label(new Rect(297, 690, 583, 19)).text;
        Check(shown == Summary(app.Game, weapon), "The actual selection-click draw immediately shows the newly inspected weapon's mastery");
        WeaponProfile candidate = app.Game.Profile(weapon, style), current = app.Game.Profile(app.Game.save.weapon, app.Game.Style);
        // Independent original ReleaseAttack contact rule, rather than reading
        // the new display helper back into its own expected value.
        string stats = L(app, "首击/每箭 / 间隔 / 耗能：", "Hit/arrow / gap / energy: ") + Contact(weapon, style, candidate.damage).ToString("0.##", CultureInfo.InvariantCulture) + " / " + candidate.interval.ToString("0.00", CultureInfo.InvariantCulture) + "s / " + candidate.energy.ToString("0.##", CultureInfo.InvariantCulture) + "\n"
            + L(app, "当前：", "Equipped: ") + Contact(app.Game.save.weapon, app.Game.Style, current.damage).ToString("0.##", CultureInfo.InvariantCulture) + " / " + current.interval.ToString("0.00", CultureInfo.InvariantCulture) + "s / " + current.energy.ToString("0.##", CultureInfo.InvariantCulture);
        Check(Label(new Rect(297, 577, 389, 59)).text == stats, "The same actual selection-click draw updates candidate damage/interval/energy while retaining equipped stats");
    }
    static float Contact(int weapon, int style, float damage) { return damage * (weapon == 1 && style == 2 ? .65f : 1); }
    static void Strip(FrontierRuntime app, int weapon, int points)
    {
        // This assertion intentionally precedes reflection, so old production
        // compiles and fails on the missing player-visible strip, not an API.
        LabelCommand summary = Label(new Rect(297, 690, 583, 19));
        Check(summary.text == Summary(app.Game, weapon) && summary.size == (app.Game.save.largeText ? 13 : 12), "Inventory shows the inspected weapon's localized production mastery summary at the reserved size");
        Check(summary.text.Contains((points / 20) + "/5") && summary.text.Contains(points + "/100"), "Displayed rank and points report the inspected weapon's actual boundaries");
        Check(summary.text.Contains(app.Game.save.english ? "Mastery" : "熟练度"), "The mastery summary follows the current language");
        LabelCommand source = Label(new Rect(297, 710, 583, 16));
        Check(source.size == (app.Game.save.largeText ? 11 : 10) && source.text.Contains("+2") && source.text.Contains("+10") && source.text.Contains("+8"), "The small second line exposes all original mastery reward amounts");
        Check(source.text == L(app, "普通敌人+2 · Boss+10 · 首次旧日补给+8 · 每级基础伤害+2", "Normal foes +2 · Bosses +10 · First caches +8 · +2 base damage/rank"), "Reward sources and per-rank base damage are localized and preserve the original amounts");
        Color mint = Get<Color>(app, "mint"); float fraction = points == 100 ? 1 : (points % 20) / 20f, width = 583 * fraction;
        Check(GUI.textureCalls.Exists(c => Same(c.rect, new Rect(297, 727, 583, 2))), "Actual mastery bar draws the complete two-pixel track");
        var fill = GUI.textureCalls.Find(c => Same(c.rect, new Rect(297, 727, width, 2)) && Math.Abs(c.color.r - mint.r) < .0001f && Math.Abs(c.color.g - mint.g) < .0001f && Math.Abs(c.color.b - mint.b) < .0001f);
        Check(fill != null, "Actual mint fill witnesses progress toward the next rank at " + points + "/100, including zero, rank reset and cap");
        float expectedScale = Math.Min(Screen.width / 1280f, Screen.height / 800f);
        Check(Math.Abs(fill.matrix.m00 - expectedScale) < .0001f && Math.Abs(fill.matrix.m11 - expectedScale) < .0001f, "The real bar participates in the screen transform at normal and 960 layouts");
        Check(GUI.textureCalls.Exists(c => Same(c.rect, new Rect(279, 448, 619, 281))), "Only the existing comparison card grows into its reserved bottom gap");
        Check(GUI.buttonCalls.Exists(b => Same(b.rect, new Rect(710, 580, 174, 52))), "The original equip button remains at its mature coordinates");
        Label(new Rect(297, 577, 389, 59)); Label(new Rect(297, 642, 583, 41)); fillWitnesses++;
    }
    static void Talents(FrontierRuntime app, int weapon)
    {
        Key(app, KeyCode.K); Check(app.Game.mode == "talents", "Actual K opens the existing talents page"); Draw(app);
        string expected = L(app, "当前 Q：", "Current Q: ") + (string)Call(app, "SkillName") + "  /  " + Summary(app.Game, weapon);
        Check(Label(new Rect(302, 603, 883, 28)).text == expected, "Existing Current Q row retains the actual class skill and uses equipped-weapon mastery");
    }
    static void Mouse(FrontierRuntime app, Point target)
    {
        Set(app, "camera", app.Game.Position); Point p = (Point)Call(app, "ScreenPoint", target);
        Input.mousePosition = new Vector3(Get<float>(app, "offsetX") + p.x * Get<float>(app, "scale"), Screen.height - Get<float>(app, "offsetY") - p.y * Get<float>(app, "scale"), 0);
    }

    static void ActualInventorySelectionAndEquippedQ(FrontierRuntime app)
    {
        Prepare(app, 1); var g = app.Game; g.save.mastery = new[] { 100, 18, 98 };
        Inventory(app); string before = JsonUtility.ToJson(g.save); int revision = g.revision; float damage = g.Damage;
        Select(app, 1, 1); Strip(app, 1, 18); Button(app, L(app, "装备此打法", "Equip style"), false);
        Check(JsonUtility.ToJson(g.save) == before && g.revision == revision && g.Damage == damage, "Comparing an owned alternate weapon/style never auto-equips or mutates progression");
        Select(app, 2, 2); Strip(app, 2, 98); Button(app, L(app, "装备此打法", "Equip style"), false);
        Check(JsonUtility.ToJson(g.save) == before && g.save.weapon == 0 && g.Style == 0, "Unowned staff remains inspectable with its own progress and cannot equip even when its styles are unlocked");
        Talents(app, 0); int technique = g.save.technique; float cooldown = g.skillCooldown; Key(app, KeyCode.Q); Draw(app);
        Check(g.mode == "talents" && g.playerAction == null && g.save.technique == technique && g.skillCooldown == cooldown, "Reading equipped mastery on K never changes or fires Q");
        g.save.mastery[1] = 20; Select(app, 1, 1); Strip(app, 1, 20);
        Click(app, L(app, "装备此打法", "Equip style"));
        Check(g.save.weapon == 1 && g.Style == 1 && g.save.technique == technique && g.MasteryRank(1) == 1, "Only the original enabled equip button applies the inspected earned style");
        Talents(app, 1); Key(app, KeyCode.Escape); Check(g.mode == "world", "Escape resumes the original world");
        g.save.energy = 100; Mouse(app, g.Position + new Point(3, 0)); Key(app, KeyCode.Q); ActorAction cast = g.playerAction;
        Check(cast != null && cast.kind == "skill" && cast.weapon == 1 && cast.profession == 1 && cast.technique == technique && cast.style == 1
            && g.save.energy == 60 && g.skillCooldown == 3.5f, "Actual Q retains the class technique, equipped style, original 40 energy and 3.5-second cooldown");
        Console.WriteLine("PASS mastery actual I weapon/style comparison, inspected alternate/unowned progress, old equip gate, equipped K stats and unchanged real Q; NATIVE_UNVERIFIED");
    }

    static void ReadOnlyPauseAndActualLegacySaves(FrontierRuntime app)
    {
        Prepare(app); var g = app.Game; g.save.mastery = new[] { 18, 20, 100 }; Inventory(app);
        g.attackCooldown = .8f; g.dashCooldown = .5f; g.skillCooldown = 2.4f; g.companionCooldown = 9; g.guardTime = 1.2f;
        var shot = new Projectile { position = g.Position, velocity = new Point(1, 0), damage = 17, friendly = true, life = 3 }; g.projectiles.Add(shot);
        string before = JsonUtility.ToJson(g.save); int revision = g.revision; object snapshot = Progress(g, 0);
        foreach (FieldInfo field in snapshot.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)) Check(field.IsInitOnly && field.FieldType.IsPrimitive, "The exposed mastery snapshot contains readonly scalar values only");
        for (int i = 0; i < 24; i++)
        {
            Progress(g, i % 3); Summary(g, i % 3); Draw(app); Frames(app, 1);
        }
        Check(JsonUtility.ToJson(g.save) == before && g.revision == revision && g.projectiles.Contains(shot) && shot.life == 3
            && g.attackCooldown == .8f && g.dashCooldown == .5f && g.skillCooldown == 2.4f && g.companionCooldown == 9 && g.guardTime == 1.2f,
            "Repeated production GUI/getters preserve every save field, revision, paused world clock, resources, cooldowns and live projectile");
        Check(Member<int>(snapshot, "weapon") == 0 && Member<int>(snapshot, "points") == 18 && Member<int>(snapshot, "rank") == 0
            && Member<int>(snapshot, "nextThreshold") == 20 && Member<int>(snapshot, "remaining") == 2 && Member<int>(snapshot, "baseDamageBonus") == 0
            && !Member<bool>(snapshot, "maximum") && !Member<bool>(snapshot, "stylesUnlocked") && Math.Abs(Member<float>(snapshot, "fraction") - .9f) < .0001f,
            "The 18-point snapshot truthfully exposes its next threshold and remaining original damage/style benefits");
        g.save.mastery[0] = 20; Check(Member<int>(snapshot, "points") == 18 && Member<int>(Progress(g, 0), "points") == 20, "A returned snapshot never changes behind the reader when future mastery changes");
        g.save.mastery[0] = 18; Check(Progress(g, -1) == null && Progress(g, 3) == null, "Invalid weapon requests return null without mutating the save");
        g.PrepareSave(); Check(SaveStore.Write(g.save, 0), "Actual checksum save stores existing mastery"); string warning;
        SaveData loaded = SaveStore.Read(0, out warning); Check(loaded != null && warning == null && loaded.mastery[0] == 18 && loaded.mastery[1] == 20 && loaded.mastery[2] == 100, "Actual SaveStore checksum roundtrip preserves mastery points exactly");
        string raw = JsonUtility.ToJson(loaded);
        Check(!raw.Contains("nextThreshold") && !raw.Contains("stylesUnlocked") && !raw.Contains("WeaponMasteryProgress"), "The read-only mastery presentation introduces no persisted snapshot fields");
        foreach (int version in new[] { 1, 2 })
        {
            SaveData old = JsonUtility.FromJson<SaveData>(raw); old.version = version; old.mastery = null;
            string legacy = JsonUtility.ToJson(old).Replace("\"mastery\":null,", "").Replace(",\"mastery\":null", "");
            Check(!legacy.Contains("\"mastery\""), "The legacy fixture truly omits mastery"); File.WriteAllText(SaveStore.PathFor(1), legacy);
            SaveData prior = SaveStore.Read(1, out warning); Check(prior != null && warning == null, "The real raw JSON reader accepts legacy v" + version);
            g.Load(prior); g.Start(); Check(g.save.mastery != null && g.save.mastery.Length == 3 && Array.TrueForAll(g.save.mastery, p => p == 0), "Existing load sanitation repairs absent old mastery without inventing ranks");
            Inventory(app); Strip(app, 0, 0); string migrated = JsonUtility.ToJson(g.save); Draw(app); Draw(app);
            Check(JsonUtility.ToJson(g.save) == migrated && g.save.level == 5 && g.save.skillPoints == 3 && g.save.coins == 313, "Reading migrated zero mastery preserves mature level, talent points and economy");
        }
        Console.WriteLine("PASS mastery readonly immutable snapshots, repeated actual GUI/menu pause, resources/cooldowns/projectiles, checksum and genuine v1/v2 absent-field legacy SaveStore; NATIVE_UNVERIFIED");
    }

    static void KillWithRealJ(FrontierRuntime app, bool boss)
    {
        var g = app.Game; Check(g.mode == "world", "Real combat starts in the world");
        Frames(app, 45); var foe = new Enemy(boss ? 911 : 910, 0, g.Position.x + 1, g.Position.y, 1, boss) { stun = 20, timer = 9999 };
        g.enemies.Add(foe); Mouse(app, foe.Position); Input.held.Add(KeyCode.J); Update(app); Input.held.Clear();
        Check(g.playerAction != null && g.playerAction.kind == "attack", "Actual held J starts the production basic attack");
        for (int frame = 0; frame < 80 && g.enemies.Contains(foe); frame++) Update(app);
        Check(!g.enemies.Contains(foe), "Actual sword action crosses contact and removes the " + (boss ? "boss" : "ordinary foe"));
    }
    static void CacheE(FrontierRuntime app)
    {
        var g = app.Game; WorldObject cache = Array.Find(g.Map.objects, o => o.kind.StartsWith("cache"));
        Check(cache != null, "An original authored cache exists in the test region"); bool found = false;
        foreach (Point offset in new[] { new Point(0, -.7f), new Point(0, .7f), new Point(-.7f, 0), new Point(.7f, 0) })
        {
            Point p = new Point(cache.x, cache.y) + offset; if (!g.CanStand(p)) continue;
            Check(app.SetVisualReviewPosition(p), "Cache setup uses real walkable ground"); if (g.Nearest() == cache) { found = true; break; }
        }
        Check(found, "The authored cache is the actual nearest E target"); Key(app, KeyCode.E); Check(g.mode == "dialog", "Actual E opens the cache record");
        for (int page = 0; page < 24; page++) { Draw(app); if ((bool)Call(app, "DialogueChoicesVisible")) break; Key(app, KeyCode.Return); }
        Check((bool)Call(app, "DialogueChoicesVisible"), "Actual cache record pages reach their original numbered close choice");
        Key(app, KeyCode.Alpha1); Check(g.mode == "world", "The original cache close choice returns to world");
    }
    static void RealRewardsRefreshWithoutExtraReward(FrontierRuntime app)
    {
        Prepare(app, 0, 1); var g = app.Game; g.save.flags.Add("seal_1"); Check(app.SetVisualReviewPosition(new Point(12, 14)), "Combat uses unchanged legal terrain");
        Select(app, 0, 1); Strip(app, 0, 0); Button(app, L(app, "装备此打法", "Equip style"), false); float baseDamage = g.Damage;
        int coins = g.save.coins, xp = g.save.xp, shards = g.save.shards, herb = g.save.inventory[2], core = g.save.inventory[5], stew = g.save.inventory[6], kills = g.save.kills[1];
        Key(app, KeyCode.Escape); KillWithRealJ(app, false); Check(g.save.mastery[0] == 2, "Actual ordinary defeat grants exactly the advertised +2 mastery");
        Inventory(app); Strip(app, 0, 2); Button(app, L(app, "装备此打法", "Equip style"), false); Key(app, KeyCode.Escape);
        KillWithRealJ(app, true); Check(g.save.mastery[0] == 12, "Actual boss defeat grants exactly the advertised +10 mastery");
        Inventory(app); Strip(app, 0, 12); Button(app, L(app, "装备此打法", "Equip style"), false); Key(app, KeyCode.Escape);
        CacheE(app); Check(g.save.mastery[0] == 20 && g.MasteryRank(0) == 1 && g.Damage == baseDamage + 2, "The original first-cache +8 crosses exactly 0 to 20 and grants the original +2 base damage");
        Check(g.save.coins == coins + 106 && g.save.xp == xp + 92 && g.save.shards == shards + 3 && g.save.inventory[2] == herb + 1
            && g.save.inventory[5] == core + 1 && g.save.inventory[6] == stew + 1 && g.save.kills[1] == kills + 2 && g.save.skillPoints == 3,
            "Only the original two defeat rewards and once-only cache reward are paid; mastery presentation grants no extra reward");
        Select(app, 0, 1); Strip(app, 0, 20); Button(app, L(app, "装备此打法", "Equip style"), true); Draw(app, "earned-rank-one");
        string reward = JsonUtility.ToJson(g.save); int revision = g.revision; for (int i = 0; i < 12; i++) { Draw(app); Frames(app, 1); }
        Check(JsonUtility.ToJson(g.save) == reward && g.revision == revision, "Refreshing the newly unlocked style never repeats rank, currency, XP or cache rewards");
        Click(app, L(app, "装备此打法", "Equip style")); Check(g.Style == 1, "The original button can explicitly equip the newly earned style");
        Key(app, KeyCode.Escape); int mastery = g.save.mastery[0]; coins = g.save.coins; shards = g.save.shards; stew = g.save.inventory[6]; CacheE(app);
        Check(g.save.mastery[0] == mastery && g.save.coins == coins && g.save.shards == shards && g.save.inventory[6] == stew, "Actual E rereading the first cache cannot farm mastery or supplies");
        Console.WriteLine("PASS mastery real J ordinary +2/boss +10 and authored E first-cache +8, 0-to-20 live progress/style refresh, original +2 damage and no extra/repeated rewards; NATIVE_UNVERIFIED");
    }

    static void BilingualBoundaryLayoutsAndActualFill(FrontierRuntime app)
    {
        foreach (bool english in new[] { false, true }) foreach (bool large in new[] { false, true })
        {
            Prepare(app, 2); var g = app.Game; g.save.english = english; g.save.largeText = large;
            g.save.mealKind = 1; g.save.mealUntil = g.save.totalTime + 180;
            Screen.width = large ? 960 : 1280; Screen.height = large ? 600 : 800; Call(app, "Resize");
            string suffix = (english ? "en" : "zh") + (large ? "-large960" : "-normal1280");
            foreach (int points in new[] { 0, 18, 20, 98, 100 })
            {
                int weapon = points == 18 ? 1 : 0; g.save.mastery = new[] { 60, 40, 0 }; g.save.mastery[weapon] = points;
                Select(app, weapon, 1); Draw(app, "inventory-" + points + "-" + suffix); Strip(app, weapon, points);
                Check(Label(new Rect(313, 422, 285, 21)).text == g.RoadMealStatus && g.RoadMealActive, "The active mature road-meal strip stays visible above the comparison card");
                Button(app, L(app, "装备此打法", "Equip style"), points >= 20);
                string before = JsonUtility.ToJson(g.save); Talents(app, 0); Draw(app, "talents-" + points + "-" + suffix);
                Check(JsonUtility.ToJson(g.save) == before, "Both localized boundary pages remain read-only");
            }
            g.save.flags.Add("core_1"); g.save.mastery[1] = 0; Select(app, 1, 2); Draw(app, "core-unlocked-zero-" + suffix); Strip(app, 1, 0);
            Button(app, L(app, "装备此打法", "Equip style"), true); Check(Member<bool>(Progress(g, 1), "stylesUnlocked"), "First-core ownership still unlocks styles at zero mastery");
            g.save.mastery[2] = 100; Select(app, 2, 2); Draw(app, "unowned-max-" + suffix); Strip(app, 2, 100);
            Button(app, L(app, "装备此打法", "Equip style"), false); Check(g.save.weapon == 0 && !g.Has("weapon_2"), "Localized MAX inspection never fabricates weapon ownership or equipment");
        }
        Console.WriteLine("PASS mastery zh/en normal1280 and large960 at 0/18/20/98/100, first-core zero unlock and unowned MAX, unchanged old controls and actual transformed bar-fill witnesses=" + fillWitnesses + "; NATIVE_UNVERIFIED");
    }


    static string CombatSnapshot(GameCore g)
    {
        string value = JsonUtility.ToJson(g.save) + "|" + g.revision + "|" + JsonUtility.ToJson(g.playerAction)
            + "|" + g.attackCooldown + "|" + g.skillCooldown + "|" + g.dashCooldown + "|" + g.companionCooldown;
        foreach (Projectile shot in g.projectiles) value += "|" + JsonUtility.ToJson(shot);
        return value;
    }
    static void EquipForContact(FrontierRuntime app, int weapon, int style)
    {
        Prepare(app, 0, 1); var g = app.Game;
        g.save.flags.Add("core_1"); g.save.flags.Add("weapon_2"); g.save.mastery = new[] { 20, 20, 20 };
        Check(app.SetVisualReviewPosition(new Point(12, 14)), "Contact evidence starts on legal authored terrain");
        Select(app, weapon, style); float hp = g.save.health, energy = g.save.energy;
        Click(app, L(app, "装备此打法", "Equip style"));
        Check(g.save.weapon == weapon && g.Style == style && g.save.health == hp && g.save.energy == energy,
            "Original explicit equipment action changes only the selected owned weapon/style, with no heal or energy grant");
        Draw(app); ComparedInSameDraw(app, weapon, style);
        Key(app, KeyCode.Escape); g.save.energy = 100; Mouse(app, g.Position + new Point(4, 0));
        Input.held.Add(KeyCode.J); Update(app); Input.held.Clear();
        Check(g.playerAction != null && g.playerAction.kind == "attack" && !g.playerAction.released,
            "Actual J starts the original unreleased attack preparation");
    }
    static void ActualOpeningContactsAndUnchangedCapturedAttacks(FrontierRuntime app)
    {
        int releases = 0, arrows = 0;
        for (int weapon = 0; weapon < 3; weapon++) for (int style = 0; style < 3; style++)
        {
            EquipForContact(app, weapon, style); var g = app.Game; ActorAction captured = g.playerAction;
            WeaponProfile profile = g.Profile(weapon, style); float expected = Contact(weapon, style, profile.damage);
            Check(captured.weapon == weapon && captured.style == style && captured.combo == 0
                && Math.Abs(captured.damage - profile.damage) < .0001f && Math.Abs(g.save.energy - (100 - profile.energy)) < .0001f,
                "The first real attack keeps original captured damage, style, combo zero and exact original energy debit");
            Check(Math.Abs(g.CurrentBattlePlanStats().attackDamage - expected) < .0001f,
                "The existing equipment-plan first-hit/per-arrow comparison agrees before any attack releases");
            if (weapon == 1 && style == 2)
            {
                Inventory(app); string prepared = CombatSnapshot(g);
                for (int candidate = 0; candidate < 3; candidate++) for (int stance = 0; stance < 3; stance++)
                { Select(app, candidate, stance); Draw(app); ComparedInSameDraw(app, candidate, stance); }
                Check(CombatSnapshot(g) == prepared && ReferenceEquals(captured, g.playerAction),
                    "All nine candidate inspections preserve the exact prepared action, costs, aim, impact, save and cooldowns");
                Key(app, KeyCode.Escape);
            }
            for (int frame = 0; frame < 50 && !captured.released; frame++) Update(app);
            Check(captured.released, "Original contact clock actually releases the captured basic attack"); releases++;
            if (weapon == 0) Check(g.projectiles.Count == 0, "Sword remains melee with no fabricated projectile");
            else
            {
                int count = weapon == 1 && style == 2 ? 3 : 1;
                Check(g.projectiles.Count == count, "Actual contact emits the original one shot or three Flow arrows");
                foreach (Projectile shot in g.projectiles)
                {
                    Check(shot.friendly && Math.Abs(shot.damage - expected) < .0001f,
                        "Every actual projectile carries exactly the independently expected displayed first-contact damage"); arrows++;
                }
                if (weapon == 1 && style == 2)
                {
                    Check(Math.Abs(expected / g.Profile(1, 0).damage - .52f) < .0001f,
                        "Flow's 0.8 profile times actual 0.65 release equals 52 percent per arrow, not the old advertised 80 percent");
                    Inventory(app); string flying = CombatSnapshot(g);
                    foreach (bool english in new[] { false, true }) foreach (bool large in new[] { false, true }) foreach (int width in new[] { 960, 1920 })
                    {
                        g.save.english = english; g.save.largeText = large; Screen.width = width; Screen.height = width * 5 / 8; Call(app, "Resize");
                        string unchanged = CombatSnapshot(g);
                        for (int candidate = 0; candidate < 3; candidate++) for (int stance = 0; stance < 3; stance++)
                        {
                            Select(app, candidate, stance);
                            string suffix = (english ? "en" : "zh") + (large ? "-large" : "-normal") + width + "-w" + candidate + "s" + stance;
                            Draw(app, "contacts-" + suffix); ComparedInSameDraw(app, candidate, stance);
                            Check(Label(new Rect(297, 577, 389, 59)).size == (large ? 14 : 13),
                                "Comparison retains its original rectangle and normal/large type size");
                            if (candidate == 1 && stance == 2)
                                Check(Label(new Rect(297, 642, 583, 41)).text == L(app,
                                    "三箭散射 · 伤害按每箭、耗能按整次攻击计算。",
                                    "3-arrow spread · damage per arrow, energy per attack."),
                                    "Unlocked owned Flow bow explains three separate arrows with damage per arrow and energy per complete attack");
                            else Check(Label(new Rect(297, 642, 583, 41)).text == g.Profile(candidate, stance).benefit + " · " + g.Profile(candidate, stance).cost,
                                "Other eight mature weapon/style descriptions remain unchanged");
                        }
                        Check(CombatSnapshot(g) == unchanged, "All actual localized selection and draw paths preserve every flying projectile and prepared action field");
                    }
                    // Return deliberate test display preferences before checking the
                    // exact shot/save snapshot captured before the layout matrix.
                    g.save.english = true; g.save.largeText = false;
                    Check(CombatSnapshot(g) == flying, "Layout language/scale matrix changes no combat or persisted gameplay data");
                }
            }
        }
        Check(releases == 9 && arrows == 8, "All nine real first attacks and original eight emitted projectiles were witnessed");
        Console.WriteLine("PASS comparison actual I nine owned weapon/styles, explicit equipment, original first releases and eight real projectiles, per-arrow plan agreement, prepared/flying read-only and 72 bilingual/scaled layouts; NATIVE_UNVERIFIED");
    }

    public static int Main(string[] args)
    {
        string previous = Application.persistentDataPath, directory = Path.Combine(Path.GetTempPath(), "starfall-weapon-mastery-save-" + Guid.NewGuid());
        Application.persistentDataPath = directory; captures = args.Length > 0 && args[0] != "-" ? args[0] : null;
        if (captures != null) Directory.CreateDirectory(captures); var app = new FrontierRuntime(); int failed = 0;
        try
        {
            Call(app, "Awake"); Check(app.Game != null, "Actual Awake loads the shipped full campaign");
            foreach (Action<FrontierRuntime> test in new Action<FrontierRuntime>[] { ActualInventorySelectionAndEquippedQ, ReadOnlyPauseAndActualLegacySaves,
                RealRewardsRefreshWithoutExtraReward, BilingualBoundaryLayoutsAndActualFill, ActualOpeningContactsAndUnchangedCapturedAttacks })
                try { test(app); } catch (Exception error) { failed++; Console.WriteLine("FAIL " + test.Method.Name + ": " + (error.InnerException ?? error).Message); }
            app.EndVisualReview(); Call(app, "OnDestroy");
        }
        finally { Application.persistentDataPath = previous; Input.Clear(); GUI.queuedClick = null; if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        Console.WriteLine("Runtime weapon mastery: " + (5 - failed) + "/5 groups passed; captures=" + captureCount + "; NATIVE_UNVERIFIED"); return failed == 0 ? 0 : 1;
    }
}
