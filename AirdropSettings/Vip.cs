//Reference: NLua

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EconomicsVip.Diagnostics;
using EconomicsVip.Services;
using EconomicsVip.Settings;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries;
using UnityEngine;
using LogType = Oxide.Core.Logging.LogType;

namespace Oxide.Plugins
{
	[Info("Vip", "baton", 0.1, ResourceId = 715)]
	[Description("vip for lovely day")]
	public sealed class Vip : RustPlugin
	{
		private static PluginSettings _settings;
		private static List<VipUserInfo> _vipUserList;

		void OnServerInitialized()
		{
			LoadConfig();
			_settings = PluginSettingsRepository.Load(Config);

			var library = Interface.GetMod().GetLibrary<Permission>();
			if (!library.PermissionExists("vip"))
				library.RegisterPermission("vip", this);
			if (!library.PermissionExists("donator"))
				library.RegisterPermission("donator", this);
			if (!library.PermissionExists("moderator1"))
				library.RegisterPermission("moderator1", this);

			_vipUserList = Interface.Oxide.DataFileSystem.ReadObject<List<VipUserInfo>>("vipuserlist");
			timer.Every(_settings.CheckVipTimerIntervalInSeconds, CheckUserList);

			PluginSettingsRepository.Save(_settings, Config);
			SaveConfig();
		}

		[ConsoleCommand("shopvip")]
		private void AddVip(ConsoleSystem.Arg arg)
		{
			if (arg == null || !arg.HasArgs())
				return;

			var userId = arg.GetString(0);
			var days = arg.GetInt(1);
			if (days == 0)
			{
				Puts("zero days");
				return;
			}

			if (string.IsNullOrEmpty(userId))
			{
				Puts("Vip: no userId");
				return;
			}

			if (arg.Player() != null)
			{
				Puts("Vip: user param is present");
				return;
			}

			ulong steamId;
			if (!ulong.TryParse(userId, out steamId))
			{
				Puts("Vip: user id is not valid");
				return;
			}

			var player = covalence.Players.GetPlayer(userId);
			if (player == null)
			{
				Puts("Vip: covalence player not found");
				return;
			}

			if (!permission.GroupExists(_settings.VipGroupName))
			{
				Puts("Vip: created group");
				permission.CreateGroup(_settings.VipGroupName, "vip", 0);
			}

			var vipEntry = _vipUserList.FirstOrDefault(u => u.UserId == steamId);
			if (vipEntry == null)
			{
				Puts("user {0} is not yet vip: adding {1} days", player.Nickname, days);
				_vipUserList.Add(new VipUserInfo
				{
					ExpirationDate = DateTime.Now.AddDays(days),
					UserId = steamId
				});
			}
			else
			{
				Puts("user {0} is already vip: adding {1} days", player.Nickname, days);
				var date = vipEntry.ExpirationDate;
				var endDate = date.AddDays(days);
				vipEntry.ExpirationDate = endDate;
				Puts("user {0} vip end date:{1}", player.Nickname, endDate);
			}

			permission.AddUserGroup(userId, _settings.VipGroupName);

			var onlinePlayer = BasePlayer.FindByID(steamId);
			Interface.Oxide.DataFileSystem.WriteObject("vipuserlist", _vipUserList);

			if (onlinePlayer == null)
			{
				Puts("Vip: online player not found");
				return;
			}
			Diagnostics.MessageToPlayer(onlinePlayer, "Ты стал VIP!");
		}

		[ChatCommand("vip")]
		private void VipChatCommand(BasePlayer player, string command, string[] args)
		{
			if (player == null)
				return;

			var id = player.userID;
			var vipEntry = _vipUserList.FirstOrDefault(u => u.UserId == id);
			if (vipEntry == null)
			{
				Diagnostics.MessageToPlayer(player, "У тебя нет статуса VIP!");
			}
			else
			{
				Diagnostics.MessageToPlayer(player, "У тебя оплачен статус VIP до: {0}!", vipEntry.ExpirationDate.ToString(CultureInfo.GetCultureInfo("Ru-ru")));
				Diagnostics.MessageToPlayer(player, "Тебе доступен специальный набор vip1");
				Diagnostics.MessageToPlayer(player, "Чтобы получить набери /kit vip1");
			}
		}

		private void CheckUserList()
		{
			var now = DateTime.Now;

			var usersToRemove = new List<VipUserInfo>();
			foreach (var vipUserInfo in _vipUserList)
			{
				var dateTime = vipUserInfo.ExpirationDate;
				if (dateTime <= now)
					usersToRemove.Add(vipUserInfo);
			}

			foreach (var vipUserInfo in usersToRemove)
			{
				_vipUserList.Remove(vipUserInfo);
				var uid = Convert.ToString(vipUserInfo.UserId);
				Diagnostics.MessageToServer("Removing user {0} from group {1}", uid, _settings.VipGroupName);
				permission.RemoveUserGroup(uid, _settings.VipGroupName);
			}

			Interface.Oxide.DataFileSystem.WriteObject("vipuserlist", _vipUserList);
		}
	}
}

namespace EconomicsVip.Settings
{
	public sealed class PluginSettings
	{
		public const string DefaultGroupName = "vip";
		public const int DefaultCheckVipTimerIntervalInSeconds = 300;

		private float _checkVipTimerIntervalInSeconds = 300;
		private string _vipGroupName = "vip";

		public string VipGroupName
		{
			get { return _vipGroupName; }
			set { _vipGroupName = string.IsNullOrEmpty(value) ? DefaultGroupName : value; }
		}

		public float CheckVipTimerIntervalInSeconds
		{
			get { return _checkVipTimerIntervalInSeconds; }
			set { _checkVipTimerIntervalInSeconds = value; }
		}
	}

	public static class PluginSettingsRepository
	{
		public static PluginSettings Load(DynamicConfigFile configFile)
		{
			if (configFile == null) throw new ArgumentNullException("configFile");
			var settings = new PluginSettings();
			try
			{
				settings.VipGroupName = configFile.Get("GroupName") == null
					? PluginSettings.DefaultGroupName
					: configFile.Get("GroupName").ToString();

				settings.CheckVipTimerIntervalInSeconds = configFile.Get("CheckVipTimerIntervalInSeconds") == null
					? PluginSettings.DefaultCheckVipTimerIntervalInSeconds
					: int.Parse(configFile.Get("CheckVipTimerIntervalInSeconds").ToString());
			}
			catch (Exception ex)
			{
				Diagnostics.Diagnostics.MessageToServer("Failed to load plugin settings:{0}", ex);
			}

			return settings;
		}

		public static void Save(PluginSettings settings, DynamicConfigFile config)
		{
			if (settings == null) throw new ArgumentNullException("settings");
			if (config == null) throw new ArgumentNullException("config");

			config["GroupName"] = settings.VipGroupName;
			config["CheckVipTimerIntervalInSeconds"] = settings.CheckVipTimerIntervalInSeconds;
		}
	}
}

namespace EconomicsVip.Services
{
	public sealed class VipUserInfo
	{
		public ulong UserId { get; set; }
		public DateTime ExpirationDate { get; set; }
	}
}

namespace EconomicsVip.Diagnostics
{
	public static class Diagnostics
	{
		public static void MessageToPlayer(BasePlayer player, string message, params object[] args)
		{
			const string format = "<color=orange>vip</color>:";
			player.SendConsoleCommand("chat.add", new object[] { 0, format + string.Format(message, args), 1f });
		}

		public static void MessageToServer(string message, params object[] args)
		{
			Interface.GetMod().RootLogger.Write(LogType.Info, message, args);
		}
	}
}
