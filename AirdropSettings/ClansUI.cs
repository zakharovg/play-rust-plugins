﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ClansUI;
using ClansUI.Extensions;
using Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using LogType = Oxide.Core.Logging.LogType;

namespace Oxide.Plugins
{
    [Info("ClanUI", "baton", "0.1.1", ResourceId = 1317)]
    [Description("Custom Clans UI for rustrd.com .")]
    public sealed class ClansUI : RustPlugin
    {
        private const string DataFileName = "ClansUI_data";
        private const int DefaultAmountOfTunaToCreateClan = 1000;
        private const int DefaultAmountOfTunaToMakeSpawnPoint = 500;

        private static List<ClanData> _clanDataList = new List<ClanData>();
        private static Dictionary<ulong, DateTime> _playersJoinedClan = new Dictionary<ulong, DateTime>();
        private static int _tunaToCreateAClan = DefaultAmountOfTunaToCreateClan;
        private static int _tunaToMakeSpawnPoint = DefaultAmountOfTunaToMakeSpawnPoint;
        private static int TunaItemId;

        [PluginReference] // ReSharper disable once UnassignedField.Compiler
                          // ReSharper disable once InconsistentNaming
        private Plugin Clans;

        protected override void LoadDefaultConfig()
        {
            Config.Set("TunaToCreateAClan", DefaultAmountOfTunaToCreateClan);
            Config.Set("TunaToSpawnPoint", DefaultAmountOfTunaToMakeSpawnPoint);
        }

        private void OnServerInitialized()
        {
            LoadConfig();
            LoadData();

            _tunaToCreateAClan = Config.Get<int>("TunaToCreateAClan");
            _tunaToMakeSpawnPoint = Config.Get<int>("TunaToSpawnPoint");
            TunaItemId = ItemManager.FindItemDefinition("can.tuna").itemid;
        }

        private void LoadData()
        {
            var data = Interface.Oxide.DataFileSystem.ReadObject<List<ClanData>>(DataFileName);
            if (data == null || !data.Any())
            {
                data = new List<ClanData>();
                Interface.Oxide.DataFileSystem.WriteObject(DataFileName, data);
            }

            _clanDataList = data;
        }

        private void Unloaded()
        {
            Interface.Oxide.DataFileSystem.WriteObject(DataFileName, _clanDataList);
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (player == null)
                return;

            var clanTag = Clans.Call<string>("GetClanOf", player);
            if (string.IsNullOrEmpty(clanTag))
                return;

            ClanData clanData = _clanDataList.FirstOrDefault(data => data.ClanTag.Equals(clanTag, StringComparison.Ordinal));
            if (clanData == null || clanData.SpawnPosition.Equals(Vector3.zero.ToString()))
                return;

            SleepingBag[] sleepingBags = SleepingBag.FindForPlayer(player.userID, true);
            if (sleepingBags != null && sleepingBags.Length > 0)
                return;

            Diagnostics.MessageToPlayer(player, "You have been teleported to your clan spawn location.");
            player.MovePosition(ParseVector3(clanData.SpawnPosition));
        }

        private void OnPlayerInit(BasePlayer player)
        {
            if (player == null)
                return;

            var clanTag = Clans.Call<string>("GetClanOf", player);
            if (!string.IsNullOrEmpty(clanTag) || !_clanDataList.Any())
                return;

            ClansChatCommand(player, string.Empty, new string[0]);
        }

        [ChatCommand("clans")]
        private void ClansChatCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null || Clans == null)
                return;

            var clanTag = Clans.Call<string>("GetClanOf", player);
            if (!string.IsNullOrEmpty(clanTag))
            {
                Diagnostics.MessageToPlayer(player, "You are a member of clan. Window is disabled.");
                return;
            }

            if (!_clanDataList.Any())
            {
                Diagnostics.MessageToPlayer(player, "There are no clan advertisements. Window is disabled.");
                return;
            }

            var container = new CuiElementContainer();
            var mainPanel = new CuiPanel
            {
                Image =
                {
                    Color = "0 0 0 1.0"
                },
                CursorEnabled = true,
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                }
            };
            string mainPanelName = container.Add(mainPanel);

            CuiButton closeButton = CreateIgnoreButton(mainPanelName, "#696969FF");
            container.Add(closeButton, mainPanelName);
            CuiLabel redDawnLabel = CreateRedDawnLabel();
            container.Add(redDawnLabel, mainPanelName);
            CuiLabel headerDescriptionLabel = CreateHeaderDescriptionLabel();
            container.Add(headerDescriptionLabel, mainPanelName);

            if (_clanDataList == null || _clanDataList.Count <= 0)
            {
                Diagnostics.MessageToPlayer(player, "There are no clan advertisements");
                return;
            }

            var actualClanTags = Clans.Call<JArray>("GetAllClans");
            List<ClanData> clansToRemove =
                _clanDataList.Where(
                    clanData => actualClanTags.All(ct => !(ct.ToString().Equals(clanData.ClanTag, StringComparison.Ordinal)))).ToList();
            foreach (ClanData clanData in clansToRemove)
                _clanDataList.Remove(clanData);

            for (int index = 0; index < _clanDataList.Count; index++)
            {
                ClanData clanData = _clanDataList[index];

                CuiButton clanButton = CreateClanButton(mainPanelName, index, clanData);
                CuiElement outline = CreateClanButtonOutline(mainPanelName, clanButton);
                container.Add(outline);
                string clanButtonName = container.Add(clanButton, mainPanelName);
                CuiElement clanImage = CreateClanImage(clanButtonName, clanData);
                container.Add(clanImage);
                CuiLabel clanText = CreateClanLabel(clanData);
                container.Add(clanText, clanButtonName);
            }

            CuiLabel footerDescriptionLabel = CreateFooterDescriptionLabel();
            container.Add(footerDescriptionLabel, mainPanelName);
            CuiLabel footerNoteLabel = CreateFooterNoteLabel();
            container.Add(footerNoteLabel, mainPanelName);

            SendUI(player, container);
        }

        [ChatCommand("clanspawn")]
        private void ClansSpawnCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null || Clans == null)
                return;

            string userId = player.userID.ToString(CultureInfo.InvariantCulture);
            var clanTag = Clans.Call<string>("GetClanOf", player);
            if (string.IsNullOrEmpty(clanTag))
            {
                Diagnostics.MessageToPlayer(player, "You are not a owner of a clan");
                return;
            }

            var clan = Clans.Call<JObject>("GetClan", clanTag);
            if (clan == null)
            {
                Diagnostics.MessageToPlayer(player, "You are not a owner of a clan");
                return;
            }

            var clanOwnerId = clan.Value<string>("owner");
            if (string.IsNullOrEmpty(clanOwnerId))
            {
                Diagnostics.MessageToPlayer(player, "Broken data. Try again or call admin.");
                return;
            }

            if (!userId.Equals(clanOwnerId, StringComparison.Ordinal))
            {
                Diagnostics.MessageToPlayer(player, "You are not a owner of a clan");
                return;
            }

            ClanData clanData = _clanDataList.First(data => data.ClanTag.Equals(clanTag, StringComparison.Ordinal));
            int amountOfTunaAtOnePlayer = player.inventory.GetAmount(TunaItemId);
            if (amountOfTunaAtOnePlayer < _tunaToMakeSpawnPoint)
            {
                Diagnostics.MessageToPlayer(player, "You don't have enough tuna to create a spawn point. Required amount: {0}",
                    _tunaToMakeSpawnPoint);
                return;
            }

            player.inventory.Take(null, TunaItemId, _tunaToMakeSpawnPoint);
            clanData.SetVector(player.transform.position);

            float x = player.transform.position.x;
            float y = player.transform.position.y;
            float z = player.transform.position.z;
            Diagnostics.MessageToPlayer(player, "You have successfully set clan spawn position to: {0} {1} {2}.", x, y, z);
        }

        [ChatCommand("purchaseSR")]
        private void PurchaseFrontPageListing(BasePlayer player, string command, string[] args)
        {
            if (player == null)
                return;

            string userId = player.userID.ToString(CultureInfo.InvariantCulture);
            var clanTag = Clans.Call<string>("GetClanOf", player);
            if (string.IsNullOrEmpty(clanTag))
            {
                Diagnostics.MessageToPlayer(player, "You are not a owner of a clan");
                return;
            }

            if (_clanDataList.Count == 16)
            {
                Diagnostics.MessageToPlayer(player, "Front page is full");
                return;
            }

            var clan = Clans.Call<JObject>("GetClan", clanTag);
            if (clan == null)
            {
                Diagnostics.MessageToPlayer(player, "You are not a owner of a clan");
                return;
            }

            var clanOwnerId = clan.Value<string>("owner");
            if (string.IsNullOrEmpty(clanOwnerId))
            {
                Diagnostics.MessageToPlayer(player, "Broken data. Try again or call admin.");
                return;
            }

            if (!userId.Equals(clanOwnerId, StringComparison.Ordinal))
            {
                Diagnostics.MessageToPlayer(player, "You are not a owner of a clan");
                return;
            }

            var membersArray = clan.Value<JArray>("members");
            if (membersArray == null)
            {
                Diagnostics.MessageToPlayer(player, "Could not get clan members data");
                return;
            }

            if (membersArray.Count < 6)
            {
                Diagnostics.MessageToPlayer(player,
                    "You don't have enough members to add your clan to front page. Required members: 6");
                return;
            }

            if (_clanDataList.Any(data => data.ClanTag.Equals(clanTag, StringComparison.Ordinal)))
            {
                Diagnostics.MessageToPlayer(player, "You are already registered at front page");
                return;
            }

            int amountOfTunaAtOnePlayer = player.inventory.GetAmount(TunaItemId);
            if (amountOfTunaAtOnePlayer < _tunaToCreateAClan)
            {
                Diagnostics.MessageToPlayer(player, "You don't have enough tuna to create a clan. Required amount: {0}",
                    _tunaToCreateAClan);
                return;
            }

            player.inventory.Take(null, TunaItemId, _tunaToCreateAClan);

            _clanDataList.Add(new ClanData { ClanTag = clanTag });
            Diagnostics.MessageToPlayer(player, "Your clan has been added to clans front page");
        }

        [ChatCommand("clanimage")]
        private void SetClanFrontPageImage(BasePlayer player, string command, string[] args)
        {
            if (player == null || args == null || args.Length < 1)
                return;

            string imageUrl = args[0];
            string userId = player.userID.ToString(CultureInfo.InvariantCulture);
            var clanTag = Clans.Call<string>("GetClanOf", player);
            if (string.IsNullOrEmpty(clanTag))
            {
                Diagnostics.MessageToPlayer(player, "You are not a member of a clan");
                return;
            }

            var clan = Clans.Call<JObject>("GetClan", clanTag);
            if (clan == null)
            {
                Diagnostics.MessageToPlayer(player, "You are not a owner of a clan");
                return;
            }

            var clanOwnerId = clan.Value<string>("owner");
            if (string.IsNullOrEmpty(clanOwnerId))
            {
                Diagnostics.MessageToPlayer(player, "Broken data. Try again or call admin.");
                return;
            }

            if (!userId.Equals(clanOwnerId, StringComparison.Ordinal))
            {
                Diagnostics.MessageToPlayer(player, "You are not a owner of a clan");
                return;
            }

            ClanData clanData = _clanDataList.FirstOrDefault(data => data.ClanTag.Equals(clanTag, StringComparison.Ordinal));
            if (clanData == null)
            {
                Diagnostics.MessageToPlayer(player, "Your clan is not registered at front page.");
                return;
            }
            clanData.ImageUrl = imageUrl;

            Diagnostics.MessageToPlayer(player, "You have set your front page clan image to:{0}", imageUrl);
        }

        [ConsoleCommand("clanremove")]
        private void RemoveClanFromFrontPage(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs() || _clanDataList == null)
            {
                Diagnostics.MessageToServer("Usage: clanremove \"Tag\"");
                return;
            }

            string clanTag = arg.GetString(0);
            if (clanTag == null)
                return;

            ClanData clanData =
                _clanDataList.FirstOrDefault(
                    data => data != null && data.ClanTag != null && data.ClanTag.Equals(clanTag, StringComparison.Ordinal));
            if (clanData == null)
            {
                Diagnostics.MessageToServer("There is no clan with such tag:{0} at front page", clanTag);
                return;
            }

            _clanDataList.Remove(clanData);
            Diagnostics.MessageToServer("Clan with tag:{0} has been removed from front page", clanTag);
        }

        [ConsoleCommand("joinclan")]
        private void JoinClanFrontPage(ConsoleSystem.Arg arg)
        {
            if (arg == null || arg.connection == null || arg.connection.player == null || !arg.HasArgs())
                return;
            var player = arg.connection.player as BasePlayer;

            string clanTag = arg.GetString(0);
            ClanData clanData = _clanDataList.FirstOrDefault(data => data.ClanTag.Equals(clanTag, StringComparison.Ordinal));
            if (clanData == null)
            {
                Diagnostics.MessageToPlayer(player, "There is no clan with such tag:{0} at front page", clanTag);
                return;
            }
            // latest operations
            DateTime lastJoined = DateTime.MinValue;
            _playersJoinedClan.TryGetValue(player.userID, out lastJoined);
            if (lastJoined != DateTime.MinValue && DateTime.Now - lastJoined <= TimeSpan.FromHours(1))
            {
                Diagnostics.MessageToPlayer(player, "You have joined clan in last hour, try again later.");
                return;
            }

            var result = Clans.Call<bool>("ForceJoinClan", clanData.ClanTag, player);

            if (!result)
                Diagnostics.MessageToPlayer(player, "Error during add to the clan:{0}. Try again later or contact admin.", clanTag);
            else
                _playersJoinedClan[player.userID] = DateTime.Now;
        }

        private static void SendUI(BasePlayer player, CuiElementContainer container)
        {
            string json = JsonConvert.SerializeObject(container, Formatting.None, new JsonSerializerSettings
            {
                StringEscapeHandling = StringEscapeHandling.Default,
                DefaultValueHandling = DefaultValueHandling.Ignore,
                Formatting = Formatting.Indented
            });
            json = json.Replace(@"\t", "\t");
            json = json.Replace(@"\n", "\n");

            CommunityEntity.ServerInstance.ClientRPCEx(new SendInfo { connection = player.net.connection }, null, "AddUI",
                json);
        }

        private static CuiLabel CreateRedDawnLabel()
        {
            return new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0.00 0.88",
                    AnchorMax = "1.0 1.0"
                },
                Text =
                {
                    Align = TextAnchor.MiddleCenter,
                    FontSize = 40,
                    Text = "Welcome to <color=red><size=50>RED DAWN</size></color>"
                }
            };
        }

        private static CuiLabel CreateHeaderDescriptionLabel()
        {
            return new CuiLabel
            {
                RectTransform = { AnchorMin = "0.00 0.84", AnchorMax = "1.0 0.94" },
                Text =
                {
                    Align = TextAnchor.LowerCenter,
                    FontSize = 16,
                    Text =
                        "You may pick one of the clans listed below to jump right into action. Clans below may have alliances with other clans. Pick wisely..."
                }
            };
        }

        private static CuiLabel CreateFooterDescriptionLabel()
        {
            return new CuiLabel
            {
                RectTransform = { AnchorMin = "0.00 0.26", AnchorMax = "1.0 0.35" },
                Text =
                {
                    Align = TextAnchor.UpperCenter,
                    FontSize = 16,
                    Text =
                        "These clans have worked dilligently to represent themselves on our launch screen. \n Pick from the available clans above to represent that clan and uphold their honor"
                }
            };
        }

        private static CuiLabel CreateFooterNoteLabel()
        {
            return new CuiLabel
            {
                RectTransform = { AnchorMin = "0.00 0.18", AnchorMax = "1.0 0.25" },
                Text =
                {
                    Align = TextAnchor.UpperCenter,
                    FontSize = 16,
                    Text =
                        "<color=grey>Or start on your own journey... You could be eaten by wolves or worse... Lose your tuna...</color>"
                }
            };
        }

        private static CuiButton CreateIgnoreButton(string mainPanelName, string hexColor)
        {
            Color color;
            Color.TryParseHexString(hexColor, out color);
            return new CuiButton
            {
                Button =
                {
                    Command = string.Format("infoclose {0}", mainPanelName),
                    Close = mainPanelName,
                    Color = color.ToRustFormatString()
                },
                RectTransform = { AnchorMin = "0.40 0.03", AnchorMax = "0.60 0.16" },
                Text =
                {
                    Text = "I have accepted the fact \n that I could be killed by wolves \n or lose my tuna",
                    FontSize = 16,
                    Align = TextAnchor.MiddleCenter
                }
            };
        }

        private static CuiButton CreateClanButton(string mainPanelName, int index, ClanData clanData)
        {
            int row = index / 4;
            int column = index % 4;
            const float verticalclanButtonDimesions = 0.11f;
            const float horizontalclanButtonDimesions = 0.16f;
            const double margin = 0.01;
            const double leftMargin = 0.16;
            const double topMargin = 0.16;

            double minX = Math.Round(leftMargin + margin * (column + 1) + (column * horizontalclanButtonDimesions), 2);
            double maxX = Math.Round(leftMargin + (column + 1) * (margin + horizontalclanButtonDimesions), 2);

            double minY = Math.Round(1 - (topMargin + (margin + verticalclanButtonDimesions) * (row + 1)), 2);
            double maxY = Math.Round(1 - (row * (verticalclanButtonDimesions) + (row + 1) * margin + topMargin), 2);

            string anchorMin = string.Format("{0:F2} {1:F2}", minX, minY);
            string anchorMax = string.Format("{0:F2} {1:F2}", maxX, maxY);

            string clanTag = clanData.ClanTag;

            return new CuiButton
            {
                Button =
                {
                    Command = string.Format("joinclan {0}", clanTag),
                    Close = mainPanelName,
                    Color = "0.5 0.5 0.5 1.0"
                },
                RectTransform =
                {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax
                },
                Text =
                {
                    Text = string.Empty
                }
            };
        }

        private static CuiElement CreateClanButtonOutline(string panelName, CuiButton button)
        {
            var element = new CuiElement();
            var outline = new CuiImageComponent
            {
                Color = "0.2 0.2 0.2 1.0"
            };

            double minX = float.Parse(button.RectTransform.AnchorMin.Split(' ')[0]) - 0.0035;
            double minY = float.Parse(button.RectTransform.AnchorMin.Split(' ')[1]) - 0.0035;
            double maxX = float.Parse(button.RectTransform.AnchorMax.Split(' ')[0]) + 0.0035;
            double maxY = float.Parse(button.RectTransform.AnchorMax.Split(' ')[1]) + 0.0035;

            var cuiRectTransform = new CuiRectTransformComponent
            {
                AnchorMin = string.Format("{0:F4} {1:F4}", minX, minY),
                AnchorMax = string.Format("{0:F4} {1:F4}", maxX, maxY)
            };
            element.Components.Add(outline);
            element.Components.Add(cuiRectTransform);
            element.Name = CuiHelper.GetGuid();
            element.Parent = panelName;
            return element;
        }

        private static CuiElement CreateClanImage(string panelName, ClanData clanData)
        {
            var element = new CuiElement();
            var image = new CuiRawImageComponent { Url = clanData.ImageUrl, Color = string.Format("1 1 1 0.7") };
            var rectTransform = new CuiRectTransformComponent { AnchorMin = "0.0 0.2", AnchorMax = "1 1" };
            element.Components.Add(image);
            element.Components.Add(rectTransform);
            element.Name = CuiHelper.GetGuid();
            element.Parent = panelName;
            return element;
        }

        private static CuiLabel CreateClanLabel(ClanData clanData)
        {
            return new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1.0 0.3" },
                Text = { Align = TextAnchor.LowerCenter, FontSize = 16, Color = "1.0 1.0 1.0 1.0", Text = clanData.ClanTag }
            };
        }

        private static Vector3 ParseVector3(string str)
        {
            string[] temp = str.Substring(1, str.Length - 2).Split(',');
            float x = float.Parse(temp[0]);
            float y = float.Parse(temp[1]);
            float z = float.Parse(temp[2]);
            return new Vector3(x, y, z);
        }
    }
}

namespace ClansUI
{
    public sealed class ClanData
    {
        public ClanData()
        {
            SpawnPosition = Vector3.zero.ToString();
            ImageUrl = "http://www.kiragameworld.com/wp-content/uploads/2014/01/Tve_clan.png";
        }

        public string ClanTag { get; set; }
        public string SpawnPosition { get; set; }
        public string ImageUrl { get; set; }

        public void SetVector(Vector3 vector3)
        {
            SpawnPosition = vector3.ToString();
        }
    }
}

namespace ClansUI.Extensions
{
    public static class ColorExtensions
    {
        public static string ToRustFormatString(this Color color)
        {
            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }
    }

    public static class Diagnostics
    {
        private const string Format = "<color={0}>{1}</color>";
        public static string Prefix = "[clans]:";
        public static string Color = "orange";

        public static void MessageToServer(string message, params object[] args)
        {
            Interface.GetMod().RootLogger.Write(LogType.Info, message, args);
        }

        public static void MessageToPlayer(BasePlayer player, string message, params object[] args)
        {
            player.SendConsoleCommand("chat.add",
                new object[] { 0, string.Format(Format, Color, Prefix) + string.Format(message, args), 1f });
        }
    }
}