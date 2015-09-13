using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ClansUI;
using ClansUI.Extensions;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using LogType = Oxide.Core.Logging.LogType;

namespace Oxide.Plugins
{
	[Info("ClanUI", "baton", "0.1.0", ResourceId = 1317)]
	[Description("Custom Clans UI for rustrd.com .")]
	public sealed class ClansUI : RustPlugin
	{
		private const string DataFileName = "ClansUI_data";
		private const int DefaultAmountOfTunaToCreateClan = 1000;
		private const int DefaultAmountOfTunaToMakeSpawnPoint = 500;

		[PluginReference]
		// ReSharper disable once UnassignedField.Compiler
		// ReSharper disable once InconsistentNaming
		Plugin Clans;

		private static List<ClanData> _clanDataList = new List<ClanData>();
		private static int _tunaToCreateAClan = DefaultAmountOfTunaToCreateClan;
		private static int _tunaToMakeSpawnPoint = DefaultAmountOfTunaToMakeSpawnPoint;

		private static readonly int TunaItemId = ItemManager.FindItemDefinition("can.tuna").itemid;

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

			var clanData = _clanDataList.FirstOrDefault(data => data.ClanTag.Equals(clanTag, StringComparison.Ordinal));
			if (clanData == null || clanData.SpawnPosition.Equals(Vector3.zero.ToString()))
				return;

			var sleepingBags = SleepingBag.FindForPlayer(player.userID, true);
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
			Diagnostics.MessageToServer("Clans plugin is empty:{0}", Clans == null);
			if (player == null || Clans == null)
				return;

			var clan = Clans.Call<string>("GetClanOf", player);
			if (!string.IsNullOrEmpty(clan))
			{
				Diagnostics.MessageToPlayer(player, "You are already a member of clan: {0}", clan.ToString(CultureInfo.InvariantCulture));
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
					AnchorMin = "0.15 0.2",
					AnchorMax = "0.9 0.9"
				}
			};
			var mainPanelName = container.Add(mainPanel);

			var closeButton = CreateIgnoreButton(mainPanelName, "#FF02F3FF");
			container.Add(closeButton, mainPanelName);
			var headerLabel = CreateHeaderLabel(mainPanelName);
			container.Add(headerLabel, mainPanelName);

			if (_clanDataList == null || _clanDataList.Count <= 0)
			{
				Diagnostics.MessageToPlayer(player, "There are no clan advertisements");
				return;
			}

			for (int index = 0; index < _clanDataList.Count; index++)
			{
				var clanData = _clanDataList[index];
				var clanButton = CreateClanButton(mainPanelName, index, clanData);
				var clanButtonName = container.Add(clanButton, mainPanelName);
				var clanImage = CreateClanImage(clanButtonName, clanData);
				container.Add(clanImage);
				Diagnostics.MessageToPlayer(player, "clan name:{0}", clanData.ClanTag);
				var clanText = CreateClanLabel(clanData);
				container.Add(clanText, clanButtonName);
			}

			CuiHelper.AddUi(player, container);
		}

		[ChatCommand("clanspawn")]
		private void ClansSpawnCommand(BasePlayer player, string command, string[] args)
		{
			if (player == null || Clans == null)
				return;

			var userId = player.userID.ToString(CultureInfo.InvariantCulture);
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

			var clanData = _clanDataList.First(data => data.ClanTag.Equals(clanTag, StringComparison.Ordinal));
			var amountOfTunaAtOnePlayer = player.inventory.GetAmount(TunaItemId);
			if (amountOfTunaAtOnePlayer < _tunaToMakeSpawnPoint)
			{
				Diagnostics.MessageToPlayer(player, "You don't have enough tuna to create a spawn point. Required amount: {0}", _tunaToMakeSpawnPoint);
				return;
			}

			player.inventory.Take(null, TunaItemId, _tunaToMakeSpawnPoint);
			clanData.SetVector(player.transform.position);

			var x = player.transform.position.x;
			var y = player.transform.position.y;
			var z = player.transform.position.z;
			Diagnostics.MessageToPlayer(player, "You have successfully set clan spawn position to: {0} {1} {2}.", x, y, z);
		}

		[ChatCommand("purchaseSR")]
		private void PurchaseFrontPageListing(BasePlayer player, string command, string[] args)
		{
			if (player == null)
				return;

			var userId = player.userID.ToString(CultureInfo.InvariantCulture);
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
				Diagnostics.MessageToPlayer(player, "You don't have enough members to add your clan to front page. Required members: 6");
				return;
			}

			if (_clanDataList.Any(data => data.ClanTag.Equals(clanTag, StringComparison.Ordinal)))
			{
				Diagnostics.MessageToPlayer(player, "You are already registered at front page");
				return;
			}

			var amountOfTunaAtOnePlayer = player.inventory.GetAmount(TunaItemId);
			if (amountOfTunaAtOnePlayer < _tunaToCreateAClan)
			{
				Diagnostics.MessageToPlayer(player, "You don't have enough tuna to create a clan. Required amount: {0}", _tunaToCreateAClan);
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

			var imageUrl = args[0];
			var userId = player.userID.ToString(CultureInfo.InvariantCulture);
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

			var clanData = _clanDataList.FirstOrDefault(data => data.ClanTag.Equals(clanTag, StringComparison.Ordinal));
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
			if (!arg.HasArgs())
			{
				Diagnostics.MessageToServer("Usage: clanremove \"Tag\"");
				return;
			}

			var clanTag = arg.GetString(0);
			var clanData = _clanDataList.FirstOrDefault(data => data.ClanTag.Equals(clanTag, StringComparison.Ordinal));
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

			var clanTag = arg.GetString(0);
			var clanData = _clanDataList.FirstOrDefault(data => data.ClanTag.Equals(clanTag, StringComparison.Ordinal));
			if (clanData == null)
			{
				Diagnostics.MessageToPlayer(player, "There is no clan with such tag:{0} at front page", clanTag);
				return;
			}

			var result = Clans.Call<bool>("ForceJoinClan", clanData.ClanTag, player);
			if (!result)
			{
				Diagnostics.MessageToPlayer(player, "Error during add to the clan:{0}. Try again later or contact admin.", clanTag);
			}
		}

		private static CuiLabel CreateHeaderLabel(string mainPanelName)
		{
			return new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0.01 0.88",
					AnchorMax = "1.0 0.99"
				},
				Text =
				{
					Align = TextAnchor.MiddleCenter,
					FontSize = 20,
					Text = "There are following clans on this server. Click button of clan to join. Or ignore. To show this windows type /clans"
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
				RectTransform =
				{
					AnchorMin = "0.20 0.03",
					AnchorMax = "0.80 0.13"
				},
				Text =
				{
					Text = "Ignore existing clans. I'm a lone wolf, woof!",
					FontSize = 18,
					Align = TextAnchor.MiddleCenter
				}
			};
		}

		private static CuiButton CreateClanButton(string mainPanelName, int index, ClanData clanData)
		{
			var row = index / 4;
			var column = index % 4;
			const float clanButtonDimesions = 0.16f;
			const double margin = 0.02;
			const double leftMargin = 0.12;
			const double topMargin = 0.12;

			var minX = Math.Round(leftMargin + margin * (column + 1) + (column * clanButtonDimesions), 2);
			var maxX = Math.Round(leftMargin + (column + 1) * (margin + clanButtonDimesions), 2);

			var minY = Math.Round(1 - (topMargin + (margin + clanButtonDimesions) * (row + 1)), 2);
			var maxY = Math.Round(1 - (row * (clanButtonDimesions) + (row + 1) * margin + topMargin), 2);

			string anchorMin = string.Format("{0:F2} {1:F2}", minX, minY);
			string anchorMax = string.Format("{0:F2} {1:F2}", maxX, maxY);

			var clanTag = clanData.ClanTag;

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

		private static CuiElement CreateClanImage(string panelName, ClanData clanData)
		{
			var element = new CuiElement();
			var image = new CuiRawImageComponent
			{
				Url = clanData.ImageUrl,
				Color = string.Format("1 1 1 0.7")
			};

			var rectTransform = new CuiRectTransformComponent
			{
				AnchorMin = "0.0 0.0",
				AnchorMax = "1.0 1.0"
			};
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
				RectTransform =
				{
					AnchorMin = "0.0 0.0",
					AnchorMax = "1.0 1.0"
				},
				Text =
				{
					Align = TextAnchor.MiddleCenter,
					FontSize = 32,
					Color = "1.0 1.0 1.0 1.0",
					Text = clanData.ClanTag
				}
			};
		}

		private static Vector3 ParseVector3(string @string)
		{
			var temp = @string.Substring(1, @string.Length - 2).Split(',');
			var x = float.Parse(temp[0]);
			var y = float.Parse(temp[1]);
			var z = float.Parse(temp[2]);
			return new Vector3(x, y, z);
		}
	}
}

namespace ClansUI
{
	public sealed class ClanData
	{
		public string ClanTag { get; set; }
		public string SpawnPosition { get; set; }
		public string ImageUrl { get; set; }

		public void SetVector(Vector3 vector3)
		{
			SpawnPosition = vector3.ToString();
		}

		public ClanData()
		{
			SpawnPosition = Vector3.zero.ToString();
			ImageUrl = "http://www.kiragameworld.com/wp-content/uploads/2014/01/Tve_clan.png";
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
		public static string Prefix = "[clans]:";
		public static string Color = "orange";
		private const string Format = "<color={0}>{1}</color>";

		public static void MessageToServer(string message, params object[] args)
		{
			Interface.GetMod().RootLogger.Write(LogType.Info, message, args);
		}

		public static void MessageToPlayer(BasePlayer player, string message, params object[] args)
		{
			player.SendConsoleCommand("chat.add", new object[] { 0, string.Format(Format, Color, Prefix) + string.Format(message, args), 1f });
		}
	}
}