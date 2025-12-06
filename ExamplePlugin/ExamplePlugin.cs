using BepInEx;
using R2API;
using RoR2;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

using System.Linq;

namespace ExamplePlugin
{
    [BepInDependency(R2API.R2API.PluginGUID)]

    //This attribute is required, and lists metadata for your plugin.
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]

    public class ExamplePlugin : BaseUnityPlugin
    {
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "ravenr";
        public const string PluginName = "challengeoforder";
        public const string PluginVersion = "1.0.0";


        //The Awake() method is run at the very start when the game is initialized.
        public void Awake()
        {
            //Init our logging class so that we can properly log for debugging
            Log.Init(Logger);
            Log.Info("Challenge of Order: Awake() called, setting up hooks.");


            On.RoR2.Inventory.GiveItem_ItemIndex_int += Inventory_GiveItem_ItemIndex_int;

            // This line of log will appear in the bepinex console when the Awake method is done.
            Log.Info(nameof(Awake) + " done.");
        }

        public class PerTierTracker : MonoBehaviour
        {
            private readonly Dictionary<ItemTier, ItemIndex> chosenByTier = new Dictionary<ItemTier, ItemIndex>();

            public bool TryGetChosenItem(ItemTier tier, out ItemIndex itemIndex)
            {
                return chosenByTier.TryGetValue(tier, out itemIndex);
            }

            public void SetChosenItem(ItemTier tier, ItemIndex itemIndex)
            {
                chosenByTier[tier] = itemIndex;
            }
        }

        private void Inventory_GiveItem_ItemIndex_int(
            On.RoR2.Inventory.orig_GiveItem_ItemIndex_int orig,
            Inventory self,
            ItemIndex itemIndex,
            int count)
        {
            Log.Info($"OnlyOneItemPerTier: GiveItem called – item={itemIndex}, count={count}, inv={self?.name}");

            if(self == null || count <= 0 || itemIndex == ItemIndex.None)
            {
                orig(self, itemIndex, count);
                return;
            }

            ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
            if (itemDef == null)
            {
                orig(self, itemIndex, count);
                return;
            }

            ItemTier tier = itemDef.tier;

            if(!IsTierHandled(tier))
            {
                orig(self, itemIndex, count);
                return;
            }

            var tracker = self.GetComponent<PerTierTracker>();
            if (tracker == null)
            {
                tracker = self.gameObject.AddComponent<PerTierTracker>();
            }

            ItemIndex chosenItemIndex;

            if (!tracker.TryGetChosenItem(tier, out chosenItemIndex))
            {
                chosenItemIndex = GetRandomItemOfTier(tier);
                if (chosenItemIndex == ItemIndex.None)
                {
                    // If something is weird and we can't find a candidate, just fall back to original
                    orig(self, itemIndex, count);
                    return;
                }

                tracker.SetChosenItem(tier, chosenItemIndex);
            }

            orig(self, chosenItemIndex, count);
        }

        private bool IsTierHandled(ItemTier tier)
        {
            // Basic example: only common / uncommon / legendary
            return tier == ItemTier.Tier1
                || tier == ItemTier.Tier2
                || tier == ItemTier.Tier3
                || tier == ItemTier.Lunar
                || tier == ItemTier.Boss
                || tier == ItemTier.VoidTier1
                || tier == ItemTier.VoidTier2
                || tier == ItemTier.VoidTier3
                || tier == ItemTier.VoidBoss;
        }

        private ItemIndex GetRandomItemOfTier(ItemTier tier)
        {
            var run = Run.instance;
            if (run == null)
            {
                return ItemIndex.None;
            }

            // Use availableItems so we don't pick locked / disabled content
            var available = run.availableItems;

            // Collect all candidate items of the requested tier that are available
            List<ItemIndex> candidates = new List<ItemIndex>();

            foreach (ItemIndex idx in ItemCatalog.allItems)
            {
                if (!available.Contains(idx))
                    continue;

                ItemDef def = ItemCatalog.GetItemDef(idx);
                if (def != null && def.tier == tier)
                {
                    candidates.Add(idx);
                }
            }

            if (candidates.Count == 0)
            {
                return ItemIndex.None;
            }

            // Use the run's treasureRng for consistency with the game's randomness
            int choice = run.treasureRng.RangeInt(0, candidates.Count);
            return candidates[choice];
        }
    }
}
