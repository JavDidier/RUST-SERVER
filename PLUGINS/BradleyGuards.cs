/*
 * Copyright (c) 2023 Bazz3l
 * 
 * Bradley Guards cannot be copied, edited and/or (re)distributed without the express permission of Bazz3l.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 * 
 */

using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System;
using Oxide.Plugins.BradleyGuardsExtensionMethods;
using Oxide.Core.Plugins;
using Oxide.Core;
using Rust;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Network;

namespace Oxide.Plugins
{
    [Info("Bradley Guards", "Bazz3l", "1.7.1")]
    [Description("Spawn reinforcements for bradley when destroyed at configured monuments.")]
    internal class BradleyGuards : RustPlugin
    {
        [PluginReference] private Plugin NpcSpawn, GUIAnnouncements;
        
        #region Fields
        
        private const string PERM_USE = "bradleyguards.use";
        private const float INITIALIZE_DELAY = 10f;
        
        private readonly object _objFalse = false;
        private readonly object _objTrue = true;
        
        private StoredData _storedData;
        private ConfigData _configData;
        private Coroutine _setupRoutine;
        
        private static BradleyGuards Instance { get; set; }

        #endregion
        
        #region Local
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { MessageKeys.NoPermission, "Sorry you don't have permission to do that." },
                { MessageKeys.Prefix, "<color=#8a916f>Bradley Guards</color>:\n" },
                
                { MessageKeys.EventStart, "Armed reinforcements en route to <color=#e7cf85>{0}</color> eliminate the guards to gain access to high-value loot." },
                { MessageKeys.EventEnded, "Armed reinforcements have been eliminated at <color=#e7cf85>{0}</color>." },
                { MessageKeys.EventUpdated, "Event updated, please reload the plugin to take effect." },
                { MessageKeys.EventNotFound, "Event not found, please make sure you have typed the correct name." },
                { MessageKeys.EventDebug, "Event debug information has been <color=#e7cf85>enabled</color> for <color=#e7cf85>{0}</color> seconds." },
                
                { MessageKeys.DisplayNameEmpty, "Invalid event name provided." },
                { MessageKeys.InvalidGuardAmount, "Invalid guard amount must be between <color=#e7cf85>{0}</color> - <color=#e7cf85>{1}</color>." },
                { MessageKeys.InvalidBooleanValue, "Invalid boolean value provided, must be true or false." },
                
                { MessageKeys.HelpGuardAmount, "<color=#e7cf85>/{0}</color> \"<color=#e7cf85><monument-name></color>\" amount <color=#e7cf85><number></color>\n" },
                { MessageKeys.HelpGuardLoadout, "<color=#e7cf85>/{0}</color> \"<color=#e7cf85><monument-name></color>\" loadout" },
                { MessageKeys.HelpEventEnable, "<color=#e7cf85>/{0}</color> \"<color=#e7cf85><monument-name></color>\" enable <color=#e7cf85><true|false></color>\n" },
                { MessageKeys.HelpEventName, "<color=#e7cf85>/{0}</color> \"<color=#e7cf85><monument-name></color>\" display \"<color=#e7cf85><name-here></color>\"\n" },
                { MessageKeys.HelpEventDebug, "<color=#e7cf85>/{0}</color> \"<color=#e7cf85><monument-name></color>\", toggles debug information for \"<color=#e7cf85>{1}</color>\" seconds.\n" },
            }, this);
        }

        private struct MessageKeys
        {
            public const string Prefix = "Prefix";
            public const string NoPermission = "NoPermission";
            
            public const string EventStart = "EventStart";
            public const string EventEnded = "EventEnded";
            public const string EventNotFound = "EventNotFound";
            public const string EventUpdated = "EventUpdated";
            public const string EventDebug = "EventDebug";
            
            public const string DisplayNameEmpty = "InvalidDisplayName";
            public const string InvalidGuardAmount = "InvalidGuardAmount";
            public const string InvalidBooleanValue = "InvalidBooleanValue";
            
            public const string HelpEventEnable = "HelpEventEnable";
            public const string HelpEventName = "HelpEventName";
            public const string HelpEventDebug = "HelpEventDebug";
            public const string HelpGuardAmount = "HelpGuardAmount";
            public const string HelpGuardLoadout = "HelpGuardLoadout";
        }

        private void MessagePlayer(BasePlayer player, string langKey, params object[] args)
        {
            if (player == null || !player.IsConnected) return;
            string message = lang.GetMessage(langKey, this);
            player.ChatMessage(args?.Length > 0 ? string.Format(message, args) : message);
        }
        
        private void MessagePlayers(string langKey, params object[] args)
        {
            string message = lang.GetMessage(langKey, this);
            if (args?.Length > 0) message = string.Format(message, args);
            
            if (_configData.MessageSettings.EnableChat)
                ConsoleNetwork.BroadcastToAllClients("chat.add", 2, _configData.MessageSettings.ChatIcon, _configData.MessageSettings.EnableChatPrefix ? (lang.GetMessage(MessageKeys.Prefix, this) + message) : message);
            
            if (_configData.MessageSettings.EnableToast)
                ConsoleNetwork.BroadcastToAllClients("gametip.showtoast_translated", 2, null, message);
            
            if (_configData.MessageSettings.EnableGuiAnnouncements && GUIAnnouncements.IsReady())
                GUIAnnouncements?.Call("CreateAnnouncement", message, _configData.MessageSettings.GuiAnnouncementsBgColor, _configData.MessageSettings.GuiAnnouncementsTextColor, null, 0.03f);
        }
        
        #endregion
        
        #region Config

        protected override void LoadDefaultConfig()
        {
            _configData = ConfigData.DefaultConfig();
            _configData.Version = Version;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _configData = Config.ReadObject<ConfigData>();
                if (_configData == null) throw new Exception();

                bool wasUpdated = false;
                
                if (_configData.Version != Version)
                {
                    LoadDefaultConfig();
                    wasUpdated = true;
                }
                
                if (wasUpdated)
                {
                    PrintWarning("Config was updated.");
                    SaveConfig();
                }
            }
            catch(Exception e)
            {
                PrintWarning(e.Message);
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_configData, true);
        
        private class ConfigData
        {
            [JsonProperty("CommandName: setup chat command name ex: (bguard)")]
            public string CommandName;
            [JsonProperty("EnableUnlocking: automatically unlock crates when guards are eliminated ex: (true or false)")]
            public bool EnableUnlocking;
            [JsonProperty("EnableExtinguish: automatically extinguish crates when guards are eliminated ex: (true or false)")]
            public bool EnableExtinguish;
            [JsonProperty("BradleyCrates: amount of crates that should spawn when bradley is destroyed ex: (4)")]
            public int BradleyCrates;
            [JsonProperty("BradleyHealth: amount of health the bradley apc should spawn with (1000)")]
            public float BradleyHealth;
            [JsonProperty("DebugDuration: duration in which debug information will be displayed for ex: (30 seconds)")]
            public float DebugDuration;
            [JsonProperty("MessageSettings: notification message settings")]
            public MessageSettings MessageSettings;
            [JsonProperty("VersionNumber: current version of the plugin")]
            public VersionNumber? Version;

            public static ConfigData DefaultConfig()
            {
                return new ConfigData
                {
                    CommandName = "bguard",
                    EnableUnlocking = true,
                    EnableExtinguish = true,
                    BradleyCrates = 4,
                    BradleyHealth = 1000f,
                    DebugDuration = 30f,
                    MessageSettings = new MessageSettings
                    {
                        EnableChatPrefix = true,
                        EnableToast = false,
                        EnableChat = true, 
                        ChatIcon = 76561199542550973,
                        EnableGuiAnnouncements = false,
                        GuiAnnouncementsBgColor = "Purple",
                        GuiAnnouncementsTextColor = "White"
                    }
                };
            }
        }
        
        private class MessageSettings
        {
            [JsonProperty("enable chat prefix")]
            public bool EnableChatPrefix;
            [JsonProperty("enable toast message")]
            public bool EnableToast;
            [JsonProperty("enable chat message")]
            public bool EnableChat;
            [JsonProperty("custom chat message icon (steam64)")]
            public ulong ChatIcon;
            [JsonProperty("enable gui announcements plugin from umod.org")]
            public bool EnableGuiAnnouncements;
            [JsonProperty("gui announcements text color")]
            public string GuiAnnouncementsTextColor;
            [JsonProperty("gui announcements background color")]
            public string GuiAnnouncementsBgColor;
        }

        #endregion

        #region Storage

        private void LoadDefaultData()
        {
            _storedData = new StoredData
            {
                BradleyEventEntries = new Dictionary<string, EventEntry>
                {
                    ["assets/bundled/prefabs/autospawn/monument/xlarge/launch_site_1.prefab"] = new EventEntry
                    {
                        DisplayName = "Launch Site",
                        EnabledEvent = true,
                        BoundsPosition = new Vector3(0f, 0f, 0f),
                        BoundsSize = new Vector3(580f, 280f, 300f),
                        LandingPosition = new Vector3(152.3f, 3f, 0f),
                        LandingRotation = new Vector3(0f, 90f, 0f),
                        ChinookPosition = new Vector3(-195f, 150f, 25f),
                        GuardAmount = 10,
                        GuardProfile = new GuardConfig
                        {
                            Name = "Launch Site Guard",
                            WearItems = new List<GuardConfig.WearEntry>
                            {
                                new()
                                {
                                    ShortName = "hazmatsuit_scientist_peacekeeper",
                                    SkinID = 0UL
                                }
                            },
                            BeltItems = new List<GuardConfig.BeltEntry>
                            {
                                new()
                                {
                                    ShortName = "smg.mp5",
                                    Amount = 1,
                                    SkinID = 0UL,
                                    Mods = new List<string>()
                                },
                                new()
                                {
                                    ShortName = "syringe.medical",
                                    Amount = 10,
                                    SkinID = 0UL,
                                    Mods = new List<string>()
                                },
                            },
                            Kit = "",
                            Health = 250f,
                            RoamRange = 25f,
                            ChaseRange = 40f,
                            SenseRange = 150f,
                            AttackRangeMultiplier = 8f,
                            CheckVisionCone = false,
                            VisionCone = 180f,
                            DamageScale = 1f,
                            TurretDamageScale = 0.25f,
                            AimConeScale = 0.35f,
                            DisableRadio = false,
                            CanRunAwayWater = true,
                            CanSleep = false,
                            Speed = 8.5f,
                            AreaMask = 1,
                            AgentTypeID = -1372625422,
                            MemoryDuration = 30f
                        }
                    },
                    ["assets/bundled/prefabs/autospawn/monument/large/airfield_1.prefab"] = new EventEntry
                    {
                        DisplayName = "Airfield Guard",
                        EnabledEvent = false,
                        BoundsPosition = new Vector3(0f, 0f, 0f),
                        BoundsSize = new Vector3(340f, 260f, 300f),
                        LandingPosition = new Vector3(0f, 0f, -28f),
                        LandingRotation = new Vector3(0f, 0f, 0f),
                        ChinookPosition = new Vector3(-195f, 150f, 25f),
                        GuardAmount = 10,
                        GuardProfile = new GuardConfig
                        {
                            Name = "Guarded Crate",
                            WearItems = new List<GuardConfig.WearEntry>
                            {
                                new()
                                {
                                    ShortName = "hazmatsuit_scientist_peacekeeper",
                                    SkinID = 0UL
                                }
                            },
                            BeltItems = new List<GuardConfig.BeltEntry>
                            {
                                new()
                                {
                                    ShortName = "smg.mp5",
                                    Amount = 1,
                                    SkinID = 0UL,
                                    Mods = new List<string>()
                                },
                                new()
                                {
                                    ShortName = "syringe.medical",
                                    Amount = 10,
                                    SkinID = 0UL,
                                    Mods = new List<string>()
                                },
                            },
                            Kit = "",
                            Health = 250f,
                            RoamRange = 25f,
                            ChaseRange = 40f,
                            SenseRange = 150f,
                            AttackRangeMultiplier = 8f,
                            CheckVisionCone = false,
                            VisionCone = 180f,
                            DamageScale = 1f,
                            TurretDamageScale = 0.25f,
                            AimConeScale = 0.35f,
                            DisableRadio = false,
                            CanRunAwayWater = true,
                            CanSleep = false,
                            Speed = 8.5f,
                            AreaMask = 1,
                            AgentTypeID = -1372625422,
                            MemoryDuration = 30f
                        }
                    }
                }
            };

            SaveData();
        }

        private void LoadData()
        {
            try
            {
                _storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
                if (_storedData == null || !_storedData.IsValid) throw new Exception();
            }
            catch
            {
                PrintWarning("Loaded default data.");
                LoadDefaultData();
            }
        }

        private void SaveData()
        {
            if (_storedData == null) return;
            Interface.Oxide.DataFileSystem.WriteObject(Name, _storedData);
        }

        private EventEntry FindEventEntry(string eventName)
        {
            return _storedData.BradleyEventEntries.Values.FirstOrDefault(x =>
                x.DisplayName.Equals(eventName, StringComparison.OrdinalIgnoreCase)
            );
        }

        private class StoredData
        {
            public Dictionary<string, EventEntry> BradleyEventEntries = new(StringComparer.OrdinalIgnoreCase);
            
            public EventEntry FindEntryByName(string monumentName)
            {
                BradleyEventEntries.TryGetValue(monumentName, out EventEntry eventEntry);
                return eventEntry;
            }
            
            [JsonIgnore]
            public bool IsValid => BradleyEventEntries?.Count > 0;
        }

        private class EventEntry
        {
            [JsonProperty("display name")]
            public string DisplayName;
            
            [JsonProperty("enabled")]
            public bool EnabledEvent;
            
            [JsonProperty("bounds center")]
            public Vector3 BoundsPosition;
            [JsonProperty("bounds size")]
            public Vector3 BoundsSize;
            
            [JsonProperty("landing position")]
            public Vector3 LandingPosition;
            [JsonProperty("landing rotation")]
            public Vector3 LandingRotation;
            
            [JsonProperty("chinook position")]
            public Vector3 ChinookPosition;
            
            [JsonProperty("guard spawn amount")]
            public int GuardAmount;
            
            [JsonProperty("guard spawn profile")]
            public GuardConfig GuardProfile;
            
            [JsonIgnore]
            public bool IsValid => GuardAmount > 0 && GuardProfile != null && GuardProfile.WearItems != null && GuardProfile.BeltItems != null;
        }
        
        private class GuardConfig
        {
            public string Name;
            public string Kit;
            public float Health;
            public float RoamRange;
            public float ChaseRange;
            public float SenseRange;
            public float AttackRangeMultiplier;
            public bool CheckVisionCone;
            public float VisionCone;
            public float DamageScale;
            public float TurretDamageScale;
            public float AimConeScale;
            public bool DisableRadio;
            public bool CanRunAwayWater;
            public bool CanSleep;
            public float Speed;
            public int AreaMask;
            public int AgentTypeID;
            public float MemoryDuration;
            public List<WearEntry> WearItems;
            public List<BeltEntry> BeltItems; 

            [JsonIgnore]
            public string[] Kits;
            
            [JsonIgnore]
            public JObject Parsed;

            public class BeltEntry
            {
                public string ShortName;
                public ulong SkinID;
                public int Amount;
                public string Ammo;
                public List<string> Mods;

                public static List<BeltEntry> SaveItems(ItemContainer container)
                {
                    List<BeltEntry> items = new List<BeltEntry>();
                    
                    foreach (Item item in container.itemList)
                    {
                        BeltEntry beltEntry = new BeltEntry
                        {
                            ShortName = item.info.shortname,
                            SkinID = item.skin,
                            Amount = item.amount,
                            Mods = new List<string>()
                        };

                        if (item.GetHeldEntity() is BaseProjectile projectile && projectile?.primaryMagazine != null && projectile.primaryMagazine.ammoType != null)
                            beltEntry.Ammo = projectile.primaryMagazine.ammoType.shortname;

                        if (item?.contents?.itemList != null)
                        {
                            foreach (Item itemContent in item.contents.itemList)
                                beltEntry.Mods.Add(itemContent.info.shortname);
                        }
                        
                        items.Add(beltEntry);
                    }

                    return items;
                }
            }

            public class WearEntry
            {
                public string ShortName; 
                public ulong SkinID;

                public static List<WearEntry> SaveItems(ItemContainer container)
                {
                    List<WearEntry> items = new List<WearEntry>();
                    
                    foreach (Item item in container.itemList)
                    {
                        WearEntry wearEntry = new WearEntry
                        {
                            ShortName = item.info.shortname,
                            SkinID = item.skin
                        };
                        
                        items.Add(wearEntry);
                    }

                    return items;
                }
            }

            public void CacheConfig()
            {
                Kits = Kit?.Split(',') ?? Array.Empty<string>();
                
                Parsed = new JObject
                {
                    ["Name"] = Name,
                    ["WearItems"] = new JArray(WearItems.Select(x => new JObject
                    {
                        ["ShortName"] = x.ShortName, 
                        ["SkinID"] = x.SkinID
                    })),
                    ["BeltItems"] = new JArray(BeltItems.Select(x => new JObject
                    {
                        ["ShortName"] = x.ShortName, 
                        ["SkinID"] = x.SkinID, 
                        ["Amount"] = x.Amount, 
                        ["Ammo"] = x.Ammo, 
                        ["Mods"] = new JArray(x.Mods)
                    })),
                    ["Kit"] = Kit,
                    ["Health"] = Health,
                    ["RoamRange"] = RoamRange,
                    ["ChaseRange"] = ChaseRange,
                    ["SenseRange"] = SenseRange,
                    ["ListenRange"] = SenseRange / 2,
                    ["AttackRangeMultiplier"] = AttackRangeMultiplier,
                    ["CheckVisionCone"] = CheckVisionCone,
                    ["VisionCone"] = VisionCone,
                    ["DamageScale"] = DamageScale,
                    ["TurretDamageScale"] = TurretDamageScale,
                    ["AimConeScale"] = AimConeScale,
                    ["DisableRadio"] = DisableRadio,
                    ["CanRunAwayWater"] = CanRunAwayWater,
                    ["CanSleep"] = CanSleep,
                    ["Speed"] = Speed,
                    ["AreaMask"] = AreaMask,
                    ["AgentTypeID"] = AgentTypeID,
                    ["MemoryDuration"] = MemoryDuration,
                    ["States"] = new JArray(new HashSet<string> { "RoamState", "ChaseState", "CombatState" })
                };
            }
        }

        #endregion
        
        #region Oxide Hooks

        private void OnServerInitialized()
        {
            CustomProtection.Initialize();
            PerformSetupRoutine();
            
            if (string.IsNullOrEmpty(_configData.CommandName))
                return;
            
            cmd.AddChatCommand(_configData.CommandName, this, nameof(EventCommands));
        }

        private void Init()
        {
            permission.RegisterPermission(PERM_USE, this);
            
            Instance = this;
            
            LoadData();
        }

        private void Unload()
        {
            try
            {
                DestroySetupRoutine();
                BradleyGuardsEvent.OnUnload();
            }
            finally
            {
                CustomProtection.OnUnload();
                EntityCache.OnUnload();
                Instance = null;
            }
        }

        private void OnEntitySpawned(BradleyAPC bradley)
        {
            if (bradley == null || bradley.IsDestroyed)
                return;
            
            Vector3 position = bradley.transform.position;
            if (position == Vector3.zero)
                return;
            
            BradleyGuardsEvent closestEvent = BradleyGuardsEvent.GetClosest(position);
            if (closestEvent == null)
                return;
            
            closestEvent.ResetEvent();
            
            bradley.maxCratesToSpawn = _configData.BradleyCrates;
            bradley.startHealth = _configData.BradleyHealth;
            bradley._maxHealth = _configData.BradleyHealth;
            bradley.InitializeHealth(_configData.BradleyHealth, _configData.BradleyHealth); 
        }

        private void OnEntityDeath(BradleyAPC bradley, HitInfo info)
        {
            if (bradley == null || bradley.IsDestroyed || info?.InitiatorPlayer == null)
                return;
            
            Vector3 deathPosition = bradley.transform.position;
            if (deathPosition == Vector3.zero)
                return;
            
            BradleyGuardsEvent closestEvent = BradleyGuardsEvent.GetClosest(deathPosition);
            if (closestEvent == null || closestEvent.IsStarted()) 
                return;
                
            if (!NpcSpawn.IsReady())
            {
                PrintWarning("Missing dependency [NpcSpawn v2.4.8] this can be found over at codefling.com thanks to KpucTaJl");
                return;
            }

            closestEvent.StartEvent(deathPosition);
        }
        
        private void OnEntityDeath(ScientistNPC npc, HitInfo hitInfo)
        {
            EntityCache.FindEventByEntity(npc.net.ID)
                ?.OnGuardDeath(npc, hitInfo?.InitiatorPlayer);
        }
        
        private void OnEntityKill(ScientistNPC npc)
        {
            EntityCache.FindEventByEntity(npc.net.ID)
                ?.OnGuardDeath(npc, null);
        }
        
        private void OnEntityDismounted(BaseMountable mountable, ScientistNPC scientist)
        {
            CH47HelicopterAIController chinook = mountable.VehicleParent() as CH47HelicopterAIController;
            if (chinook == null || chinook.IsDestroyed || chinook.OwnerID != 111999)
                return;
            
            scientist.Brain.Navigator.ForceToGround();
            scientist.Brain.Navigator.SetDestination(scientist.finalDestination);
            scientist.Brain.SwitchToState(AIState.MoveToVector3, -1);

            if (chinook.NumMounted() != 1)
                return;
            
            chinook.Invoke(() =>
            {
                if (chinook.IsDestroyed)
                    return;
                
                chinook.ClearLandingTarget();
                chinook.SetMinHoverHeight(150f);
                chinook.EnableFacingOverride(false);
                
                if (!chinook.TryGetComponent(out CH47AIBrain ch47AIBrain))
                    return;
                
                ch47AIBrain.ResetState();
                ch47AIBrain.SwitchToState(AIState.Egress, ch47AIBrain.currentStateContainerID);
            }, 5f);
        }

        #endregion

        #region Event

        private void PerformSetupRoutine()
        {
            DestroySetupRoutine();
            
            _setupRoutine = ServerMgr.Instance.StartCoroutine(SetupEventsRoutine());
        }

        private void DestroySetupRoutine()
        {
            if (_setupRoutine != null)
                ServerMgr.Instance.StopCoroutine(_setupRoutine);
            
            _setupRoutine = null;
        }
        
        private IEnumerator SetupEventsRoutine()
        {
            yield return CoroutineEx.waitForEndOfFrame;
            Interface.Oxide.LogDebug("Initializing (Bradley Guard Events) in ({0})s.", INITIALIZE_DELAY);
            yield return CoroutineEx.waitForSeconds(INITIALIZE_DELAY);
            
            using (new DebugStopwatch("Finished initializing (Bradley Guard Events) in ({0})ms."))
            {
                foreach (MonumentInfo monumentInfo in TerrainMeta.Path.Monuments)
                {
                    if (string.IsNullOrEmpty(monumentInfo.name))
                        continue;
                    
                    EventEntry eventEntry = _storedData.FindEntryByName(monumentInfo.name);
                    if (eventEntry == null || !eventEntry.EnabledEvent)
                        continue;

                    if (!eventEntry.IsValid)
                    {
                        PrintWarning("Skipping ({0}), invalid event settings", eventEntry.DisplayName);
                        continue;
                    }
                    
                    yield return BradleyGuardsEvent.CreateNew(monumentInfo.transform, eventEntry, _configData);
                }
            }
            
            _setupRoutine = null;
        }
        
        private class BradleyGuardsEvent : FacepunchBehaviour
        {
            public static List<BradleyGuardsEvent> EventComponents = new();
            
            public List<BaseEntity> spawnInstances = new();
            public CH47LandingZone eventLandingZone;
            public CurrentState eventState;
            public Vector3 eventPosition;
            public GuardConfig guardConfig;
            public string displayName;
            public int guardAmount;
            public BasePlayer winningPlayer;
            public Vector3 chinookPosition;
            public bool enableExtinguish;
            public bool enableUnlocking;
            public OBB bounds;
            
            public static BradleyGuardsEvent GetClosest(Vector3 position)
            {
                for (int i = 0; i < EventComponents.Count; i++)
                {
                    BradleyGuardsEvent component = EventComponents[i];
                    if (component.bounds.Contains(position)) 
                        return component;
                }

                return (BradleyGuardsEvent)null;
            }
            
            public static IEnumerator CreateNew(Transform transform, EventEntry eventEntry, ConfigData configData)
            {
                Vector3 landingRotation = transform.TransformDirection(transform.rotation.eulerAngles - eventEntry.LandingRotation);
                Vector3 landingPosition = transform.TransformPoint(eventEntry.LandingPosition);
                Vector3 chinookPosition = transform.TransformPoint(eventEntry.ChinookPosition);
                
                if (eventEntry.GuardProfile.Parsed == null)
                    eventEntry.GuardProfile.CacheConfig();
                
                BradleyGuardsEvent component = Utilities.CreateObjectWithComponent<BradleyGuardsEvent>(landingPosition, Quaternion.Euler(landingRotation), "Bradley_Guards_Event");
                component.chinookPosition = chinookPosition;
                component.ConfigureEvent(eventEntry, configData);
                component.CreateLandingZone();
                
                yield return CoroutineEx.waitForEndOfFrame;
            }

            private void ConfigureEvent(EventEntry eventEntry, ConfigData configData)
            {
                bounds = new OBB(transform.position, transform.rotation, new Bounds(eventEntry.BoundsPosition, eventEntry.BoundsSize));
                displayName = eventEntry.DisplayName;
                guardConfig = eventEntry.GuardProfile;
                guardAmount = eventEntry.GuardAmount;
                enableExtinguish = configData.EnableExtinguish;
                enableUnlocking = configData.EnableUnlocking;
            }

            public static void OnUnload()
            {
                if (Rust.Application.isQuitting)
                    return;
                
                for (int i = EventComponents.Count - 1; i >= 0; i--)
                    EventComponents[i]?.DestroyMe();
                
                EventComponents.Clear();
            }
            
            #region Unity
            
            public void Awake()
            {
                BradleyGuardsEvent.EventComponents.Add(this);
            }

            public void OnDestroy()
            {
                BradleyGuardsEvent.EventComponents.Remove(this);   
            }

            public void DestroyMe()
            {
                ClearGuards();
                UnityEngine.GameObject.Destroy(gameObject);
            }

            #endregion
            
            #region Event Management
            
            public bool IsStarted() => eventState == CurrentState.Started;

            public void StartEvent(Vector3 deathPosition)
            {
                if (IsStarted())
                    return;
                
                winningPlayer = null;
                
                eventPosition = deathPosition;
                eventState = CurrentState.Started;
                
                SpawnChinook();
                RemoveDamage();
                
                Instance?.HookResubscribe();
                Instance?.MessagePlayers(MessageKeys.EventStart, displayName);
            }

            private void StopEvent()
            {
                eventState = CurrentState.Waiting;
                
                Instance?.HookUnsubscribe();
                Instance?.MessagePlayers(MessageKeys.EventEnded, displayName);
            }

            public void ResetEvent()
            {
                ClearGuards();

                eventState = CurrentState.Waiting;
            }

            public void CheckEvent()
            {
                if (enableExtinguish)
                    RemoveFlames();
                
                if (enableUnlocking)
                    UnlockCrates();

                if (winningPlayer != null)
                    Interface.CallHook("OnBradleyGuardsEventEnded", winningPlayer);
                
                StopEvent();
            }

            private bool HasGuards() => spawnInstances.Count > 0;

            private void UnlockCrates()
            {
                List<LockedByEntCrate> list = Facepunch.Pool.Get<List<LockedByEntCrate>>();

                try
                {
                    Vis.Entities(eventPosition, 25f, list);
                    
                    foreach (LockedByEntCrate lockedByEntCrate in list)
                    {
                        if (lockedByEntCrate.IsValid() && !lockedByEntCrate.IsDestroyed)
                        {
                            lockedByEntCrate.SetLocked(false);
                            
                            if (lockedByEntCrate.lockingEnt == null)
                                continue;
                            
                            BaseEntity entity = lockedByEntCrate.lockingEnt.GetComponent<BaseEntity>();
                            if (entity != null && !entity.IsDestroyed)
                                entity.Kill();
                        }
                    }
                }
                finally
                {
                    Facepunch.Pool.FreeUnmanaged<LockedByEntCrate>(ref list);
                }
            }

            private void RemoveFlames()
            {
                List<FireBall> list = Facepunch.Pool.Get<List<FireBall>>();

                try
                {
                    Vis.Entities<FireBall>(eventPosition, 25f, list);

                    foreach (FireBall fireBall in list)
                    {
                        if (fireBall.IsValid() && !fireBall.IsDestroyed)
                            fireBall.Extinguish();
                    }
                }
                finally
                {
                    Facepunch.Pool.FreeUnmanaged<FireBall>(ref list);
                }
            }
            
            private void RemoveDamage()
            {
                List<FireBall> list = Facepunch.Pool.Get<List<FireBall>>();

                try
                {
                    Vis.Entities<FireBall>(eventPosition, 25f, list);

                    foreach (FireBall fireBall in list)
                    {
                        if (fireBall.IsValid() && !fireBall.IsDestroyed)
                            fireBall.ignoreNPC = true;
                    }
                }
                finally
                {
                    Facepunch.Pool.FreeUnmanaged<FireBall>(ref list);
                }
            }

            #endregion

            #region Chinook

            private void SpawnChinook()
            {
                CH47HelicopterAIController component = GameManager.server.CreateEntity("assets/prefabs/npc/ch47/ch47scientists.entity.prefab", chinookPosition, Quaternion.identity)?.GetComponent<CH47HelicopterAIController>();
                component.transform.position = chinookPosition;
                component.SetLandingTarget(eventLandingZone.transform.position);
                component.SetMinHoverHeight(0.0f);
                component.OwnerID = 111999;
                component.Spawn();
                component.CancelInvoke(new Action(component.CheckSpawnScientists));
                component.Invoke(new Action(() => SpawnGuards(component, guardAmount)), 0.25f);
                component.Invoke(new Action(() => DropSmokeGrenade(eventLandingZone.transform.position)), 1f);
                
                CustomProtection.ModifyProtection(component);
            }
            
            private void DropSmokeGrenade(Vector3 position)
            {
                SmokeGrenade component = GameManager.server.CreateEntity("assets/prefabs/tools/smoke grenade/grenade.smoke.deployed.prefab", position, Quaternion.identity).GetComponent<SmokeGrenade>();
                component.smokeDuration = 60f;
                component.Spawn();
            }

            #endregion

            #region Landing

            public void CreateLandingZone()
            {
                eventLandingZone = gameObject.AddComponent<CH47LandingZone>();
                eventLandingZone.enabled = true;
            }

            #endregion

            #region Guard
            
            private void SpawnGuards(CH47HelicopterAIController chinook, int numToSpawn)
            {
                int num = Mathf.Clamp(numToSpawn, 1, chinook.mountPoints.Count);
                
                for (int i = 0; i < 2; i++)
                    SpawnGuard(chinook, chinook.transform.position + chinook.transform.forward * 10f);

                num -= 2;
                
                if (num <= 0)
                    return;
                
                for (int i = 0; i < num; i++)
                    SpawnGuard(chinook, chinook.transform.position - chinook.transform.forward * 15f);
            }

            private void SpawnGuard(CH47HelicopterAIController chinook, Vector3 position)
            {
                Vector3 destination = eventPosition.GetPointAround(2f);
                
                guardConfig.Parsed["HomePosition"] = destination.ToString();
                guardConfig.Parsed["Kit"] = guardConfig.Kits.Length > 0 ? guardConfig.Kits.GetRandom() : string.Empty;
                
                ScientistNPC scientist = (ScientistNPC)Instance?.NpcSpawn.Call("SpawnNpc", position, guardConfig.Parsed);
                if (scientist == null || scientist.IsDestroyed)
                    return;
                
                scientist.finalDestination = destination;
                CachedGuardAdd(scientist);
                chinook.AttemptMount((BasePlayer) scientist, false);
            }
            
            public void ClearGuards()
            {
                for (int i = spawnInstances.Count - 1; i >= 0; i--)
                {
                    BaseEntity entity = spawnInstances[i];
                    if (entity != null && !entity.IsDestroyed)
                        entity.Kill();
                }
                
                spawnInstances.Clear();
            }
            
            #endregion
            
            #region Oxide Hooks
            
            public void OnGuardDeath(ScientistNPC npc, BasePlayer player)
            {
                CachedGuardRemove(npc);
                
                if (HasGuards())
                    return;
                
                winningPlayer = player;
                CheckEvent();
            }
            
            #endregion
            
            #region Cache Guard Entity

            private void CachedGuardAdd(BaseEntity entity)
            {
                spawnInstances.Add(entity);
                
                EntityCache.CreateEntity(entity.net.ID, this);
            }

            private void CachedGuardRemove(BaseEntity entity)
            {
                spawnInstances.Remove(entity);
                
                EntityCache.RemoveEntity(entity.net.ID);
            }

            #endregion
            
            #region Debug Info

            public void DisplayInfo(BasePlayer player, float duration)
            {
                bool isAdmin = player.IsAdmin;
                
                try
                {
                    if (!isAdmin)
                        player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);

                    Utilities.DText(player.net.connection, transform.position, "Landing Position", Color.magenta, duration);
                    Utilities.DText(player.net.connection, chinookPosition, "Chinook Position", Color.green, duration);
                    Utilities.DLine(player.net.connection, chinookPosition, eventLandingZone.transform.position, Color.yellow, duration);
                    Utilities.DText(player.net.connection, bounds.position, "Bounds Position", Color.cyan, duration);
                    Utilities.DCube(player.net.connection, bounds.position, bounds.rotation, bounds.extents, Color.blue, duration);
                }
                finally
                {
                    if (!isAdmin)
                        player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                }
            }

            #endregion
        }

        private enum CurrentState
        {
            Waiting, 
            Started
        }
        
        #endregion
        
        #region Entities Lookup

        private static class EntityCache
        {
            public static Dictionary<ulong, BradleyGuardsEvent> Entities = new Dictionary<ulong, BradleyGuardsEvent>();
            
            public static void OnUnload() => Entities.Clear();
            
            public static BradleyGuardsEvent FindEventByEntity(NetworkableId id)
            {
                if (!id.IsValid)
                    return null;
                
                Entities.TryGetValue(id.Value, out BradleyGuardsEvent component);
                return component;
            }

            public static void CreateEntity(NetworkableId id, BradleyGuardsEvent component)
            {
                if (!id.IsValid)
                    return;
                
                Entities.Add(id.Value, component);
                //Interface.Oxide.LogDebug("EntityCache::CreateEntity: {0}", Entities.Count);
            }

            public static void RemoveEntity(NetworkableId id)
            {
                if (!id.IsValid)
                    return;
                
                Entities.Remove(id.Value);
                //Interface.Oxide.LogDebug("EntityCache::RemoveEntity: {0}", Entities.Count);
            }
        }

        #endregion        
        
        #region External Hooks

        private object OnNpcRustEdit(ScientistNPC npc)
        {
            if (npc?.net == null)
                return null;
            
            return EntityCache.FindEventByEntity(npc.net.ID) != null ? _objTrue : null;
        }

        #endregion
        
        #region Hook Subscriber
        
        private void HookUnsubscribe() => Unsubscribe(nameof(OnEntityDismounted));
        
        private void HookResubscribe() => Subscribe(nameof(OnEntityDismounted));

        #endregion
        
        #region Custom Protection

        private static class CustomProtection
        {
            private static ProtectionProperties ProtectionInstance;
            
            public static void Initialize()
            {
                ProtectionInstance = ScriptableObject.CreateInstance<ProtectionProperties>();
                ProtectionInstance.name = "Bradley_Guards_Protection";
                ProtectionInstance.Add(1);
            }

            public static void OnUnload()
            {
                if (ProtectionInstance == null)
                    return;
                
                UnityEngine.Object.Destroy(ProtectionInstance);
                ProtectionInstance = null;
            }

            public static void ModifyProtection(BaseCombatEntity combatEntity)
            {
                if (combatEntity != null && !combatEntity.IsDestroyed) 
                    combatEntity.baseProtection = ProtectionInstance;
            }
        }

        #endregion
        
        #region Chat Command
        
        private void EventCommands(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERM_USE))
            {
                MessagePlayer(player, MessageKeys.NoPermission);
                return;
            }
            
            if (args.Length < 2)
                goto INVALID_SYNTAX;
            
            string option = args[1];
            
            if (option.Equals("enable"))
            {
                EventEntry eventEntry = FindEventEntry(args[0]);
                if (eventEntry == null)
                {
                    MessagePlayer(player, MessageKeys.EventNotFound);
                    return;
                }
                
                if (args.Length != 3)
                    goto INVALID_SYNTAX;
                
                if (bool.TryParse(args[2], out bool enabled))
                {
                    MessagePlayer(player, MessageKeys.InvalidBooleanValue);
                    return;
                }
                
                eventEntry.EnabledEvent = enabled;
                
                SaveData();
                MessagePlayer(player, MessageKeys.EventUpdated);
                return;
            }
            
            if (option.Equals("display"))
            {
                EventEntry eventEntry = FindEventEntry(args[0]);
                if (eventEntry == null)
                {
                    MessagePlayer(player, MessageKeys.EventNotFound);
                    return;
                }
                
                if (args.Length != 3)
                    goto INVALID_SYNTAX;
                
                string displayName = string.Join(" ", args.Skip(2));
                if (string.IsNullOrEmpty(displayName))
                {
                    MessagePlayer(player, MessageKeys.DisplayNameEmpty);
                    return;
                }
                
                eventEntry.DisplayName = displayName;
                
                SaveData();
                MessagePlayer(player, MessageKeys.EventUpdated);
                return;
            }
            
            if (option.Equals("amount"))
            {
                EventEntry eventEntry = FindEventEntry(args[0]);
                if (eventEntry == null)
                {
                    MessagePlayer(player, MessageKeys.EventNotFound);
                    return;
                }
                
                if (args.Length != 3)
                    goto INVALID_SYNTAX;

                int amount;
                if (!int.TryParse(args[2], out amount) || (amount < 2 || amount > 10))
                {
                    MessagePlayer(player, MessageKeys.InvalidGuardAmount);
                    return;
                }

                eventEntry.GuardAmount = amount;
                
                SaveData();
                MessagePlayer(player, MessageKeys.EventUpdated);
                return;
            }
            
            if (option.Equals("loadout"))
            {
                EventEntry eventEntry = FindEventEntry(args[0]);
                if (eventEntry == null)
                {
                    MessagePlayer(player, MessageKeys.EventNotFound);
                    return;
                }
                
                if (player.inventory == null)
                    return;
                
                eventEntry.GuardProfile.BeltItems = GuardConfig.BeltEntry.SaveItems(player.inventory.containerBelt);
                eventEntry.GuardProfile.WearItems = GuardConfig.WearEntry.SaveItems(player.inventory.containerWear);
                eventEntry.GuardProfile.CacheConfig();
                SaveData();
                MessagePlayer(player, MessageKeys.EventUpdated);
                return;
            }
            
            if (option.Equals("debug"))
            {
                BradleyGuardsEvent component = BradleyGuardsEvent.EventComponents.Find(x => x.displayName.StartsWith(args[0], StringComparison.OrdinalIgnoreCase));
                if (component == null)
                {
                    MessagePlayer(player, MessageKeys.EventNotFound);
                    return;
                }

                component.DisplayInfo(player, _configData.DebugDuration);
                MessagePlayer(player, MessageKeys.EventDebug, _configData.DebugDuration);
                return;
            }

            INVALID_SYNTAX:
            EventHelpText(player, command, _configData.DebugDuration);
        }

        private void EventHelpText(BasePlayer player, string command, float duration)
        {
            StringBuilder sb = Facepunch.Pool.Get<StringBuilder>();
            
            try
            {
                sb.Clear();
                sb.AppendFormat(lang.GetMessage(MessageKeys.Prefix, this, player.UserIDString))
                    .AppendFormat(lang.GetMessage(MessageKeys.HelpEventEnable, this, player.UserIDString), command)
                    .AppendFormat(lang.GetMessage(MessageKeys.HelpEventName, this, player.UserIDString), command)
                    .AppendFormat(lang.GetMessage(MessageKeys.HelpEventDebug, this, player.UserIDString), command, duration)
                    .AppendFormat(lang.GetMessage(MessageKeys.HelpGuardAmount, this, player.UserIDString), command)
                    .AppendFormat(lang.GetMessage(MessageKeys.HelpGuardLoadout, this, player.UserIDString), command);
            
                player.ChatMessage(sb.ToString());
            }
            finally
            {
                sb.Clear();
                Facepunch.Pool.FreeUnmanaged(ref sb);
            }
        }

        #endregion

        #region Debug Stopwatch

        private class DebugStopwatch : IDisposable
        {
            private Stopwatch _stopwatch;
            private string _format;

            public DebugStopwatch(string format)
            {
                _format = format;
                
                _stopwatch = Facepunch.Pool.Get<Stopwatch>();
                _stopwatch.Start();
            }

            public void Dispose()
            {
                Interface.Oxide.LogDebug(_format, _stopwatch.ElapsedMilliseconds);
                
                _stopwatch.Reset();
                
                Facepunch.Pool.FreeUnmanaged(ref _stopwatch);
            }
        }

        #endregion

        #region Utilities

        private static class Utilities
        {
            public static T CreateObjectWithComponent<T>(Vector3 position, Quaternion rotation, string name) where T : MonoBehaviour
            {
                return new GameObject(name)
                {
                    layer = (int)Layer.Reserved1,
                    transform =
                    {
                        position = position,
                        rotation = rotation
                    }
                }.AddComponent<T>();
            }
            
            public static void Segments(Connection connection, Vector3 origin, Vector3 target, Color color, float duration)
            {
                Vector3 delta = target - origin;
                float distance = delta.magnitude;
                Vector3 direction = delta.normalized;

                float segmentLength = 10f;
                int numSegments = Mathf.CeilToInt(distance / segmentLength);

                for (int i = 0; i < numSegments; i++)
                {
                    float length = segmentLength;
                    if (i == numSegments - 1 && distance % segmentLength != 0)
                        length = distance % segmentLength;

                    Vector3 start = origin + i * segmentLength * direction;
                    Vector3 end = start + length * direction;
                    
                    Utilities.DLine(connection, start, end, color, duration);
                }
            }
            
            public static void DLine(Connection connection, Vector3 start, Vector3 end, Color color, float duration)
            {
                if (connection == null)     
                    return;
                
                ConsoleNetwork.SendClientCommand(connection, "ddraw.line", duration, color, start, end);
            }
            
            public static void DText(Connection connection, Vector3 origin, string text, Color color, float duration)
            {
                if (connection == null)     
                    return;
                
                ConsoleNetwork.SendClientCommand(connection, "ddraw.text", duration, color, origin, text);
            }
            
            public static void DCube(Connection connection, Vector3 center, Quaternion rotation, Vector3 extents, Color color, float duration)
            {
                Vector3 forwardUpperLeft = center + rotation * extents.WithX(-extents.x);
                Vector3 forwardUpperRight = center + rotation * extents;
                Vector3 forwardLowerLeft = center + rotation * extents.WithX(-extents.x).WithY(-extents.y);
                Vector3 forwardLowerRight = center + rotation * extents.WithY(-extents.y);
                Vector3 backLowerRight = center + rotation * -extents.WithX(-extents.x);
                Vector3 backLowerLeft = center + rotation * -extents;
                Vector3 backUpperRight = center + rotation * -extents.WithX(-extents.x).WithY(-extents.y);
                Vector3 backUpperLeft = center + rotation * -extents.WithY(-extents.y);
                
                Utilities.Segments(connection, forwardUpperLeft, forwardUpperRight, color, duration);
                Utilities.Segments(connection, forwardLowerLeft, forwardLowerRight, color, duration);
                Utilities.Segments(connection, forwardUpperLeft, forwardLowerLeft, color, duration);
                Utilities.Segments(connection, forwardUpperRight, forwardLowerRight, color, duration);

                Utilities.Segments(connection, backUpperLeft, backUpperRight, color, duration);
                Utilities.Segments(connection, backLowerLeft, backLowerRight, color, duration);
                Utilities.Segments(connection, backUpperLeft, backLowerLeft, color, duration);
                Utilities.Segments(connection, backUpperRight, backLowerRight, color, duration);

                Utilities.Segments(connection, forwardUpperLeft, backUpperLeft, color, duration);
                Utilities.Segments(connection, forwardLowerLeft, backLowerLeft, color, duration);
                Utilities.Segments(connection, forwardUpperRight, backUpperRight, color, duration);
                Utilities.Segments(connection, forwardLowerRight, backLowerRight, color, duration);
            }
        }

        #endregion

        #region Json Converters

        private class UnityVector3Converter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                Vector3 vector = (Vector3)value;
                writer.WriteValue($"{vector.x} {vector.y} {vector.z}");
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.String)
                {
                    string[] strings = reader.Value.ToString().Trim().Split(' ');
                    return new Vector3(Convert.ToSingle(strings[0]), Convert.ToSingle(strings[1]), Convert.ToSingle(strings[2]));
                }
                
                JObject jObject = JObject.Load(reader);
                return new Vector3(Convert.ToSingle(jObject["x"]), Convert.ToSingle(jObject["y"]), Convert.ToSingle(jObject["z"]));
            }

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(Vector3) || (objectType.IsGenericType && objectType.GetGenericTypeDefinition() == typeof(Nullable<>) && objectType.GenericTypeArguments[0] == typeof(Vector3));
            }
        }

        #endregion
    }
}

namespace Oxide.Plugins.BradleyGuardsExtensionMethods
{
    public static class ExtensionMethods
    {
        public static bool IsReady(this Plugin plugin) => plugin != null && plugin.IsLoaded;
        
        public static Vector3 GetPointAround(this Vector3 position, float radius)
        {
            float angle = UnityEngine.Random.value * 360f;
            
            Vector3 pointAround = position;
            pointAround.x = position.x + radius * Mathf.Sin(angle * Mathf.Deg2Rad);
            pointAround.z = position.z + radius * Mathf.Cos(angle * Mathf.Deg2Rad);
            pointAround.y = position.y;
            pointAround.y = TerrainMeta.HeightMap.GetHeight(pointAround);
            return pointAround;
        }
    }
}
