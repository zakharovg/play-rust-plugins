﻿using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Game.Rust.Cui;
using ServerInfo;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("ServerInfo", "baton", "0.0.1", ResourceId = 100500)]
	[Description("UI customizable server info with multiple tabs.")]
	public sealed class ServerInfo : RustPlugin
	{
		private static Settings _settings;
		private static readonly Dictionary<ulong, PlayerInfoState> PlayerActiveTabs = new Dictionary<ulong, PlayerInfoState>();

		private void OnServerInitialized()
		{
			LoadConfig();
			var configFileName = Manager.ConfigPath + "/server_info_text.json";

			if (!Config.Exists(configFileName))
			{
				Config.WriteObject(Settings.CreateDefault(), false, configFileName);
			}
			_settings = null;
			_settings = Config.ReadObject<Settings>(configFileName);
		}

		[ConsoleCommand("changetab")]
		private void ChangeTab(ConsoleSystem.Arg arg)
		{
			if (arg.connection == null || arg.connection.player == null || !arg.HasArgs())
				return;

			var player = (BasePlayer)arg.connection.player;
			var previousTabIndex = PlayerActiveTabs[player.userID].ActiveTabIndex;
			var tabToChangeTo = arg.GetInt(0, 65535);

			if (previousTabIndex == tabToChangeTo || !arg.HasArgs(4))
				return;

			var tabToSelectIndex = arg.GetInt(0);
			var tabContentName = arg.GetString(1);
			var activeButtonName = arg.GetString(2);
			var tabToSelectButtonName = arg.GetString(3);
			var mainPanelName = arg.GetString(4);

			CuiHelper.DestroyUi(player, tabContentName);
			CuiHelper.DestroyUi(player, activeButtonName);
			CuiHelper.DestroyUi(player, tabToSelectButtonName);

			var tabToSelect = _settings.Tabs[tabToSelectIndex];
			PlayerActiveTabs[player.userID].ActiveTabIndex = tabToSelectIndex;

			var container = new CuiElementContainer();
			var tabContentPanelName = CreateTabContent(tabToSelect, container, mainPanelName);
			var newActiveButtonName = AddActiveButton(tabToSelectIndex, container, mainPanelName);
			AddNonActiveButton(previousTabIndex, container, mainPanelName, tabContentPanelName, newActiveButtonName);

			CuiHelper.AddUi(player, container);
		}

		private void OnPlayerInit(BasePlayer player)
		{
			if (player == null || !_settings.ShowInfoOnPlayerInit)
				return;

			PlayerInfoState state;
			PlayerActiveTabs.TryGetValue(player.userID, out state);

			if (state == null)
			{
				state = new PlayerInfoState();
				PlayerActiveTabs.Add(player.userID, state);
			}

			if (state.InfoShownOnLogin)
				return;

			ShowInfo(player, string.Empty, new string[0]);
		}

		[ChatCommand("info")]
		private void ShowInfo(BasePlayer player, string command, string[] args)
		{
			if (player == null)
				return;

			if (!PlayerActiveTabs.ContainsKey(player.userID))
				PlayerActiveTabs.Add(player.userID, new PlayerInfoState());

			var container = new CuiElementContainer();
			var mainPanelName = AddMainPanel(container);

			//Add tab buttons

			var activeTabIndex = PlayerActiveTabs[player.userID].ActiveTabIndex;
			var activeTab = _settings.Tabs[activeTabIndex];

			var tabContentPanelName = CreateTabContent(activeTab, container, mainPanelName);
			var activeTabButtonName = AddActiveButton(activeTabIndex, container, mainPanelName);

			for (int tabIndex = 0; tabIndex < _settings.Tabs.Count; tabIndex++)
			{
				if (tabIndex == activeTabIndex)
					continue;

				AddNonActiveButton(tabIndex, container, mainPanelName, tabContentPanelName, activeTabButtonName);
			}

			CuiHelper.AddUi(player, container);
		}

		private static string AddMainPanel(CuiElementContainer container)
		{
			var mainPanel = new CuiPanel
			{
				CursorEnabled = true,
				Image =
				{
					Color = "0.1 0.1 0.1 1",
				},
				RectTransform =
				{
					AnchorMin = "0.15 0.2",
					AnchorMax = "0.9 0.9"
				}
			};

			var mainPanelName = container.Add(mainPanel);
			var closeButton = new CuiButton
			{
				Button =
				{
					Close = mainPanelName,
					Color = "0.8 0.8 0.8 0.2"
				},
				RectTransform =
				{
					AnchorMin = "0.86 0.92",
					AnchorMax = "0.97 0.98"
				},
				Text =
				{
					Text = "Close",
					FontSize = 18,
					Align = TextAnchor.MiddleCenter
				}
			};
			container.Add(closeButton, mainPanelName);
			return mainPanelName;
		}

		private string CreateTabContent(HelpTab helpTab, CuiElementContainer container, string mainPanelName)
		{
			var tabContentPanelName = container.Add(new CuiPanel
			{
				CursorEnabled = false,
				Image =
				{
					Color = "0.1 0.1 0.1 1",
				},
				RectTransform =
				{
					AnchorMin = "0.22 0.01",
					AnchorMax = "0.85 0.98"
				}

			}, mainPanelName);

			var cuiLabel = new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0.22 0.85",
					AnchorMax = "0.9 0.98"
				},
				Text =
				{
					Align = TextAnchor.UpperLeft,
					FontSize = 32,
					Text = helpTab.Name
				}
			};
			container.Add(cuiLabel, tabContentPanelName);

			const float firstLineMargin = 0.86f;
			const float textLineHeight = 0.04f;

			for (int textRow = 0; textRow < helpTab.TextLines.Count; textRow++)
			{
				var textLine = helpTab.TextLines[textRow];
				var textLineLabel = new CuiLabel
				{
					RectTransform =
					{
						AnchorMin = "0.22 " + (firstLineMargin - textLineHeight * (textRow + 1)),
						AnchorMax = "0.9 " + (firstLineMargin - textLineHeight * textRow)
					},
					Text =
					{
						Align = TextAnchor.UpperLeft,
						FontSize = 16,
						Text = textLine
					}
				};
				container.Add(textLineLabel, tabContentPanelName);
			}

			return tabContentPanelName;
		}

		private static void AddNonActiveButton(
			int tabIndex,
			CuiElementContainer container,
			string mainPanelName,
			string tabContentPanelName,
			string activeTabButtonName)
		{
			const string nonActiveButtonColor = "0.8 0.8 0.8 0.2";

			var helpTab = _settings.Tabs[tabIndex];
			var helpTabButton = CreateTabButton(tabIndex, helpTab, nonActiveButtonColor);
			var helpTabButtonName = container.Add(helpTabButton, mainPanelName);

			var helpTabButtonCuiElement = container.First(i => i.Name.Equals(helpTabButtonName, StringComparison.OrdinalIgnoreCase));
			var generatedHelpTabButton = helpTabButtonCuiElement.Components.OfType<CuiButtonComponent>().First();

			var command = string.Format("changeTab {0} {1} {2} {3} {4}", tabIndex, tabContentPanelName, activeTabButtonName, helpTabButtonName, mainPanelName);
			generatedHelpTabButton.Command = command;
		}

		private static string AddActiveButton(
			int activeTabIndex,
			CuiElementContainer container,
			string mainPanelName)
		{
			const string activeButtonColor = "0.3 0.4 0.5 1";
			var activeTab = _settings.Tabs[activeTabIndex];

			var activeHelpTabButton = CreateTabButton(activeTabIndex, activeTab, activeButtonColor);
			var activeTabButtonName = container.Add(activeHelpTabButton, mainPanelName);

			var activeTabButtonCuiElement = container.First(i => i.Name.Equals(activeTabButtonName, StringComparison.OrdinalIgnoreCase));
			var activeTabButton = activeTabButtonCuiElement.Components.OfType<CuiButtonComponent>().First();

			var command = string.Format("changeTab {0}", activeTabIndex);
			activeTabButton.Command = command;

			return activeTabButtonName;
		}

		private static CuiButton CreateTabButton(int tabIndex, HelpTab helpTab, string color)
		{
			const float verticalMargin = 0.03f;
			const float buttonHeight = 0.06f;

			return new CuiButton
			{
				Button =
				{
					Color = color
				},
				RectTransform =
				{
					AnchorMin = string.Format("0.01 {0}", 1 - ((verticalMargin + buttonHeight) * (tabIndex + 1))),
					AnchorMax = string.Format("0.20 {0}", 1 - ((verticalMargin * (tabIndex + 1)) + (tabIndex * buttonHeight)))
				},
				Text =
				{
					Text = helpTab.Name,
					FontSize = 18,
					Align = TextAnchor.MiddleCenter
				}
			};
		}
	}
}

namespace ServerInfo
{
	public sealed class Settings
	{
		public List<HelpTab> Tabs { get; set; }
		public bool ShowInfoOnPlayerInit { get; set; }

		public Settings()
		{
			Tabs = new List<HelpTab>();
			ShowInfoOnPlayerInit = true;
		}

		public static Settings CreateDefault()
		{
			var settings = new Settings();
			settings.Tabs.Add(new HelpTab { Name = "First Tab", TextLines = { "This is first tab" } });
			settings.Tabs.Add(new HelpTab { Name = "Second Tab", TextLines = { "This is second tab" } });
			settings.Tabs.Add(new HelpTab { Name = "Third Tab", TextLines = { "This is third tab" } });
			return settings;
		}
	}

	public sealed class HelpTab
	{
		public string Name { get; set; }
		public List<string> TextLines { get; set; }

		public HelpTab()
		{
			Name = "Default ServerInfo Help Tab";
			TextLines = new List<string>();
		}
	}

	public sealed class PlayerInfoState
	{
		public int ActiveTabIndex { get; set; }
		public bool InfoShownOnLogin { get; set; }
	}
}