﻿using MoreItems.Buffs;
using MoreItems.Items;
using R2API;
using R2API.Networking;
using Rewired.ComponentControls.Data;
using Rewired.Utils;
using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using static MoreItems.MoreItems;
using static RoR2.MasterSpawnSlotController;

namespace MoreItems.Equipments
{
    public class PurityAltar : Equipment
    {
        public override string Name => "Altar of Purity";

        public override string NameToken => "PURITYALTAR";

        public override string PickupToken => "Restore a random broken or depleted item.<style=cIsHealth> Sacrifices a random item per restoration.</style>";

        public override string Description => "Restore a <style=cIsUtility>single random</style> consumed item or <style=cLunarObjective>cure a single tonic affliction.</style> <style=cIsHealth>A common item is sacrificed per restoration.</style> <style=cDeath>A legendary item is sacrificed for legendary-tier items.</style>";
        public override string Lore => "<style=cLunarObjective>Peer into the abyss. Be tempted. Curiosity will take hold. Allow it.\n\nGaze into the abyss. It offers great power. To turn back time. To undue mistakes.\n\nStare into the abyss. For with each glance, I grow in power. And you grow weaker.\n\nI am coming for you. With your stolen strength.</style>";

        public override bool isLunar => true;

        public override float cooldown => 45f;

        public override Sprite Icon => MainAssets.LoadAsset<Sprite>("PurityAltar.png");

        public override GameObject Model => MainAssets.LoadAsset<GameObject>("PurityAltar.prefab");

        private List<Tuple<ItemDef, ItemDef>> validItems;

        public override bool UseEquipment(EquipmentSlot slot)
        {
            foreach (Tuple<ItemDef, ItemDef> item in validItems)
            {
                // If the player has a valid item that can be restored or cleansed...
                if (slot.characterBody.inventory.GetItemCount(item.Item1) > 0)
                {
                    // Dios Best Fried or its void counterpart: Tier 3 (Legendary) sacrifice. Otherwise, Tier 1 (Common).
                    ItemTier sacrificeTier;

                    if (item.Item1.itemIndex == ItemCatalog.FindItemIndex("ExtraLifeConsumed") ||
                        item.Item1.itemIndex == ItemCatalog.FindItemIndex("ExtraVoidLifeConsumed"))
                    {
                        sacrificeTier = ItemTier.Tier3;
                    }
                    else
                    {
                        sacrificeTier = ItemTier.Tier1;
                    }

                    // Sacrifice a random item with the chosen rarity.
                    // If this fails (No item was valid for sacrifice for the current consumed item), try the next consumed item.
                    if (!DestroyRandomItem(sacrificeTier, slot.characterBody)) { continue; }

                    // Remove the consumed item.
                    DebugLog.Log($"Removing { item.Item1.nameToken}");
                    slot.characterBody.inventory.RemoveItem(item.Item1, 1);

                    // (Don't give a restored item if a tonic affliction stack was removed as it has no item counterpart).
                    if(item.Item1.itemIndex == ItemCatalog.FindItemIndex("TonicAffliction"))
                    {
                        return true;
                    }

                    // Give the player the restored item.
                    slot.characterBody.inventory.GiveItem(item.Item2.itemIndex, 1);

                    // Broadcast to the player the item they restored, with the message being the opposite of the broken item message
                    // (Broken item icon transformed into normal item icon counterpart).
                    CharacterMasterNotificationQueue.SendTransformNotification(slot.characterBody.master,
                    item.Item1.itemIndex,
                    item.Item2.itemIndex,
                    CharacterMasterNotificationQueue.TransformationType.Default);
                    return true;
                }
            }   

            return false;
        }

        private bool DestroyRandomItem(ItemTier sacrificeTier, CharacterBody self)
        {
            // Dump all variable data to DebugLog.Log on Ror2.Content.Items.TonicAffliction.
            // Name every dump value beforehand.

            // Record the relevant void tier. that can also be sacrificed.
            var voidSacrificeTier = (sacrificeTier == ItemTier.Tier1) ? ItemTier.VoidTier1 : ItemTier.VoidTier3;

            DebugLog.Log($"Tier: {sacrificeTier}, Void Tier: {voidSacrificeTier}");

            // Return false if there are no items available to sacrifice for the chosen rarity.
            DebugLog.Log($"Number of items of same tier is {self.inventory.GetTotalItemCountOfTier(sacrificeTier)} and {self.inventory.GetTotalItemCountOfTier(voidSacrificeTier)}");

            if(self.inventory.GetTotalItemCountOfTier(sacrificeTier) <= 0 && self.inventory.GetTotalItemCountOfTier(voidSacrificeTier) <= 0)
            {
                DebugLog.Log("No valid items to sacrifice.");
                return false;
            }

            // Get all items in the player's inventory.
            List<ItemIndex> ValidItems = new List<ItemIndex>();

            foreach(ItemIndex item in self.inventory.itemAcquisitionOrder)
            {
                // Get the item's tier.
                ItemTier tier = ItemCatalog.GetItemDef(item).tier;
                if( tier == sacrificeTier || tier == voidSacrificeTier)
                {
                    ValidItems.Add(item);
                }
            }

            // Destroy a single valid item randomly.
            var deadItem = ValidItems[UnityEngine.Random.Range(0, ValidItems.Count)];
            DebugLog.Log($"Destroying {ItemCatalog.GetItemDef(deadItem).name}");
            self.inventory.RemoveItem(deadItem, 1);

            // Modifying the item conversion notification, indicate to the player what item was destroyed, with additional flavour text.
            var consumeItem = ItemList.Find(x => x.NameToken == "PURITYALTARCONSUME").itemDef;
            consumeItem.pickupToken = null;
            consumeItem.pickupToken = SetPurityAltarConsumeFlavourText();

            CharacterMasterNotificationQueue.SendTransformNotification(self.master,
            deadItem, 
            consumeItem.itemIndex,
            CharacterMasterNotificationQueue.TransformationType.Default);

            return true;
        }

        public override void SetupHooks()
        {
            On.RoR2.Run.Start += (orig, self) =>
            {
                orig(self);

                // Item 1: Consumed Item. Item 2: Item to restore (Before consumption).
                validItems = new List<Tuple<ItemDef, ItemDef>>
                {
                    new Tuple<ItemDef, ItemDef>(ItemCatalog.GetItemDef(ItemCatalog.FindItemIndex("ExtraLifeConsumed")), ItemCatalog.GetItemDef(ItemCatalog.FindItemIndex("ExtraLife"))), // Dio's Best Friend.
                    new Tuple<ItemDef, ItemDef>(ItemCatalog.GetItemDef(ItemCatalog.FindItemIndex("ExtraLifeVoidConsumed")), ItemCatalog.GetItemDef(ItemCatalog.FindItemIndex("ExtraLifeVoid"))), // Pluripotent Larva.
                    new Tuple<ItemDef, ItemDef>(ItemCatalog.GetItemDef(ItemCatalog.FindItemIndex("FragileDamageBonusConsumed")), ItemCatalog.GetItemDef(ItemCatalog.FindItemIndex("FragileDamageBonus"))), // Delicate Watch.
                    new Tuple<ItemDef, ItemDef>(ItemCatalog.GetItemDef(ItemCatalog.FindItemIndex("HealingPotionConsumed")), ItemCatalog.GetItemDef(ItemCatalog.FindItemIndex("HealingPotion"))), // Power Elixir.
                    new Tuple<ItemDef, ItemDef>(ItemCatalog.GetItemDef(ItemCatalog.FindItemIndex("TonicAffliction")), null) // Spinel Tonic. (Has no item counterpart).
                };
            };
        }

        private string SetPurityAltarConsumeFlavourText()
        {
            string[] MithrixResponses = new string[]
            {
                "<style=cLunarObjective>You only grow my power.</style>",
                "<style=cLunarObjective>I draw closer.</style>",
                "<style=cLunarObjective>My strength grows.</style>",
                "<style=cDeath>Behind you.</style>",
                "<style=cLunarObjective>You will perish.</style>",
                "<style=cLunarObjective>I am your end.</style>", // DHUUMS GAZE FALLS ON ME
                "<style=cLunarObjective>Surrender in body and spirit.</style>",
            };

            var result = MithrixResponses[UnityEngine.Random.Range(0, MithrixResponses.Length)];
            DebugLog.Log($"Returning {result}");
            return result;
        }   
    }
}