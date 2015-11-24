//Reference: NLua

using System;
using System.Collections.Generic;
using System.Linq;
using EconomicsVip.Diagnostics;
using EconomicsVip.Services;
using EconomicsVip.Settings;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
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

			var covalence = Interface.Oxide.GetLibrary<Covalence>();
			var player = covalence.Players.GetPlayer(userId);
			if (player == null)
			{
				Puts("Vip: covalence player not found");
				return;
			}

			if (!permission.GroupExists(_settings.VipGroupName))
			{
				Puts("Vip: created group");
				permission.CreateGroup(_settings.VipGroupName, _settings.VipGroupTitle, _settings.VipGroupRank);
			}

			if (permission.UserHasGroup(userId, _settings.VipGroupName))
			{
				Puts("Vip: user {0} is already in group", userId);
			}

			permission.AddUserGroup(userId, _settings.VipGroupName);
			var user = _vipUserList.FirstOrDefault(u => u.UserId == steamId);
			if (user == null)
			{
				_vipUserList.Add(new VipUserInfo
				{
					ExpirationDateString = DateTime.Now.AddSeconds(_settings.VipDurationInSeconds).ToString(),
					UserId = steamId
				});
			}
			else
			{

			}

			var onlinePlayer = BasePlayer.FindByID(steamId);
			Interface.Oxide.DataFileSystem.WriteObject("vipuserlist", _vipUserList);

			if (onlinePlayer == null)
			{
				Puts("Vip: online player not found");
				return;
			}
			Diagnostics.MessageToPlayer(onlinePlayer, "Ты стал VIP!");
		}

		private void CheckUserList()
		{
			var now = DateTime.Now;

			var usersToRemove = new List<VipUserInfo>();
			foreach (var vipUserInfo in _vipUserList)
			{
				var dateTime = DateTime.Parse(vipUserInfo.ExpirationDateString);
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
		public const int DefaultVipDurationInSeconds = 60 * 60 * 24 * 30;
		public const int DefaultRequiredBalance = 1000;
		public const int DefaultCheckVipTimerIntervalInSeconds = 300;

		private string _vipGroupName;
		private int _requiredBalance = 1000;
		private int _vipDurationInSeconds = DefaultVipDurationInSeconds;
		private int _vipGroupRank;
		private string _vipGroupTitle;
		private float _checkVipTimerIntervalInSeconds = 300;

		public string VipGroupName
		{
			get { return _vipGroupName; }
			set { _vipGroupName = string.IsNullOrEmpty(value) ? DefaultGroupName : value; }
		}

		public string VipGroupTitle
		{
			get { return _vipGroupTitle; }
			set { _vipGroupTitle = string.IsNullOrEmpty(value) ? DefaultGroupName : value; }
		}

		public int VipGroupRank
		{
			get { return _vipGroupRank; }
			set { _vipGroupRank = value < 0 ? 0 : value; }
		}

		public int RequiredBalance
		{
			get { return _requiredBalance; }
			set { _requiredBalance = value <= 0 ? 1000 : value; }
		}

		public int VipDurationInSeconds
		{
			get { return _vipDurationInSeconds; }
			set { _vipDurationInSeconds = value < 0 ? 0 : value; }
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
				settings.VipGroupName = configFile.Get("VipGroupName") == null
					? PluginSettings.DefaultGroupName
					: configFile.Get("VipGroupName").ToString();

				settings.RequiredBalance = configFile.Get("RequiredBalance") == null
					? PluginSettings.DefaultRequiredBalance
					: int.Parse(configFile.Get("RequiredBalance").ToString());

				settings.VipDurationInSeconds = configFile.Get("VipDurationInSeconds") == null
					? PluginSettings.DefaultVipDurationInSeconds
					: int.Parse(configFile.Get("VipDurationInSeconds").ToString());

				settings.CheckVipTimerIntervalInSeconds = configFile.Get("CheckVipTimerIntervalInSeconds") == null
					? PluginSettings.DefaultCheckVipTimerIntervalInSeconds
					: int.Parse(configFile.Get("CheckVipTimerIntervalInSeconds").ToString());

				settings.VipGroupRank = configFile.Get("VipGroupRank") == null
					? 0
					: int.Parse(configFile.Get("VipGroupRank").ToString());

				settings.VipGroupTitle = configFile.Get("VipGroupTitle") == null
					? PluginSettings.DefaultGroupName
					: configFile.Get("VipGroupTitle").ToString();
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

			config["VipGroupName"] = settings.VipGroupName;
			config["VipDurationInSeconds"] = settings.VipDurationInSeconds;
			config["VipGroupRank"] = settings.VipGroupRank;
			config["VipGroupTitle"] = settings.VipGroupTitle;
			config["RequiredBalance"] = settings.RequiredBalance;
			config["CheckVipTimerIntervalInSeconds"] = settings.CheckVipTimerIntervalInSeconds;
		}
	}
}

namespace EconomicsVip.Services
{
	public sealed class VipUserInfo
	{
		public ulong UserId { get; set; }
		public string ExpirationDateString { get; set; }
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
