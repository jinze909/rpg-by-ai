using System;
using System.Collections.Generic;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private GameCore supplySourceGame;
        private SaveData supplySourceSave;
        private Conversation supplySourceConversation;
        private WorldObject supplySourceHost;
        private Point supplySourcePosition, supplySourceHostPosition;
        private int supplySourceGeneration, supplySourceRoom, supplySourcePage;
        private string supplySourceDialogParent;

        private bool CanReturnToSupplyDialogue
        {
            get
            {
                return supplySourceConversation != null && ReferenceEquals(Game, supplySourceGame)
                    && ReferenceEquals(Game.save, supplySourceSave) && ReferenceEquals(Game.conversation, supplySourceConversation)
                    && (Game.mode == "materials" || Game.mode == "map") && CanOpenMaterialSources
                    && Game.WorldGeneration == supplySourceGeneration && Game.save.room == supplySourceRoom
                    && (Game.Position - supplySourcePosition).Length < .001f
                    && supplySourceHost != null && (supplySourceHost.kind == "cook" || supplySourceHost.kind == "campfire")
                    && supplySourceHost.x == supplySourceHostPosition.x
                    && supplySourceHost.y == supplySourceHostPosition.y && ReferenceEquals(Game.Nearest(), supplySourceHost);
            }
        }
        private void ClearSupplySourceReturn()
        {
            supplySourceGame = null; supplySourceSave = null; supplySourceConversation = null;
            supplySourceHost = null; supplySourceDialogParent = null;
        }
        private void ValidateSupplySourceReturn()
        { if (supplySourceConversation != null && !CanReturnToSupplyDialogue) ClearSupplySourceReturn(); }

        private void OpenSupplyIngredientSources()
        {
            if (Game.mode != "dialog" || !DialogueChoicesVisible() || !CanOpenMaterialSources) return;
            SupplyIngredientNeed[] needs = Game.SupplyIngredientNeeds();
            if (needs.Length == 0) return;
            supplySourceGame = Game; supplySourceSave = Game.save; supplySourceConversation = Game.conversation;
            supplySourceGeneration = Game.WorldGeneration; supplySourceRoom = Game.save.room;
            supplySourcePosition = Game.Position; supplySourceHost = Game.Nearest();
            supplySourceHostPosition = new Point(supplySourceHost.x, supplySourceHost.y);
            supplySourcePage = dialoguePage; supplySourceDialogParent = dialogParent;
            materialItem = needs[0].item; materialPage = selectedMaterialSource = 0;
            materialGeneration = Game.WorldGeneration;
            confirmControl = pressedControl = null;
            EnterMenu("materials");
        }
        private void ReturnFromMaterialSources()
        {
            if (Game.mode != "materials") return;
            if (!CanReturnToSupplyDialogue) { OpenMenu("inventory"); return; }
            Conversation conversation = supplySourceConversation;
            int page = supplySourcePage; string parent = supplySourceDialogParent;
            ClearSupplySourceReturn(); confirmControl = pressedControl = null;
            Game.mode = "dialog"; lastConversation = conversation; dialoguePage = page; dialogParent = parent;
        }
        private void DrawSupplyIngredientShortcut()
        {
            if (Game.mode != "dialog" || !DialogueChoicesVisible() || !CanOpenMaterialSources) return;
            SupplyIngredientNeed[] needs = Game.SupplyIngredientNeeds();
            if (needs.Length == 0) return;
            string action = needs[0].action;
            bool stew = action == "cook" || action.StartsWith("supply_batch_0_", StringComparison.Ordinal);
            string quantity = action.StartsWith("supply_batch_", StringComparison.Ordinal) ? action.Substring(action.LastIndexOf('_') + 1) : "1";
            var lines = new List<string> { Game.L(stew ? "炖汤" : "凉茶", stew ? "Stew" : "Tea") + " ×" + quantity, Game.L("还需：", "Need:") };
            foreach (SupplyIngredientNeed need in needs)
                lines.Add(Game.L(Game.data.items[need.item].name, Game.data.items[need.item].en) + " ×" + need.missing);
            Text(112, 585, 174, 91, string.Join("\n", lines.ToArray()), 13, gold);
            if (Button(112, 539, 174, 37, Game.L("查找材料", "Find ingredients"))) OpenSupplyIngredientSources();
        }
    }
}
