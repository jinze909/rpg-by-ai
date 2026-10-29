using System.Collections.Generic;

namespace Starfall
{
    // A read-only shortage for one existing recipe, never a purchase or craft intent.
    public sealed class SupplyIngredientNeed
    {
        public readonly int item, required, held, missing;
        public readonly string action;
        internal SupplyIngredientNeed(int item, int required, int held, string action)
        { this.item = item; this.required = required; this.held = held; missing = required - held; this.action = action; }
    }

    public sealed partial class GameCore
    {
        public SupplyIngredientNeed[] SupplyIngredientNeeds()
        {
            if (save == null || save.inventory == null || save.inventory.Length != 8 || save.coins < 0
                || save.inventory[1] < 0 || save.inventory[2] < 0 || save.inventory[3] < 0
                || save.inventory[6] < 0 || save.tea < 0 || mode != "dialog" || conversation == null
                || conversation.choices == null || Dying || practicing || InAbyss)
                return new SupplyIngredientNeed[0];
            WorldObject host = Nearest();
            if (host == null || host.kind != "cook" && host.kind != "campfire") return new SupplyIngredientNeed[0];
            foreach (Choice choice in conversation.choices)
            {
                if (choice == null) continue;
                string action = choice.action; int herbs, fish, crystals;
                if (action == "cook" || action == "tea")
                {
                    if (host.kind == "campfire" && action == "cook") continue;
                    SupplyServiceQuote single = GetSupplyServiceQuote(action);
                    if (single == null || single.issue != 3) continue;
                    herbs = single.herbs; fish = single.fish; crystals = single.crystals;
                }
                else
                {
                    int recipe, quantity;
                    if (!ParseSupplyBatch(action, out recipe, out quantity)) continue;
                    SupplyBatchQuote batch = GetSupplyBatchQuote(recipe, quantity);
                    if (!batch.ingredientShortage) continue;
                    herbs = batch.herbs; fish = batch.fish; crystals = batch.crystals;
                }
                // Keep the first blocked recipe's quantities together. A later
                // recipe may need the same herbs in an incompatible quantity.
                var needs = new List<SupplyIngredientNeed>();
                if (save.inventory[2] < herbs) needs.Add(new SupplyIngredientNeed(2, herbs, save.inventory[2], action));
                if (save.inventory[3] < fish) needs.Add(new SupplyIngredientNeed(3, fish, save.inventory[3], action));
                if (save.inventory[1] < crystals) needs.Add(new SupplyIngredientNeed(1, crystals, save.inventory[1], action));
                return needs.ToArray();
            }
            return new SupplyIngredientNeed[0];
        }
    }
}
