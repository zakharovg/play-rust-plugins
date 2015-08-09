//Reference: NLua

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EconomicsVip.Diagnostics;
using EconomicsVip.Services;
using EconomicsVip.Settings;
using NLua;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Logging;
using Oxide.Core.Plugins;
using Lua = KeraLua.Lua;

namespace Oxide.Plugins
{
	[Info("EconomicsVip", "baton", 0.1, ResourceId = 715)]
	[Description("vip for economics")]
	public sealed class EconomicsVip : RustPlugin
	{
		private static PluginSettings _settings;
		private Plugin _economicsPlugin;
		private static List<VipUserInfo> _vipUserList;

		void OnServerInitialized()
		{
			LoadConfig();
			_settings = PluginSettingsRepository.Load(Config);

			_economicsPlugin = plugins.Find("00-Economics");
			_vipUserList = Interface.Oxide.DataFileSystem.ReadObject<List<VipUserInfo>>("vipuserlist");
			timer.Every(_settings.CheckVipTimerIntervalInSeconds, CheckUserList);

			PluginSettingsRepository.Save(_settings, Config);
			SaveConfig();
		}

		private void CheckUserList()
		{
			var now = DateTime.Now;

			var usersToRemove = new List<VipUserInfo>();
			foreach (var vipUserInfo in _vipUserList)
			{
				var dateTime = DateTime.Parse(vipUserInfo.ExpirationDateString);
				if(dateTime <= now)
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

		[ChatCommand("vip")]
		private void VipCommand(BasePlayer player, string command, string[] args)
		{
			if (player == null)
				return;

			if (!permission.GroupExists(_settings.VipGroupName))
				permission.CreateGroup(_settings.VipGroupName, _settings.VipGroupTitle, _settings.VipGroupRank);

			var userInfo = _vipUserList.FirstOrDefault(u => u.UserId == player.userID);
			if (userInfo != null)
			{
				var dateTime = DateTime.Parse(userInfo.ExpirationDateString);
				Diagnostics.MessageToPlayer(player, "Your vip status expires at {0}", dateTime);
				return;
			}

			var balance = GetBalance(player.userID);
			if (balance < _settings.RequiredBalance)
			{
				Diagnostics.MessageToPlayer(player, "You do not have enough money: {0}.", _settings.RequiredBalance);
				return;
			}

			var uid = Convert.ToString(player.userID);
			permission.AddUserGroup(uid, _settings.VipGroupName);

			_vipUserList.Add(new VipUserInfo { ExpirationDateString = DateTime.Now.AddSeconds(_settings.VipDurationInSeconds).ToString(), UserId = player.userID });
			Interface.Oxide.DataFileSystem.WriteObject("vipuserlist", _vipUserList);
			Diagnostics.MessageToPlayer(player, "You have become a vip!");
		}

		private double GetBalance(ulong uid)
		{
			var balance = -1.0d;
			var result = (LuaTable)_economicsPlugin.CallHook("GetEconomyAPI");
			foreach (var key in result.Keys)
			{
				if (key == null)
					continue;

				if (!key.ToString().Equals("GetUserData", StringComparison.OrdinalIgnoreCase))
					continue;

				var luaFunction = (LuaFunction)result[key];
				var luaState = typeof (LuaFunction).GetField("_Interpreter",
					BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				Diagnostics.MessageToServer("lua state field:{0}", luaState == null);
				var userdata = (LuaTable)luaFunction.Call(_economicsPlugin, uid.ToString())[0];

				foreach (var k in userdata.Keys)
				{
					Diagnostics.MessageToServer("userdata has key:{0}", k.GetType());
					//Lua.LuaGetMetatable()
				}
				Diagnostics.MessageToServer("userdata:{0}", userdata);
				return 0;
				//var balanceEnumerator = userdata.Values.GetEnumerator();
				//if (!balanceEnumerator.MoveNext() || balanceEnumerator.Current == null)
				//	balance = -1.0d;
				//else
				//{
				//	try
				//	{
				//		balance = (double)balanceEnumerator.Current;
				//	}
				//	catch (Exception)
				//	{
				//		balance = -1.0d;
				//	}
				//}
				//break;
			}
			return balance;
		}
	}
}

namespace EconomicsVip.Settings
{
	public sealed class PluginSettings
	{
		public const string DefaultGroupName = "vip";
		public const int DefaultVipDurationInSeconds = 60*60*24*30;
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
