using System.Collections.Generic;

namespace Starfall
{
    // The guild responds to earned chapters and records the player actually found.
    // These conversations never replace a delivery, gate or ending condition.
    public sealed partial class GameCore
    {
        public bool GuildEvidenceFound(int index)
        { return index >= 0 && index < 3 && (Has("lore_" + (index + 1)) || Has("history_" + index)); }
        public int GuildEvidenceCount
        { get { int count = 0; for (int i = 0; i < 3; i++) if (GuildEvidenceFound(i)) count++; return count; } }
        public bool GuildEvidenceReviewed { get { return Has("guild_evidence_reviewed"); } }
        public bool GuildLetterReviewed { get { return Has("guild_letter_reviewed"); } }
        public bool CanReviewGuildEvidence { get { return save.stage >= 3 && GuildEvidenceCount == 3; } }

        private bool NearGuildStory()
        {
            if (mode != "world" || Dying || practicing || save.room != 0) return false;
            foreach (WorldObject o in Map.objects)
                if (o.kind == "guild" && IsObjectPresent(o) && (Position - new Point(o.x, o.y)).Length < 1.75f) return true;
            return false;
        }
        private Choice[] GuildStoryChoices(params Choice[] original)
        {
            var choices = new List<Choice>(original);
            choices.Add(C("共读调查记录", "Review the investigation", "guild_evidence"));
            if (save.stage >= 3) choices.Add(C("巡路队的回信", "The patrol's reply", "guild_letter"));
            return choices.ToArray();
        }
        private void TalkGuildStory(WorldObject o)
        {
            if (save.ending > 0)
            {
                Dialog(o.name, o.en, "灾变过去了，冒险还没有结束。旅店有热汤，密林里仍有星晶，大家都在等你。", "The crisis is over. The adventure is not. There is warm stew at the inn, starcrystal in the woods, and friends waiting here.", GuildStoryChoices(C("开始新的远征", "Another expedition", "routes"), C("回顾结局", "Revisit the ending", "epilogue"), C("再见", "Goodbye", "close"))); return;
            }
            int next = save.stage + 1;
            if (save.stage < 3 && Has("core_" + next))
            {
                string[] zh = {
                    "你真的做到了。统领说这些星核会重新稳住边境……可最近镇里的作物越来越难生长。\n\n也许，下一片遗迹会告诉我们原因。",
                    "你从沉没遗迹带回了第二枚星核。城墙上的光更亮了，井里的水却仍在减少。\n\n下一站是赤铁矿场。找回第三枚星核，也看看这些力量是如何送往堡垒的。",
                    "第三枚星核回来了。把它交回后，通向星陨堡垒的道路就会开启。\n\n城墙变强了，城外的人却没有好起来。带着你找到的记录去问厄洛：屏障的代价要谁付出？" };
                string[] en = {
                    "You did it. The warden says these cores will secure the frontier. Yet our crops keep withering.\n\nPerhaps the next ruins will tell us why.",
                    "You brought the second core from Drowned Ruins. The walls shine brighter, but our well keeps falling.\n\nIronreach is next. Recover the third core and learn how this power reaches the citadel.",
                    "The third core is here. Delivering it opens the road to Starfall Citadel.\n\nThe walls grow stronger, yet the people outside do not recover. Take the records you found to Orrin: who bears the shield's cost?" };
                Dialog(o.name, o.en, zh[save.stage], en[save.stage], GuildStoryChoices(C("交回星核 · 开启下一章", "Deliver the starcore · next chapter", "deliver"), C("稍后再来", "Later", "close"))); return;
            }
            string[] waitingZh = {
                "先去北方的荆棘密林，救出侦察员，挑战巨兽。收取星核后交回公会。按 Tab 查看主线和支线。",
                "第一枚星核已经交回。下一站是沉没遗迹：读清碑文，启动三座石碑，找回第二枚星核。\n\n森林的根为什么会反抗采矿？遗迹里的记录也许能说明星核原本的作用。发现的记录可带回来共读。",
                "第二枚星核已经交回。前往赤铁矿场，修好升降机，挑战守护者，找回第三枚星核。\n\n我们还需要知道：矿场如何把力量送往堡垒，谁又为城墙付出代价。发现的记录可带回来共读。",
                "三枚星核汇聚后，堡垒的屏障越来越强，边境却越来越虚弱。去见厄洛，问问他究竟在保护什么。\n\n如果你帮助过药师、渔夫和铁匠，他们也许会带来另一种办法。" };
            string[] waitingEn = {
                "Go north to Thornwood. Rescue the scout, defeat the beast, and return its core. Tab tracks the main quest and your side commissions.",
                "The first core is delivered. Drowned Ruins is next: read the tablet, awaken its three stones and recover the second core.\n\nWhy did the forest roots resist mining? The ruins may explain what a core was meant to do. Bring any records back to discuss them.",
                "The second core is delivered. Go to Ironreach, repair the elevator, challenge its guardian and recover the third core.\n\nWe still need to learn how the mine powers the citadel, and who pays for the walls. Bring any records back to discuss them.",
                "The shield grows stronger as the frontier weakens. Ask Orrin what he is protecting.\n\nIf you helped An, Old Tide and Noll, they may bring another solution." };
            Dialog(o.name, o.en, waitingZh[save.stage], waitingEn[save.stage], GuildStoryChoices(C("查看冒险路线", "Choose a route", "routes"), C("我出发了", "On my way", "close")));
        }
        public void ShowGuildEvidence()
        {
            if (!NearGuildStory()) return;
            string zh = "莉雅：我们只共读你实际找回的记录。还没有找到的部分，不能当成证据。\n";
            string en = "Lyra: let us work from the records you actually recovered. A missing record is not evidence.\n";
            for (int i = 0; i < 3; i++)
            {
                LoreData note = data.lore[i];
                zh += "\n" + note.title + " · " + (GuildEvidenceFound(i) ? "已找到\n" + note.zh : "尚未找到 · " + data.maps[i + 1].name) + "\n";
                en += "\n" + note.en + " · " + (GuildEvidenceFound(i) ? "Recovered\n" + note.text : "Not found · " + data.maps[i + 1].en) + "\n";
            }
            if (GuildEvidenceCount == 3)
            {
                zh += "\n三份记录连起来了：星核维系土地，采矿伤害根系，屏障抽走城外的生机。我们要守住的人，也在城墙之外。";
                en += "\nTogether they explain the cost: cores sustain the land, mining wounds the roots, and the shield drains life outside the walls. The people we protect live outside those walls too.";
            }
            else
            {
                zh += "\n读清记录再作选择。找回星核、交回公会的主线仍可继续。";
                en += "\nRead the missing records before drawing a conclusion. Recovering and delivering the cores can continue independently.";
            }
            var choices = new List<Choice>();
            if (CanReviewGuildEvidence) choices.Add(C(GuildEvidenceReviewed ? "重读公会回话" : "记下公会回话", GuildEvidenceReviewed ? "Reread the guild's response" : "Record the guild's response", "guild_evidence_record"));
            choices.Add(C("继续冒险", "Keep adventuring", "close"));
            Dialog("莉雅 · 调查记录", "Lyra · The evidence trail", zh, en, choices.ToArray());
        }
        public void ShowGuildLetter()
        {
            if (!NearGuildStory() || save.stage < 3) return;
            string zh = "莉雅：你带来的旧信，我收好了。巡路队没有回来，不等于他们留下的解释也该消失。\n\n公会曾把采矿写成保护边境的办法。我们不能只看城墙亮了多少，也要问城外的人还能不能生活。去见厄洛时，带上你自己的选择。";
            string en = "Lyra: I kept the old letter you brought. The patrol never returned, but their explanation should not disappear with them.\n\nThe guild called mining a way to protect the frontier. We must count more than brighter walls: can the people outside still live here? Take your own judgment to Orrin.";
            if (Has("history_10"))
            {
                zh += "\n\n树上的那份路标也是写给我的。他们说，不要把所有野兽都写成敌人。我会把这句话留在公会的记录里。";
                en += "\n\nThe note in the bark was addressed to me too. They asked us not to put every beast in the enemy column. I will keep that request in the guild record.";
            }
            if (Has("history_16"))
            {
                zh += "\n\n你看过旅店的赊账簿。阿茴留着的座位，是给回来的人，也是给还在等的人。名单可以慢慢补，先把人带回家。";
                en += "\n\nYou read the inn ledger. Ari keeps those places for people returning and for those still waiting. We can finish the list later. Bring people home first.";
            }
            if (Has("scout_saved"))
            {
                zh += "\n\n你已经让侦察员回家了。这封回信就从他的名字开始。";
                en += "\n\nYou brought the scout home. Start this reply with his name.";
            }
            if (save.ending > 0)
            {
                zh += "\n\n灾变过后，这份回信仍要留着。新的巡路队出发时，要记得回来的人和等他们的人。";
                en += "\n\nKeep the reply after the crisis. The next patrol must remember both those who return and those waiting for them.";
            }
            if(HomewardComplete)
            {
                zh+="\n\n米拉已经把新路图带回营地。那不是失踪者回来的答案，是活着的人下一次出发的路线。";
                en+="\n\nMira brought the new chart to camp. It does not answer the missing names; it gives the living a route for their next journey.";
            }
            Dialog("莉雅 · 巡路队的回信", "Lyra · The patrol's reply", zh, en, C(GuildLetterReviewed ? "收起回信" : "记下回信", GuildLetterReviewed ? "Close the reply" : "Keep the reply", "guild_letter_record"), C("稍后再读", "Read later", "close"));
        }
        public bool ChooseGuildStory(string action)
        {
            if (action == "guild_evidence") { ShowGuildEvidence(); return true; }
            if (action == "guild_letter") { ShowGuildLetter(); return true; }
            if (action == "guild_evidence_record")
            {
                if (!NearGuildStory() || !CanReviewGuildEvidence) return true;
                if (!GuildEvidenceReviewed)
                {
                    Flag("guild_evidence_reviewed");
                    RecordMoment("公会调查：三份记录说明了屏障的代价。保护城墙，也要保护城外的人。", "Guild investigation: three records explain the shield's cost. Protect the people outside the walls too.");
                }
                Dialog("莉雅 · 公会回话", "Lyra · The guild's response", "我会保留三份记录，也保留巡路队的解释。你可以继续挑战厄洛，也可以寻找居民和远岸的办法。决定仍由你来作。", "I will keep all three records, including the patrol's explanation. Challenge Orrin, or seek another answer with the residents and the distant shores. The decision remains yours.", C("带着记录继续", "Carry the records onward", "close")); return true;
            }
            if (action == "guild_letter_record")
            {
                if (!NearGuildStory() || save.stage < 3) return true;
                if (!GuildLetterReviewed)
                {
                    Flag("guild_letter_reviewed");
                    RecordMoment("收到莉雅给巡路队的回信：名单可以慢慢补，先把人带回家。", "Lyra's reply to the patrol: finish the list later; bring people home first.");
                }
                return true;
            }
            return false;
        }
        private void OnGuildDelivery(int chapter)
        {
            string flag = "guild_delivery_" + chapter;
            if (!Has(flag))
            {
                Flag(flag);
                string[] zh = { "", "第一枚星核已交回：下一站沉没遗迹，调查星核原本的作用。", "第二枚星核已交回：下一站赤铁矿场，追查力量如何送往堡垒。", "第三枚星核已交回：道路通向星陨堡垒，带着记录去问屏障的代价。" };
                string[] en = { "", "First core delivered: Drowned Ruins is next. Learn what cores were meant to do.", "Second core delivered: Ironreach is next. Trace the power sent to the citadel.", "Third core delivered: the road to Starfall Citadel opens. Ask who pays for the shield." };
                RecordMoment(zh[chapter], en[chapter]);
            }
            Notify("新章节开启！获得硬币、材料与一份炖汤。下一站：" + data.maps[chapter + 1].name + "。", "New chapter! Coins, materials and a warm stew received. Next: " + data.maps[chapter + 1].en + ".");
            sound = "reward";
        }
    }
}
