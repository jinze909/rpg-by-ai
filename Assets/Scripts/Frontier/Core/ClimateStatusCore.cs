namespace Starfall
{
    // Current exposure, not a forecast: moving or expiring tea changes the next reading.
    // This derived snapshot never adds state to a save or advances the climate clock.
    public sealed class ClimateReading
    {
        public readonly bool cold, slowed, harmful;
        public readonly float amount, rate;
        public readonly string protection;
        internal ClimateReading(bool cold, float amount, float rate, string protection)
        {
            this.cold = cold; this.amount = amount; this.rate = rate; this.protection = protection;
            slowed = cold && amount > 70; harmful = amount >= (cold ? 90 : 85);
        }
    }

    public sealed partial class GameCore
    {
        private string HeatProtection
        { get { return save.teaUntil > save.totalTime ? "tea" : NearKind("shade", 3) ? "shade" : racing ? "race" : "none"; } }
        private bool NearLitBrazier
        {
            get
            {
                foreach (WorldObject o in Map.objects)
                    if (o.kind == "brazier" && Has("warm_" + o.id) && (new Point(o.x, o.y) - Position).Length < 3.5f) return true;
                return false;
            }
        }
        private string ColdProtection
        { get { return save.teaUntil > save.totalTime ? "tea" : NearLitBrazier ? "brazier" : ShelterColdProtected ? "shelter" : "none"; } }
        private float HeatChangeRate
        { get { return save.room == 6 && HeatProtection == "none" ? 1.3f : -10; } }
        private float ColdChangeRate
        { get { return save.room != 7 ? -8 : ColdRate(ColdProtection); } }
        private static float ColdRate(string protection)
        { return protection == "none" ? 1.1f : protection == "shelter" ? 0 : -8; }
        public ClimateReading GetClimateReading()
        {
            if (save.room == 6)
            {
                string protection = HeatProtection;
                return new ClimateReading(false, save.heat, protection == "none" ? 1.3f : -10, protection);
            }
            if (save.room == 7)
            {
                string protection = ColdProtection;
                return new ClimateReading(true, save.cold, ColdRate(protection), protection);
            }
            return null;
        }
        public bool ClimateWarning
        { get { return save.room == 6 && save.heat >= 85 || save.room == 7 && save.cold > 70; } }
        private string ClimateConditions()
        {
            ClimateReading reading = GetClimateReading();
            if (reading == null) return "";
            string source;
            switch (reading.protection)
            {
                case "tea": source = reading.cold ? L("凉茶抗寒", "Tea warming") : L("凉茶降温", "Tea cooling"); break;
                case "shade": source = L("绿洲降温", "Shade cooling"); break;
                case "race": source = L("竞速降温", "Race cooling"); break;
                case "brazier": source = L("火盆取暖", "Brazier warming"); break;
                case "shelter": source = L("避风保持", "Shelter: held"); break;
                default: source = L("持续累积", "Rising"); break;
            }
            string threshold = reading.cold
                ? reading.harmful ? L("寒冷危险 · 靠近火盆或喝茶", "Cold danger · find fire or tea")
                    : reading.slowed ? L("移速与回复降低 · 90起受伤", "Slower / regen down · harm at 90")
                    : L("超过70减速 · 90起受伤", "Over 70: slower · 90: harm")
                : reading.harmful ? L("暑热危险 · 进入绿洲或喝茶", "Heat danger · find shade or tea")
                    : L("85起受伤 · 绿洲或凉茶降温", "Harm at 85 · shade or tea cools");
            return (reading.cold ? L("寒意 ", "COLD ") : L("热度 ", "HEAT ")) + (int)reading.amount + "/100 · " + source + "\n" + threshold;
        }
    }
}
