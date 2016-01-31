using Oxide.Game.Rust.Cui;
using Oxide.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Enhanced Hammer", "Visagalis", "0.4.5", ResourceId = 1439)]
    public class EnhancedHammer : RustPlugin
    {
        public class PlayerDetails
        {
            public PlayerFlags flags = PlayerFlags.MESSAGES_DISABLED;
            public BuildingGrade.Enum upgradeInfo = BuildingGrade.Enum.Count; // HAMMER
            public int backToDefaultTimer = 0;
        }

        private string pluginPrefix = "[Быстрый апгрейд] ";
        public enum PlayerFlags
        {
            NONE = 0,
            ICONS_DISABLED = 2,
            PLUGIN_DISABLED = 4,
            MESSAGES_DISABLED = 8
        }

        public static Dictionary<ulong, PlayerDetails> playersInfo = new Dictionary<ulong, PlayerDetails>();
        public static Dictionary<ulong, Timer> playersTimers = new Dictionary<ulong, Timer>();

        void OnStructureRepair(BuildingBlock block, BasePlayer player)
        {
            if (PlayerHasFlag(player.userID, PlayerFlags.PLUGIN_DISABLED))
                return;

            if (playersInfo[player.userID].upgradeInfo == BuildingGrade.Enum.Count
                || playersInfo[player.userID].upgradeInfo <= block.currentGrade.gradeBase.type
                || !player.CanBuild())
            {
                if (playersInfo[player.userID].upgradeInfo != BuildingGrade.Enum.Count && playersInfo[player.userID].upgradeInfo <= block.currentGrade.gradeBase.type)
                {
                    if(!PlayerHasFlag(player.userID, PlayerFlags.MESSAGES_DISABLED))
                        SendReply(player, pluginPrefix + "Режим ремонта.");
                    playersInfo[player.userID].upgradeInfo = BuildingGrade.Enum.Count;
                    RenderMode(player, true);
                }
                else if (!player.CanBuild())
                {
                    SendReply(player, pluginPrefix + "Строительство заблокировано!");
                }
            }
            else
            {
                if (block.name.ToLower().Contains("wall.external"))
                {
                    SendReply(player, pluginPrefix + "Не могу улучшить стену! Переключаюсь в режим ремонта.");
                    playersInfo[player.userID].upgradeInfo = BuildingGrade.Enum.Count;
                    return;
                }

                float currentHealth = block.health;
                var desiredGrade = playersInfo[player.userID].upgradeInfo;
	            var hasEnough = CanAffordUpgrade(desiredGrade, player, block);

				if (hasEnough)
				{
					var grade = GetGrade(desiredGrade, block);
					PayForUpgrade(grade, player);
					block.SetGrade(desiredGrade);
                    block.SetHealthToMax();
                    block.SetFlag(BaseEntity.Flags.Reserved1, true); // refresh rotation
                    block.Invoke("StopBeingRotatable", 600f);

					Effect.server.Run("assets/bundled/prefabs/fx/build/promote_" + playersInfo[player.userID].upgradeInfo.ToString().ToLower() + ".prefab", block, 0u, Vector3.zero, Vector3.zero, null, false);
                }
                else
                {
                    block.health = currentHealth;
                    SendReply(player, pluginPrefix + "Не хватает ресурсов для улучшения!");
                }
            }

            RefreshTimer(player);
        }

		private bool CanAffordUpgrade(BuildingGrade.Enum iGrade, BasePlayer player, BuildingBlock block)
		{
			using (List<ItemAmount>.Enumerator enumerator = GetGrade(iGrade, block).costToBuild.GetEnumerator())
			{
				while (enumerator.MoveNext())
				{
					var current = enumerator.Current;
					if (player.inventory.GetAmount(current.itemid) < (double)current.amount)
						return false;
				}
			}
			return true;
		}

		private ConstructionGrade GetGrade(BuildingGrade.Enum iGrade, BuildingBlock block)
		{
			return block.grade < (BuildingGrade.Enum)block.blockDefinition.grades.Length 
				? block.blockDefinition.grades[(int)iGrade] 
				: block.blockDefinition.defaultGrade;
		}

		private void PayForUpgrade(ConstructionGrade g, BasePlayer player)
		{
			var collect = new List<Item>();
			using (var enumerator = g.costToBuild.GetEnumerator())
			{
				while (enumerator.MoveNext())
				{
					var current = enumerator.Current;
					player.inventory.Take(collect, current.itemid, (int)current.amount);
					BasePlayer basePlayer = player;
					var objArray1 = new object[4];
					const int index1 = 0;
					const string str1 = "note.inv ";
					objArray1[index1] = str1;
					const int index2 = 1;
					// ISSUE: variable of a boxed type
					var local1 = (ValueType)current.itemid;
					objArray1[index2] = local1;
					const int index3 = 2;
					const string str2 = " ";
					objArray1[index3] = str2;
					const int index4 = 3;
					// ISSUE: variable of a boxed type
					var local2 = (ValueType)(float)((double)current.amount * -1.0);
					objArray1[index4] = (object)local2;
					string strCommand = string.Concat(objArray1);
					var objArray2 = new object[0];
					basePlayer.Command(strCommand, objArray2);
				}
			}
			using (List<Item>.Enumerator enumerator = collect.GetEnumerator())
			{
				while (enumerator.MoveNext())
					enumerator.Current.Remove(0.0f);
			}
		}

	    void RefreshTimer(BasePlayer player)
        {
            if (playersInfo[player.userID].backToDefaultTimer == 0)
                return;

            if (playersTimers.ContainsKey(player.userID))
            {
                playersTimers[player.userID].Destroy();
                playersTimers.Remove(player.userID);
            }

            var timerIn = timer.Once(playersInfo[player.userID].backToDefaultTimer, () => SetBackToDefault(player));
            playersTimers.Add(player.userID, timerIn);
        }

        void OnStructureUpgrade(BuildingBlock block, BasePlayer player, BuildingGrade.Enum grade)
        {
            if (PlayerHasFlag(player.userID, PlayerFlags.PLUGIN_DISABLED))
                return;

            if (playersInfo[player.userID].upgradeInfo != grade)
            {
                playersInfo[player.userID].upgradeInfo = grade;
                RenderMode(player, false);
                if (!PlayerHasFlag(player.userID, PlayerFlags.MESSAGES_DISABLED))
                    SendReply(player, pluginPrefix + "Режим улучшения. [" + grade.ToString() + "]");
            }

            RefreshTimer(player);
        }

        void RenderMode(BasePlayer player, bool repair = false)
        {
            CuiHelper.DestroyUi(player, "EnhancedHammerUI");
            if (PlayerHasFlag(player.userID, PlayerFlags.PLUGIN_DISABLED) || 
                PlayerHasFlag(player.userID, PlayerFlags.ICONS_DISABLED) || 
                (!repair && playersInfo[player.userID].upgradeInfo == BuildingGrade.Enum.Count))
                return;

            CuiElementContainer panel = new CuiElementContainer();
            string icon = "http://i.imgur.com/Nq6DNSX.png";
            if (!repair)
            {
                switch (playersInfo[player.userID].upgradeInfo)
                {
                    case BuildingGrade.Enum.Wood:
                        icon = "http://i.imgur.com/F4XBBhY.png";
                        break;
                    case BuildingGrade.Enum.Stone:
                        icon = "http://i.imgur.com/S7Sl9oh.png";
                        break;
                    case BuildingGrade.Enum.Metal:
                        icon = "http://i.imgur.com/fVjzbag.png";
                        break;
                    case BuildingGrade.Enum.TopTier:
                        icon = "http://i.imgur.com/f0WklR3.png";
                        break;
                }

            }
            CuiElement ehUI = new CuiElement { Name = "EnhancedHammerUI", Parent = "HUD/Overlay", FadeOut = 0.5f };
            CuiRawImageComponent ehUI_IMG = new CuiRawImageComponent { FadeIn = 0.5f, Url = icon };
            CuiRectTransformComponent ehUI_RECT = new CuiRectTransformComponent
            {
                AnchorMin = "0.32 0.09",
                AnchorMax = "0.34 0.13"
            };
            ehUI.Components.Add(ehUI_IMG);
            ehUI.Components.Add(ehUI_RECT);
            panel.Add(ehUI);
            CuiHelper.AddUi(player, panel);
        }

        void SetBackToDefault(BasePlayer player)
        {
            if(playersTimers.ContainsKey(player.userID))
                playersTimers.Remove(player.userID);
            playersInfo[player.userID].upgradeInfo = BuildingGrade.Enum.Count;
            RemoveUI(player);
            if (!PlayerHasFlag(player.userID, PlayerFlags.MESSAGES_DISABLED))
                SendReply(player, pluginPrefix + "Режим ремонта.");
        }

        void RemoveUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "EnhancedHammerUI");
        }

        void OnPlayerInit(BasePlayer player)
        {
            playersInfo.Add(player.userID, new PlayerDetails());
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (playersInfo.ContainsKey(player.userID))
                playersInfo.Remove(player.userID);
        }

        public PlayerFlags GetPlayerFlags(ulong userID)
        {
            if (playersInfo.ContainsKey(userID))
                    return playersInfo[userID].flags;

            return PlayerFlags.NONE;
        }

        void Init()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                playersInfo.Add(player.userID, new PlayerDetails());
            }
        }

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                RemoveUI(player);
            }
        }

        [ChatCommand("eh")]
        private void OnEhCommand(BasePlayer player, string command, string[] arg)
        {
            bool incorrectUsage = arg.Length == 0;
            bool ADD = true;
            bool REMOVE = false;
            if (arg.Length == 1)
            {
                switch (arg[0].ToLower())
                {
                    case "enable":
                        ModifyPlayerFlags(player, REMOVE, PlayerFlags.PLUGIN_DISABLED);
                        break;
                    case "disable":
                        ModifyPlayerFlags(player, ADD, PlayerFlags.PLUGIN_DISABLED);
                        break;
                    case "show":
                        ModifyPlayerFlags(player, REMOVE, PlayerFlags.ICONS_DISABLED);
                        break;
                    case "hide":
                        ModifyPlayerFlags(player, ADD, PlayerFlags.ICONS_DISABLED);
                        break;
                    default:
                        incorrectUsage = true;
                        break;
                }
                if (!incorrectUsage)
                    RenderMode(player);
            }
            else if (arg.Length == 2)
            {
                if (arg[0].ToLower() == "timer")
                {
                    int seconds;
                    if (int.TryParse(arg[1], out seconds) && seconds >= 0)
                    {
                        playersInfo[player.userID].backToDefaultTimer = seconds;
                        string msg = "";
                        if (seconds > 0)
                            msg += " Таймер будет выключаться через " + seconds + " секунд.";
                        else
                            msg += " Timer will never end.";
                        SendReply(player, pluginPrefix + msg);
                        incorrectUsage = false;
                    }
                }
                else if (arg[0].ToLower() == "msgs")
                {
                    if (arg[1].ToLower() == "show")
                        ModifyPlayerFlags(player, false, PlayerFlags.MESSAGES_DISABLED);
                    else if (arg[1].ToLower() == "hide")
                        ModifyPlayerFlags(player, true, PlayerFlags.MESSAGES_DISABLED);
                    else
                        incorrectUsage = true;
                }
            }

            if (incorrectUsage)
            {
                SendReply(player, "Как использовать:");
                SendReply(player, "/eh [enable/disable] - включает или отключает улучшенный молоток.");
                SendReply(player, "/eh [show/hide] - Включает/выключает иконки.");
                SendReply(player, "/eh timer [0/seconds] - Через сколько переключится в режим ремонта.");
                SendReply(player, "/eh msgs [show/hide] - Отображать уведомления о режиме улучшения/починки в чат.");
            }
        }

        private bool PlayerHasFlag(ulong userID, PlayerFlags flag)
        {
            return (GetPlayerFlags(userID) & flag) == flag;
        }

        private void ModifyPlayerFlags(BasePlayer player, bool addFlag, PlayerFlags flag)
        {
            bool actionCompleted = false;
            if (addFlag)
            {
                if ((playersInfo[player.userID].flags & flag) != flag)
                {
                    playersInfo[player.userID].flags |= flag;
                    actionCompleted = true;
                }
            }
            else
            {
                if ((playersInfo[player.userID].flags & flag) == flag)
                {
                    playersInfo[player.userID].flags &= ~flag;
                    actionCompleted = true;
                }
            }

            if (actionCompleted)
            {
                string msg = "";
                switch (flag)
                {
                    case PlayerFlags.ICONS_DISABLED:
                        msg += "ICONS";
                        break;
                    case PlayerFlags.PLUGIN_DISABLED:
                        msg += "PLUGIN";
                        break;
                    case PlayerFlags.MESSAGES_DISABLED:
                        msg += "MESSAGES";
                        break;
                }
                SendReply(player, pluginPrefix + msg + " был " + (!addFlag? "включен" : "выключен") + ".");
            }
        }
    }
}