using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Game.Rust.Cui;
using ServerInfo;
using ServerInfo.Extensions;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("ServerInfo", "baton", "0.2.1", ResourceId = 1317)]
	[Description("UI customizable server info with multiple tabs.")]
	public sealed class ServerInfo : RustPlugin
	{
		private static Settings _settings;
		private static readonly Dictionary<ulong, PlayerInfoState> PlayerActiveTabs = new Dictionary<ulong, PlayerInfoState>();
		private static readonly Permission Permission = Interface.GetMod().GetLibrary<Permission>();

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

		private void OnUnload()
		{
			PlayerActiveTabs.Clear();
		}

		[ConsoleCommand("changetab")]
		private void ChangeTab(ConsoleSystem.Arg arg)
		{
			if (arg.connection == null || arg.connection.player == null || !arg.HasArgs())
				return;

			var player = arg.connection.player as BasePlayer;
			if (player == null)
				return;

			if (!PlayerActiveTabs.ContainsKey(player.userID))
				return;

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

			var allowedTabs = _settings.Tabs
				.Where((tab, tabIndex) => string.IsNullOrEmpty(tab.OxideGroup) ||
					tab.OxideGroup.Split(',')
						.Any(group => Permission.UserHasGroup(player.userID.ToString(CultureInfo.InvariantCulture), group)))
				.ToList();
			var tabToSelect = allowedTabs[tabToSelectIndex];
			PlayerActiveTabs[player.userID].ActiveTabIndex = tabToSelectIndex;
			PlayerActiveTabs[player.userID].PageIndex = 0;

			var container = new CuiElementContainer();
			var tabContentPanelName = CreateTabContent(tabToSelect, container, mainPanelName);
			var newActiveButtonName = AddActiveButton(tabToSelectIndex, tabToSelect, container, mainPanelName);
			AddNonActiveButton(previousTabIndex, container, _settings.Tabs[previousTabIndex], mainPanelName, tabContentPanelName, newActiveButtonName);

			CuiHelper.AddUi(player, container);
		}

		[ConsoleCommand("changepage")]
		private void ChangePage(ConsoleSystem.Arg arg)
		{
			if (arg.connection == null || arg.connection.player == null || !arg.HasArgs(3))
				return;

			var player = arg.connection.player as BasePlayer;
			if (player == null)
				return;

			if (!PlayerActiveTabs.ContainsKey(player.userID))
				return;

			var playerInfoState = PlayerActiveTabs[player.userID];
			var currentTab = _settings.Tabs[playerInfoState.ActiveTabIndex];
			var currentPageIndex = playerInfoState.PageIndex;

			var pageToChangeTo = arg.GetInt(0, 65535);
			var tabContentPanelName = arg.GetString(1);
			var mainPanelName = arg.GetString(2);

			if (pageToChangeTo == currentPageIndex)
				return;

			CuiHelper.DestroyUi(player, tabContentPanelName);

			playerInfoState.PageIndex = pageToChangeTo;

			var container = new CuiElementContainer();
			CreateTabContent(currentTab, container, mainPanelName, pageToChangeTo);

			CuiHelper.AddUi(player, container);
		}

		[ConsoleCommand("infoclose")]
		private void CloseInfo(ConsoleSystem.Arg arg)
		{
			if (arg.connection == null || arg.connection.player == null || !arg.HasArgs())
				return;

			var player = arg.connection.player as BasePlayer;
			if (player == null)
				return;

			if (!PlayerActiveTabs.ContainsKey(player.userID))
				return;

			const string defaultName = "defaultString";
			string mainPanelName = arg.GetString(0, defaultName);

			if (mainPanelName.Equals(defaultName, StringComparison.OrdinalIgnoreCase))
				return;

			PlayerInfoState state;
			PlayerActiveTabs.TryGetValue(player.userID, out state);
			if (state == null)
				return;

			state.ActiveTabIndex = _settings.TabToOpenByDefault;
			state.PageIndex = 0;

			CuiHelper.DestroyUi(player, mainPanelName);
		}

		private void OnPlayerInit(BasePlayer player)
		{
			if (player == null || !_settings.ShowInfoOnPlayerInit)
				return;

			PlayerInfoState state;
			PlayerActiveTabs.TryGetValue(player.userID, out state);

			if (state == null)
			{

				state = new PlayerInfoState(_settings);
				PlayerActiveTabs.Add(player.userID, state);
			}

			if (!state.InfoShownOnLogin)
				return;

			ShowInfo(player, string.Empty, new string[0]);
		}

		[ChatCommand("info")]
		private void ShowInfo(BasePlayer player, string command, string[] args)
		{
			if (player == null)
				return;

			if (!PlayerActiveTabs.ContainsKey(player.userID))
				PlayerActiveTabs.Add(player.userID, new PlayerInfoState(_settings));

			var container = new CuiElementContainer();
			string mainPanelName = AddMainPanel(container);

			//Add tab buttons

			var tabToSelectIndex = _settings.TabToOpenByDefault;
			var allowedTabs = _settings.Tabs
				.Where((tab, tabIndex) => string.IsNullOrEmpty(tab.OxideGroup) ||
					tab.OxideGroup.Split(',')
						.Any(group => Permission.UserHasGroup(player.userID.ToString(CultureInfo.InvariantCulture), group)))
				.ToList();
			if (allowedTabs.Count <= 0)
			{
				SendReply(player, "[GUI Help] You don't have permissions to see info.");
				return;
			}
			var activeAllowedTab = allowedTabs[tabToSelectIndex];
			var tabContentPanelName = CreateTabContent(activeAllowedTab, container, mainPanelName);
			var activeTabButtonName = AddActiveButton(tabToSelectIndex, activeAllowedTab, container, mainPanelName);

			for (int tabIndex = 0; tabIndex < allowedTabs.Count; tabIndex++)
			{
				if (tabIndex == tabToSelectIndex)
					continue;

				AddNonActiveButton(tabIndex, container, allowedTabs[tabIndex], mainPanelName, tabContentPanelName, activeTabButtonName);
			}
			CuiHelper.AddUi(player, container);
		}

		private static string AddMainPanel(CuiElementContainer container)
		{
			Color backgroundColor;
			Color.TryParseHexString(_settings.BackgroundColor, out backgroundColor);
			var mainPanel = new CuiPanel
			{
				CursorEnabled = true,
				Image =
				{
					Color = backgroundColor.ToRustFormatString(),
				},
				RectTransform =
				{
					AnchorMin = _settings.Position.GetRectTransformAnchorMin(),
					AnchorMax = _settings.Position.GetRectTransformAnchorMax()
				}
			};

			string mainPanelName = container.Add(mainPanel);
			return mainPanelName;
		}

		private string CreateTabContent(HelpTab helpTab, CuiElementContainer container, string mainPanelName, int pageIndex = 0)
		{
			Color backgroundColor;
			Color.TryParseHexString(_settings.BackgroundColor, out backgroundColor);
			var tabContentPanelName = CreateTabContentPanel(container, mainPanelName, backgroundColor);

			var cuiLabel = CreateHeaderLabel(helpTab);
			container.Add(cuiLabel, tabContentPanelName);

			var closeButton = CreateCloseButton(mainPanelName, _settings.CloseButtonColor);
			container.Add(closeButton, tabContentPanelName);

			const float firstLineMargin = 0.91f;
			const float textLineHeight = 0.04f;

			var currentPage = helpTab.Pages.ElementAtOrDefault(pageIndex);
			if (currentPage == null)
				return tabContentPanelName;

			for (var textRow = 0; textRow < currentPage.TextLines.Count; textRow++)
			{
				var textLine = currentPage.TextLines[textRow];
				var textLineLabel = CreateTextLineLabel(helpTab, firstLineMargin, textLineHeight, textRow, textLine);
				container.Add(textLineLabel, tabContentPanelName);
			}

			if (pageIndex > 0)
			{
				var prevPageButton = CreatePrevPageButton(mainPanelName, pageIndex, tabContentPanelName, _settings.PrevPageButtonColor);
				container.Add(prevPageButton, tabContentPanelName);
			}

			if (helpTab.Pages.Count - 1 == pageIndex)
				return tabContentPanelName;

			var nextPageButton = CreateNextPageButton(mainPanelName, pageIndex, tabContentPanelName, _settings.NextPageButtonColor);
			container.Add(nextPageButton, tabContentPanelName);

			return tabContentPanelName;
		}

		private static string CreateTabContentPanel(CuiElementContainer container, string mainPanelName, Color backgroundColor)
		{
			return container.Add(new CuiPanel
			{
				CursorEnabled = false,
				Image =
				{
					Color = backgroundColor.ToRustFormatString(),
				},
				RectTransform =
				{
					AnchorMin = "0.22 0.01",
					AnchorMax = "0.99 0.98"
				}
			}, mainPanelName);
		}

		private static CuiLabel CreateHeaderLabel(HelpTab helpTab)
		{
			return new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0.01 0.85",
					AnchorMax = "1.0 0.98"
				},
				Text =
				{
					Align = helpTab.HeaderAnchor,
					FontSize = helpTab.HeaderFontSize,
					Text = helpTab.HeaderText
				}
			};
		}

		private static CuiButton CreateCloseButton(string mainPanelName, string hexColor)
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
					AnchorMin = "0.86 0.93",
					AnchorMax = "0.97 0.99"
				},
				Text =
				{
					Text = "Close",
					FontSize = 18,
					Align = TextAnchor.MiddleCenter
				}
			};
		}

		private static CuiLabel CreateTextLineLabel(HelpTab helpTab, float firstLineMargin, float textLineHeight, int textRow,
			string textLine)
		{
			var textLineLabel = new CuiLabel
			{
				RectTransform =
				{
					AnchorMin = "0.01 " + (firstLineMargin - textLineHeight * (textRow + 1)),
					AnchorMax = "0.85 " + (firstLineMargin - textLineHeight * textRow)
				},
				Text =
				{
					Align = helpTab.TextAnchor,
					FontSize = helpTab.TextFontSize,
					Text = textLine
				}
			};
			return textLineLabel;
		}

		private static CuiButton CreatePrevPageButton(string mainPanelName, int pageIndex, string tabContentPanelName, string hexColor)
		{
			Color color;
			Color.TryParseHexString(hexColor, out color);
			return new CuiButton
			{
				Button =
				{
					Command = string.Format("changepage {0} {1} {2}", pageIndex - 1, tabContentPanelName, mainPanelName),
					Color = color.ToRustFormatString()
				},
				RectTransform =
				{
					AnchorMin = "0.86 0.01",
					AnchorMax = "0.97 0.07"
				},
				Text =
				{
					Text = "Prev Page",
					FontSize = 18,
					Align = TextAnchor.MiddleCenter
				}
			};
		}

		private static CuiButton CreateNextPageButton(string mainPanelName, int pageIndex, string tabContentPanelName, string hexColor)
		{
			Color color;
			Color.TryParseHexString(hexColor, out color);
			return new CuiButton
			{
				Button =
				{
					Command = string.Format("changepage {0} {1} {2}", pageIndex + 1, tabContentPanelName, mainPanelName),
					Color = color.ToRustFormatString()
				},
				RectTransform =
				{
					AnchorMin = "0.86 0.08",
					AnchorMax = "0.97 0.15"
				},
				Text =
				{
					Text = "Next Page",
					FontSize = 18,
					Align = TextAnchor.MiddleCenter
				}
			};
		}

		private static void AddNonActiveButton(
			int tabIndex,
			CuiElementContainer container,
			HelpTab helpTab,
			string mainPanelName,
			string tabContentPanelName,
			string activeTabButtonName)
		{
			Color nonActiveButtonColor;
			Color.TryParseHexString(_settings.InactiveButtonColor, out nonActiveButtonColor);

			CuiButton helpTabButton = CreateTabButton(tabIndex, helpTab, nonActiveButtonColor);
			string helpTabButtonName = container.Add(helpTabButton, mainPanelName);

			CuiElement helpTabButtonCuiElement =
				container.First(i => i.Name.Equals(helpTabButtonName, StringComparison.OrdinalIgnoreCase));
			CuiButtonComponent generatedHelpTabButton = helpTabButtonCuiElement.Components.OfType<CuiButtonComponent>().First();

			string command = string.Format("changeTab {0} {1} {2} {3} {4}", tabIndex, tabContentPanelName, activeTabButtonName,
				helpTabButtonName, mainPanelName);
			generatedHelpTabButton.Command = command;
		}

		private static string AddActiveButton(
			int activeTabIndex,
			HelpTab activeTab,
			CuiElementContainer container,
			string mainPanelName)
		{
			Color activeButtonColor;
			Color.TryParseHexString(_settings.ActiveButtonColor, out activeButtonColor);

			var activeHelpTabButton = CreateTabButton(activeTabIndex, activeTab, activeButtonColor);
			var activeTabButtonName = container.Add(activeHelpTabButton, mainPanelName);

			var activeTabButtonCuiElement =
				container.First(i => i.Name.Equals(activeTabButtonName, StringComparison.OrdinalIgnoreCase));
			var activeTabButton = activeTabButtonCuiElement.Components.OfType<CuiButtonComponent>().First();

			var command = string.Format("changeTab {0}", activeTabIndex);
			activeTabButton.Command = command;

			return activeTabButtonName;
		}

		private static CuiButton CreateTabButton(int tabIndex, HelpTab helpTab, Color color)
		{
			const float verticalMargin = 0.03f;
			const float buttonHeight = 0.06f;

			return new CuiButton
			{
				Button =
				{
					Color = color.ToRustFormatString()
				},
				RectTransform =
				{
					AnchorMin = string.Format("0.01 {0}", 1 - ((verticalMargin + buttonHeight) * (tabIndex + 1))),
					AnchorMax = string.Format("0.20 {0}", 1 - ((verticalMargin * (tabIndex + 1)) + (tabIndex * buttonHeight)))
				},
				Text =
				{
					Text = helpTab.ButtonText,
					FontSize = helpTab.TabButtonFontSize,
					Align = helpTab.TabButtonAnchor
				}
			};
		}
	}
}

namespace ServerInfo
{
	public sealed class Settings
	{
		public Settings()
		{
			Tabs = new List<HelpTab>();
			ShowInfoOnPlayerInit = true;
			TabToOpenByDefault = 0;
			Position = new WindowPosition();

			ActiveButtonColor = "#" + Color.cyan.ToHexStringRGBA();
			InactiveButtonColor = "#" + Color.gray.ToHexStringRGBA();
			CloseButtonColor = "#" + Color.gray.ToHexStringRGBA();
			PrevPageButtonColor = "#" + Color.gray.ToHexStringRGBA();
			NextPageButtonColor = "#" + Color.gray.ToHexStringRGBA();
			BackgroundColor = "#" + new Color(0.1f, 0.1f, 0.1f, 1f).ToHexStringRGBA();
		}

		public List<HelpTab> Tabs { get; set; }
		public bool ShowInfoOnPlayerInit { get; set; }
		public int TabToOpenByDefault { get; set; }

		public WindowPosition Position { get; set; }

		public string ActiveButtonColor { get; set; }
		public string InactiveButtonColor { get; set; }
		public string CloseButtonColor { get; set; }
		public string NextPageButtonColor { get; set; }
		public string PrevPageButtonColor { get; set; }
		public string BackgroundColor { get; set; }

		public static Settings CreateDefault()
		{
			var settings = new Settings();
			settings.Tabs.Add(new HelpTab
			{
				ButtonText = "First Tab",
				HeaderText = "First Tab",
				Pages =
				{
					new HelpTabPage
					{
						TextLines =
						{
							"This is first tab, first page.",
							"Add some text here by adding more lines.",
							"You should replace all default text lines with whatever you feel up to",
							"type <color=red> /info </color> to open this window",
							"Press next page to check second page.",
							"You may add more pages in config file."
						}
					},
					new HelpTabPage
					{
						TextLines =
						{
							"This is first tab, second page",
							"Add some text here by adding more lines.",
							"You should replace all default text lines with whatever you feel up to",
							"type <color=red> /info </color> to open this window",
							"Press next page to check third page.",
							"Press prev page to go back to first page.",
							"You may add more pages in config file."
						}
					}
					,
					new HelpTabPage
					{
						TextLines =
						{
							"This is first tab, third page",
							"Add some text here by adding more lines.",
							"You should replace all default text lines with whatever you feel up to",
							"type <color=red> /info </color> to open this window",
							"Press prev page to go back to second page.",
						}
					}
				}
			});
			settings.Tabs.Add(new HelpTab
			{
				ButtonText = "Second Tab",
				HeaderText = "Second Tab",
				Pages =
				{
					new HelpTabPage
					{
						TextLines =
						{
							"This is second tab, first page.",
							"Add some text here by adding more lines.",
							"You should replace all default text lines with whatever you feel up to",
							"type <color=red> /info </color> to open this window",
							"You may add more pages in config file."
						}
					}
				}
			});
			settings.Tabs.Add(new HelpTab
			{
				ButtonText = "Third Tab",
				HeaderText = "Third Tab",
				Pages =
				{
					new HelpTabPage
					{
						TextLines =
						{
							"This is third tab, first page.",
							"Add some text here by adding more lines.",
							"You should replace all default text lines with whatever you feel up to",
							"type <color=red> /info </color> to open this window",
							"You may add more pages in config file."
						}
					}
				}
			});
			return settings;
		}
	}

	public sealed class WindowPosition
	{
		public WindowPosition()
		{
			MinX = 0.15f;
			MaxX = 0.9f;
			MinY = 0.2f;
			MaxY = 0.9f;
		}

		public float MinX { get; set; }
		public float MaxX { get; set; }
		public float MinY { get; set; }
		public float MaxY { get; set; }

		public string GetRectTransformAnchorMin()
		{
			return string.Format("{0} {1}", MinX, MinY);
		}

		public string GetRectTransformAnchorMax()
		{
			return string.Format("{0} {1}", MaxX, MaxY);
		}
	}

	public sealed class HelpTab
	{
		private string _headerText;
		private string _buttonText;

		public HelpTab()
		{
			ButtonText = "Default ServerInfo Help Tab";
			HeaderText = "Default ServerInfo Help";
			Pages = new List<HelpTabPage>();
			TextFontSize = 16;
			HeaderFontSize = 32;
			TabButtonFontSize = 16;
			TextAnchor = TextAnchor.MiddleLeft;
			HeaderAnchor = TextAnchor.UpperLeft;
			TabButtonAnchor = TextAnchor.MiddleCenter;
			OxideGroup = string.Empty;
		}

		public string ButtonText
		{
			get { return string.IsNullOrEmpty(_buttonText) ? _headerText : _buttonText; }
			set { _buttonText = value; }
		}

		public string HeaderText
		{
			get
			{
				return string.IsNullOrEmpty(_headerText) ? _buttonText : _headerText;
			}
			set { _headerText = value; }
		}

		public List<HelpTabPage> Pages { get; set; }

		public TextAnchor TabButtonAnchor { get; set; }
		public int TabButtonFontSize { get; set; }

		public TextAnchor HeaderAnchor { get; set; }
		public int HeaderFontSize { get; set; }

		public int TextFontSize { get; set; }
		public TextAnchor TextAnchor { get; set; }

		public string OxideGroup { get; set; }
	}

	public sealed class HelpTabPage
	{
		public List<string> TextLines { get; set; }

		public HelpTabPage()
		{
			TextLines = new List<string>();
		}
	}

	public sealed class PlayerInfoState
	{
		public PlayerInfoState(Settings settings)
		{
			if (settings == null) throw new ArgumentNullException("settings");

			ActiveTabIndex = settings.TabToOpenByDefault;
			PageIndex = 0;
			InfoShownOnLogin = settings.ShowInfoOnPlayerInit;
		}

		public int ActiveTabIndex { get; set; }
		public int PageIndex { get; set; }
		public bool InfoShownOnLogin { get; set; }
	}
}

namespace ServerInfo.Extensions
{
	public static class ColorExtensions
	{
		public static string ToRustFormatString(this Color color)
		{
			return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
		}
	}
}