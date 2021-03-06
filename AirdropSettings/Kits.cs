using System.Collections.Generic;
using System;
using System.Linq;

using Newtonsoft.Json;

using UnityEngine;

using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Kits", "Reneb", "3.1.3")]
    class Kits : RustPlugin
    {
        readonly int playerLayer = LayerMask.GetMask("Player (Server)");

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// Plugin initialization
        //////////////////////////////////////////////////////////////////////////////////////////

        void Loaded()
        {
            LoadData();
            try
            {
                kitsData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Dictionary<string, KitData>>>("Kits_Data");
            }
            catch
            {
                kitsData = new Dictionary<ulong, Dictionary<string, KitData>>();
            }
        }

        void OnServerInitialized()
        {
            InitializePermissions();
        }

        void InitializePermissions()
        {
            foreach (var kit in storedData.Kits.Values)
            {
                if (kit.permission != null)
                    if (!permission.PermissionExists(kit.permission)) permission.RegisterPermission(kit.permission, this);
            }
        }
        //////////////////////////////////////////////////////////////////////////////////////////
        ///// Configuration
        //////////////////////////////////////////////////////////////////////////////////////////

        Dictionary<ulong, GUIKit> GUIKits;

        class GUIKit
        {
            public string description = string.Empty;
            public List<string> kits = new List<string>();
        }

        void LoadDefaultConfig() { }

        void Init()
        {
            var config = Config.ReadObject<Dictionary<string, object>>();
            if (!config.ContainsKey("NPC - GUI Kits"))
            {
                config["NPC - GUI Kits"] = GetExampleGUIKits();
                Config.WriteObject(config);
            }
            var keys = config.Keys.ToArray();
            if (keys.Length > 1)
            {
                foreach (var key in keys)
                {
                    if (!key.Equals("NPC - GUI Kits"))
                        config.Remove(key);
                }
                Config.WriteObject(config);
            }
            GUIKits = JsonConvert.DeserializeObject<Dictionary<ulong, GUIKit>>(JsonConvert.SerializeObject(config["NPC - GUI Kits"]));
        }

        static Dictionary<ulong, GUIKit> GetExampleGUIKits()
        {
            return new Dictionary<ulong, GUIKit>
            {
                {
                    1235439, new GUIKit
                    {
                        kits = {"kit1", "kit2"},
                        description = "Welcome on this server, Here is a list of free kits that you can get <color=red>only once each</color>\n\n                      <color=green>Enjoy your stay</color>"
                    }
                },
                {
                    8753201223, new GUIKit
                    {
                        kits = {"kit1", "kit3"},
                        description = "<color=red>VIPs Kits</color>"
                    }
                }
            };
        }

        void OnPlayerRespawned(BasePlayer player)
        {
            if (!storedData.Kits.ContainsKey("autokit")) return;
            var thereturn = Interface.Oxide.CallHook("canRedeemKit", player);
            if (thereturn == null)
            {
                player.inventory.Strip();
                GiveKit(player, "autokit");
            }
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// Kit Creator
        //////////////////////////////////////////////////////////////////////////////////////////

        List<KitItem> GetPlayerItems(BasePlayer player)
        {
            var kititems = player.inventory.containerWear.itemList.Select(item => new KitItem
            {
                itemid = item.info.itemid,
                bp = item.IsBlueprint(),
                container = "wear",
                amount = item.amount,
                skinid = item.skin
            }).ToList();
            kititems.AddRange(player.inventory.containerMain.itemList.Select(item => new KitItem
            {
                itemid = item.info.itemid,
                bp = item.IsBlueprint(),
                container = "main",
                amount = item.amount,
                skinid = item.skin
            }));
            kititems.AddRange(player.inventory.containerBelt.itemList.Select(item => new KitItem
            {
                itemid = item.info.itemid,
                bp = item.IsBlueprint(),
                container = "belt",
                amount = item.amount,
                skinid = item.skin
            }));
            return kititems;
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// Kit Redeemer
        //////////////////////////////////////////////////////////////////////////////////////////

        void TryGiveKit(BasePlayer player, string kitname)
        {
            var success = CanRedeemKit(player, kitname) as string;
            if (success != null)
            {
                SendReply(player, success);
                return;
            }
            success = GiveKit(player, kitname) as string;
            if (success != null)
            {
                SendReply(player, success);
                return;
            }
            SendReply(player, "?????????? "+kitname+" ??????????????");

            proccessKitGiven(player, kitname);
        }
        void proccessKitGiven(BasePlayer player, string kitname)
        {
            Kit kit;
            if (!storedData.Kits.TryGetValue(kitname, out kit)) return;

            var kitData = GetKitData(player.userID, kitname);
            if (kit.max > 0)
                kitData.max += 1;

            if (kit.cooldown > 0)
                kitData.cooldown = CurrentTime() + kit.cooldown;
        }
        object GiveKit(BasePlayer player, string kitname)
        {
            Kit kit;
            if (!storedData.Kits.TryGetValue(kitname, out kit)) return "This kit doesn't exist";

            foreach (var kitem in kit.items)
            {
                var item = ItemManager.CreateByItemID(kitem.itemid, kitem.amount, kitem.bp);
                item.skin = kitem.skinid;
                var given = player.inventory.GiveItem(item, kitem.container == "belt" ? player.inventory.containerBelt : kitem.container == "wear" ? player.inventory.containerWear : player.inventory.containerMain);
				if (!given)
					item.Drop(player.eyes.position, player.eyes.BodyForward() * 2f, new Quaternion());
            }
            return true;
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// Check Kits
        //////////////////////////////////////////////////////////////////////////////////////////

        bool isKit(string kitname)
        {
            return storedData.Kits.ContainsKey(kitname);
        }

        static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        static double CurrentTime() { return DateTime.UtcNow.Subtract(epoch).TotalSeconds; }

        bool CanSeeKit(BasePlayer player, string kitname, bool fromNPC, out string reason)
        {
            reason = string.Empty;
            Kit kit;
            if (!storedData.Kits.TryGetValue(kitname, out kit)) return false;
            if (kit.hide)
                return false;
            if (kit.authlevel > 0)
                if (player.net.connection.authLevel < kit.authlevel)
                    return false;
            if (kit.permission != null)
                if (player.net.connection.authLevel < 2 && !permission.UserHasPermission(player.userID.ToString(), kit.permission))
                    return false;
            if (kit.npconly && !fromNPC)
                return false;
            if (kit.max > 0)
            {
                int left = GetKitData(player.userID, kitname).max;
                if (left >= kit.max)
                {
                    reason += "- 0 left";
                    return false;
                }
                reason += $"- {(kit.max - left)} left";
            }
            if (kit.cooldown > 0)
            {
                double cd = GetKitData(player.userID, kitname).cooldown;
                double ct = CurrentTime();
                if (cd > ct && cd != 0.0)
                {
                    reason += $"- {Math.Abs(Math.Ceiling(cd - ct))} seconds";
                    return false;
                }
            }
            return true;

        }

        object CanRedeemKit(BasePlayer player, string kitname)
        {
            Kit kit;
            if (!storedData.Kits.TryGetValue(kitname, out kit)) return "???????????? ???????????? ??????";

            object thereturn = Interface.Oxide.CallHook("canRedeemKit", player);
            if (thereturn != null)
            {
                if (thereturn is string) return thereturn;
                return "???? ???? ???????????? ???????????? ???????????????? ???????? ??????????";
            }

            if (kit.authlevel > 0)
                if (player.net.connection.authLevel < kit.authlevel)
                    return "?? ?????? ???????????????????????? ???????? ?????? ?????????????????? ?????????? ????????????";

            if (kit.permission != null)
                if (player.net.connection.authLevel < 2 && !permission.UserHasPermission(player.userID.ToString(), kit.permission))
                    return "?? ?????? ???????????????????????? ???????? ?????? ?????????????????? ?????????? ????????????";

            var kitData = GetKitData(player.userID, kitname);
            if (kit.max > 0)
                if (kitData.max >= kit.max)
                    return "???? ?????? ???????????????? ?????? ?????????????????? ????????????";

            if (kit.cooldown > 0)
            {
                var ct = CurrentTime();
                if (kitData.cooldown > ct && kitData.cooldown != 0.0)
                    return $"?????? ?????????? ?????????????????? {Math.Abs(Math.Ceiling(kitData.cooldown - ct))} ???????????? ???????????? ?????? ?????????? ???????????????? ???????? ??????????";
            }

            if (kit.npconly)
            {
                bool foundNPC = false;
                var neededNpc = new List<ulong>();
                foreach (var pair in GUIKits)
                {
                    if (pair.Value.kits.Contains(kitname))
                        neededNpc.Add(pair.Key);
                }
                foreach (var col in Physics.OverlapSphere(player.transform.position, 3f, playerLayer))
                {
                    var targetplayer = col.GetComponentInParent<BasePlayer>();
                    if (targetplayer == null) continue;
                    if (neededNpc.Contains(targetplayer.userID))
                    {
                        foundNPC = true;
                        break;
                    }
                }
                if (!foundNPC)
                    return "?????? ?????????? ?????????? NPC ?????????????? ???????????? ???????? ??????????.";
            }
            return true;
        }


        //////////////////////////////////////////////////////////////////////////////////////
        // Kit Class
        //////////////////////////////////////////////////////////////////////////////////////
        class KitItem
        {
            public int itemid;
            public bool bp;
            public int skinid;
            public string container;
            public int amount;
        }

        class Kit
        {
            public string name;
            public string description;
            public int max;
            public double cooldown;
            public int authlevel;
            public bool hide;
            public bool npconly;
            public string permission;
            public string image;
            public List<KitItem> items = new List<KitItem>();
        }

        //////////////////////////////////////////////////////////////////////////////////////
        // Data Manager
        //////////////////////////////////////////////////////////////////////////////////////

        private void SaveKitsData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("Kits_Data", kitsData);
        }


        private StoredData storedData;
        private Dictionary<ulong, Dictionary<string, KitData>> kitsData;

        class StoredData
        {
            public Dictionary<string, Kit> Kits = new Dictionary<string, Kit>();
        }
        class KitData
        {
            public int max;
            public double cooldown;
        }
        void ResetData()
        {
            kitsData.Clear();
            SaveKitsData();
        }

        void Unload()
        {
            SaveKitsData();
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyAllGUI(player);
            }
        }
        void OnServerSave()
        {
            SaveKitsData();
        }

        void SaveKits()
        {
            Interface.Oxide.DataFileSystem.WriteObject("Kits", storedData);
        }

        void LoadData()
        {
            var kits = Interface.Oxide.DataFileSystem.GetFile("Kits");
            try
            {
                kits.Settings.NullValueHandling = NullValueHandling.Ignore;
                storedData = kits.ReadObject<StoredData>();
            }
            catch
            {
                storedData = new StoredData();
            }
            kits.Settings.NullValueHandling = NullValueHandling.Include;
        }

        KitData GetKitData(ulong userID, string kitname)
        {
            Dictionary<string, KitData> kitDatas;
            if (!kitsData.TryGetValue(userID, out kitDatas))
                kitsData[userID] = kitDatas = new Dictionary<string, KitData>();
            KitData kitData;
            if (!kitDatas.TryGetValue(kitname, out kitData))
                kitDatas[kitname] = kitData = new KitData();
            return kitData;
        }

        //////////////////////////////////////////////////////////////////////////////////////
        // Kit Editor
        //////////////////////////////////////////////////////////////////////////////////////

        readonly Dictionary<ulong, string> kitEditor = new Dictionary<ulong, string>();


        //////////////////////////////////////////////////////////////////////////////////////
        // GUI
        //////////////////////////////////////////////////////////////////////////////////////

        readonly Dictionary<ulong, PLayerGUI> PlayerGUI = new Dictionary<ulong, PLayerGUI>();

        class PLayerGUI
        {
            public ulong guiid;
            public int page;
        }

        private const string overlayjson = @"[
			{
				""name"": ""KitOverlay"",
				""parent"": ""HUD/Overlay"",
				""components"":
				[
					{
						 ""type"":""UnityEngine.UI.Image"",
						 ""color"":""0.1 0.1 0.1 0.8"",
					},
					{
						""type"":""RectTransform"",
						""anchormin"": ""0 0"",
						""anchormax"": ""1 1""
					},
					{
						""type"":""NeedsCursor"",
					}
				]
			},
			{
				""parent"": ""KitOverlay"",
				""components"":
				[
					{
						""type"":""UnityEngine.UI.Text"",
						""text"":""{msg}"",
						""fontSize"":15,
						""align"": ""MiddleLeft"",
					},
					{
						""type"":""RectTransform"",
						""anchormin"": ""0.1 0.7"",
						""anchormax"": ""0.9 0.9""
					}
				]
			},
			{
				""parent"": ""KitOverlay"",
				""components"":
				[
					{
						""type"":""UnityEngine.UI.Text"",
						""text"":""????????????????"",
						""fontSize"":15,
						""align"": ""MiddleLeft"",
					},
					{
						""type"":""RectTransform"",
						""anchormin"": ""0.15 0.65"",
						""anchormax"": ""0.25 0.7""
					}
				]
			},
			{
				""parent"": ""KitOverlay"",
				""components"":
				[
					{
						""type"":""UnityEngine.UI.Text"",
						""text"":""Description"",
						""fontSize"":10,
						""align"": ""MiddleLeft"",
					},
					{
						""type"":""RectTransform"",
						""anchormin"": ""0.25 0.65"",
						""anchormax"": ""0.70 0.7""
					}
				]
			},
			{
				""parent"": ""KitOverlay"",
				""components"":
				[
					{
						""type"":""UnityEngine.UI.Text"",
						""text"":""???????????????? ?????????? (??)"",
						""fontSize"":15,
						""align"": ""MiddleLeft"",
					},
					{
						""type"":""RectTransform"",
						""anchormin"": ""0.50 0.65"",
						""anchormax"": ""0.75 0.7""
					}
				]
			},
			{
				""parent"": ""KitOverlay"",
				""components"":
				[
					{
						""type"":""UnityEngine.UI.Text"",
						""text"":"""",
						""fontSize"":15,
						""align"": ""MiddleLeft"",
					},
					{
						""type"":""RectTransform"",
						""anchormin"": ""0.75 0.65"",
						""anchormax"": ""0.80 0.7""
					}
				]
			},
			{
				""parent"": ""KitOverlay"",
				""components"":
				[
					{
						""type"":""UnityEngine.UI.Text"",
						""text"":""????????????????"",
						""fontSize"":15,
						""align"": ""MiddleLeft"",
					},
					{
						""type"":""RectTransform"",
						""anchormin"": ""0.70 0.65"",
						""anchormax"": ""0.90 0.7""
					}
				]
			},
			{
				""parent"": ""KitOverlay"",
				""components"":
				[
					{
						""type"":""UnityEngine.UI.Text"",
						""text"":""Close"",
						""fontSize"":20,
						""align"": ""MiddleCenter"",
					},
					{
						""type"":""RectTransform"",
						""anchormin"": ""0.5 0.15"",
						""anchormax"": ""0.7 0.20""
					},
				]
			},
			{
				""parent"": ""KitOverlay"",
				""components"":
				[
					{
						""type"":""UnityEngine.UI.Button"",
						""command"":""kit.close"",
						""color"": ""0.5 0.5 0.5 0.2"",
						""imagetype"": ""Tiled""
					},
					{
						""type"":""RectTransform"",
						""anchormin"": ""0.5 0.15"",
						""anchormax"": ""0.7 0.20""
					}
				]
			}
		]
		";
        private const string kitlistoverlay = @"[
			{
				""name"": ""KitListOverlay"",
				""parent"": ""KitOverlay"",
				""components"":
				[
					{
						""type"":""UnityEngine.UI.Image"",
						""color"":""0 0 0 0"",
					},
					{
						""type"":""RectTransform"",
						""anchormin"": ""0 0.20"",
						""anchormax"": ""1 0.65""
					}
				]
			}
		]
		";

        private const string buttonjson = @"[
			{
				""parent"": ""KitListOverlay"",
				""components"":
				[
					{
						""type"":""UnityEngine.UI.RawImage"",
						""imagetype"": ""Tiled"",
						""url"": ""{imageurl}""
                    },
					{
						""type"":""RectTransform"",
						""anchormin"": ""0.10 {ymin}"",
						""anchormax"": ""0.14 {ymax}""
					}
				]
			},
			{
				""parent"": ""KitListOverlay"",
				""components"":
				[
					{
						""type"":""UnityEngine.UI.Text"",
						""text"":""{kitfullname}"",
						""fontSize"":15,
						""align"": ""MiddleLeft"",
                    },
					{
						""type"":""RectTransform"",
						""anchormin"": ""0.15 {ymin}"",
						""anchormax"": ""0.25 {ymax}""
					}
				]
			},
			{
				""parent"": ""KitListOverlay"",
				""components"":
				[
					{
						""type"":""UnityEngine.UI.Text"",
						""text"":""{kitdescription}"",
						""fontSize"":12,
						""align"": ""MiddleLeft"",
                    },
					{
						""type"":""RectTransform"",
						""anchormin"": ""0.25 {ymin}"",
						""anchormax"": ""0.70 {ymax}""
					}
				]
			},
			{
				""parent"": ""KitListOverlay"",
				""components"":
				[
					{
						""type"":""UnityEngine.UI.Text"",
						""text"":""{cooldown}"",
						""fontSize"":15,
						""align"": ""MiddleLeft"",
                    },
					{
						""type"":""RectTransform"",
						""anchormin"": ""0.50 {ymin}"",
						""anchormax"": ""0.75 {ymax}""
					}
				]
			},
			{
				""parent"": ""KitListOverlay"",
				""components"":
				[
					{
						""type"":""UnityEngine.UI.Text"",
						""text"":""{left}"",
						""fontSize"":15,
						""align"": ""MiddleLeft"",
                    },
					{
						""type"":""RectTransform"",
						""anchormin"": ""0.75 {ymin}"",
						""anchormax"": ""0.80 {ymax}""
					}
				]
			},
			{
				""parent"": ""KitListOverlay"",
				""components"":
				[
					{
						""type"":""UnityEngine.UI.Text"",
						""text"":""?????????????? ???????????? ?????????? ????????????????"",
						""fontSize"":15,
						""align"": ""MiddleCenter"",
                    },
                    {
						""type"":""RectTransform"",
						""anchormin"": ""0.70 {ymin}"",
						""anchormax"": ""0.90 {ymax}""
					}
                ]
            },
            {
                ""parent"": ""KitListOverlay"",
				""components"":
				[
					{
						""type"":""UnityEngine.UI.Button"",
						""command"":""kit.gui {guimsg}"",
						""color"": ""{color}"",
						""imagetype"": ""Tiled""
					},
					{
						""type"":""RectTransform"",
						""anchormin"": ""0.70 {ymin}"",
						""anchormax"": ""0.90 {ymax}""
					}
				]
			}
		]
		";

        private const string kitchangepage = @"[
		{
			""parent"": ""KitListOverlay"",
			""components"":
			[
				{
					""type"":""UnityEngine.UI.Text"",
					""text"":""<<"",
					""fontSize"":20,
					""align"": ""MiddleCenter"",
				},
				{
					""type"":""RectTransform"",
					""anchormin"": ""0.2 0"",
					""anchormax"": ""0.3 0.1""
				}
			]
		},
		{
			""parent"": ""KitListOverlay"",
			""components"":
			[
				{
					""type"":""UnityEngine.UI.Button"",
					""color"": ""0.5 0.5 0.5 0.2"",
					""command"":""kit.show {pageminus}"",
					""imagetype"": ""Tiled""
				},
				{
					""type"":""RectTransform"",
					""anchormin"": ""0.2 0"",
					""anchormax"": ""0.3 0.1""
				}
			]
		},
		{
			""parent"": ""KitListOverlay"",
			""components"":
			[
				{
					""type"":""UnityEngine.UI.Text"",
					""text"":"">>"",
					""fontSize"":20,
					""align"": ""MiddleCenter"",
				},
				{
					""type"":""RectTransform"",
					""anchormin"": ""0.35 0"",
					""anchormax"": ""0.45 0.1""
				}
			]
		},
		{
			""parent"": ""KitListOverlay"",
			""components"":
			[
				{
					""type"":""UnityEngine.UI.Button"",
					""color"": ""0.5 0.5 0.5 0.2"",
					""command"":""kit.show {pageplus}"",
					""imagetype"": ""Tiled""
				},
				{
					""type"":""RectTransform"",
					""anchormin"": ""0.35 0"",
					""anchormax"": ""0.45 0.1""
				}
			]
		},
		]
		";

        void NewKitPanel(BasePlayer player, ulong guiId = 0)
        {
            DestroyAllGUI(player);
            GUIKit kitpanel;
            if (!GUIKits.TryGetValue(guiId, out kitpanel)) return;

            //Game.Rust.Cui.CuiHelper.AddUi(player, overlayjson.Replace("{msg}", kitpanel.description));
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", new Facepunch.ObjectList(overlayjson.Replace("{msg}", kitpanel.description), null, null, null, null));

            RefreshKitPanel(player, guiId, 0);
        }
        void RefreshKitPanel(BasePlayer player, ulong guiId, int minKit = 0)
        {
            PLayerGUI playerGUI;
            if (!PlayerGUI.TryGetValue(player.userID, out playerGUI))
                PlayerGUI[player.userID] = playerGUI = new PLayerGUI();
            playerGUI.guiid = guiId;
            playerGUI.page = minKit;

            DestroyGUI(player, "KitListOverlay");
            //Game.Rust.Cui.CuiHelper.AddUi(player, kitlistoverlay);
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", new Facepunch.ObjectList(kitlistoverlay, null, null, null, null));
            var kitpanel = GUIKits[guiId];

            int current = 0;
            foreach (var kitname in kitpanel.kits)
            {
                if (current >= minKit && current < minKit + 8)
                {
                    string reason = string.Empty;
                    var cansee = CanSeeKit(player, kitname.ToLower(), true, out reason);
                    if (!cansee && string.IsNullOrEmpty(reason)) continue;

                    Kit kit = storedData.Kits[kitname.ToLower()];
                    var kitData = GetKitData(player.userID, kitname.ToLower());

                    var ckit = buttonjson.Replace("{color}", "0.5 0.5 0.5 0.2");
                    ckit = ckit.Replace("{guimsg}", $"'{kitname.ToLower()}'");
                    ckit = ckit.Replace("{ymin}", (1 - ((current - minKit) + 1) * 0.0975).ToString());
                    ckit = ckit.Replace("{ymax}", (1 - (current - minKit) * 0.0975).ToString());
                    ckit = ckit.Replace("{kitfullname}", kit.name);
                    ckit = ckit.Replace("{kitdescription}", kit.description ?? string.Empty);
                    ckit = ckit.Replace("{imageurl}", kit.image ?? "http://i.imgur.com/xxQnE1R.png");
                    ckit = ckit.Replace("{left}", kit.max <= 0 ? string.Empty : (kit.max - kitData.max).ToString());
                    ckit = ckit.Replace("{cooldown}", kit.cooldown <= 0 ? string.Empty : CurrentTime() > kitData.cooldown ? "0" : Math.Abs(Math.Ceiling(CurrentTime() - kitData.cooldown)).ToString());
                    //Game.Rust.Cui.CuiHelper.AddUi(player, ckit);
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", new Facepunch.ObjectList(ckit, null, null, null, null));
                }
                current++;
            }

            int pageminus = minKit - 8 < 0 ? 0 : minKit - 8;
            int pageplus = minKit + 8 > current ? minKit : minKit + 8;
            var kpage = kitchangepage.Replace("{pageminus}", pageminus.ToString()).Replace("{pageplus}", pageplus.ToString());
            //Game.Rust.Cui.CuiHelper.AddUi(player, kpage);
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", new Facepunch.ObjectList(kpage,null,null,null,null));
        }

        void DestroyAllGUI(BasePlayer player) { Game.Rust.Cui.CuiHelper.DestroyUi(player, "KitOverlay"); }
        void DestroyGUI(BasePlayer player, string GUIName) { Game.Rust.Cui.CuiHelper.DestroyUi(player, GUIName); }
        void OnUseNPC(BasePlayer npc, BasePlayer player)
        {
            if (!GUIKits.ContainsKey(npc.userID)) return;
            NewKitPanel(player, npc.userID);
        }


        //////////////////////////////////////////////////////////////////////////////////////
        // Console Command
        //////////////////////////////////////////////////////////////////////////////////////
        [ConsoleCommand("kit.gui")]
        void cmdConsoleKitGui(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
            {
                SendReply(arg, "You can't use this command from the server console");
                return;
            }
            if (!arg.HasArgs())
            {
                SendReply(arg, "You are not allowed to use manually this command");
                return;
            }
            var player = arg.Player();
            var kitname = arg.Args[0].Substring(1, arg.Args[0].Length - 2);
            TryGiveKit(player, kitname);
            RefreshKitPanel(player, PlayerGUI[player.userID].guiid, PlayerGUI[player.userID].page);
        }

        [ConsoleCommand("kit.close")]
        void cmdConsoleKitClose(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
            {
                SendReply(arg, "You can't use this command from the server console");
                return;
            }
            DestroyAllGUI(arg.Player());
        }

        [ConsoleCommand("kit.show")]
        void cmdConsoleKitShow(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
            {
                SendReply(arg, "You can't use this command from the server console");
                return;
            }
            if (!arg.HasArgs())
            {
                SendReply(arg, "You are not allowed to use manually this command");
                return;
            }

            var player = arg.Player();

            PLayerGUI playerGUI;
            if (!PlayerGUI.TryGetValue(player.userID, out playerGUI)) return;

            RefreshKitPanel(player, playerGUI.guiid, arg.GetInt(0));
        }

        List<BasePlayer> FindPlayer(string arg)
        {
            var listPlayers = new List<BasePlayer>();

            ulong steamid;
            ulong.TryParse(arg, out steamid);
            string lowerarg = arg.ToLower();

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (steamid != 0L)
                    if (player.userID == steamid)
                    {
                        listPlayers.Clear();
                        listPlayers.Add(player);
                        return listPlayers;
                    }
                string lowername = player.displayName.ToLower();
                if (lowername.Contains(lowerarg))
                {
                    listPlayers.Add(player);
                }
            }
            return listPlayers;
        }

        //////////////////////////////////////////////////////////////////////////////////////
        // Chat Command
        //////////////////////////////////////////////////////////////////////////////////////

        bool hasAccess(BasePlayer player)
        {
            if (player.net.connection.authLevel > 1)
                return true;
            return false;
        }
        void SendListKitEdition(BasePlayer player)
        {
            SendReply(player, "permission \"permission name\" => set the permission needed to get this kit");
            SendReply(player, "description \"description text here\" => set a description for this kit");
            SendReply(player, "image \"image http url\" => set an image for this kit (gui only)");
            SendReply(player, "authlevel XXX");
            SendReply(player, "cooldown XXX");
            SendReply(player, "max XXX");
            SendReply(player, "items => set new items for your kit (will copy your inventory)");
            SendReply(player, "npconly TRUE/FALSE => only get this kit out of a NPC");
            SendReply(player, "hide TRUE/FALSE => dont show this kit in lists (EVER)");
        }
        [ChatCommand("kit")]
        void cmdChatKit(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                if (GUIKits.ContainsKey(0))
                    NewKitPanel(player, 0);
                else
                {
                    string reason = string.Empty;
                    foreach (var pair in storedData.Kits)
                    {
						var cansee = CanSeeKit(player, pair.Key, false, out reason);
						if (!cansee && string.IsNullOrEmpty(reason)) continue;
						SendReply(player, $"{pair.Value.name} - {pair.Value.description} {reason}");
                    }
					SendReply(player, "?????????????????????? ???????????? ?????????? ???????????? ?? ?????????? ???????????????? ???? ????????????:\n <color=#FFCC33>rustld.playrust.ru</color>");
                }
                return;
            }
            if (args.Length == 1)
            {
                switch (args[0])
                {
                    case "help":
                        SendReply(player, "====== Player Commands ======");
                        SendReply(player, "/kit => to get the list of kits");
                        SendReply(player, "/kit KITNAME => to redeem the kit");
                        if (!hasAccess(player)) { return; }
                        SendReply(player, "====== Admin Commands ======");
                        SendReply(player, "/kit add KITNAME => add a kit");
                        SendReply(player, "/kit remove KITNAME => remove a kit");
                        SendReply(player, "/kit edit KITNAME => edit a kit");
                        SendReply(player, "/kit list => get a raw list of kits (the real full list)");
                        SendReply(player, "/kit give PLAYER/STEAMID KITNAME => give a kit to a player");
                        SendReply(player, "/kit resetkits => deletes all kits");
                        SendReply(player, "/kit resetdata => reset player data");
                        break;
                    case "add":
                    case "remove":
                    case "edit":
                        if (!hasAccess(player)) { SendReply(player, "You don't have access to this command"); return; }
                        SendReply(player, $"/kit {args[0]} KITNAME");
                        break;
                    case "give":
                        if (!hasAccess(player)) { SendReply(player, "You don't have access to this command"); return; }
                        SendReply(player, "/kit give PLAYER/STEAMID KITNAME");
                        break;
                    case "list":
                        if (!hasAccess(player)) { SendReply(player, "You don't have access to this command"); return; }
                        foreach (var kit in storedData.Kits.Values)
                        {
                            SendReply(player, $"{kit.name} - {kit.description}");
                        }
                        break;
                    case "items":
                        break;
                    case "resetkits":
                        if (!hasAccess(player)) { SendReply(player, "You don't have access to this command"); return; }
                        storedData.Kits.Clear();
                        kitEditor.Clear();
                        ResetData();
                        SaveKits();
                        SendReply(player, "Resetted all kits and player data");
                        break;
                    case "resetdata":
                        if (!hasAccess(player)) { SendReply(player, "You don't have access to this command"); return; }
                        ResetData();
                        SendReply(player, "Resetted all player data");
                        break;
                    default:
                        TryGiveKit(player, args[0].ToLower());
                        break;
                }
                if (args[0] != "items")
                    return;

            }
            if (!hasAccess(player)) { SendReply(player, "You don't have access to this command"); return; }

            string kitname;
            switch (args[0])
            {
                case "add":
                    kitname = args[1].ToLower();
                    if (storedData.Kits.ContainsKey(kitname))
                    {
                        SendReply(player, "This kit already exists.");
                        return;
                    }
                    storedData.Kits[kitname] = new Kit { name = args[1] };
                    kitEditor[player.userID] = kitname;
                    SendReply(player, "You've created a new kit: " + args[1]);
                    SendListKitEdition(player);
                    break;
                case "give":
                    if (args.Length < 3)
                    {
                        SendReply(player, "/kit give PLAYER/STEAMID KITNAME");
                        return;
                    }
                    kitname = args[2].ToLower();
                    if (!storedData.Kits.ContainsKey(kitname))
                    {
                        SendReply(player, "This kit doesn't seem to exist.");
                        return;
                    }
                    var findPlayers = FindPlayer(args[1]);
                    if (findPlayers.Count == 0)
                    {
                        SendReply(player, "No players found.");
                        return;
                    }
                    if (findPlayers.Count > 1)
                    {
                        SendReply(player, "Multiple players found.");
                        return;
                    }
                    GiveKit(findPlayers[0], kitname);
                    SendReply(player, $"You gave {findPlayers[0].displayName} the kit: {storedData.Kits[kitname].name}");
                    SendReply(findPlayers[0], string.Format("You've received the kit {1} from {0}", player.displayName, storedData.Kits[kitname].name));
                    break;
                case "edit":
                    kitname = args[1].ToLower();
                    if (!storedData.Kits.ContainsKey(kitname))
                    {
                        SendReply(player, "This kit doesn't seem to exist");
                        return;
                    }
                    kitEditor[player.userID] = kitname;
                    SendReply(player, $"You are now editing the kit: {kitname}");
                    SendListKitEdition(player);
                    break;
                case "remove":
                    kitname = args[1].ToLower();
                    if (!storedData.Kits.Remove(kitname))
                    {
                        SendReply(player, "This kit doesn't seem to exist");
                        return;
                    }
                    SendReply(player, $"{kitname} was removed");
                    if (kitEditor[player.userID] == kitname) kitEditor.Remove(player.userID);
                    break;
                default:
                    if (!kitEditor.TryGetValue(player.userID, out kitname))
                    {
                        SendReply(player, "You are not creating or editing a kit");
                        return;
                    }
                    Kit kit;
                    if (!storedData.Kits.TryGetValue(kitname, out kit))
                    {
                        SendReply(player, "There was an error while getting this kit, was it changed while you were editing it?");
                        return;
                    }
                    for (var i = 0; i < args.Length; i++)
                    {
                        object editvalue;
                        var key = args[i].ToLower();
                        switch (key)
                        {
                            case "items":
                                kit.items = GetPlayerItems(player);
                                SendReply(player, "The items were copied from your inventory");
                                continue;
                            case "name":
                                continue;
                            case "description":
                                editvalue = kit.description = args[++i];
                                break;
                            case "max":
                                editvalue = kit.max = int.Parse(args[++i]);
                                break;
                            case "cooldown":
                                editvalue = kit.cooldown = double.Parse(args[++i]);
                                break;
                            case "authlevel":
                                editvalue = kit.authlevel = int.Parse(args[++i]);
                                break;
                            case "hide":
                                editvalue = kit.hide = bool.Parse(args[++i]);
                                break;
                            case "npconly":
                                editvalue = kit.npconly = bool.Parse(args[++i]);
                                break;
                            case "permission":
                                editvalue = kit.permission = args[++i];
                                InitializePermissions();
                                break;
                            case "image":
                                editvalue = kit.image = args[++i];
                                break;
                            default:
                                SendReply(player, $"{args[i]} is not a valid argument");
                                continue;
                        }
                        SendReply(player, $"{key} set to {editvalue ?? "null"}");
                    }
                    break;
            }
            SaveKits();
        }
    }
}