using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Newtonsoft.Json;
using Rust;

namespace Oxide.Plugins
{
    [Info("ZLevels Remastered", "nivex", "3.2.2")]
    [Description("Lets players level up as they harvest different resources and when crafting")]

    class ZLevelsRemastered : RustPlugin
    {
        [PluginReference]
        Plugin EventManager, CraftingController, ZoneManager, Economics, IQEconomic, ServerRewards, SkillTree;

        private StoredData data = new();
        private Dictionary<string, ItemDefinition> CraftItems;
        private CraftData _craftData = new();
        private StringBuilder _sb = new();
        private bool newSaveDetected;
        private bool bonusOn;
        private int MaxB = 10001;
        private int MinB = 10;

        public enum Skills
        {
            ACQUIRE,
            CRAFTING,
            MINING,
            SKINNING,
            WOODCUTTING,
            ALL
        }

        private Skills[] AllSkills = new Skills[] { Skills.ACQUIRE, Skills.CRAFTING, Skills.MINING, Skills.SKINNING, Skills.WOODCUTTING };

        private class CraftData
        {
            public Dictionary<string, CraftInfo> CraftList = new();
            public CraftData() { }
        }

        private class CraftInfo
        {
            public int MaxBulkCraft;
            public int MinBulkCraft;
            public string shortName;
            public bool Enabled;
            public CraftInfo() { }
        }

        private class StoredData
        {
            public Hash<ulong, PlayerInfo> PlayerInfo = new();
            public StoredData() { }
        }

        private enum LevelType
        {
            ACQUIRE_LEVEL,
            ACQUIRE_POINTS,
            CRAFTING_LEVEL,
            CRAFTING_POINTS,
            MINING_LEVEL,
            MINING_POINTS,
            WOODCUTTING_LEVEL,
            WOODCUTTING_POINTS,
            SKINNING_LEVEL,
            SKINNING_POINTS,
            LAST_DEATH,
            XP_MULTIPLIER,
        }

        private class PlayerInfo : IEquatable<PlayerInfo>
        {
            private double _acquireLevel = 1.0;
            private double _acquirePoints = 10.0;
            private double _craftingLevel = 1.0;
            private double _craftingPoints = 10.0;
            private double _miningLevel = 1.0;
            private double _miningPoints = 10.0;
            private double _skinningLevel = 1.0;
            private double _skinningPoints = 10.0;
            private double _woodcuttingLevel = 1.0;
            private double _woodcuttingPoints = 10.0;
            private double _lastDeath = 0.0;
            private double _xpMultiplier = 100.0;

            private double RoundValue(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

            [JsonProperty(PropertyName = "AL")]
            public double ACQUIRE_LEVEL
            {
                get => RoundValue(_acquireLevel);
                set => _acquireLevel = RoundValue(value);
            }

            [JsonProperty(PropertyName = "AP")]
            public double ACQUIRE_POINTS
            {
                get => RoundValue(_acquirePoints);
                set => _acquirePoints = RoundValue(value);
            }

            [JsonProperty(PropertyName = "CL")]
            public double CRAFTING_LEVEL
            {
                get => RoundValue(_craftingLevel);
                set => _craftingLevel = RoundValue(value);
            }

            [JsonProperty(PropertyName = "CP")]
            public double CRAFTING_POINTS
            {
                get => RoundValue(_craftingPoints);
                set => _craftingPoints = RoundValue(value);
            }

            [JsonProperty(PropertyName = "ML")]
            public double MINING_LEVEL
            {
                get => RoundValue(_miningLevel);
                set => _miningLevel = RoundValue(value);
            }

            [JsonProperty(PropertyName = "MP")]
            public double MINING_POINTS
            {
                get => RoundValue(_miningPoints);
                set => _miningPoints = RoundValue(value);
            }

            [JsonProperty(PropertyName = "SL")]
            public double SKINNING_LEVEL
            {
                get => RoundValue(_skinningLevel);
                set => _skinningLevel = RoundValue(value);
            }

            [JsonProperty(PropertyName = "SP")]
            public double SKINNING_POINTS
            {
                get => RoundValue(_skinningPoints);
                set => _skinningPoints = RoundValue(value);
            }

            [JsonProperty(PropertyName = "WCL")]
            public double WOODCUTTING_LEVEL
            {
                get => RoundValue(_woodcuttingLevel);
                set => _woodcuttingLevel = RoundValue(value);
            }

            [JsonProperty(PropertyName = "WCP")]
            public double WOODCUTTING_POINTS
            {
                get => RoundValue(_woodcuttingPoints);
                set => _woodcuttingPoints = RoundValue(value);
            }

            [JsonProperty(PropertyName = "LD")]
            public double LAST_DEATH
            {
                get => RoundValue(_lastDeath);
                set => _lastDeath = RoundValue(value);
            }

            [JsonProperty(PropertyName = "XPM")]
            public double XP_MULTIPLIER
            {
                get => RoundValue(_xpMultiplier);
                set => _xpMultiplier = RoundValue(value);
            }

            [JsonProperty(PropertyName = "CUI")]
            public bool CUI { get; set; } = true;

            [JsonProperty(PropertyName = "ONOFF")]
            public bool ENABLED { get; set; } = true;

            public PlayerInfo() { }

            public bool Equals(PlayerInfo other)
            {
                if (other == null)
                    return false;

                return ACQUIRE_LEVEL == other.ACQUIRE_LEVEL &&
                       ACQUIRE_POINTS == other.ACQUIRE_POINTS &&
                       CRAFTING_LEVEL == other.CRAFTING_LEVEL &&
                       CRAFTING_POINTS == other.CRAFTING_POINTS &&
                       MINING_LEVEL == other.MINING_LEVEL &&
                       MINING_POINTS == other.MINING_POINTS &&
                       SKINNING_LEVEL == other.SKINNING_LEVEL &&
                       SKINNING_POINTS == other.SKINNING_POINTS &&
                       WOODCUTTING_LEVEL == other.WOODCUTTING_LEVEL &&
                       WOODCUTTING_POINTS == other.WOODCUTTING_POINTS &&
                       XP_MULTIPLIER == other.XP_MULTIPLIER &&
                       CUI == other.CUI &&
                       ENABLED == other.ENABLED;
            }

            public override bool Equals(object obj)
            {
                return obj is PlayerInfo other && Equals(other);
            }

            public override int GetHashCode()
            {
                HashCode hash = new();
                hash.Add(ACQUIRE_LEVEL);
                hash.Add(ACQUIRE_POINTS);
                hash.Add(CRAFTING_LEVEL);
                hash.Add(CRAFTING_POINTS);
                hash.Add(MINING_LEVEL);
                hash.Add(MINING_POINTS);
                hash.Add(SKINNING_LEVEL);
                hash.Add(SKINNING_POINTS);
                hash.Add(WOODCUTTING_LEVEL);
                hash.Add(WOODCUTTING_POINTS);
                hash.Add(XP_MULTIPLIER);
                hash.Add(CUI);
                hash.Add(ENABLED);
                return hash.ToHashCode();
            }

            public static bool operator ==(PlayerInfo left, PlayerInfo right)
            {
                if (ReferenceEquals(left, right))
                    return true;

                if (left is null)
                    return false;

                return left.Equals(right);
            }

            public static bool operator !=(PlayerInfo left, PlayerInfo right)
            {
                return !(left == right);
            }

            internal bool IsDefault(PlayerInfo other) => Equals(other);
        }

        #region Main

        private void Init()
        {
            Unsubscribe();
            RegisterPermissions();

            int index = 0;

            foreach (Skills skill in AllSkills)
            {
                if (IsSkillEnabled(skill))
                {
                    skillIndex.Add(skill, ++index);
                }
            }
        }

        private void Unload()
        {
            Clean();
            SaveData();
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyGUI(player);
            }
            if (TOD_Sky.Instance?.Components?.Time != null)
            {
                TOD_Sky.Instance.Components.Time.OnSunrise -= OnTimeSunrise;
                TOD_Sky.Instance.Components.Time.OnSunset -= OnTimeSunset;
            }
        }

        private void OnNewSave(string strFilename)
        {
            if (config.generic.wipeDataOnNewSave)
            {
                Puts("New save detected...");
                newSaveDetected = true;
            }
        }

        private void OnServerInitialized()
        {
            LoadData();
            if (config.nightbonus.enableNightBonus && TOD_Sky.Instance.IsNight)
            {
                pointsPerHitCurrent = config.nightbonus.pointsPerHitAtNight;
                resourceMultipliersCurrent = config.nightbonus.resourceMultipliersAtNight;
                pointsPerHitPowerToolCurrent = config.nightbonus.pointsPerPowerToolAtNight;
                bonusOn = true;
            }
            else
            {
                pointsPerHitCurrent = config.settings.pointsPerHit;
                resourceMultipliersCurrent = config.settings.resourceMultipliers;
                pointsPerHitPowerToolCurrent = config.settings.pointsPerPowerTool;
            }

            foreach (var player in BasePlayer.allPlayerList)
            {
                var pi = GetPlayerInfo(player);
                if (player.IsConnected) CreateGUI(player, pi);
            }

            Puts("Stats can be reset by > zl.reset <");
            Subscribe();
            SaveData();
            RegisterCommands();
        }

        private void OnPlayerConnected(BasePlayer player) => OnPlayerSleepEnded(player);

        private void OnPlayerDisconnected(BasePlayer player) => GetPlayerInfo(player);

        private void OnEntityKill(BasePlayer player) => OnPlayerSleep(player);

        private void OnPlayerRespawned(BasePlayer player) => OnPlayerSleepEnded(player);

        private void OnPlayerSleepEnded(BasePlayer player) => CreateGUI(player, GetPlayerInfo(player));

        private void OnPlayerSleep(BasePlayer player) => DestroyGUI(player);

        private void OnEntityDeath(BasePlayer player, HitInfo hitInfo)
        {
            if (hitInfo == null || !IsValid(player) || isZoneExcluded(player))
            {
                return;
            }

            DestroyGUI(player);

            if (!hasRights(player.UserIDString))
            {
                return;
            }

            var pi = GetPlayerInfo(player);

            if (!pi.ENABLED || permission.UserHasPermission(player.UserIDString, config.generic.permissionNameXP))
            {
                return;
            }

            bool isSuicide = hitInfo.damageTypes.Has(DamageType.Suicide) && (player.IsAdmin || player.IsDeveloper || !player.CanSuicide());

            if (!config.generic.penaltyOnSuicide && isSuicide) return;
            if (!config.generic.penaltyOnDeath && !isSuicide) return;

            if (EventManager?.Call("IsEventPlayer", player) != null)
            {
                return;
            }

            if (Interface.CallHook("CanBePenalized", player) != null)
            {
                return;
            }

            _sb.Clear();

            foreach (Skills skill in AllSkills)
            {
                if (IsSkillEnabled(skill))
                {
                    var penalty = GetPenalty(player, pi, skill, isSuicide);

                    if (penalty > 0)
                    {
                        _sb.AppendLine("* -" + penalty + " " + _(skill + "Skill") + " XP.");
                        removePoints(pi, skill, penalty);
                    }
                }
            }
            
            if (_sb.Length > 0)
            {
                Message(player, "PenaltyText", _sb.ToString());
            }

            pi.LAST_DEATH = ToEpochTime(DateTime.UtcNow);
        }

        private void OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (item == null || dispenser == null || !IsValid(player) || !hasRights(player.UserIDString))
            {
                return;
            }

            Skills? skill = dispenser.gatherType switch
            {
                ResourceDispenser.GatherType.Tree => Skills.WOODCUTTING,
                ResourceDispenser.GatherType.Flesh => Skills.SKINNING,
                ResourceDispenser.GatherType.Ore => Skills.MINING,
                _ => null
            };

            if (skill == null || !IsSkillEnabled(skill.Value))
            {
                return;
            }

            var pi = GetPlayerInfo(player);

            if (!pi.ENABLED)
            {
                return;
            }

            Item activeItem = player.GetActiveItem();

            if (permission.UserHasPermission(player.UserIDString, config.generic.BlockWeaponsGather) && activeItem?.info.category == ItemCategory.Weapon)
            {
                return;
            }

            if (!config.functions.ShouldAllowGather(item.info.shortname, out bool grantXPOnly))
            {
                return;
            }

            int prevAmount = item.amount;
            bool isPowerTool = false;
            
            if (skill != Skills.SKINNING && activeItem != null)
            {
                HeldEntity heldEntity = activeItem.GetHeldEntity() as HeldEntity;

                if (heldEntity is Jackhammer || heldEntity is Chainsaw)
                {
                    if (skill == Skills.WOODCUTTING && !CanUseChainsaw(player))
                    {
                        return;
                    }

                    if (skill == Skills.MINING && !CanUseJackhammer(player))
                    {
                        return;
                    }

                    if (!config.functions.gibs && dispenser.GetComponent<ServerGib>() != null)
                    {
                        return;
                    }

                    isPowerTool = true;
                }
            }

            if (!config.functions.gibs && skill == Skills.MINING && dispenser.GetComponent<ServerGib>() != null)
            {
                return;
            }

            int amount = levelHandler(pi, player, item.amount, skill.Value, dispenser.baseEntity, isPowerTool);

            if (!grantXPOnly)
            {
                item.amount = amount;
            }

            if (item.amount != prevAmount)
            {
                Interface.CallHook("OnZLevelDispenserGather", dispenser, player, item, prevAmount, item.amount, isPowerTool);
            }
        }

        private bool CanUseJackhammer(BasePlayer player) => permission.UserHasPermission(player.UserIDString, config.generic.AllowJackhammerGather);

        private bool CanUseChainsaw(BasePlayer player) => permission.UserHasPermission(player.UserIDString, config.generic.AllowChainsawGather);

        private void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item) => OnDispenserGather(dispenser, player, item);

        private List<string> _warnings = new();

        private object OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
        {
            if (collectible == null || collectible.itemList.IsNullOrEmpty() || !IsValid(player) || !hasRights(player.UserIDString))
                return null;

            var pi = GetPlayerInfo(player);

            if (pi == null || !pi.ENABLED)
                return null;

            if (config.functions.enabledCollectibleEntity.TryGetValue(collectible.ShortPrefabName, out var enabled) && !enabled)
                return null;

            Skills skill;

            for (int i = 0; i < collectible.itemList.Length; i++)
            {
                ItemAmount itemAmount = collectible.itemList[i];
                if (itemAmount == null) continue;
                if (itemAmount.itemDef == null) continue;
                switch (itemAmount.itemDef.shortname)
                {
                    case "black.berry":
                    case "blue.berry":
                    case "bone.fragments":
                    case "clone.seed":
                    case "clone.orchid":
                    case "clone.rose":
                    case "clone.sunflower":
                    case "cloth":
                    case "corn":
                    case "diesel_barrel":
                    case "green.berry":
                    case "mushroom":
                    case "orchid":
                    case "potato":
                    case "pumpkin":
                    case "red.berry":
                    case "rose":
                    case "seed.black.berry":
                    case "seed.blue.berry":
                    case "seed.corn":
                    case "seed.green.berry":
                    case "seed.hemp":
                    case "seed.orchid":
                    case "seed.potato":
                    case "seed.pumpkin":
                    case "seed.red.berry":
                    case "seed.rose":
                    case "seed.sunflower":
                    case "seed.wheat":
                    case "seed.white.berry":
                    case "seed.yellow.berry":
                    case "sunflower":
                    case "wheat":
                    case "white.berry":
                    case "yellow.berry":
                        skill = IsSkillEnabled(Skills.ACQUIRE) ? Skills.ACQUIRE : Skills.SKINNING;
                        break;
                    case "metal.ore":
                    case "sulfur.ore":
                    case "stones":
                        skill = config.settings.MiningStonesOre ? Skills.MINING : Skills.ACQUIRE;
                        break;
                    case "wood":
                        skill = config.settings.AcquireWood ? Skills.ACQUIRE : Skills.WOODCUTTING;
                        break;
                    default:
                        if (IsSkillEnabled(Skills.ACQUIRE)) skill = Skills.ACQUIRE;
                        else if (IsSkillEnabled(Skills.SKINNING)) skill = Skills.SKINNING;
                        else return null;
                        if (!_warnings.Contains(itemAmount.itemDef.shortname))
                        {
                            PrintWarning($"WARNING: {player} picked up undefined item: {itemAmount.itemDef.shortname}. Defaulting to {skill} skill.");
                            _warnings.Add(itemAmount.itemDef.shortname);
                        }
                        break;
                }

                if (IsSkillEnabled(skill))
                {
                    int prevAmount = (int)itemAmount.amount;
                    itemAmount.amount = levelHandler(pi, player, prevAmount, skill, collectible);
                    if (Interface.CallHook("OnZLevelCollectiblePickup", itemAmount, player, collectible, prevAmount, itemAmount.amount) != null)
                    {
                        return true;
                    }
                }
            }
            return null;
        }

        private void OnTimeSunset()
        {
            if (!config.nightbonus.enableNightBonus || bonusOn)
            {
                return;
            }

            bonusOn = true;
            pointsPerHitCurrent = config.nightbonus.pointsPerHitAtNight;
            resourceMultipliersCurrent = config.nightbonus.resourceMultipliersAtNight;
            pointsPerHitPowerToolCurrent = config.nightbonus.pointsPerPowerToolAtNight;
            if (config.nightbonus.broadcastEnabledBonus)
            {
                foreach (var player in BasePlayer.activePlayerList)
                    Message(player, "NightBonusOn");
            }
            if (config.nightbonus.logEnabledBonusConsole)
                Puts("Nightbonus points enabled");
        }

        private void OnTimeSunrise()
        {
            if (!config.nightbonus.enableNightBonus || !bonusOn)
            {
                return;
            }
            bonusOn = false;
            pointsPerHitCurrent = config.settings.pointsPerHit;
            resourceMultipliersCurrent = config.settings.resourceMultipliers;
            pointsPerHitPowerToolCurrent = config.settings.pointsPerPowerTool;
            if (config.nightbonus.broadcastEnabledBonus)
            {
                foreach (var player in BasePlayer.activePlayerList)
                    Message(player, "NightBonusOff");
            }
            if (config.nightbonus.logEnabledBonusConsole)
                Puts("Nightbonus points disabled");
        }

        private object OnGrowableGathered(GrowableEntity plant, Item item, BasePlayer player)
        {
            if (item == null || player == null || !hasRights(player.UserIDString))
            {
                return true;
            }

            var pi = GetPlayerInfo(player);

            if (!pi.ENABLED)
            {
                return true;
            }

            Skills skill;
            if (IsSkillEnabled(Skills.ACQUIRE))
                skill = Skills.ACQUIRE;
            else skill = Skills.SKINNING;

            var prevAmount = item.amount;

            item.amount = levelHandler(pi, player, item.amount, skill, plant);

            Interface.CallHook("OnZLevelGrowableGathered", plant, item, player, prevAmount, item.amount);

            return null;
        }


        private object OnItemCraft(ItemCraftTask task, BasePlayer crafter, Item fromTempBlueprint)
        {
            if (!hasRights(crafter.UserIDString)) return null;

            var pi = GetPlayerInfo(crafter);

            if (!pi.ENABLED) return null;

            var bypassCraftLevelRequirement = CanInstantCraftNoLevelRequirement(crafter);
            var craftingTime = task.blueprint.time;

            if (bypassCraftLevelRequirement)
            {
                craftingTime = 0;
            }
            else if (IsSkillEnabled(Skills.CRAFTING))
            {
                if (pi.CRAFTING_LEVEL > 1)
                    craftingTime -= task.blueprint.time * (float)((pi.CRAFTING_LEVEL * config.settings.craftingDetails.percent) / 100);
            }
            else return null;

            if (craftingTime <= 0)
            {
                craftingTime = 0;

                foreach (var craftInfo in _craftData.CraftList.Values)
                {
                    if (!craftInfo.Enabled || craftInfo.shortName != task.blueprint.targetItem.shortname)
                    {
                        continue;
                    }
                    if (task.amount >= craftInfo.MinBulkCraft && task.amount <= craftInfo.MaxBulkCraft && task.amount > 0)
                    {
                        var stacks = GetStacks(task.amount * task.blueprint.amountToCreate, task.blueprint.targetItem.stackable);
                        if (!HasPlace(crafter, stacks))
                        {
                            ReturnCraft(task, crafter);
                            return true;
                        }
                        if (bypassCraftLevelRequirement || CanInstantCraft(crafter))
                        {
                            if (!task.blueprint.name.Contains("(Clone)"))
                                task.blueprint = UnityEngine.Object.Instantiate(task.blueprint);
                            task.blueprint.amountToCreate *= task.amount;
                            crafter.inventory.crafting.FinishCrafting(task);
                            task.cancelled = true;
                            return true;
                        }
                    }
                }
            }

            if (!task.blueprint.name.Contains("(Clone)"))
                task.blueprint = UnityEngine.Object.Instantiate(task.blueprint);

            task.blueprint.time = craftingTime;

            return null;
        }
        private List<int> GetStacks(int amount, int maxStack)
        {
            List<int> stacks = new();
            while (amount > maxStack && amount > 0)
            {
                amount -= maxStack;
                stacks.Add(maxStack);
            }
            if (amount > 0)
            {
                stacks.Add(amount);
            }
            return stacks;
        }
        private bool HasPlace(BasePlayer crafter, List<int> stacks)
        {
            if (!config.settings.craftingDetails.slots)
            {
                return true;
            }
            var capacity = crafter.inventory.containerMain.capacity + crafter.inventory.containerBelt.capacity;
            var taken = crafter.inventory.containerMain.itemList.Count + crafter.inventory.containerBelt.itemList.Count;
            var slots = capacity - taken;
            if (slots - stacks.Count < 0)
            {
                return false;
            }
            return slots > 0;
        }
        private bool CanInstantCraftNoLevelRequirement(BasePlayer crafter)
        {
            return config.settings.craftingDetails.noLevelRequirement && permission.UserHasPermission(crafter.UserIDString, config.settings.craftingDetails.Permission);
        }
        private bool CanInstantCraft(BasePlayer crafter)
        {
            if (config.settings.craftingDetails.usePermission)
            {
                return permission.UserHasPermission(crafter.UserIDString, config.settings.craftingDetails.Permission);
            }
            return true;
        }
        private void ReturnCraft(ItemCraftTask task, BasePlayer crafter)
        {
            task.cancelled = true;
            Message(crafter, "NoSlots");
            foreach (var item in task.takenItems)
            {
                if (item.amount > 0)
                    crafter.GiveItem(item);
            }
        }
        private object OnItemCraftFinished(ItemCraftTask task, Item item, ItemCrafter craft)
        {
            if (task == null) return null;
            var crafter = craft?.owner;
            if (crafter == null || !hasRights(crafter.UserIDString)) return null;
            var bypassLevelRequirement = CanInstantCraftNoLevelRequirement(crafter);
            if (!bypassLevelRequirement && !IsSkillEnabled(Skills.CRAFTING)) return null;
            var pi = GetPlayerInfo(crafter);
            var xpPercentBefore = getExperiencePercent(pi, Skills.CRAFTING);
            if (task.blueprint == null)
            {
                Puts("There is problem obtaining task.blueprint on 'OnItemCraftFinished' hook! This is usually caused by some incompatable plugins.");
                return null;
            }
            var experienceGain = (task.blueprint.time + 0.99f) / config.settings.craftingDetails.time;
            if (experienceGain == 0)
                return null;

            double Level = pi.CRAFTING_LEVEL;
            double Points = pi.CRAFTING_POINTS;
            Points += experienceGain * config.settings.craftingDetails.xp;
            if (Points >= getLevelPoints(Level + 1))
            {
                var levelCap = config.settings.levelCaps.CRAFTING;
                var maxLevel = levelCap > 0 && Level + 1 > levelCap;
                if (!maxLevel)
                {
                    Level = getPointsLevel(Points, Skills.CRAFTING);
                    string format = $"<color={config.settings.colors.CRAFTING}>{_("LevelUpText", crafter.UserIDString)}</color>";
                    string message = string.Format(format, _("CRAFTINGSkill", crafter.UserIDString), Level, Points, getLevelPoints(Level + 1), pi.CRAFTING_LEVEL * Convert.ToDouble(config.settings.craftingDetails.percent));
                    Player.Message(crafter, message, string.IsNullOrEmpty(config.generic.pluginPrefix) ? string.Empty : config.generic.pluginPrefix, config.generic.steamIDIcon);
                    GiveReward(crafter, Skills.CRAFTING, Level);
                    if (config.generic.enableLevelupBroadcast)
                    {
                        foreach (var target in BasePlayer.activePlayerList)
                        {
                            if (target != null && target.userID != crafter.userID && hasRights(target.UserIDString) && GetPlayerInfo(target).ENABLED)
                            {
                                Message(target, "LevelUpTextBroadcast", crafter.displayName, Level, config.settings.colors.CRAFTING, _("CRAFTINGSkill", crafter.UserIDString));
                            }
                        }
                    }
                }
            }
            try
            {
                if (item.info.shortname != "lantern_a" && item.info.shortname != "lantern_b")
                    setPointsAndLevel(pi, Skills.CRAFTING, Points, Level);
            }
            catch { }

            try
            {
                var xpPercentAfter = getExperiencePercent(pi, Skills.CRAFTING);
                if (xpPercentAfter != xpPercentBefore)
                    GUIUpdateSkill(crafter, Skills.CRAFTING);
            }
            catch { }

            if (task.amount > 0) return null;
            if (task.blueprint != null && task.blueprint.name.Contains("(Clone)"))
            {
                var behaviours = task.blueprint.GetComponents<MonoBehaviour>();
                foreach (var behaviour in behaviours)
                {
                    if (behaviour.name.Contains("(Clone)")) UnityEngine.Object.Destroy(behaviour);
                }
            }
            return null;
        }

        #endregion Serverhooks

        #region Commands

        [HookMethod("SendHelpText"), ChatCommand("stathelp")]
        private void SendHelpText(BasePlayer player)
        {
            _sb.Clear();
            _sb.AppendLine("<size=18><color=orange>ZLevels</color></size><size=14><color=#ce422b>REMASTERED</color></size>");
            _sb.AppendLine("/stats - Displays your stats.");
            _sb.AppendLine("/statsui - Enable/Disable stats UI.");
            _sb.AppendLine("/statsonoff - Enable/Disable whole leveling.");
            _sb.AppendLine("/statinfo - Displays information about skills.");
            _sb.AppendLine("/stathelp - Displays the help.");
            //sb.AppendLine("/topskills - Display max levels reached so far.");
            SendReply(player, _sb.ToString());
        }

        private void PointsPerHitCommand(IPlayer user, string command, string[] args)
        {
            if (!user.IsAdmin) return;
            if (args.Length < 2)
            {
                user.Reply("Syntax: zl.pointsperhit skill number");
                user.Reply("Possible skills are: WC, M, S, A, C, *(All skills)");
                _sb.Clear();
                _sb.Append("Current points per hit:");
                foreach (var currSkill in AllSkills)
                {
                    if (!IsSkillEnabled(currSkill)) continue;
                    _sb.Append($" {currSkill} > {pointsPerHitCurrent.Get(currSkill)} |");
                }
                user.Reply(_sb.ToString().TrimEnd('|'));
                return;
            }
            if (!double.TryParse(args[1], out var points) || points < 1)
            {
                user.Reply("Incorrect number. Must be greater than or equal to 1");
                return;
            }

            var skillName = args[0].ToUpper();
            Skills skill;
            if (skillName == "*") skill = Skills.ALL;
            else if (skillName.Equals("C")) skill = Skills.CRAFTING;
            else if (skillName.Equals("WC")) skill = Skills.WOODCUTTING;
            else if (skillName.Equals("M")) skill = Skills.MINING;
            else if (skillName.Equals("A")) skill = Skills.ACQUIRE;
            else if (skillName.Equals("S")) skill = Skills.SKINNING;
            else { user.Reply("Incorrect skill. Possible skills are: WC, M, S, A, C, *(All skills)."); return; }

            if (skill == Skills.ALL)
            {
                foreach (var currSkill in AllSkills)
                {
                    if (!IsSkillEnabled(currSkill)) continue;
                    pointsPerHitCurrent.Set(currSkill, points);
                }
                _sb.Clear();
                _sb.Append("New points per hit:");
                foreach (var currSkill in AllSkills)
                {
                    if (!IsSkillEnabled(currSkill)) continue;
                    _sb.Append($" {currSkill} > {pointsPerHitCurrent.Get(currSkill)} |");
                }
                user.Reply(_sb.ToString().TrimEnd('|'));
            }
            else
            {
                pointsPerHitCurrent.Set(skill, points);
                _sb.Clear();
                _sb.Append("New points per hit:");
                foreach (var currSkill in AllSkills)
                {
                    if (!IsSkillEnabled(currSkill)) continue;
                    _sb.Append($" {currSkill} > {pointsPerHitCurrent.Get(currSkill)} |");
                }
                user.Reply(_sb.ToString().TrimEnd('|'));
            }
        }

        private void PlayerXpmCommand(IPlayer user, string command, string[] args)
        {
            if (!user.IsAdmin) return;
            if (args.Length < 1)
            {
                user.Reply(_("XPM USE 1", user.Id));
                user.Reply(_("XPM USE 2", user.Id));
                return;
            }
            IPlayer target = covalence.Players.FindPlayer(args[0]);
            if (target == null)
            {
                user.Reply(_("PLAYER NOT FOUND", user.Id));
                return;
            }
            if (!data.PlayerInfo.TryGetValue(Convert.ToUInt64(target.Id), out var playerData))
            {
                user.Reply("PlayerData is NULL!");
                return;
            }
            if (args.Length < 2)
            {
                var bonus1 = getXpMulti(playerData, target.Id);
                var baseMultiplier = playerData.XP_MULTIPLIER;
                if (baseMultiplier != bonus1)
                {
                    user.Reply($"Current XP multiplier for player '{target.Name}' is {baseMultiplier}% with vip raising total to {bonus1}%");
                }
                else user.Reply($"Current XP multiplier for player '{target.Name}' is {baseMultiplier}%");
                return;
            }
            double multiplier = -1;
            if (double.TryParse(args[1], out multiplier))
            {
                if (multiplier <= 0)
                {
                    user.Reply("Incorrect number. Must be greater than 0");
                    return;
                }
            }
            playerData.XP_MULTIPLIER = multiplier;
            var bonus2 = getXpMulti(playerData, target.Id);
            if (multiplier != bonus2)
            {
                user.Reply($"New XP multiplier for player '{target.Name}' is {getXpMulti(playerData, target.Id)}% with vip raising total to {bonus2}%");
            }
            else user.Reply($"New XP multiplier for player '{target.Name}' is {getXpMulti(playerData, target.Id)}%");
        }

        private void InfoCommand(IPlayer user, string command, string[] args)
        {
            if (!user.IsAdmin)
            {
                return;
            }
            if (args.Length < 1)
            {
                if (!GetLookingAtPlayer(user))
                {
                    user.Reply(_("INFO USE", user.Id));
                }
                return;
            }
            IPlayer target = covalence.Players.FindPlayer(args[0]);
            if (target == null)
            {
                user.Reply(_("PLAYER NOT FOUND", user.Id));
                return;
            }
            if (!data.PlayerInfo.TryGetValue(Convert.ToUInt64(target.Id), out var playerData))
            {
                user.Reply("PlayerData is NULL!");
                return;
            }
            PrintInfo(user, target.Id, target.Name, playerData);
        }

        private bool GetLookingAtPlayer(IPlayer user)
        {
            BasePlayer player = user.Object as BasePlayer;
            if (player == null) return false;
            BasePlayer target = RelationshipManager.GetLookingAtPlayer(player);
            if (target == null) return false;
            if (!data.PlayerInfo.TryGetValue(target.userID, out var playerData)) return false;
            PrintInfo(user, target.UserIDString, target.displayName, playerData);
            return true;
        }

        private void PrintInfo(IPlayer user, string targetId, string targetName, PlayerInfo playerData)
        {
            TextTable textTable = new();
            textTable.AddColumn("FieldInfo");
            textTable.AddColumn("Level");
            textTable.AddColumn("Points");
            textTable.AddRow(new string[] { _("ACQUIRESkill", targetId), $"{playerData.ACQUIRE_LEVEL:#0.##}", $"{playerData.ACQUIRE_POINTS:#0.##}" });
            textTable.AddRow(new string[] { _("CRAFTINGSkill", targetId), $"{playerData.CRAFTING_LEVEL:#0.##}", $"{playerData.CRAFTING_POINTS:#0.##}" });
            textTable.AddRow(new string[] { _("MININGSkill", targetId), $"{playerData.MINING_LEVEL:#0.##}", $"{playerData.MINING_POINTS:#0.##}" });
            textTable.AddRow(new string[] { _("SKINNINGSkill", targetId), $"{playerData.SKINNING_LEVEL:#0.##}", $"{playerData.SKINNING_POINTS:#0.##}" });
            textTable.AddRow(new string[] { _("WOODCUTTINGSkill", targetId), $"{playerData.WOODCUTTING_LEVEL:#0.##}", $"{playerData.WOODCUTTING_POINTS:#0.##}" });
            textTable.AddRow(new string[] { _("XPM", targetId), $"{getXpMulti(playerData, targetId):#0.##}%", string.Empty });
            user.Reply($"\n{_("STATS", targetId)}{targetName}\n{textTable}");
        }

        private void ResetCommand(IPlayer user, string command, string[] args)
        {
            if (!user.IsAdmin) return;
            if (args.Length != 1 || args[0] != "true")
            {
                user.Reply(_("RESET USE", user.Id));
                return;
            }
            WipeData();
            SaveData();
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyGUI(player);
                CreateGUI(player, GetPlayerInfo(player));
            }
            user.Reply("Userdata was successfully reset to zero");
            Puts("{0} ({1}) reset userdata to zero", user.Name, user.Id);
        }

        private void ZlvlCommand(IPlayer user, string command, string[] args)
        {
            if (!user.IsAdmin) return;

            if (args.Length < 3)
            {
                _sb.Clear();
                _sb.AppendLine("Syntax: zl.lvl name|steamid skill [OPERATOR]NUMBER");
                _sb.AppendLine("Example: zl.lvl Player WC /2 -- Player gets his WC level divided by 2.");
                _sb.AppendLine("Example: zl.lvl * * +3 -- Everyone currently playing in the server gets +3 for all skills.");
                _sb.AppendLine("Example: zl.lvl ** * /2 -- Everyone (including offline players) gets their level divided by 2.");
                _sb.AppendLine("Instead of names you can use wildcard(*): * - affects online players, ** - affects all players");
                _sb.AppendLine("Possible operators: *(XP Modified %), +(Adds level), -(Removes level), /(Divides level)").AppendLine();
                user.Reply(_sb.ToString());
                return;
            }
            var playerName = args[0];
            IPlayer target = covalence.Players.FindPlayer(playerName);

            if (playerName != "*" && playerName != "**" && target == null)
            {
                user.Reply(_("PLAYER NOT FOUND", user.Id));
                return;
            }
            if (playerName != "*" && playerName != "**" && !data.PlayerInfo.ContainsKey(Convert.ToUInt64(target.Id)))
            {
                user.Reply("PlayerData is NULL!");
                return;
            }

            if (target != null || playerName == "*" || playerName == "**")
            {
                var playerMode = 0; // Exact player
                if (playerName == "*")
                    playerMode = 1; // Online players
                else if (playerName == "**")
                    playerMode = 2; // All players
                var skillName = args[1].ToUpper();
                Skills skill;
                if (skillName == "*") skill = Skills.ALL;
                else if (skillName.Equals("C")) skill = Skills.CRAFTING;
                else if (skillName.Equals("WC")) skill = Skills.WOODCUTTING;
                else if (skillName.Equals("M")) skill = Skills.MINING;
                else if (skillName.Equals("A")) skill = Skills.ACQUIRE;
                else if (skillName.Equals("S")) skill = Skills.SKINNING;
                else { user.Reply("Incorrect skill. Possible skills are: WC, M, S, A, C, *(All skills)."); return; }
                var mode = 0; // 0 = SET, 1 = ADD, 2 = SUBTRACT, 3 = multiplier, 4 = divide
                int value;
                bool correct;
                if (args[2][0] == '+')
                {
                    mode = 1;
                    correct = int.TryParse(args[2].Replace("+", string.Empty), out value);
                }
                else if (args[2][0] == '-')
                {
                    mode = 2;
                    correct = int.TryParse(args[2].Replace("-", string.Empty), out value);
                }
                else if (args[2][0] == '*')
                {
                    mode = 3;
                    correct = int.TryParse(args[2].Replace("*", string.Empty), out value);
                }
                else if (args[2][0] == '/')
                {
                    mode = 4;
                    correct = int.TryParse(args[2].Replace("/", string.Empty), out value);
                }
                else
                {
                    correct = int.TryParse(args[2], out value);
                }
                if (correct)
                {
                    if (mode == 3) // Change XP Multiplier.
                    {
                        if (skill != Skills.ALL)
                        {
                            user.Reply("XPMultiplier is changeable for all skills! Use * instead of " + skill + ".");
                            return;
                        }
                        if (playerMode == 1)
                        {
                            foreach (var currPlayer in BasePlayer.activePlayerList)
                                editMultiplierForPlayer(value, currPlayer.userID);
                        }
                        else if (playerMode == 2)
                            editMultiplierForPlayer(value);
                        else if (target != null)
                            editMultiplierForPlayer(value, Convert.ToUInt64(target.Id));

                        var whom = playerMode == 1 ? "ALL ONLINE PLAYERS" : playerMode == 2 ? "ALL PLAYERS" : target.Name;
                        user.Reply($"XP rates has changed to {value}% of normal XP for {whom}");
                        return;
                    }
                    if (playerMode == 1)
                    {
                        foreach (var connPlayer in covalence.Players.Connected)
                        {
                            adminModifyPlayerStats(user, skill, value, mode, connPlayer);
                        }
                    }
                    else if (playerMode == 2)
                    {
                        foreach (var other in covalence.Players.All)
                        {
                            if (data.PlayerInfo.ContainsKey(Convert.ToUInt64(other.Id)))
                            {
                                adminModifyPlayerStats(user, skill, value, mode, other);
                            }
                        }
                    }
                    else
                    {
                        adminModifyPlayerStats(user, skill, value, mode, target);
                    }
                }
            }
        }

        private void CheckCommand(IPlayer user, string command, string[] args)
        {
            if (!user.IsAdmin) return;

            int count = 0;
            var pi = CreatePlayerInfo();

            foreach (var info in data.PlayerInfo)
            {
                if (info.Value.IsDefault(pi))
                {
                    count++;
                }
            }

            user.Reply($"{count} / {data.PlayerInfo.Count} entries in datafile can be removed");
        }

        private void CleanCommand(IPlayer user, string command, string[] args)
        {
            if (!user.IsAdmin) return;

            int count = data.PlayerInfo.Count;
            int cleaned = Clean();

            user.Reply($"{cleaned} / {count} entries in datafile have been removed");
            SaveData();
        }

        private int Clean()
        {
            int count = 0;

            if (data == null)
            {
                return count;
            }

            var pi = CreatePlayerInfo();

            foreach (var info in data.PlayerInfo.ToList())
            {
                if (info.Value.IsDefault(pi))
                {
                    count++;
                    data.PlayerInfo.Remove(info.Key);
                }
            }

            return count;
        }

        private void PenaltyCommand(IPlayer user, string command, string[] args)
        {
            if (!user.IsAdmin) return;

            if (args.Length == 1 && args[0] == "status")
            {
                user.Reply(config.generic.penaltyOnDeath ? "Penalty is currently enabled." : "Penalty is currently disabled.");
                return;
            }

            if (args.Length == 0) config.generic.penaltyOnDeath = !config.generic.penaltyOnDeath;
            else config.generic.penaltyOnSuicide = !config.generic.penaltyOnSuicide;

            if (config.generic.penaltyOnDeath || config.generic.penaltyOnSuicide)
            {
                Subscribe(nameof(OnEntityDeath));
                user.Reply("Penalty is now enabled.");
            }
            else
            {
                Unsubscribe(nameof(OnEntityDeath));
                user.Reply("Penalty is now disabled.");
            }

            SaveConfig();
        }

        [ChatCommand("stats")]
        private void StatsCommand(BasePlayer player, string command, string[] args)
        {
            if (!hasRights(player.UserIDString))
            {
                Message(player, "NoPermission");
                return;
            }
            _sb.Clear();
            _sb.AppendLine("<size=18><color=orange>ZLevels</color></size><size=14><color=#ce422b>REMASTERED</color></size>");
            foreach (Skills skill in AllSkills) _sb.Append(getStatPrint(player, skill));
            var pi = GetPlayerInfo(player.userID);
            var alive = ReadableTimeSpan(DateTime.UtcNow - ToDateTimeFromEpoch(pi.LAST_DEATH));
            _sb.AppendLine().AppendLine($"Time alive: {alive}");
            _sb.AppendLine($"XP rates for you are {getXpMulti(pi, player.UserIDString)}%");
            SendReply(player, _sb.ToString());
        }

        [ChatCommand("statinfo")]
        private void StatInfoCommand(BasePlayer player, string command, string[] args)
        {
            if (!hasRights(player.UserIDString))
            {
                Message(player, "NoPermission");
                return;
            }

            _sb.Clear();
            var colors = config.settings.colors;
            var craftingDetails = config.settings.craftingDetails;
            var xpm = getXpMulti(GetPlayerInfo(player.userID), player.UserIDString) / 100f;
            var m = player.GetHeldEntity() is Jackhammer ? pointsPerHitPowerToolCurrent.Get(Skills.MINING) : pointsPerHitCurrent.Get(Skills.MINING);
            var wc = player.GetHeldEntity() is Chainsaw ? pointsPerHitPowerToolCurrent.Get(Skills.WOODCUTTING) : pointsPerHitCurrent.Get(Skills.WOODCUTTING);

            _sb.AppendLine("<size=18><color=orange>ZLevels</color></size><size=14><color=#ce422b>REMASTERED</color></size>");

            AppendLine(colors.ACQUIRE, pointsPerHitCurrent.Get(Skills.ACQUIRE) * xpm, Skills.ACQUIRE);
            AppendLine(colors.MINING, m * xpm, Skills.MINING);
            AppendLine(colors.SKINNING, pointsPerHitCurrent.Get(Skills.SKINNING) * xpm, Skills.SKINNING);
            AppendLine(colors.WOODCUTTING, wc * xpm, Skills.WOODCUTTING);

            _sb.AppendLine($"<color={colors.CRAFTING}>Crafting</color> {(!IsSkillEnabled(Skills.CRAFTING) ? "(DISABLED)" : string.Empty)}");
            _sb.AppendLine($"XP gain: <color={colors.SKINNING}>You get {craftingDetails.xp} XP per {craftingDetails.time}s spent crafting.</color>");
            _sb.AppendLine($"Bonus: <color={colors.SKINNING}>Crafting time is decreased by {craftingDetails.percent}% per every level.</color>");

            SendReply(player, _sb.ToString());
            _sb.Clear();
        }

        private void AppendLine(string color, double xp, Skills skill)
        {
            var state = !IsSkillEnabled(skill) ? "(DISABLED)" : string.Empty;
            var bonus = (getGathMult(2, skill) - 1) * 100;

            _sb.AppendLine($"<color={color}>{skill}</color> {state}");
            _sb.AppendLine($"XP per hit: <color={color}>{xp}</color>");
            _sb.AppendLine($"Bonus materials per level: <color={color}>{bonus:#0.##} %</color>");
        }

        [ChatCommand("statsui")]
        private void StatsUICommand(BasePlayer player, string command, string[] args)
        {
            if (!hasRights(player.UserIDString)) return;
            var pi = GetPlayerInfo(player);
            if (pi.CUI)
            {
                DestroyGUI(player);
                pi.CUI = false;
            }
            else
            {
                pi.CUI = true;
                CreateGUI(player, pi);
            }
        }

        [ChatCommand("statsonoff")]
        private void StatsOnOffCommand(BasePlayer player, string command, string[] args)
        {
            if (!hasRights(player.UserIDString)) return;
            var pi = GetPlayerInfo(player);
            if (pi.ENABLED)
            {
                DestroyGUI(player);
                pi.ENABLED = false;
                Message(player, "PluginPlayerOff");
            }
            else
            {
                pi.ENABLED = true;
                Message(player, "PluginPlayerOn");
                if (pi.CUI)
                    CreateGUI(player, pi);
            }
        }

        #endregion Commands

        #region Helpers

        private void Unsubscribe()
        {
            Unsubscribe(nameof(OnCollectiblePickup));
            Unsubscribe(nameof(OnDispenserBonus));
            Unsubscribe(nameof(OnDispenserGather));
            Unsubscribe(nameof(OnEntityDeath));
            Unsubscribe(nameof(OnEntityKill));
            Unsubscribe(nameof(OnGrowableGathered));
            Unsubscribe(nameof(OnItemCraft));
            Unsubscribe(nameof(OnItemCraftFinished));
            Unsubscribe(nameof(OnPlayerConnected));
            Unsubscribe(nameof(OnPlayerDisconnected));
            Unsubscribe(nameof(OnPlayerRespawned));
            Unsubscribe(nameof(OnPlayerSleep));
            Unsubscribe(nameof(OnPlayerSleepEnded));
            Unsubscribe(nameof(OnTimeSunrise));
            Unsubscribe(nameof(OnTimeSunset));
            Unsubscribe(nameof(Unload));
        }

        private void RegisterPermissions()
        {
            foreach (var name in config.generic.vip.Keys)
            {
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }
                if (name.Contains("."))
                {
                    if (!permission.PermissionExists(name))
                    {
                        permission.RegisterPermission(name, this);
                    }
                }
                else
                {
                    if (!permission.GroupExists(name))
                    {
                        permission.CreateGroup(name, name, 0);
                    }
                }
            }

            permission.RegisterPermission(config.generic.nowipe, this);
            permission.RegisterPermission(config.generic.permissionName, this);
            permission.RegisterPermission(config.generic.permissionNameXP, this);
            permission.RegisterPermission(config.generic.AllowChainsawGather, this);
            permission.RegisterPermission(config.generic.AllowJackhammerGather, this);
            permission.RegisterPermission(config.generic.BlockWeaponsGather, this);
            permission.RegisterPermission(config.settings.craftingDetails.Permission, this);
        }

        private void RegisterCommands()
        {
            AddCovalenceCommand("zl.pointsperhit", nameof(PointsPerHitCommand));
            AddCovalenceCommand("zl.playerxpm", nameof(PlayerXpmCommand));
            AddCovalenceCommand("zl.info", nameof(InfoCommand));
            AddCovalenceCommand("zl.reset", nameof(ResetCommand));
            AddCovalenceCommand("zl.lvl", nameof(ZlvlCommand));
            AddCovalenceCommand("check_zlevel_datafile", nameof(CheckCommand));
            AddCovalenceCommand("clean_zlevel_datafile", nameof(CleanCommand));
            AddCovalenceCommand("zl.toggledeathpenalty", nameof(PenaltyCommand));
        }

        private void Subscribe()
        {
            if (config.generic.penaltyOnDeath || config.generic.penaltyOnSuicide)
            {
                Subscribe(nameof(OnEntityDeath));
            }

            if (config.functions.enableCollectiblePickup)
            {
                Subscribe(nameof(OnCollectiblePickup));
            }

            if (config.functions.enableCropGather)
            {
                Subscribe(nameof(OnGrowableGathered));
            }

            if (config.functions.enableDispenserGather)
            {
                Subscribe(nameof(OnDispenserBonus));
                Subscribe(nameof(OnDispenserGather));
            }

            if (IsCraftingEnabled())
            {
                Subscribe(nameof(OnItemCraft));
                Subscribe(nameof(OnItemCraftFinished));
            }

            if (config.nightbonus.enableNightBonus)
            {
                Plugin plugin = plugins.Find("TimeOfDay");
                if (plugin != null && plugin.IsLoaded)
                {
                    Subscribe(nameof(OnTimeSunrise));
                    Subscribe(nameof(OnTimeSunset));
                }
                else if (TOD_Sky.Instance?.Components?.Time != null)
                {
                    TOD_Sky.Instance.Components.Time.OnSunrise += OnTimeSunrise;
                    TOD_Sky.Instance.Components.Time.OnSunset += OnTimeSunset;
                }
            }

            Subscribe(nameof(OnPlayerConnected));
            Subscribe(nameof(OnPlayerDisconnected));
            Subscribe(nameof(OnPlayerRespawned));
            Subscribe(nameof(OnPlayerSleep));
            Subscribe(nameof(OnPlayerSleepEnded));
            Subscribe(nameof(OnEntityKill));
            Subscribe(nameof(Unload));

            timer.Repeat(300f, 0, SaveData);
        }

        private void CheckCollectible()
        {
            var collectList = Resources.FindObjectsOfTypeAll<CollectibleEntity>().Select(c => c.ShortPrefabName).Distinct().ToList();
            if (collectList == null || collectList.Count == 0)
            {
                return;
            }

            if (config.functions.enabledCollectibleEntity == null)
            {
                config.functions.enabledCollectibleEntity = new();
            }

            bool updated = false;
            foreach (var collect in collectList)
            {
                if (!config.functions.enabledCollectibleEntity.ContainsKey(collect))
                {
                    config.functions.enabledCollectibleEntity.Add(collect, true);
                    updated = true;
                }
            }

            if (updated)
            {
                SaveConfig();
            }
        }

        private void SaveData()
        {
            if (data != null)
            {
                Interface.Oxide.DataFileSystem.WriteObject(Name, data);
            }
        }

        private void SaveDetails()
        {
            if (_craftData != null)
            {
                Interface.Oxide.DataFileSystem.WriteObject("ZLevelsCraftDetails", _craftData);
            }
        }

        private void LoadData()
        {
            try
            {
                _craftData = Interface.Oxide.DataFileSystem.ReadObject<CraftData>("ZLevelsCraftDetails");
            }
            catch (Exception ex)
            {
                Puts("CraftData threw an exception!");
                Puts(ex.ToString());
            }

            if (_craftData == null)
            {
                Puts("Crafting data is null and has been reset!");
                _craftData = new();
            }

            if (_craftData.CraftList == null)
            {
                Puts("Crafting list is null and has been reset!");
                _craftData.CraftList = new();
            }

            if (_craftData.CraftList.Count == 0)
            {
                GenerateItems(true);
            }

            CheckCollectible();

            try
            {
                data = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            }
            catch (Exception ex)
            {
                Puts("StoredData threw an exception!");
                Puts(ex.ToString());
            }

            if (data == null || data.PlayerInfo == null)
            {
                Puts("Data is null, resetting to defaults!");
                data = new();
            }
            else if (newSaveDetected)
            {
                Puts("Wiping data... new save detected");
                WipeData();
            }
        }

        private void WipeData()
        {
            if (string.IsNullOrEmpty(config.generic.nowipe))
            {
                data = new();
                return;
            }
            foreach (var pair in data.PlayerInfo.ToList())
            {
                if (permission.UserHasPermission(pair.Key.ToString(), config.generic.nowipe))
                {
                    continue;
                }
                data.PlayerInfo.Remove(pair.Key);
            }
        }

        private bool hasRights(string UserIDString)
        {
            return !config.generic.enablePermission || permission.UserHasPermission(UserIDString, config.generic.permissionName);
        }

        private void editMultiplierForPlayer(double multiplier, ulong userID = ulong.MinValue)
        {
            if (userID == ulong.MinValue)
            {
                foreach (var p in data.PlayerInfo.ToList())
                    data.PlayerInfo[p.Key].XP_MULTIPLIER = multiplier;
                return;
            }
            if (data.PlayerInfo.TryGetValue(userID, out var playerData))
                playerData.XP_MULTIPLIER = multiplier;
        }

        private void adminModifyPlayerStats(IPlayer user, Skills skill, double level, int mode, IPlayer target)
        {
            var id = Convert.ToUInt64(target.Id);
            var pi = GetPlayerInfo(id);

            if (skill == Skills.ALL)
            {
                _sb.Clear();
                foreach (var currSkill in AllSkills)
                {
                    if (!IsSkillEnabled(currSkill)) continue;
                    var modifiedLevel = getLevel(pi, currSkill);
                    if (mode == 0) // SET
                        modifiedLevel = level;
                    else if (mode == 1) // ADD
                        modifiedLevel += level;
                    else if (mode == 2) // SUBTRACT
                        modifiedLevel -= level;
                    else if (mode == 4) // DIVIDE
                        modifiedLevel /= level;
                    if (modifiedLevel < 1)
                        modifiedLevel = 1;
                    var levelCap = config.settings.levelCaps.Get(currSkill);
                    if (modifiedLevel > levelCap && levelCap != 0)
                        modifiedLevel = levelCap;
                    setPointsAndLevel(pi, currSkill, getLevelPoints(modifiedLevel), modifiedLevel);
                    var basePlayer = BasePlayer.FindByID(id);
                    if (basePlayer != null) CreateGUI(basePlayer, GetPlayerInfo(basePlayer));
                    _sb.Append($"({_(currSkill + "Skill")} > {modifiedLevel}) ");
                }
                user.Reply($"\nChanges for '{target.Name}': " + _sb.ToString().TrimEnd());
            }
            else
            {
                var modifiedLevel = getLevel(pi, skill);
                if (mode == 0) // SET
                    modifiedLevel = level;
                else if (mode == 1) // ADD
                    modifiedLevel += level;
                else if (mode == 2) // SUBTRACT
                    modifiedLevel -= level;
                else if (mode == 4) // DIVIDE
                    modifiedLevel /= level;
                if (modifiedLevel < 1)
                    modifiedLevel = 1;
                var levelCap = config.settings.levelCaps.Get(skill);
                if (modifiedLevel > levelCap && levelCap != 0)
                {
                    modifiedLevel = levelCap;
                }
                setPointsAndLevel(pi, skill, getLevelPoints(modifiedLevel), modifiedLevel);
                var basePlayer = BasePlayer.FindByID(id);
                if (basePlayer != null) GUIUpdateSkill(basePlayer, skill);
                user.Reply($"{_(skill + "Skill")} Lvl for [{target.Name}] set to: [{modifiedLevel}]");
            }
        }

        private string getStatPrint(BasePlayer player, Skills skill)
        {
            if (!IsSkillEnabled(skill))
                return string.Empty;

            var pi = GetPlayerInfo(player);
            var levelCap = config.settings.levelCaps.Get(skill);
            var skillMaxed = levelCap != 0 && getLevel(pi, skill) == levelCap;

            string bonusText;
            if (skill == Skills.CRAFTING)
                bonusText = $"{(getLevel(pi, skill) * config.settings.craftingDetails.percent):#0.##}";
            else
                bonusText = $"{((getGathMult(getLevel(pi, skill), skill) - 1) * 100.0):#0.##}";

            string format = $"<color={config.settings.colors.Get(skill)}>{_("StatsText", player.UserIDString)}</color>\n";
            var skillName = _(skill + "Skill", player.UserIDString);
            var xp = $"{getLevel(pi, skill):0}" + (levelCap > 0 ? ("/" + $"{levelCap:0}") : string.Empty);
            var points = $"{getPoints(pi, skill):0}";
            var level = skillMaxed ? "∞" : $"{getLevelPoints(getLevel(pi, skill) + 1):0}";
            var percent = $"{getExperiencePercent(pi, skill):0}%";
            var penalty = $"{getPenaltyPercent(player, pi, skill, false):0}%";
            return string.Format(format, skillName, xp, points, level, bonusText, percent, penalty);
        }

        private void removePoints(PlayerInfo pi, Skills skill, double points)
        {
            switch (skill)
            {
                case Skills.ACQUIRE:
                    pi.ACQUIRE_POINTS = pi.ACQUIRE_POINTS - 10.0 > points ? pi.ACQUIRE_POINTS - points : 10.0;
                    pi.ACQUIRE_LEVEL = getPointsLevel(pi.ACQUIRE_POINTS, skill);
                    break;
                case Skills.CRAFTING:
                    pi.CRAFTING_POINTS = pi.CRAFTING_POINTS - 10.0 > points ? pi.CRAFTING_POINTS - points : 10.0;
                    pi.CRAFTING_LEVEL = getPointsLevel(pi.CRAFTING_POINTS, skill);
                    break;
                case Skills.MINING:
                    pi.MINING_POINTS = pi.MINING_POINTS - 10.0 > points ? pi.MINING_POINTS - points : 10.0;
                    pi.MINING_LEVEL = getPointsLevel(pi.MINING_POINTS, skill);
                    break;
                case Skills.SKINNING:
                    pi.SKINNING_POINTS = pi.SKINNING_POINTS - 10.0 > points ? pi.SKINNING_POINTS - points : 10.0;
                    pi.SKINNING_LEVEL = getPointsLevel(pi.SKINNING_POINTS, skill);
                    break;
                case Skills.WOODCUTTING:
                    pi.WOODCUTTING_POINTS = pi.WOODCUTTING_POINTS - 10.0 > points ? pi.WOODCUTTING_POINTS - points : 10.0;
                    pi.WOODCUTTING_LEVEL = getPointsLevel(pi.WOODCUTTING_POINTS, skill);
                    break;
            }
        }

        private double GetPenalty(BasePlayer player, PlayerInfo pi, Skills skill, bool isSuicide)
        {
            double penaltyPercent = getPenaltyPercent(player, pi, skill, isSuicide);

            return skill switch
            {
                Skills.ACQUIRE => config.generic.penaltyReset ? pi.ACQUIRE_POINTS : getPercentAmount(pi.ACQUIRE_LEVEL, penaltyPercent),
                Skills.CRAFTING => config.generic.penaltyReset ? pi.CRAFTING_POINTS : getPercentAmount(pi.CRAFTING_LEVEL, penaltyPercent),
                Skills.MINING => config.generic.penaltyReset ? pi.MINING_POINTS : getPercentAmount(pi.MINING_LEVEL, penaltyPercent),
                Skills.SKINNING => config.generic.penaltyReset ? pi.SKINNING_POINTS : getPercentAmount(pi.SKINNING_LEVEL, penaltyPercent),
                Skills.WOODCUTTING => config.generic.penaltyReset ? pi.WOODCUTTING_POINTS : getPercentAmount(pi.WOODCUTTING_LEVEL, penaltyPercent),
                _ => 0,
            };
        }

        private double getPenaltyPercent(BasePlayer player, PlayerInfo pi, Skills skill, bool isSuicide)
        {
            var penaltyPercent = 0.0;
            var details = pi.LAST_DEATH;
            var currentTime = DateTime.UtcNow;
            var lastDeath = ToDateTimeFromEpoch(details);
            var timeAlive = currentTime - lastDeath;
            if (timeAlive.TotalMinutes >= (isSuicide ? config.generic.penaltySuicideMinutes : config.generic.penaltyMinutes))
            {
                var percent = isSuicide ? config.settings.percentLostOnSuicide.Get(skill) : config.settings.percentLostOnDeath.Get(skill);
                if (percent == 0)
                    return 0;
                penaltyPercent = percent - (timeAlive.TotalHours * percent / 10.0);
                if (penaltyPercent < 0)
                    penaltyPercent = 0;
            }
            return Math.Round(penaltyPercent, 2);
        }

        private int levelHandler(PlayerInfo pi, BasePlayer player, int prevAmount, Skills skill, BaseEntity source, bool isPowerTool = false)
        {
            object extCanGainZLevelXP = Interface.CallHook("CanGainXP", new object[] { player, source });

            if (extCanGainZLevelXP is bool && !(bool)extCanGainZLevelXP)
            {
                return prevAmount;
            }

            var xpPercentBefore = getExperiencePercent(pi, skill);
            var currentLevel = getLevel(pi, skill);
            var newPoints = getPoints(pi, skill);
            var newAmount = Mathf.CeilToInt(prevAmount * (float)getGathMult(currentLevel, skill));
            var pointsToGet = isPowerTool ? pointsPerHitPowerToolCurrent.Get(skill) : pointsPerHitCurrent.Get(skill);
            newPoints += pointsToGet * (getXpMulti(pi, player.UserIDString) / 100.0);

            if (newPoints >= getLevelPoints(currentLevel + 1))
            {
                var levelCap = config.settings.levelCaps.Get(skill);
                var maxLevel = levelCap > 0 && currentLevel + 1 > levelCap;

                if (!maxLevel)
                {
                    currentLevel = getPointsLevel(newPoints, skill);

                    var levelPoints = getLevelPoints(currentLevel + 1);
                    var color = config.settings.colors.Get(skill);
                    var skillName = _(skill + "Skill", player.UserIDString);
                    var gatherMulti = ((getGathMult(currentLevel, skill) - 1) * 100.0).ToString("0.##");
                    var format = $"<color={color}>{_("LevelUpText", player.UserIDString)}</color>";

                    PrintToChat(player, string.Format(format, skillName, currentLevel, newPoints, levelPoints, gatherMulti));
                    BroadcastLevel(player, skill, color, currentLevel);
                    GiveReward(player, skill, currentLevel);
                }
            }

            setPointsAndLevel(pi, skill, newPoints, currentLevel);
            var xpPercentAfter = getExperiencePercent(pi, skill);
            if (xpPercentAfter != xpPercentBefore)
                GUIUpdateSkill(player, skill);
            return newAmount;
        }

        private void BroadcastLevel(BasePlayer player, Skills skill, string color, double Level)
        {
            if (config.generic.enableLevelupBroadcast && (int)Level % 10 == 0)
            {
                foreach (var target in BasePlayer.activePlayerList)
                {
                    if (target.userID == player.userID || !hasRights(target.UserIDString) || !GetPlayerInfo(target.userID).ENABLED)
                        continue;

                    Message(target, "LevelUpTextBroadcast", player.displayName, Level, color, _(skill + "Skill", target.UserIDString));
                }
            }
        }

        private double getExperiencePercentProc(PlayerInfo pi, Skills skill)
        {
            var Level = getLevel(pi, skill);
            var startingPoints = getLevelPoints(Level);
            var nextLevelPoints = getLevelPoints(Level + 1) - startingPoints;
            var Points = getPoints(pi, skill) - startingPoints;
            var experienceProc = Math.Round((Points / nextLevelPoints) * 100.0, 2);
            return experienceProc;
        }

        private double getExperiencePercent(PlayerInfo pi, Skills skill)
        {
            var experienceProc = getExperiencePercentProc(pi, skill);
            if (experienceProc >= 100)
                experienceProc = 99.99;
            return experienceProc;
        }

        private void setPointsAndLevel(PlayerInfo pi, Skills skill, double points, double level)
        {
            double levelPoints = getLevelPoints(level);

            switch (skill)
            {
                case Skills.ACQUIRE:
                    pi.ACQUIRE_LEVEL = level;
                    pi.ACQUIRE_POINTS = points <= 0.0 ? levelPoints : points;
                    break;
                case Skills.CRAFTING:
                    pi.CRAFTING_LEVEL = level;
                    pi.CRAFTING_POINTS = points <= 0.0 ? levelPoints : points;
                    break;
                case Skills.MINING:
                    pi.MINING_LEVEL = level;
                    pi.MINING_POINTS = points <= 0.0 ? levelPoints : points;
                    break;
                case Skills.SKINNING:
                    pi.SKINNING_LEVEL = level;
                    pi.SKINNING_POINTS = points <= 0.0 ? levelPoints : points;
                    break;
                case Skills.WOODCUTTING:
                    pi.WOODCUTTING_LEVEL = level;
                    pi.WOODCUTTING_POINTS = points <= 0.0 ? levelPoints : points;
                    break;
            }
        }

        private double getLevel(PlayerInfo pi, Skills skill)
        {
            switch (skill)
            {
                case Skills.ACQUIRE:
                    return pi.ACQUIRE_LEVEL;
                case Skills.CRAFTING:
                    return pi.CRAFTING_LEVEL;
                case Skills.MINING:
                    return pi.MINING_LEVEL;
                case Skills.SKINNING:
                    return pi.SKINNING_LEVEL;
                case Skills.WOODCUTTING:
                    return pi.WOODCUTTING_LEVEL;
            }

            return 0.0;
        }

        private double getPoints(PlayerInfo pi, Skills skill)
        {
            switch (skill)
            {
                case Skills.ACQUIRE:
                    return pi.ACQUIRE_POINTS;
                case Skills.CRAFTING:
                    return pi.CRAFTING_POINTS;
                case Skills.MINING:
                    return pi.MINING_POINTS;
                case Skills.SKINNING:
                    return pi.SKINNING_POINTS;
                case Skills.WOODCUTTING:
                    return pi.WOODCUTTING_POINTS;
            }

            return 0.0;
        }

        private double getLevelPoints(double level) => Math.Round(110 * level * level - 100 * level, 2); // TODO

        private double getPointsLevel(double points, Skills skill)
        {
            var a = 110;
            var b = 100;
            var c = -points;
            var x1 = (-b - Math.Sqrt(b * b - 4 * a * c)) / (2 * a);
            var levelCap = config.settings.levelCaps.Get(skill);
            if ((int)levelCap == 0 || (int)-x1 <= (int)levelCap)
                return (int)-x1;
            return Math.Round(levelCap, 2);
        }

        private double getGathMult(double skillLevel, Skills skill)
        {
            return Math.Round(config.settings.defaultMultipliers.Get(skill) + resourceMultipliersCurrent.Get(skill) * 0.1d * (skillLevel - 1.0), 2);
        }

        private double getXpMulti(PlayerInfo pi, string userid)
        {
            double baseMultiplier = pi.XP_MULTIPLIER;
            double highestMultiplier = double.MinValue;
            foreach (var (name, multiplier) in config.generic.vip)
            {
                if (string.IsNullOrEmpty(name) || multiplier < highestMultiplier)
                {
                    continue;
                }
                if (name.Contains('.') ? permission.UserHasPermission(userid, name) : permission.UserHasGroup(userid, name))
                {
                    highestMultiplier = multiplier;
                }
            }
            if (highestMultiplier == double.MinValue)
            {
                highestMultiplier = 1.0;
            }
            return baseMultiplier * highestMultiplier;
        }

        private bool IsCraftingEnabled()
        {
            if (CraftingController != null) return false;
            return config.settings.levelCaps.Get(Skills.CRAFTING) != -1 || config.settings.craftingDetails.noLevelRequirement;
        }

        private bool IsSkillEnabled(Skills skill)
        {
            if (skill == Skills.CRAFTING && ConVar.Craft.instant)
            {
                return false;
            }
            return config.settings.levelCaps.Get(skill) != -1;
        }

        private double getPointsNeededForNextLevel(double level)
        {
            return getLevelPoints(level + 1) - getLevelPoints(level);
        }

        private double getPercentAmount(double level, double percent)
        {
            return (getPointsNeededForNextLevel(level) * percent) / 100.0;
        }

        private bool IsValid(BasePlayer player)
        {
            return player != null && player.userID.IsSteamId();
        }

        private bool isZoneExcluded(BasePlayer player)
        {
            if (config.settings.zones.Count == 0 || ZoneManager == null)
            {
                return false;
            }

            var values = (string[])ZoneManager?.Call("GetPlayerZoneIDs", player);

            return values?.Length > 0 && config.settings.zones.Any(values.Contains);
        }

        private string ReadableTimeSpan(TimeSpan span)
        {
            var formatted = string.Format("{0}{1}{2}{3}{4}",
                (span.Days / 7) > 0 ? string.Format("{0:0} weeks, ", span.Days / 7) : string.Empty,
                span.Days % 7 > 0 ? string.Format("{0:0} days, ", span.Days % 7) : string.Empty,
                span.Hours > 0 ? string.Format("{0:0} hours, ", span.Hours) : string.Empty,
                span.Minutes > 0 ? string.Format("{0:0} minutes, ", span.Minutes) : string.Empty,
                span.Seconds > 0 ? string.Format("{0:0} seconds, ", span.Seconds) : string.Empty);

            if (formatted.EndsWith(", ")) formatted = formatted.Substring(0, formatted.Length - 2);
            return formatted;
        }

        private long ToEpochTime(DateTime dateTime)
        {
            var date = dateTime.ToUniversalTime();
            var ticks = date.Ticks - new DateTime(1970, 1, 1, 0, 0, 0, 0).Ticks;
            var ts = ticks / TimeSpan.TicksPerSecond;
            return ts;
        }

        private DateTime ToDateTimeFromEpoch(double intDate)
        {
            var timeInTicks = (long)intDate * TimeSpan.TicksPerSecond;
            return new DateTime(1970, 1, 1, 0, 0, 0, 0).AddTicks(timeInTicks);
        }

        private string _(string key, string id = null) => lang.GetMessage(key, this, id);

        private void Message(BasePlayer player, string key, params object[] args)
        {
            string message = string.Format(_(key, player.UserIDString), args);

            Player.Message(player, message, string.IsNullOrEmpty(config.generic.pluginPrefix) ? string.Empty : config.generic.pluginPrefix, config.generic.steamIDIcon);
        }

        private ItemDefinition GetItem(string shortname)
        {
            if (string.IsNullOrEmpty(shortname) || CraftItems == null) return null;
            if (CraftItems.TryGetValue(shortname, out var item)) return item;
            return null;
        }

        private void GenerateItems(bool reset = false)
        {
            if (!reset)
            {
                var config_protocol = config.generic.gameProtocol;

                if (config_protocol != Protocol.network)
                {
                    config.generic.gameProtocol = Protocol.network;
                    Config["Generic", "gameProtocol"] = config.generic.gameProtocol;
                    Puts("Updating item list from protocol " + config_protocol + " to protocol " + config.generic.gameProtocol + ".");
                    GenerateItems(true);
                    SaveConfig();
                    return;
                }
            }

            if (reset)
            {
                Interface.Oxide.DataFileSystem.WriteObject("ZLevelsCraftDetails.old", _craftData);
                _craftData.CraftList.Clear();
                Puts("Generating new item list...");
            }

            CraftItems = ItemManager.GetItemDefinitions().ToDictionary(i => i.shortname);
            int loaded = 0, enabled = 0;
            foreach (var definition in CraftItems)
            {
                if (definition.Value.shortname.Length >= 1)
                {
                    if (_craftData.CraftList.TryGetValue(definition.Value.shortname, out var p))
                    {
                        if (p.Enabled) { enabled++; }
                        loaded++;
                    }
                    else
                    {
                        CraftInfo z = new()
                        {
                            shortName = definition.Value.shortname,
                            MaxBulkCraft = MaxB,
                            MinBulkCraft = MinB,
                            Enabled = true
                        };
                        _craftData.CraftList.Add(definition.Value.shortname, z);
                        loaded++;
                    }
                }
            }
            var inactive = loaded - enabled;
            Puts("Loaded {0} items (Enabled: {1} | Inactive: {2})", loaded, enabled, inactive);
            SaveDetails();
        }
        #endregion Helpers

        #region CUI

        private void GUIUpdateSkill(BasePlayer player, Skills skill)
        {
            if (!skillIndex.ContainsKey(skill)) return;
            double maxRows = skillIndex.Count;
            double rowNumber = skillIndex[skill];
            var pi = GetPlayerInfo(player);
            double level = getLevel(pi, skill);
            //If the player has the max level we don't care about the percentage
            bool isMaxLevel = level >= config.settings.levelCaps.Get(skill);
            double percent = isMaxLevel ? 100.0 : getExperiencePercent(pi, skill);
            var skillName = _(skill.ToString() + "Skill", player.UserIDString);
            var mainPanel = "ZL" + skillName;

            CuiHelper.DestroyUi(player, mainPanel);

            var value = 1 / maxRows;
            var positionMin = 1 - (value * rowNumber);
            var positionMax = 2 - (1 - (value * (1 - rowNumber)));

            var container = new CuiElementContainer()
            {
                {
                    new CuiPanel
                    {
                        Image = {Color = config.cui.cuiBoundsBackground},
                        RectTransform = { AnchorMin = "0 " + positionMin.ToString("0.####"), AnchorMax = $"1 "+ positionMax.ToString("0.####") },
                    },
                    new CuiElement().Parent = "ZLevelsUI",
                    mainPanel
                }
            };

            var innerXPBar1 = new CuiElement
            {
                Name = CuiHelper.GetGuid(),
                Parent = mainPanel,
                Components =
                {
                    new CuiImageComponent { Color = config.cui.cuiXpBarBackground },
                    new CuiRectTransformComponent{ AnchorMin = "0.190 0.05", AnchorMax = "0.750 0.8" }
                }
            };
            container.Add(innerXPBar1);

            var innerXPBarProgress1 = new CuiElement
            {
                Name = CuiHelper.GetGuid(),
                Parent = innerXPBar1.Name,
                Components =
                {
                    new CuiImageComponent() { Color = config.cui.cuiColors.Get(skill) },
                    new CuiRectTransformComponent{ AnchorMin = "0 0", AnchorMax = (percent / 100.0) + " 0.95" }
                }
            };
            container.Add(innerXPBarProgress1);

            if (config.cui.cuiTextShadow)
            {
                var innerXPBarTextShadow1 = new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = innerXPBar1.Name,
                    Components =
                    {
                        new CuiTextComponent { Color = "0.1 0.1 0.1 0.75", Text = skillName, FontSize = config.cui.cuiFontSizeBar, Align = TextAnchor.MiddleCenter},
                        new CuiRectTransformComponent{ AnchorMin = "0.035 -0.1", AnchorMax = "1 1" }
                    }
                };
                container.Add(innerXPBarTextShadow1);
            }

            var innerXPBarText1 = new CuiElement
            {
                Name = CuiHelper.GetGuid(),
                Parent = innerXPBar1.Name,
                Components =
                {
                    new CuiTextComponent { Color = config.cui.cuiFontColor, Text = skillName, FontSize = config.cui.cuiFontSizeBar, Align = TextAnchor.MiddleCenter},
                    new CuiRectTransformComponent{ AnchorMin = "0.05 0", AnchorMax = "1 1" }
                }
            };
            container.Add(innerXPBarText1);

            if (config.cui.cuiTextShadow)
            {
                var lvShader1 = new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = mainPanel,
                    Components =
                    {
                        new CuiTextComponent { Text = $"{_("Lv.", player.UserIDString)}{level:0}", FontSize = config.cui.cuiFontSizeLvl , Align = TextAnchor.MiddleLeft, Color = "0.1 0.1 0.1 0.75" },
                        new CuiRectTransformComponent{ AnchorMin = "0.035 -0.1", AnchorMax = $"0.5 1" }
                    }
                };
                container.Add(lvShader1);
            }

            var lvText1 = new CuiElement
            {
                Name = CuiHelper.GetGuid(),
                Parent = mainPanel,
                Components =
                {
                    new CuiTextComponent { Text = $"{_("Lv.", player.UserIDString)}{level:0}", FontSize = config.cui.cuiFontSizeLvl, Align = TextAnchor.MiddleLeft, Color = config.cui.cuiFontColor },
                    new CuiRectTransformComponent{ AnchorMin = "0.025 0", AnchorMax = $"0.5 1" }
                }
            };

            container.Add(lvText1);

            if (config.cui.cuiTextShadow)
            {
                var percShader1 = new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = mainPanel,
                    Components =
                    {
                        new CuiTextComponent { Text = isMaxLevel ? _("MAX", player.UserIDString) : $"{percent:N2}%", FontSize = config.cui.cuiFontSizePercent, Align = TextAnchor.MiddleRight, Color = "0.1 0.1 0.1 0.75" },
                        new CuiRectTransformComponent{ AnchorMin = "0.5 -0.1", AnchorMax = $"1 1" }
                    }
                };
                container.Add(percShader1);
            }

            var percText1 = new CuiElement
            {
                Name = CuiHelper.GetGuid(),
                Parent = mainPanel,
                Components =
                {
                    new CuiTextComponent { Text = isMaxLevel ? _("MAX", player.UserIDString) : $"{percent:N2}%", FontSize = config.cui.cuiFontSizePercent, Align = TextAnchor.MiddleRight, Color = config.cui.cuiFontColor },
                    new CuiRectTransformComponent{ AnchorMin = "0.5 0", AnchorMax = $"1 1" }
                }
            };
            container.Add(percText1);
            CuiHelper.AddUi(player, container);
        }

        private void DestroyGUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "ZLevelsUI");
        }

        private void CreateGUI(BasePlayer player, PlayerInfo pi)
        {
            if (!config.cui.cuiEnabled || pi == null || !pi.ENABLED || !pi.CUI || !hasRights(player.UserIDString) || !player.IsAlive() || player.IsSleeping())
            {
                return;
            }

            var panelName = "ZLevelsUI";
            CuiHelper.DestroyUi(player, panelName);
            var mainContainer = new CuiElementContainer()
            {
                {
                    new CuiPanel
                    {
                        Image = {Color = "0 0 0 0"},
                        RectTransform = {AnchorMin = $"{config.cui.cuiPositioning.widthLeft} {config.cui.cuiPositioning.heightLower}", AnchorMax =$"{config.cui.cuiPositioning.widthRight} {config.cui.cuiPositioning.heightUpper}"},
                        CursorEnabled = false
                    },
                    new CuiElement().Parent = "Under",
                    panelName
                }
            };
            CuiHelper.AddUi(player, mainContainer);
            foreach (Skills skill in AllSkills)
                if (IsSkillEnabled(skill))
                    GUIUpdateSkill(player, skill);
        }

        private PlayerInfo GetPlayerInfo(BasePlayer player)
        {
            if (!player.userID.IsSteamId())
            {
                return null;
            }

            return GetPlayerInfo(player.userID);
        }

        private PlayerInfo CreatePlayerInfo()
        {
            return new()
            {
                ACQUIRE_LEVEL = config.settings.stats.acquire_level,
                ACQUIRE_POINTS = config.settings.stats.acquire_points,
                CRAFTING_LEVEL = config.settings.stats.crafting_level,
                CRAFTING_POINTS = config.settings.stats.crafting_points,
                MINING_LEVEL = config.settings.stats.mining_level,
                MINING_POINTS = config.settings.stats.mining_points,
                SKINNING_LEVEL = config.settings.stats.skinning_level,
                SKINNING_POINTS = config.settings.stats.skinning_points,
                WOODCUTTING_LEVEL = config.settings.stats.woodcutting_level,
                WOODCUTTING_POINTS = config.settings.stats.woodcutting_points,
                XP_MULTIPLIER = config.settings.stats.xpm
            };
        }

        private PlayerInfo GetPlayerInfo(ulong userID)
        {
            if (!data.PlayerInfo.TryGetValue(userID, out var pi))
            {
                data.PlayerInfo[userID] = pi = CreatePlayerInfo();

                pi.LAST_DEATH = ToEpochTime(DateTime.UtcNow);
                pi.CUI = config.generic.playerCuiDefaultEnabled;
                pi.ENABLED = config.generic.playerPluginDefaultEnabled;
            }

            return pi;
        }

        #endregion CUI

        #region Config

        private class Configuration
        {
            [JsonProperty(PropertyName = "CUI")]
            public ConfigurationCui cui { get; set; } = new();

            [JsonProperty(PropertyName = "Functions")]
            public ConfigurationFunctions functions { get; set; } = new();

            [JsonProperty(PropertyName = "Generic")]
            public ConfigurationGeneric generic { get; set; } = new();

            [JsonProperty(PropertyName = "Night Bonus")]
            public ConfigurationNightBonus nightbonus { get; set; } = new();

            [JsonProperty(PropertyName = "Settings")]
            public ConfigurationSettings settings { get; set; } = new();

            [JsonProperty(PropertyName = "Level Up Rewards (Reward * Level = Amount)")]
            public Rewards Rewards { get; set; } = new();
        }

        private class ConfigurationCui
        {
            [JsonProperty(PropertyName = "Bounds")]
            public ConfigurationCuiPositions cuiPositioning { get; set; } = new("0.725", "0.83", "0.02", "0.1225");

            [JsonProperty(PropertyName = "Xp Bar Colors")]
            public ConfigurationColors cuiColors { get; set; } = new("0.4 0 0.8 0.5", "0 1 0 0.5", "0 0 1 0.5", "1 0 0 0.5", "1 0.6 0 0.5");

            [JsonProperty(PropertyName = "Bounds Background")]
            public string cuiBoundsBackground { get; set; } = "0.1 0.1 0.1 0.1";

            [JsonProperty(PropertyName = "CUI Enabled")]
            public bool cuiEnabled { get; set; } = true;

            [JsonProperty(PropertyName = "Font Color")]
            public string cuiFontColor { get; set; } = "0.74 0.76 0.78 1";

            [JsonProperty(PropertyName = "FontSize Bar")]
            public int cuiFontSizeBar { get; set; } = 11;

            [JsonProperty(PropertyName = "FontSize Level")]
            public int cuiFontSizeLvl { get; set; } = 11;

            [JsonProperty(PropertyName = "FontSize Percent")]
            public int cuiFontSizePercent { get; set; } = 11;

            [JsonProperty(PropertyName = "Text Shadow Enabled")]
            public bool cuiTextShadow { get; set; } = true;

            [JsonProperty(PropertyName = "Xp Bar Background")]
            public string cuiXpBarBackground { get; set; } = "0.2 0.2 0.2 0.2";
        }

        private class ConfigurationFunctions
        {
            [JsonProperty(PropertyName = "Collectible Entities", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, bool> enabledCollectibleEntity { get; set; } = new();

            [JsonProperty(PropertyName = "Enable Collectible Pickup")]
            public bool enableCollectiblePickup { get; set; } = true;

            [JsonProperty(PropertyName = "Enable Crop Gather")]
            public bool enableCropGather { get; set; } = true;

            [JsonProperty(PropertyName = "Enable Wood Gather")]
            public bool wood { get; set; } = true;

            [JsonProperty(PropertyName = "Grant Wood XP Only")]
            public bool woodXpOnly { get; set; }

            [JsonProperty(PropertyName = "Enable Stone Ore Gather")]
            public bool stone { get; set; } = true;

            [JsonProperty(PropertyName = "Grant Stone XP Only")]
            public bool stoneXpOnly { get; set; }

            [JsonProperty(PropertyName = "Enable Sulfur Ore Gather")]
            public bool sulfur { get; set; } = true;

            [JsonProperty(PropertyName = "Grant Sulfur XP Only")]
            public bool sulfurXpOnly { get; set; }

            [JsonProperty(PropertyName = "Enable Metal Gather")]
            public bool metal { get; set; } = true;

            [JsonProperty(PropertyName = "Grant Metal XP Only")]
            public bool metalXpOnly { get; set; }

            [JsonProperty(PropertyName = "Enable HQM Gather")]
            public bool hqm { get; set; } = true;

            [JsonProperty(PropertyName = "Grant HQM XP Only")]
            public bool hqmXpOnly { get; set; }

            [JsonProperty(PropertyName = "Allow Mining Multiplier On Gibs")]
            public bool gibs { get; set; } = true;

            internal bool enableDispenserGather => wood || stone || sulfur || metal || hqm || woodXpOnly || stoneXpOnly || sulfurXpOnly || metalXpOnly || hqmXpOnly;

            public bool ShouldAllowGather(string shortName, out bool grantXPOnly)
            {
                (bool main, bool xpOnly) = shortName switch
                {
                    "wood" => (wood, woodXpOnly),
                    "sulfur.ore" => (sulfur, sulfurXpOnly),
                    "stones" => (stone, stoneXpOnly),
                    "metal.ore" => (metal, metalXpOnly),
                    "hq.metal.ore" => (hqm, hqmXpOnly),
                    _ => (true, false)
                };

                grantXPOnly = !main && xpOnly;
                return main || xpOnly;
            }
        }

        private class ConfigurationGeneric
        {
            [JsonProperty(PropertyName = "Enable Level Up Broadcast")]
            public bool enableLevelupBroadcast { get; set; }

            [JsonProperty(PropertyName = "Enable Permission")]
            public bool enablePermission { get; set; }

            [JsonProperty(PropertyName = "Chainsaw On Gather Permission")]
            public string AllowChainsawGather { get; set; } = "zlevelsremastered.chainsaw.allowed";

            [JsonProperty(PropertyName = "Jackhammer On Gather Permission")]
            public string AllowJackhammerGather { get; set; } = "zlevelsremastered.jackhammer.allowed";

            [JsonProperty(PropertyName = "Weapons On Gather Permission")]
            public string BlockWeaponsGather { get; set; } = "zlevelsremastered.weapons.blocked";

            [JsonProperty(PropertyName = "gameProtocol")]
            public int gameProtocol { get; set; } = Protocol.network;

            [JsonProperty(PropertyName = "Penalty Minutes")]
            public int penaltyMinutes { get; set; } = 10;

            [JsonProperty(PropertyName = "Penalty Minutes (Suicide)")]
            public int penaltySuicideMinutes { get; set; } = 10;

            [JsonProperty(PropertyName = "Penalty Resets All Levels To Default")]
            public bool penaltyReset { get; set; }

            [JsonProperty(PropertyName = "Penalty On Death")]
            public bool penaltyOnDeath { get; set; } = true;

            [JsonProperty(PropertyName = "Penalty On Suicide")]
            public bool penaltyOnSuicide { get; set; }

            [JsonProperty(PropertyName = "Permission Name")]
            public string permissionName { get; set; } = "zlevelsremastered.use";

            [JsonProperty(PropertyName = "Permission Name XP")]
            public string permissionNameXP { get; set; } = "zlevelsremastered.noxploss";

            [JsonProperty(PropertyName = "Additional Boost Multipliers")]
            public Dictionary<string, double> vip { get; set; } = new()
            {
                ["zlevelsremastered.vip1"] = 2.0,
                ["zlevelsremastered.vip2"] = 3.0,
                ["zlevelsremastered.vip3"] = 4.0,
                ["zlevelsvip1"] = 5.0,
                ["zlevelsvip2"] = 6.0,
                ["zlevelsvip3"] = 7.0,
            };

            [JsonProperty(PropertyName = "Permission Name No Wipes")]
            public string nowipe { get; set; } = "zlevelsremastered.nowipes";

            [JsonProperty(PropertyName = "Player CUI Default Enabled")]
            public bool playerCuiDefaultEnabled { get; set; } = true;

            [JsonProperty(PropertyName = "Player Plugin Default Enabled")]
            public bool playerPluginDefaultEnabled { get; set; } = true;

            [JsonProperty(PropertyName = "Plugin Prefix")]
            public string pluginPrefix { get; set; } = "<color=orange>ZLevels</color>: ";

            [JsonProperty(PropertyName = "SteamID Icon")]
            public ulong steamIDIcon { get; set; }

            [JsonProperty(PropertyName = "Wipe Data OnNewSave")]
            public bool wipeDataOnNewSave { get; set; }
        }

        private class ConfigurationNightBonus
        {
            [JsonProperty(PropertyName = "Points Per Hit At Night")]
            public ConfigurationResources pointsPerHitAtNight { get; set; } = new(60, null, 60, 60, 60);

            [JsonProperty(PropertyName = "Points Per PowerTool At Night")]
            public ConfigurationResources pointsPerPowerToolAtNight { get; set; } = new(null, null, 60, null, 60);

            [JsonProperty(PropertyName = "Resource Per Level Multiplier At Night")]
            public ConfigurationResources resourceMultipliersAtNight { get; set; } = new(2, null, 2, 2, 2);

            [JsonProperty(PropertyName = "Enable Night Bonus")]
            public bool enableNightBonus { get; set; }

            [JsonProperty(PropertyName = "Broadcast Enabled Bonus")]
            public bool broadcastEnabledBonus { get; set; } = true;

            [JsonProperty(PropertyName = "Log Enabled Bonus Console")]
            public bool logEnabledBonusConsole { get; set; }
        }

        private class ConfigurationSettings
        {
            [JsonProperty(PropertyName = "Crafting Details", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public ConfigurationCraftingDetails craftingDetails { get; set; } = new(1, 3, 5);

            [JsonProperty(PropertyName = "Default Resource Multiplier")]
            public ConfigurationResources defaultMultipliers { get; set; } = new(1, null, 1, 1, 1);

            [JsonProperty(PropertyName = "Level Caps")]
            public ConfigurationResources levelCaps { get; set; } = new(200, 20, 200, 200, 200);

            [JsonProperty(PropertyName = "Percent Lost On Death")]
            public ConfigurationResources percentLostOnDeath { get; set; } = new(50, 50, 50, 50, 50);

            [JsonProperty(PropertyName = "Percent Lost On Suicide")]
            public ConfigurationResources percentLostOnSuicide { get; set; } = new(0, 0, 0, 0, 0);

            [JsonProperty(PropertyName = "No Penalty Zones", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> zones { get; set; } = new() { "adminzone1", "999999" };

            [JsonProperty(PropertyName = "Points Per Hit")]
            public ConfigurationResources pointsPerHit { get; set; } = new(30, null, 30, 30, 30);

            [JsonProperty(PropertyName = "Points Per Power Tool")]
            public ConfigurationResources pointsPerPowerTool { get; set; } = new(null, null, 30, null, 30);

            [JsonProperty(PropertyName = "Resource Per Level Multiplier")]
            public ConfigurationResources resourceMultipliers { get; set; } = new(2, null, 2, 2, 2);

            [JsonProperty(PropertyName = "Skill Colors")]
            public ConfigurationSkillColors colors { get; set; } = new();

            [JsonProperty(PropertyName = "Starting Stats")]
            public ConfigurationStartingStats stats { get; set; } = new();

            [JsonProperty(PropertyName = "Use Mining Skill When Picking Up Stones And Ore")]
            public bool MiningStonesOre { get; set; } = true;

            [JsonProperty(PropertyName = "Use Acquire Skill When Picking Up Wood")]
            public bool AcquireWood { get; set; }
        }

        public class ConfigurationStartingStats
        {
            [JsonProperty(PropertyName = "Acquire Level")]
            public double acquire_level { get; set; } = 1.0;

            [JsonProperty(PropertyName = "Acquire Points")]
            public double acquire_points { get; set; } = 10.0;

            [JsonProperty(PropertyName = "Crafting Level")]
            public double crafting_level { get; set; } = 1.0;

            [JsonProperty(PropertyName = "Crafting Points")]
            public double crafting_points { get; set; } = 10.0;

            [JsonProperty(PropertyName = "Mining Level")]
            public double mining_level { get; set; } = 1.0;

            [JsonProperty(PropertyName = "Mining Points")]
            public double mining_points { get; set; } = 10.0;

            [JsonProperty(PropertyName = "Skinning Level")]
            public double skinning_level { get; set; } = 1.0;

            [JsonProperty(PropertyName = "Skinning Points")]
            public double skinning_points { get; set; } = 10.0;

            [JsonProperty(PropertyName = "Woodcutting Level")]
            public double woodcutting_level { get; set; } = 1.0;

            [JsonProperty(PropertyName = "Woodcutting Points")]
            public double woodcutting_points { get; set; } = 10.0;

            [JsonProperty(PropertyName = "XP Multiplier")]
            public double xpm { get; set; } = 100.0;

        }

        public class ConfigurationResources
        {
            [JsonProperty(PropertyName = nameof(Skills.ACQUIRE), NullValueHandling = NullValueHandling.Ignore)]
            public double? ACQUIRE { get; set; }

            [JsonProperty(PropertyName = nameof(Skills.CRAFTING), NullValueHandling = NullValueHandling.Ignore)]
            public double? CRAFTING { get; set; }

            [JsonProperty(PropertyName = nameof(Skills.MINING), NullValueHandling = NullValueHandling.Ignore)]
            public double? MINING { get; set; }

            [JsonProperty(PropertyName = nameof(Skills.SKINNING), NullValueHandling = NullValueHandling.Ignore)]
            public double? SKINNING { get; set; }

            [JsonProperty(PropertyName = nameof(Skills.WOODCUTTING), NullValueHandling = NullValueHandling.Ignore)]
            public double? WOODCUTTING { get; set; }

            public ConfigurationResources(double? ACQUIRE, double? CRAFTING, double? MINING, double? SKINNING, double? WOODCUTTING)
            {
                this.ACQUIRE = ACQUIRE;
                this.CRAFTING = CRAFTING;
                this.MINING = MINING;
                this.SKINNING = SKINNING;
                this.WOODCUTTING = WOODCUTTING;
            }

            public double Get(Skills skill)
            {
                switch (skill)
                {
                    case Skills.ACQUIRE:
                        return ACQUIRE.HasValue ? ACQUIRE.Value : 0.0;
                    case Skills.CRAFTING:
                        return CRAFTING.HasValue ? CRAFTING.Value : 0.0;
                    case Skills.MINING:
                        return MINING.HasValue ? MINING.Value : 0.0;
                    case Skills.SKINNING:
                        return SKINNING.HasValue ? SKINNING.Value : 0.0;
                    case Skills.WOODCUTTING:
                        return WOODCUTTING.HasValue ? WOODCUTTING.Value : 0.0;
                }

                return 0;
            }

            public void Set(Skills skill, double value)
            {
                switch (skill)
                {
                    case Skills.ACQUIRE:
                        ACQUIRE = value;
                        break;
                    case Skills.CRAFTING:
                        CRAFTING = value;
                        break;
                    case Skills.MINING:
                        MINING = value;
                        break;
                    case Skills.SKINNING:
                        SKINNING = value;
                        break;
                    case Skills.WOODCUTTING:
                        WOODCUTTING = value;
                        break;
                }
            }
        }

        public class ConfigurationColors
        {
            [JsonProperty(PropertyName = nameof(Skills.ACQUIRE))]
            public string ACQUIRE { get; set; }

            [JsonProperty(PropertyName = nameof(Skills.CRAFTING))]
            public string CRAFTING { get; set; }

            [JsonProperty(PropertyName = nameof(Skills.MINING))]
            public string MINING { get; set; }

            [JsonProperty(PropertyName = nameof(Skills.SKINNING))]
            public string SKINNING { get; set; }

            [JsonProperty(PropertyName = nameof(Skills.WOODCUTTING))]
            public string WOODCUTTING { get; set; }

            public ConfigurationColors(string ACQUIRE, string CRAFTING, string MINING, string SKINNING, string WOODCUTTING)
            {
                this.ACQUIRE = ACQUIRE;
                this.CRAFTING = CRAFTING;
                this.MINING = MINING;
                this.SKINNING = SKINNING;
                this.WOODCUTTING = WOODCUTTING;
            }

            public string Get(Skills skill)
            {
                switch (skill)
                {
                    case Skills.ACQUIRE:
                        return ACQUIRE;
                    case Skills.CRAFTING:
                        return CRAFTING;
                    case Skills.MINING:
                        return MINING;
                    case Skills.SKINNING:
                        return SKINNING;
                    case Skills.WOODCUTTING:
                        return WOODCUTTING;
                }

                return "1 1 1 1";
            }
        }

        public class ConfigurationCraftingDetails
        {
            [JsonProperty(PropertyName = "Time Spent")]
            public double time { get; set; }

            [JsonProperty(PropertyName = "XP Per Time Spent")]
            public double xp { get; set; }

            [JsonProperty(PropertyName = "Percent Faster Per Level")]
            public double percent { get; set; }

            [JsonProperty(PropertyName = "Require Permission For Instant Bulk Craft")]
            public bool usePermission { get; set; }

            [JsonProperty(PropertyName = "Instant Bulk Craft Permission Does Not Require Max Level")]
            public bool noLevelRequirement { get; set; }

            [JsonProperty(PropertyName = "Permission For Instant Bulk Crafting At Max Level")]
            public string Permission { get; set; } = "zlevelsremastered.crafting.instantbulk";

            [JsonProperty(PropertyName = "Require Inventory Slots")]
            public bool slots;

            public ConfigurationCraftingDetails(double time, double xp, double percent)
            {
                this.time = time;
                this.xp = xp;
                this.percent = percent;
            }
        }

        public class ConfigurationCuiPositions
        {
            [JsonProperty(PropertyName = "Width Left")]
            public string widthLeft { get; set; }

            [JsonProperty(PropertyName = "Width Right")]
            public string widthRight { get; set; }

            [JsonProperty(PropertyName = "Height Lower")]
            public string heightLower { get; set; }

            [JsonProperty(PropertyName = "Height Upper")]
            public string heightUpper { get; set; }

            public ConfigurationCuiPositions(string widthLeft, string widthRight, string heightLower, string heightUpper)
            {
                this.widthLeft = widthLeft;
                this.widthRight = widthRight;
                this.heightLower = heightLower;
                this.heightUpper = heightUpper;
            }
        }

        public class ConfigurationSkillColors
        {
            [JsonProperty(PropertyName = nameof(Skills.ACQUIRE))]
            public string ACQUIRE { get; set; } = "#7700AA";

            [JsonProperty(PropertyName = nameof(Skills.CRAFTING))]
            public string CRAFTING { get; set; } = "#00FF00";

            [JsonProperty(PropertyName = nameof(Skills.MINING))]
            public string MINING { get; set; } = "#0000FF";

            [JsonProperty(PropertyName = nameof(Skills.SKINNING))]
            public string SKINNING { get; set; } = "#FF0000";

            [JsonProperty(PropertyName = nameof(Skills.WOODCUTTING))]
            public string WOODCUTTING { get; set; } = "#FF9900";

            public string Get(Skills skill)
            {
                switch (skill)
                {
                    case Skills.ACQUIRE:
                        return ACQUIRE;
                    case Skills.CRAFTING:
                        return CRAFTING;
                    case Skills.MINING:
                        return MINING;
                    case Skills.SKINNING:
                        return SKINNING;
                    case Skills.WOODCUTTING:
                        return WOODCUTTING;
                }

                return "#FF0000";
            }
        }

        public class RewardType
        {
            [JsonProperty(PropertyName = "Acquire")]
            public double Acquire { get; set; }

            [JsonProperty(PropertyName = "Crafting")]
            public double Crafting { get; set; }

            [JsonProperty(PropertyName = "Mining")]
            public double Mining { get; set; }

            [JsonProperty(PropertyName = "Skinning")]
            public double Skinning { get; set; }

            [JsonProperty(PropertyName = "Woodcutting")]
            public double Woodcutting { get; set; }

            public double Get(Skills skill)
            {
                switch (skill)
                {
                    case Skills.ACQUIRE: return Acquire;
                    case Skills.CRAFTING: return Crafting;
                    case Skills.MINING: return Mining;
                    case Skills.SKINNING: return Skinning;
                    case Skills.WOODCUTTING: return Woodcutting;
                    default: return 0.0;
                }
            }
        }

        public class Rewards
        {
            [JsonProperty(PropertyName = "Economics Money")]
            public RewardType Money { get; set; } = new();

            [JsonProperty(PropertyName = "ServerRewards Points")]
            public RewardType Points { get; set; } = new();

            [JsonProperty(PropertyName = "SkillTree XP")]
            public RewardType XP { get; set; } = new();
        }

        public void GiveReward(BasePlayer player, Skills skill, double level)
        {
            if (Economics != null && Economics.IsLoaded)
            {
                var money = config.Rewards.Money.Get(skill) * level;
                if (money > 0)
                {
                    Economics?.Call("Deposit", player.UserIDString, money);
                    Message(player, "EconomicsDeposit", money);
                }
            }

            if (IQEconomic != null && IQEconomic.IsLoaded)
            {
                var money = config.Rewards.Money.Get(skill) * level;
                if (money > 0)
                {
                    IQEconomic?.Call("API_SET_BALANCE", player.UserIDString, (int)money);
                    Message(player, "EconomicsDeposit", (int)money);
                }
            }

            if (ServerRewards != null && ServerRewards.IsLoaded)
            {
                var points = config.Rewards.Points.Get(skill) * level;
                if (points > 0)
                {
                    ServerRewards?.Call("AddPoints", player.UserIDString, (int)points);
                    Message(player, "ServerRewardPoints", (int)points);
                }
            }

            if (SkillTree != null && SkillTree.IsLoaded)
            {
                var xp = config.Rewards.XP.Get(skill) * level;
                if (xp > 0)
                {
                    SkillTree?.Call("AwardXP", player, xp);
                    Message(player, "SkillTreeXP", xp);
                }
            }
        }

        private Configuration config = new();
        private ConfigurationResources pointsPerPowerTool;
        private ConfigurationResources pointsPerHitCurrent;
        private ConfigurationResources pointsPerPowerToolAtNight;
        private ConfigurationResources pointsPerHitPowerToolCurrent;
        private ConfigurationResources resourceMultipliersCurrent;
        private Dictionary<Skills, int> skillIndex = new();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            canSaveConfig = false;
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
                canSaveConfig = true;
            }
            catch (Exception ex)
            {
                Puts(ex.ToString());
                LoadDefaultConfig();
                return;
            }
            CheckStartingStats();
            SaveConfig();
        }

        public void CheckStartingStats()
        {
            var pi = CreatePlayerInfo();
            var ACQUIRE = getExperiencePercentProc(pi, Skills.ACQUIRE);

            if (ACQUIRE < 0.0)
            {
                var value = Math.Ceiling(getPointsNeededForNextLevel(Math.Max(1.0, pi.ACQUIRE_LEVEL)) * (-1 * ACQUIRE) / 100);
                Puts("Invalid acquire starting points: {0} ({1}% multiplier) resulting in {2}% towards next level. Points adjusted to: {3}", pi.ACQUIRE_POINTS, pi.XP_MULTIPLIER, ACQUIRE, value);
                config.settings.stats.acquire_points += value;
                pi.ACQUIRE_POINTS += value;
            }

            var CRAFTING = getExperiencePercentProc(pi, Skills.CRAFTING);

            if (CRAFTING < 0.0)
            {
                var value = Math.Ceiling(getPointsNeededForNextLevel(Math.Max(1.0, pi.CRAFTING_LEVEL)) * (-1 * CRAFTING) / 100);
                Puts("Invalid crafting starting points: {0} ({1}% multiplier) resulting in {2}% towards next level. Points adjusted to: {3}", pi.CRAFTING_POINTS, pi.XP_MULTIPLIER, CRAFTING, value);
                config.settings.stats.crafting_points += value;
                pi.CRAFTING_POINTS += value;
            }

            var MINING = getExperiencePercentProc(pi, Skills.MINING);

            if (MINING < 0.0)
            {
                var value = Math.Ceiling(getPointsNeededForNextLevel(Math.Max(1.0, pi.MINING_LEVEL)) * (-1 * MINING) / 100);
                Puts("Invalid mining starting points: {0} ({1}% multiplier) resulting in {2}% towards next level. Points adjusted to: {3}", pi.MINING_POINTS, pi.XP_MULTIPLIER, MINING, value);
                config.settings.stats.mining_points += value;
                pi.MINING_POINTS += value;
            }

            var SKINNING = getExperiencePercentProc(pi, Skills.SKINNING);

            if (SKINNING < 0.0)
            {
                var value = Math.Ceiling(getPointsNeededForNextLevel(Math.Max(1.0, pi.SKINNING_LEVEL)) * (-1 * SKINNING) / 100);
                Puts("Invalid skinning starting points: {0} ({1}% multiplier) resulting in {2}% towards next level. Points adjusted to: {3}", pi.SKINNING_POINTS, pi.XP_MULTIPLIER, SKINNING, value);
                config.settings.stats.skinning_points += value;
                pi.SKINNING_POINTS += value;
            }

            var WOODCUTTING = getExperiencePercentProc(pi, Skills.WOODCUTTING);

            if (WOODCUTTING < 0.0)
            {
                var value = Math.Ceiling(getPointsNeededForNextLevel(Math.Max(1.0, pi.WOODCUTTING_LEVEL)) * (-1 * WOODCUTTING) / 100);
                Puts("Invalid woodcutting starting points: {0} ({1}% multiplier) resulting in {2}% towards next level. Points adjusted to: {3}", pi.WOODCUTTING_POINTS, pi.XP_MULTIPLIER, WOODCUTTING, value);
                config.settings.stats.woodcutting_points += value;
                pi.WOODCUTTING_POINTS += value;
            }
        }

        private bool canSaveConfig = true;

        protected override void SaveConfig()
        {
            if (canSaveConfig)
            {
                Config.WriteObject(config, true);
            }
        }

        protected override void LoadDefaultConfig()
        {
            config = new();
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new()
            {
                {"StatsHeadline", "Level stats (/statinfo - To get more information about skills)"},
                {"StatsText",   "-{0}\nLevel: {1} (+{4}% bonus) \nXP: {2}/{3} [{5}].\n<color=red>-{6} XP loss on death.</color>"},
                {"LevelUpText", "{0} Level up\nLevel: {1} (+{4}% bonus) \nXP: {2}/{3}"},
                {"LevelUpTextBroadcast", "<color=#5af>{0}</color> has reached level <color=#5af>{1}</color> in <color={2}>{3}</color>"},
                {"PenaltyText", "<color=orange>You have lost XP for dying:{0}</color>"},
                {"NoPermission", "You don't have permission to use this command"},
                {"WOODCUTTINGSkill", "Woodcutting"},
                {"MININGSkill", "Mining"},
                {"SKINNINGSkill", "Skinning"},
                {"CRAFTINGSkill", "Crafting" },
                {"ACQUIRESkill", "Acquire" },
                {"XPM", "XP Multiplier"},
                {"STATS", "Stats for player: "},
                {"INFO USE", "Usage: zl.info name|steamid"},
                {"RESET USE", "Usage: zl.reset true | Resets all userdata to zero"},
                {"XPM USE 1", "Syntax: zl.playerxpm name|steamid (to show current XP multiplier)"},
                {"XPM USE 2", "Syntax: zl.playerxpm name|steamid number (to set current XP multiplier >= 100)"},
                {"PLAYER NOT FOUND", "Player not found!"},
                {"NightBonusOn", "Nightbonus for points per hit enabled"},
                {"NightBonusOff", "Nightbonus for points per hit disabled"},
                {"PluginPlayerOn", "The plugin functions are now enabled again"},
                {"PluginPlayerOff", "The plugin functions are now disabled for your character"},
                {"Lv.", "Lv."},
                {"MAX", "MAX"},
                {"SkillTreeXP", "You have received <color=#FFFF00>{0} XP</color> for leveling up!"},
                {"ServerRewardPoints", "You have received <color=#FFFF00>{0} RP</color> for leveling up!"},
                {"EconomicsDeposit", "You have received <color=#FFFF00>${0}</color> for leveling up!"},
                {"NoSlots", "You don't have enough slots to craft!"},
            }, this);
        }

        #endregion Config

        #region API

        private double GetMultiplier(ulong userID, string skill = "A")
        {
            double multiplier = 1;
            if (userID.IsSteamId())
            {
                switch (skill.ToUpper())
                {
                    case "A":
                        return getGathMult(GetLevel(userID, skill), Skills.ACQUIRE);
                    case "C":
                        return getGathMult(GetLevel(userID, skill), Skills.CRAFTING);
                    case "M":
                        return getGathMult(GetLevel(userID, skill), Skills.MINING);
                    case "S":
                        return getGathMult(GetLevel(userID, skill), Skills.SKINNING);
                    case "WC":
                        return getGathMult(GetLevel(userID, skill), Skills.WOODCUTTING);
                }
            }

            return multiplier;
        }

        private double GetLevel(ulong userID, string skill = "A")
        {
            if (data.PlayerInfo.TryGetValue(userID, out var pi))
            {
                switch (skill.ToUpper())
                {
                    case "A":
                    case "ACQUIRE":
                        return pi.ACQUIRE_LEVEL;
                    case "C":
                    case "CRAFTING":
                        return pi.CRAFTING_LEVEL;
                    case "M":
                    case "MINING":
                        return pi.MINING_LEVEL;
                    case "S":
                    case "SKINNING":
                        return pi.SKINNING_LEVEL;
                    case "WC":
                    case "WOODCUTTING":
                        return pi.WOODCUTTING_LEVEL;
                }
            }

            return 0;
        }

        private string api_GetPlayerInfo(ulong playerid)
        {
            if (playerid != 0)
            {
                PlayerInfo pi = GetPlayerInfo(playerid);
                if (pi == null) return string.Empty;
                return pi.ACQUIRE_LEVEL + "|" +
                    pi.ACQUIRE_POINTS + "|" +
                    pi.CRAFTING_LEVEL + "|" +
                    pi.CRAFTING_POINTS + "|" +
                    pi.CUI + "|" +
                    pi.LAST_DEATH + "|" +
                    pi.MINING_LEVEL + "|" +
                    pi.MINING_POINTS + "|" +
                    pi.ENABLED + "|" +
                    pi.SKINNING_LEVEL + "|" +
                    pi.SKINNING_POINTS + "|" +
                    pi.WOODCUTTING_LEVEL + "|" +
                    pi.WOODCUTTING_POINTS + "|" +
                    getXpMulti(pi, playerid.ToString());
            }
            return string.Empty;
        }

        private bool api_SetPlayerInfo(ulong userid, string data)
        {
            if (userid == 0 || data == null) { return false; }
            if (!this.data.PlayerInfo.TryGetValue(userid, out var pi))
            {
                this.data.PlayerInfo[userid] = pi = CreatePlayerInfo();
                pi.LAST_DEATH = ToEpochTime(DateTime.UtcNow);
                pi.CUI = config.generic.playerCuiDefaultEnabled;
                pi.ENABLED = config.generic.playerPluginDefaultEnabled;
            }
            string[] split = data.Split('|');
            if (split.Length < 14)
            {
                return false;
            }
            pi.ACQUIRE_LEVEL = double.Parse(split[0]);
            pi.ACQUIRE_POINTS = double.Parse(split[1]);
            pi.CRAFTING_LEVEL = double.Parse(split[2]);
            pi.CRAFTING_POINTS = double.Parse(split[3]);
            pi.CUI = bool.Parse(split[4]);
            pi.LAST_DEATH = double.Parse(split[5]);
            pi.MINING_LEVEL = double.Parse(split[6]);
            pi.MINING_POINTS = double.Parse(split[7]);
            pi.ENABLED = bool.Parse(split[8]);
            pi.SKINNING_LEVEL = double.Parse(split[9]);
            pi.SKINNING_POINTS = double.Parse(split[10]);
            pi.WOODCUTTING_LEVEL = double.Parse(split[11]);
            pi.WOODCUTTING_POINTS = double.Parse(split[12]);
            pi.XP_MULTIPLIER = double.Parse(split[13]);
            BasePlayer target = BasePlayer.FindByID(userid);
            if (target != null && target.IsConnected)
            {
                DestroyGUI(target);
                CreateGUI(target, pi);
            }
            return true;
        }

        #endregion API
    }
}