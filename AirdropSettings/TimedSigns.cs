//Reference: NLua

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TimedSigns.Diagnostics;
using TimedSigns.Services;
using TimedSigns.Settings;
using Oxide.Core;
using Oxide.Core.Configuration;
using UnityEngine;
using LogType = Oxide.Core.Logging.LogType;

namespace Oxide.Plugins
{
	[Info("TimedSigns", "baton", 0.1, ResourceId = 715)]
	[Description("Timed signs for lovely day")]
	public sealed class TimedSigns : RustPlugin
	{
		private const string SignUserListFileName = "sign_user_list";
		private static PluginSettings _settings;
		private static List<SignUserInfo> _signUserList;
		private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		void OnServerInitialized()
		{
			LoadConfig();
			_settings = PluginSettingsRepository.Load(Config);

			_signUserList = Interface.Oxide.DataFileSystem.ReadObject<List<SignUserInfo>>(SignUserListFileName);
			timer.Every(_settings.TimerIntervalInSeconds, CheckUserList);

			PluginSettingsRepository.Save(_settings, Config);
			SaveConfig();
		}

		[ConsoleCommand("shopsign")]
		private void AddSignAccess(ConsoleSystem.Arg arg)
		{
			if (arg == null || !arg.HasArgs())
				return;

			var userId = arg.GetString(0);
			var days = arg.GetInt(1);
			if (days == 0)
			{
				Puts("signs: zero days");
				return;
			}

			if (string.IsNullOrEmpty(userId))
			{
				Puts("signs: no userId");
				return;
			}

			Puts("signs: userid:{0}", userId);

			if (arg.Player() != null)
			{
				Puts("signs: user param is present");
				return;
			}

			ulong steamId;
			if (!ulong.TryParse(userId, out steamId))
			{
				Puts("signs: user id is not valid");
				return;
			}

			var onlinePlayer = BasePlayer.FindByID(steamId);
			if (onlinePlayer == null)
			{
				Puts("signs: covalence player not found");
				return;
			}

			if (!permission.GroupExists(_settings.GroupName))
			{
				Puts("signs: created group");
				permission.CreateGroup(_settings.GroupName, "signs", 0);
			}

			var signUserInfo = _signUserList.FirstOrDefault(u => u.UserId == steamId);
			if (signUserInfo == null)
			{
				Puts("user {0} is not yet signs: adding {1} days", onlinePlayer.displayName, days);
				var now = DateTime.Now.AddDays(days);
				var timestamp = ConvertToTimestamp(now);
				_signUserList.Add(new SignUserInfo
				{
					ExpirationDate = timestamp,
					UserId = steamId
				});
			}
			else
			{
				Puts("user {0} is already signs: adding {1} days", onlinePlayer.displayName, days);
				var timestamp = signUserInfo.ExpirationDate;
				var dateTime = UnixTimeStampToDateTime(timestamp);
				var endDate = dateTime.AddDays(days);
				signUserInfo.ExpirationDate = ConvertToTimestamp(endDate);
				Puts("user {0} signs end date:{1}", onlinePlayer.displayName, endDate);
			}

			permission.AddUserGroup(userId, _settings.GroupName);

			Interface.Oxide.DataFileSystem.WriteObject(SignUserListFileName, _signUserList);
			Diagnostics.MessageToPlayer(onlinePlayer, "Ты получил доступ к картинкам в знаках на {0} дней", days);
		}

		[ChatCommand("sign")]
		private void SignChatCommand(BasePlayer player, string command, string[] args)
		{
			if (player == null)
				return;

			var id = player.userID;
			var signUserInfo = _signUserList.FirstOrDefault(u => u.UserId == id);
			if (signUserInfo == null)
			{
				Diagnostics.MessageToPlayer(player, "У тебя нет оплаченного доступа к картинкам в знаках!");
			}
			else
			{
				var expirationTimestamp = signUserInfo.ExpirationDate;
				var unixTimeStampToDateTime = UnixTimeStampToDateTime(expirationTimestamp);
				Diagnostics.MessageToPlayer(player, "У тебя оплачен статус доступ к картинкам до: {0}!", unixTimeStampToDateTime.ToString(CultureInfo.GetCultureInfo("Ru-ru")));
				Diagnostics.MessageToPlayer(player, "Для загрузки картинки в знак используй команду /sil");
			}
		}

		private void CheckUserList()
		{
			var now = DateTime.Now;
			var nowTimestamp = ConvertToTimestamp(now);

			var usersToRemove = new List<SignUserInfo>();
			foreach (var signUserInfo in _signUserList)
			{
				var dateTime = signUserInfo.ExpirationDate;
				if (dateTime <= nowTimestamp)
					usersToRemove.Add(signUserInfo);
			}

			foreach (var signUserInfo in usersToRemove)
			{
				_signUserList.Remove(signUserInfo);
				var uid = Convert.ToString(signUserInfo.UserId);
				Diagnostics.MessageToServer("Removing user {0} from group {1}", uid, _settings.GroupName);
				permission.RemoveUserGroup(uid, _settings.GroupName);
			}

			Interface.Oxide.DataFileSystem.WriteObject(SignUserListFileName, _signUserList);
		}

		private static long ConvertToTimestamp(DateTime value)
		{
			TimeSpan elapsedTime = value - Epoch;
			return (long)elapsedTime.TotalSeconds;
		}

		public static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
		{
			return Epoch.AddSeconds(unixTimeStamp).ToLocalTime();
		}
	}
}

namespace TimedSigns.Settings
{
	public sealed class PluginSettings
	{
		public const string DefaultGroupName = "sign_users";
		public const int DefaultTimerIntervalInSeconds = 6000;

		private float _timerIntervalInSeconds = 6000;
		private string _groupName = "sign_users";

		public string GroupName
		{
			get { return _groupName; }
			set { _groupName = string.IsNullOrEmpty(value) ? DefaultGroupName : value; }
		}

		public float TimerIntervalInSeconds
		{
			get { return _timerIntervalInSeconds; }
			set { _timerIntervalInSeconds = value; }
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
				settings.GroupName = configFile.Get("GroupName") == null
					? PluginSettings.DefaultGroupName
					: configFile.Get("GroupName").ToString();

				settings.TimerIntervalInSeconds = configFile.Get("TimerIntervalInSeconds") == null
					? PluginSettings.DefaultTimerIntervalInSeconds
					: int.Parse(configFile.Get("TimerIntervalInSeconds").ToString());
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

			config["GroupName"] = settings.GroupName;
			config["TimerIntervalInSeconds"] = settings.TimerIntervalInSeconds;
		}
	}
}

namespace TimedSigns.Services
{
	public sealed class SignUserInfo
	{
		public ulong UserId { get; set; }
		public long ExpirationDate { get; set; }
	}
}

namespace TimedSigns.Diagnostics
{
	public static class Diagnostics
	{
		public static void MessageToPlayer(BasePlayer player, string message, params object[] args)
		{
			const string format = "<color=orange>Знаки</color>:";
			player.SendConsoleCommand("chat.add", new object[] { 0, format + string.Format(message, args), 1f });
		}

		public static void MessageToServer(string message, params object[] args)
		{
			Interface.GetMod().RootLogger.Write(LogType.Info, message, args);
		}
	}
}
