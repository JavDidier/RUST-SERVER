using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using UnityEngine;
using System.Collections;
using Facepunch;

namespace Oxide.Plugins
{
    [Info("Stack Modifier", "Mabel", "2.0.9")]
    [Description("Modify item stack sizes")]
    public class StackModifier : RustPlugin
    {
        #region Fields
        private static Dictionary<string, int> _defaults = null;
        private static Dictionary<string, int> _FB = new Dictionary<string, int>();

        private readonly HashSet<string> _exclude = new HashSet<string>
        {
            "water",
            "water.radioactive",
            "water.salt",
            "ammo.snowballgun",
            "motorbike",
            "motorbike_sidecar",
            "bicycle",
            "trike",
            "rowboat",
            "rhib",
            "parachute.deployed",
            "minigunammopack",
            "minihelicopter.repair",
            "scraptransportheli.repair",
            "habrepair",
            "submarinesolo",
            "submarineduo",
            "workcart",
            "mlrs",
            "snowmobile",
            "snowmobiletomaha",
            "wagon",
            "locomotive",
            "attackhelicopter",
            "tugboat",
            "vehicle.chassis.2mod",
            "vehicle.chassis.3mod",
            "vehicle.chassis.4mod",
            "vehicle.chassis",
            "vehicle.module",
            "weaponrack.light",
            "weaponrack.doublelight",
            "batteringram",
            "batteringram.head.repair",
            "ballista.static",
            "ballista.mounted",
            "catapult",
            "siegetower"
        };

        private readonly Dictionary<string, string> _corrections = new Dictionary<string, string>
        {
            {"sunglasses02black", "Sunglasses Style 2"},
            {"sunglasses02camo", "Sunglasses Camo"},
            {"sunglasses02red", "Sunglasses Red"},
            {"sunglasses03black", "Sunglasses Style 3"},
            {"sunglasses03chrome", "Sunglasses Chrome"},
            {"sunglasses03gold", "Sunglasses Gold"},
            {"twitchsunglasses", "Sunglasses Purple"},
            {"hazmatsuit_scientist_peacekeeper", "Peacekeeper Scientist Suit"},
            {"skullspikes.candles", "Skull Spikes Candles"},
            {"skullspikes.pumpkin", "Skull Spikes Pumpkin"},
            {"skull.trophy.jar", "Skull Trophy Jar"},
            {"skull.trophy.jar2", "Skull Trophy Jar 2"},
            {"skull.trophy.table", "Skull Trophy Table"},
            {"innertube.horse", "Inner Tube Horse"},
            {"innertube.unicorn", "Inner Tube Unicorn"},
            {"sled.xmas", "Xmas Sled"},
            {"discofloor.largetiles", "Disco Floor Large"},
        };
        #endregion

        #region Config
        private PluginConfig _config;
        private readonly Dictionary<string, string> _itemMap = new Dictionary<string, string>();

        private IEnumerator CheckConfig()
        {
            Puts("Checking Configuration Settings");
            yield return CoroutineEx.waitForSeconds(0.30f);

            foreach (ItemDefinition item in ItemManager.itemList)
            {
                if (_exclude.Contains(item.shortname))
                    continue;

                string categoryName = item.category.ToString();
                Dictionary<string, _Items> stackCategory;

                if (!_config.StackCategoryMultipliers.ContainsKey(categoryName))
                    _config.StackCategoryMultipliers[categoryName] = 0;

                if (!_config.StackCategories.TryGetValue(categoryName, out stackCategory))
                    _config.StackCategories[categoryName] = stackCategory = new Dictionary<string, _Items>();

                if (stackCategory.ContainsKey(item.shortname))
                    stackCategory[item.shortname].ItemId = item.itemid;

                if (!stackCategory.ContainsKey(item.shortname))
                {
                    stackCategory.Add(item.shortname, new _Items
                    {
                        ShortName = item.shortname,
                        ItemId = item.itemid,
                        DisplayName = item.displayName.english,
                        Modified = item.stackable,
                    });
                }

                if (_corrections.ContainsKey(item.shortname))
                    _config.StackCategories[categoryName][item.shortname].DisplayName = _corrections[item.shortname];

                if (stackCategory.ContainsKey(item.shortname))
                    _config.StackCategories[categoryName][item.shortname].ShortName = item.shortname;

                if (_config.StackCategories[categoryName][item.shortname].Disable)
                {
                    item.stackable = 1;
                }
                else if (_config.StackCategoryMultipliers[categoryName] > 0 &&
                         _config.StackCategories[categoryName][item.shortname].Modified == _defaults[item.shortname])
                {
                    item.stackable *= _config.StackCategoryMultipliers[categoryName];
                }
                else if (_config.StackCategories[categoryName][item.shortname].Modified > 0 &&
                         _config.StackCategories[categoryName][item.shortname].Modified != _defaults[item.shortname])
                {
                    item.stackable = _config.StackCategories[categoryName][item.shortname].Modified;
                }

                if (item.stackable == 0)
                {
                    if (_config.StackCategories[categoryName][item.shortname].Modified <= 0)
                        _config.StackCategories[categoryName][item.shortname].Modified = _defaults[item.shortname];

                    item.stackable = _defaults[item.shortname];
                    PrintError($"Error {item.shortname} server > {item.stackable} config > {_config.StackCategories[categoryName][item.shortname].Modified} \nStack size is set to ZERO this will break the item! Resetting to default!");
                }
            }

            SaveConfig();

            Puts("Successfully updated all server stack sizes.");

            Updating = null;
            yield return null;
        }

        internal class PluginConfig : SerializableConfiguration
        {
            [JsonProperty("Disable Ammo/Fuel duplication fix (Recommended false)")]
            public bool DisableFix;

            [JsonProperty("Enable VendingMachine Ammo Fix (Recommended)")]
            public bool VendingMachineAmmoFix = true;

            [JsonProperty("Category Stack Multipliers", Order = 4)]
            public Dictionary<string, int> StackCategoryMultipliers = new Dictionary<string, int>();

            [JsonProperty("Stack Categories", Order = 5)]
            public Dictionary<string, Dictionary<string, _Items>> StackCategories = new Dictionary<string, Dictionary<string, _Items>>();

            public void ResetCategory(string cat)
            {
                if (cat == "All")
                {
                    foreach (var cats in StackCategories.Values)
                    {
                        foreach (var i in cats)
                            i.Value.Modified = _defaults[i.Value.ShortName];
                    }

                    foreach (var value in StackCategories.Keys)
                        StackCategoryMultipliers[value] = 0;
                }
                else
                {
                    if (!StackCategoryMultipliers.ContainsKey(cat)) return;
                    StackCategoryMultipliers[cat] = 0;

                    foreach (var item in StackCategories[cat].Values)
                        item.Modified = _defaults[item.ShortName];
                }
            }

            public void SetCategory(string cat, int digit)
            {
                if (cat == "All")
                {
                    foreach (var value in StackCategories.Keys)
                        StackCategoryMultipliers[value] = digit;
                }
                else
                {
                    if (!StackCategoryMultipliers.ContainsKey(cat)) return;
                    StackCategoryMultipliers[cat] = digit;
                }
            }

            public void SetItems(string cat, int digit)
            {
                if (digit == 0)
                    digit = 1;

                if (cat == "All")
                {
                    foreach (var cats in StackCategories.Values)
                    {
                        foreach (var i in cats)
                            i.Value.Modified = digit;
                    }
                }
                else
                {
                    if (!StackCategoryMultipliers.ContainsKey(cat)) return;
                    foreach (var item in StackCategories[cat].Values)
                        item.Modified = digit;
                }
            }

            public void ToggleCats(string cat, bool toggle)
            {
                if (cat == "All")
                {
                    foreach (var cats in StackCategories.Values)
                    {
                        foreach (var i in cats)
                            i.Value.Disable = toggle;
                    }
                }
                else
                {
                    if (!StackCategoryMultipliers.ContainsKey(cat)) return;
                    foreach (var item in StackCategories[cat].Values)
                        item.Disable = toggle;
                }
            }
        }

        public class _Items
        {
            public string ShortName;
            public int ItemId;
            public string DisplayName;
            public int Modified;
            public bool Disable;
        }
        #region Updater

        internal class SerializableConfiguration
        {
            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
        }

        private static class JsonHelper
        {
            public static object Deserialize(string json) => ToObject(JToken.Parse(json));

            private static object ToObject(JToken token)
            {
                switch (token.Type)
                {
                    case JTokenType.Object:
                        return token.Children<JProperty>()
                            .ToDictionary(prop => prop.Name, prop => ToObject(prop.Value));
                    case JTokenType.Array:
                        return token.Select(ToObject).ToList();

                    default:
                        return ((JValue)token).Value;
                }
            }
        }

        private bool MaybeUpdateConfig(SerializableConfiguration config)
        {
            var currentWithDefaults = config.ToDictionary();
            var currentRaw = Config.ToDictionary(x => x.Key, x => x.Value);
            return MaybeUpdateConfigDict(currentWithDefaults, currentRaw);
        }

        private bool MaybeUpdateConfigDict(Dictionary<string, object> currentWithDefaults, Dictionary<string, object> currentRaw)
        {
            bool changed = false;

            foreach (var key in currentWithDefaults.Keys)
            {
                object currentRawValue;
                if (currentRaw.TryGetValue(key, out currentRawValue))
                {
                    var defaultDictValue = currentWithDefaults[key] as Dictionary<string, object>;
                    var currentDictValue = currentRawValue as Dictionary<string, object>;

                    if (defaultDictValue != null)
                    {
                        if (currentDictValue == null)
                        {
                            currentRaw[key] = currentWithDefaults[key];
                            changed = true;
                        }
                        else if (MaybeUpdateConfigDict(defaultDictValue, currentDictValue))
                            changed = true;
                    }
                }
                else
                {
                    currentRaw[key] = currentWithDefaults[key];
                    changed = true;
                }
            }

            return changed;
        }
        #endregion
        #endregion

        #region Oxide
        protected override void LoadDefaultConfig() => _config = new PluginConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<PluginConfig>();

                if (_config == null)
                {
                    PrintWarning($"No configuration file found or configuration is empty for {Name}. Generating default configuration.");
                    LoadDefaultConfig();
                    SaveConfig();
                }
                else
                {
                    if (MaybeUpdateConfig(_config))
                    {
                        PrintWarning("Configuration appears to be outdated; updating and saving.");
                        SaveConfig();
                    }
                }
            }
            catch (Exception ex)
            {
                PrintWarning($"Failed to load config file (is the config file corrupt?): {ex.Message}. Loading default configurations.");
                LoadDefaultConfig();
                SaveConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);

        private Coroutine Updating = null;

        private void Unload()
        {
            if (Updating != null)
            {
                ServerMgr.Instance.StopCoroutine(Updating);
            }

            RestoreVanillaStackSizes();
            _defaults = null;
        }

        private void OnServerShutdown()
        {
            SaveConfig();

            _defaults = null;
        }

        private void Init()
        {
            Unsubscribe(nameof(OnItemAddedToContainer));
        }

        private void InitializeFB()
        {
            _FB.Clear();

            foreach (ItemDefinition itemDefinition in ItemManager.GetItemDefinitions())
            {
                _FB[itemDefinition.shortname] = itemDefinition.stackable;
            }
        }

        private void OnServerInitialized()
        {
            LoadDefaultStackSizes();
            InitializeFB();
            PrintWarning($"Defaults initialized with {_defaults.Count} items.");

            bool updated = false;
            foreach (ItemDefinition item in ItemManager.itemList)
            {
                string categoryName = item.category.ToString();
                if (!_config.StackCategoryMultipliers.ContainsKey(categoryName) || _config.StackCategoryMultipliers[categoryName] < 1)
                {
                    _config.StackCategoryMultipliers[categoryName] = 1;
                    updated = true;
                }
            }

            if (updated)
            {
                PrintWarning("One or more Category Multipliers were below minimum of 1 and have been updated.");
                SaveConfig();
            }

            int count = 0;
            foreach (var cat in _config.StackCategories)
            {
                foreach (var item in cat.Value.ToArray())
                {
                    if (!_defaults.ContainsKey(item.Key))
                    {
                        count++;
                        cat.Value.Remove(item.Key);
                    }
                }
            }

            if (count > 0)
            {
                Puts($"Updated {count} outdated configuration options continuing to phase 2");
                SaveConfig();
            }

            Updating = ServerMgr.Instance.StartCoroutine(CheckConfig());
            Subscribe(nameof(OnItemAddedToContainer));

            SaveDefaultStackSizes();
        }

        private void SaveDefaultStackSizes()
        {
            if (_FB == null)
            {
                _FB = new Dictionary<string, int>();
            }

            foreach (ItemDefinition itemDefinition in ItemManager.GetItemDefinitions())
            {
                if (_FB.ContainsKey(itemDefinition.shortname)) continue;

                _FB[itemDefinition.shortname] = itemDefinition.stackable;
            }

            Interface.Oxide.DataFileSystem.WriteObject("Stackmodifier_Defaults", _FB);
            Puts("Default stack sizes saved.");
        }

        void LoadDefaultStackSizes()
        {
            _FB = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, int>>("Stackmodifier_Defaults");
            if (_FB == null)
            {
                _FB = new Dictionary<string, int>();
                Puts("No default stack sizes found. Creating a new dictionary.");
            }
            else
            {
                Puts("Default stack sizes loaded.");
            }
            _defaults = _FB;
        }

        private void RestoreVanillaStackSizes()
        {
            if (_defaults == null || !_defaults.Any())
            {
                PrintWarning("No default stack sizes to restore.");
                return;
            }

            foreach (ItemDefinition itemDefinition in ItemManager.GetItemDefinitions())
            {
                if (!_defaults.ContainsKey(itemDefinition.shortname)) continue;
                itemDefinition.stackable = _defaults[itemDefinition.shortname];
            }
            Puts("Vanilla stack sizes restored.");
        }

        object CanStackItem(Item item, Item targetItem)
        {
            if (item == null || targetItem == null) return null;

            if (item.name != targetItem.name || item.info.shortname != targetItem.info.shortname) return false;

            var itemOwner = item.GetOwnerPlayer();
            var targetItemOwner = targetItem.GetOwnerPlayer();
            if (itemOwner == null || targetItemOwner == null) return null;

            if (item.info.itemid == targetItem.info.itemid && !CanWaterItemsStack(item, targetItem)) return false;

            if (item.contents?.capacity != targetItem.contents?.capacity || item.contents?.itemList.Count != targetItem.contents?.itemList.Count) return false;

            if (!(targetItem != item &&
                  item.info.stackable > 1 &&
                  targetItem.info.stackable > 1 &&
                  targetItem.info.itemid == item.info.itemid &&
                  (!item.hasCondition || (double)item.condition == targetItem.info.condition.max) &&
                  (!targetItem.hasCondition || (double)targetItem.condition == targetItem.info.condition.max) &&
                  item.IsValid() &&
                  (!item.IsBlueprint() || item.blueprintTarget == targetItem.blueprintTarget) &&
                  targetItem.skin == item.skin &&
                  targetItem.name == item.name &&
                  targetItem.info.shortname == item.info.shortname &&
                  targetItem.streamerName == item.streamerName &&
                  (targetItem.info.amountType != ItemDefinition.AmountType.Genetics && item.info.amountType != ItemDefinition.AmountType.Genetics || (targetItem.instanceData != null ? targetItem.instanceData.dataInt : -1) == (item.instanceData != null ? item.instanceData.dataInt : -1)) &&
                  (item.instanceData == null || item.instanceData.subEntity == null || !(bool)item.info.GetComponent<ItemModSign>()) &&
                  (targetItem.instanceData == null || targetItem.instanceData.subEntity == null || !(bool)targetItem.info.GetComponent<ItemModSign>())))
                return false;

            if ((item.contents?.capacity ?? 0) != (targetItem.contents?.capacity ?? 0)) return false;

            if (targetItem.contents?.itemList.Count > 0)
            {
                if (!HasVanillaContainer(targetItem.info)) return false;

                for (var i = targetItem.contents.itemList.Count - 1; i >= 0; i--)
                {
                    var childItem = targetItem.contents.itemList[i];
                    item.parent.playerOwner.GiveItem(childItem);
                }
            }

            BaseProjectile.Magazine itemMag = targetItem.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine;
            if (itemMag != null)
            {
                if (itemMag.contents > 0)
                {
                    item.GetOwnerPlayer().GiveItem(ItemManager.CreateByItemID(itemMag.ammoType.itemid, itemMag.contents));
                    itemMag.contents = 0;
                }
            }

            if (targetItem.GetHeldEntity() is FlameThrower)
            {
                FlameThrower flameThrower = targetItem.GetHeldEntity().GetComponent<FlameThrower>();

                if (flameThrower.ammo > 0)
                {
                    item.GetOwnerPlayer().GiveItem(ItemManager.CreateByItemID(flameThrower.fuelType.itemid, flameThrower.ammo));
                    flameThrower.ammo = 0;
                }
            }

            if (targetItem.GetHeldEntity() is Chainsaw)
            {
                Chainsaw chainsaw = targetItem.GetHeldEntity().GetComponent<Chainsaw>();

                if (chainsaw.ammo > 0)
                {
                    item.GetOwnerPlayer().GiveItem(ItemManager.CreateByItemID(chainsaw.fuelType.itemid, chainsaw.ammo));
                    chainsaw.ammo = 0;
                }
            }
            return true;
        }

        bool HasVanillaContainer(ItemDefinition itemDefinition)
        {
            foreach (var itemMod in itemDefinition.itemMods)
            {
                if (itemMod is ItemModContainer)
                    return true;
            }
            return false;
        }

        object CanCombineDroppedItem(DroppedItem item, DroppedItem targetItem)
        {
            if (item.item.info.itemid != targetItem.item.info.itemid ||
                item.skinID != targetItem.skinID ||
                item.item.name != targetItem.item.name)
                return true;

            if (item.item.contents?.itemList.Count > 0 || targetItem.item.contents?.itemList.Count > 0)
                return true;

            if (item.item.contents?.capacity != targetItem.item.contents?.capacity)
                return true;

            return null;
        }

        Item OnItemSplit(Item item, int amount)
        {
            if (amount <= 0) return null;

            if (item.amount < amount) return null;

            if (item.skin == 2591851360 || item.skin == 2817854052 || item.skin == 2892143123 || item.skin == 2892142979 ||
                item.skin == 2892142846 || item.skin == 2817854377 || item.skin == 2817854677 || item.skin == 2888602635 ||
                item.skin == 2888602942 || item.skin == 2888603247 || item.skin == 2445048695 || item.skin == 2445033042)
            {
                return null;
            }

            var armorSlotComponent = item.info.GetComponent<ItemModContainerArmorSlot>();
            if (armorSlotComponent != null)
            {
                Item newArmorItem = ItemManager.CreateByItemID(item.info.itemid);
                if (newArmorItem == null) return null;

                int capacity = item.contents?.capacity ?? 0;
                armorSlotComponent.CreateAtCapacity(capacity, newArmorItem);

                if (item.contents != null && newArmorItem.contents != null)
                {
                    foreach (var nItem in item.contents.itemList)
                    {
                        Item cArmor = ItemManager.CreateByItemID(nItem.info.itemid, nItem.amount);
                        if (cArmor != null)
                        {
                            newArmorItem.contents.AddItem(cArmor.info, cArmor.amount);
                            cArmor.MarkDirty();
                        }
                    }
                }

                item.amount -= amount;
                newArmorItem.name = item.name;
                newArmorItem.skin = item.skin;
                newArmorItem.amount = amount;
                newArmorItem.MarkDirty();
                item.MarkDirty();

                return newArmorItem;
            }

            if (item.GetHeldEntity()?.GetComponentInChildren<BaseLiquidVessel>() != null)
            {
                Item liquidContainer = ItemManager.CreateByName(item.info.shortname);
                if (liquidContainer == null)
                {
                    return null;
                }

                liquidContainer.amount = amount;
                item.amount -= amount;
                item.MarkDirty();

                Item water = item.contents.FindItemByItemID(-1779180711);
                if (water != null)
                {
                    liquidContainer.contents.AddItem(ItemManager.FindItemDefinition(-1779180711), water.amount);
                }

                return liquidContainer;
            }

            if (item.skin != 0 && item.info.amountType != ItemDefinition.AmountType.Genetics)
            {
                Item x = ItemManager.CreateByItemID(item.info.itemid);
                if (x == null)
                {
                    return null;
                }

                BaseProjectile.Magazine itemMag = x.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine;
                if (itemMag != null && itemMag.contents > 0)
                {
                    itemMag.contents = 0;
                }

                if (item.contents != null)
                {
                    if (x.contents == null)
                    {
                        x.contents = new ItemContainer();
                        x.contents.ServerInitialize(x, item.contents.capacity);
                        x.contents.GiveUID();
                    }
                    else
                    {
                        x.contents.capacity = item.contents.capacity;
                    }
                }

                item.amount -= amount;
                x.name = item.name;
                x.skin = item.skin;
                x.amount = amount;
                x.MarkDirty();
                var heldEntity = x.GetHeldEntity();
                if (heldEntity != null)
                {
                    heldEntity.skinID = item.skin;
                }

                item.MarkDirty();

                return x;
            }

            Item newItem = ItemManager.CreateByItemID(item.info.itemid);
            if (newItem == null)
            {
                return null;
            }

            BaseProjectile.Magazine newItemMag = newItem.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine;
            if (newItem.contents?.itemList.Count == 0 && (_config.DisableFix || newItemMag?.contents == 0))
            {
                newItem.Remove();
                return null;
            }

            item.amount -= amount;
            newItem.name = item.name;
            newItem.amount = amount;
            if (item.skin != 0)
            {
                newItem.skin = item.skin;
            }
            item.MarkDirty();

            if (item.IsBlueprint())
            {
                newItem.blueprintTarget = item.blueprintTarget;
            }


            if (item.info.amountType == ItemDefinition.AmountType.Genetics && item.instanceData != null && item.instanceData.dataInt != 0)
            {
                newItem.instanceData = new ProtoBuf.Item.InstanceData()
                {
                    dataInt = item.instanceData.dataInt,
                    ShouldPool = false
                };
            }

            if (newItem.contents?.itemList.Count > 0)
            {
                item.contents.Clear();
            }


            newItem.MarkDirty();

            if (_config.VendingMachineAmmoFix && item.GetRootContainer()?.entityOwner is VendingMachine)
            {
                return newItem;
            }

            if (_config.DisableFix)
            {
                return newItem;
            }

            if (newItem.GetHeldEntity() is FlameThrower)
            {
                newItem.GetHeldEntity().GetComponent<FlameThrower>().ammo = 0;
            }

            if (newItem.GetHeldEntity() is Chainsaw)
            {
                newItem.GetHeldEntity().GetComponent<Chainsaw>().ammo = 0;
            }

            BaseProjectile.Magazine itemMagDefault = newItem.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine;
            if (itemMagDefault != null && itemMagDefault.contents > 0)
            {
                itemMagDefault.contents = 0;
            }

            return newItem;
        }

        void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            BasePlayer player = container.GetOwnerPlayer();
            if (player == null || !player.userID.IsSteamId()) return;
            if (Interface.CallHook("OnIgnoreStackSize", player, item) != null) return;
            if (player.inventory.containerWear.uid != container.uid) return;
            if (item.amount > 1)
            {
                int amount2 = item.amount -= 1;
                player.inventory.containerWear.Take(null, item.info.itemid, amount2 - 1);
                Interface.Oxide.NextTick(() =>
                {
                    Item x = ItemManager.CreateByItemID(item.info.itemid, item.amount, item.skin);
                    x.name = item.name;
                    x.skin = item.skin;
                    x.amount = amount2;
                    x._condition = item._condition;
                    x._maxCondition = item._maxCondition;
                    x.MarkDirty();
                    if (!x.MoveToContainer(player.inventory.containerMain))
                        x.DropAndTossUpwards(player.transform.position);
                });
            }
        }

        object OnCardSwipe(CardReader cardReader, Keycard card, BasePlayer player)
        {
            var item = card.GetItem();
            if (item == null || item.isBroken || item.amount <= 1) return null;

            int division = item.amount / 1;

            for (int i = 0; i < division; i++)
            {
                Item x = item.SplitItem(1);
                if (x != null && !x.MoveToContainer(player.inventory.containerMain, -1, false) && (item.parent == null || !x.MoveToContainer(item.parent)))
                    x.Drop(player.inventory.containerMain.dropPosition, player.inventory.containerMain.dropVelocity);
            }
            return null;
        }
        #endregion

        #region Helpers
        bool CanWaterItemsStack(Item item, Item targetItem)
        {
            var itemVessel = item.GetHeldEntity()?.GetComponentInChildren<BaseLiquidVessel>();
            var targetItemVessel = targetItem.GetHeldEntity()?.GetComponentInChildren<BaseLiquidVessel>();

            if (itemVessel == null && targetItemVessel == null) return true;

            if (itemVessel == null || targetItemVessel == null) return false;

            var itemMaxCapacity = item.info.stackable;
            var targetItemMaxCapacity = targetItem.info.stackable;

            if (itemMaxCapacity != targetItemMaxCapacity) return false;

            if (targetItem.contents.IsEmpty() && item.contents.IsEmpty()) return true;

            if (!targetItem.contents.IsEmpty() && !item.contents.IsEmpty())
            {
                var first = item.contents.itemList.First();
                var second = targetItem.contents.itemList.First();

                if (first.info.itemid == second.info.itemid)
                {
                    int combinedAmount = first.amount + second.amount;
                    return combinedAmount <= itemMaxCapacity;
                }
            }
            return false;
        }
        #endregion
    }
}