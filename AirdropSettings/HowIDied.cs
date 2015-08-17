using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using Time = Oxide.Core.Libraries.Time;

namespace Oxide.Plugins
{
	[Info("HowIDied", "4seti [Lunatiq] for Rust Planet", "0.0.2", ResourceId = 989)]
	public class HowIDied : RustPlugin
	{
		#region Utility Methods

		private void Log(string message)
		{
			Puts("{0}: {1}", Title, message);
		}

		private void Warn(string message)
		{
			PrintWarning("{0}: {1}", Title, message);
		}

		private void Error(string message)
		{
			PrintError("{0}: {1}", Title, message);
		}

		// Gets a config value of a specific type
		private T GetConfig<T>(string name, T defaultValue)
		{
			if (Config[name] == null)
				return defaultValue;
			return (T) Convert.ChangeType(Config[name], typeof (T));
		}

		#endregion

		private void Loaded()
		{
			Log("Plugin is operating!");
		}

		// Loads the default configuration
		protected override void LoadDefaultConfig()
		{
			Log("Creating a new config file");
			Config.Clear();
			LoadVariables();
		}

		private void LoadVariables()
		{
			Config["messages"] = _defMsg;
			Config["version"] = Version;
			Config["SaveTimer"] = _saveTimer;
			Config["RemoveHours"] = _removeHours;
			Config["MinimumDistance"] = _minimumDistance;
		}

		private void Init()
		{
			try
			{
				LoadConfig();
				var version = GetConfig<Dictionary<string, object>>("version", null);
				var verNum = new VersionNumber(Convert.ToUInt16(version["Major"]), Convert.ToUInt16(version["Minor"]),
					Convert.ToUInt16(version["Patch"]));
				var cfgMessages = GetConfig<Dictionary<string, object>>("messages", null);
				_messages = new Dictionary<string, string>();
				if (cfgMessages != null)
					foreach (var pair in cfgMessages)
						_messages[pair.Key] = Convert.ToString(pair.Value);

				_eyesAdjust = new Vector3(0f, 1.5f, 0f);
				_removeHours = GetConfig("RemoveHours", 48);
				_saveTimer = GetConfig("SaveTimer", 600);
				_minimumDistance = GetConfig("MinimumDistance", 5f);
				if (verNum < Version || _defMsg.Count > _messages.Count)
				{
					foreach (var pair in _defMsg)
						if (!_messages.ContainsKey(pair.Key))
							_messages[pair.Key] = pair.Value;
				}
				LoadData();
				timer.Once(_saveTimer, SaveDataLoop);
			}
			catch (Exception ex)
			{
				Error("Init failed: " + ex.Message);
			}
		}

		private void LoadData()
		{
			try
			{
				_killLog = Interface.GetMod().DataFileSystem.ReadObject<List<KillData>>("hid-data");
				Log("Old data loaded!");
				TryClear();
			}
			catch
			{
				_killLog = new List<KillData>();
				Warn("New Data file initiated!");
				SaveData();
			}
		}

		private void TryClear(float remH = -1)
		{
			var removeList = _killLog.Where(item => (_time.GetUnixTimestamp() - item.TimeSpamp)/3600.0f >= (remH >= 0 ? remH : _removeHours)).ToList();
			if (removeList.Count > 0)
			{
				foreach (KillData item in removeList)
				{
					_killLog.Remove(item);
				}
				Warn(string.Format("Removed {0} old records", removeList.Count));
			}
		}

		private void Unload()
		{
			SaveData();
		}

		private void SaveDataLoop()
		{
			SaveData();
			timer.Once(_saveTimer, SaveDataLoop);
		}

		private void SaveData()
		{
			Interface.GetMod().DataFileSystem.WriteObject("hid-data", _killLog);
			Log("Data saved!");
		}

		[HookMethod("OnEntityDeath")]
		private void OnEntityDeath(BaseCombatEntity entity, HitInfo hitInfo)
		{
			if (entity == null || hitInfo == null) return;
			
			var killer = hitInfo.Initiator as BasePlayer;
			var victim = entity as BasePlayer;
			if (killer == null || victim == null) 
				return;

			if (!(GetDistance(hitInfo.HitPositionWorld, killer.transform.position) > _minimumDistance)) 
				return;

			var killData = new KillData(_time.GetUnixTimestamp(), DateTime.Now.ToString("d/M/yyyy HH:mm:ss"),
				killer.transform.position.x, killer.transform.position.y, killer.transform.position.z,
				hitInfo.HitPositionWorld.x, hitInfo.HitPositionWorld.y, hitInfo.HitPositionWorld.z,
				killer.userID, victim.userID, killer.displayName, victim.displayName, hitInfo.isHeadshot);

			_killLog.Add(killData);
		}

		[ConsoleCommand("hidsave")]
		private void cmdSaveA(ConsoleSystem.Arg arg)
		{
			SaveData();
		}

		[ChatCommand("hidsave")]
		private void cmdSave(BasePlayer player, string cmd, string[] args)
		{
			if (player.net.connection.authLevel == 0) return;
			SaveData();
			ReplyChat(player, "Data Saved!");
		}

		[ChatCommand("hidclear")]
		private void cmdClear(BasePlayer player, string cmd, string[] args)
		{
			if (player.net.connection.authLevel == 0) return;
			int oldAmount = _killLog.Count;
			float remH = -1;
			if (args.Length > 0) float.TryParse(args[0], out remH);
			TryClear(remH);
			ReplyChat(player,
				string.Format(_messages["RemovedRows"], (oldAmount - _killLog.Count), (remH >= 0 ? remH : _removeHours),
					_killLog.Count));
		}

		[ChatCommand("hid")]
		private void cmdHid(BasePlayer player, string cmd, string[] args)
		{
			if (player.net.connection.authLevel == 0) return;

			float rad = 5f;
			string name = string.Empty;
			string mode = string.Empty;
			if (args.Length > 0) float.TryParse(args[0], out rad);
			if (args.Length > 1) name = args[1];
			if (args.Length > 2) mode = args[2];
			var tempList = new List<KillData>();
			if (name == string.Empty)
				tempList =
					_killLog.Where(
						x =>
							GetDistance(x.KillerV3(), player.transform.position) < rad ||
							GetDistance(x.VictimV3(), player.transform.position) < rad).ToList();
			else
			{
				if (mode == string.Empty)
					tempList =
						_killLog.OrderByDescending(o => o.TimeSpamp)
							.Where(
								x =>
									(GetDistance(x.KillerV3(), player.transform.position) < rad ||
									 GetDistance(x.VictimV3(), player.transform.position) < rad) && x.CheckNameVictim(name))
							.Take(7)
							.ToList();
				else if (mode == "k")
					tempList =
						_killLog.OrderByDescending(o => o.TimeSpamp)
							.Where(
								x =>
									(GetDistance(x.KillerV3(), player.transform.position) < rad ||
									 GetDistance(x.VictimV3(), player.transform.position) < rad) && x.CheckNameKiller(name))
							.Take(7)
							.ToList();
			}

			if (tempList.Count == 0)
			{
				ReplyChat(player, _messages["NoData"]);
			}
			else if (tempList.Count == 1)
			{
				ShowKillInfo(player, tempList.First(), true);
			}
			else if (tempList.Count > 1 && tempList.Count < 8)
			{
				ReplyChat(player, _messages["KillList"]);
				int i = 0;
				foreach (KillData kill in tempList)
				{
					ReplyChat(player, string.Format(_messages["KillIndex"], i, kill.Date, kill.killer_Name, kill.victim_Name));
					i++;
				}
				_localKillList[player.userID] = tempList;
			}
			else
			{
				ReplyChat(player, _messages["TooMuch"]);
			}
		}

		[ChatCommand("hidall")]
		private void cmdHidAll(BasePlayer player, string cmd, string[] args)
		{
			if (player.net.connection.authLevel == 0) return;

			float rad = 5f;
			string name = string.Empty;
			string mode = string.Empty;
			if (args.Length > 0) float.TryParse(args[0], out rad);
			if (args.Length > 1) name = args[1];
			if (args.Length > 2) mode = args[2];
			var tempList = new List<KillData>();
			if (name == string.Empty)
				tempList =
					_killLog.Where(
						x =>
							GetDistance(x.KillerV3(), player.transform.position) < rad ||
							GetDistance(x.VictimV3(), player.transform.position) < rad).ToList();
			else
			{
				if (mode == string.Empty)
					tempList =
						_killLog.OrderByDescending(o => o.TimeSpamp)
							.Where(
								x =>
									(GetDistance(x.KillerV3(), player.transform.position) < rad ||
									 GetDistance(x.VictimV3(), player.transform.position) < rad) && x.CheckNameVictim(name))
							.Take(30)
							.ToList();
				else if (mode == "k")
					tempList =
						_killLog.OrderByDescending(o => o.TimeSpamp)
							.Where(
								x =>
									(GetDistance(x.KillerV3(), player.transform.position) < rad ||
									 GetDistance(x.VictimV3(), player.transform.position) < rad) && x.CheckNameKiller(name))
							.Take(30)
							.ToList();
			}

			if (tempList.Count == 0)
			{
				ReplyChat(player, _messages["NoData"]);
			}
			else if (tempList.Count > 0 && tempList.Count <= 30)
			{
				foreach (KillData kill in tempList)
				{
					ShowKillInfo(player, kill, false);
				}
			}
			else
			{
				ReplyChat(player, _messages["TooMuch"]);
			}
		}

		[ChatCommand("hidshow")]
		private void cmdHidShow(BasePlayer player, string cmd, string[] args)
		{
			if (player.net.connection.authLevel == 0) return;

			var rad = 5f;
			var name = string.Empty;

			if (args.Length == 0) 
				return;

			var pList = FindPlayerByName(args[0]);
			if (pList.Count > 1)
			{
				ReplyChat(player, _messages["MatchOverflow"]);
				return;
			}
			if (pList.Count == 0)
			{
				ReplyChat(player, _messages["MatchNoone"]);
				return;
			}

			var target = pList.First();
			if (args.Length > 1) 
				float.TryParse(args[1], out rad);
			if (args.Length > 2) 
				name = args[2];

			List<KillData> tempList;
			if (name == string.Empty)
				tempList =
					_killLog.Where(
						x =>
							GetDistance(x.KillerV3(), player.transform.position) < rad ||
							GetDistance(x.VictimV3(), player.transform.position) < rad).ToList();
			else
				tempList =
					_killLog.OrderByDescending(o => o.TimeSpamp)
						.Where(
							x =>
								(GetDistance(x.KillerV3(), player.transform.position) < rad ||
								 GetDistance(x.VictimV3(), player.transform.position) < rad) && x.CheckNameVictim(name))
						.Take(7)
						.ToList();

			if (tempList.Count == 0)
			{
				ReplyChat(player, _messages["NoData"]);
			}
			else if (tempList.Count == 1)
			{
				ShowKillInfo(player, tempList.First(), true);
				ReplyChat(player, string.Format(_messages["DataShow"], target.displayName));
				ShowKillInfo(target, tempList.First(), true);
			}
			else if (tempList.Count > 1 && tempList.Count < 8)
			{
				ReplyChat(player, _messages["KillList"]);
				int i = 0;
				foreach (KillData kill in tempList)
				{
					ReplyChat(player, string.Format(_messages["KillIndex"], i, kill.Date, kill.killer_Name, kill.victim_Name));
					i++;
				}
				_localKillList[player.userID] = tempList;
			}
			else
			{
				ReplyChat(player, _messages["TooMuch"]);
			}
		}

		[ChatCommand("hidshowall")]
		private void cmdHidShowAll(BasePlayer player, string cmd, string[] args)
		{
			if (player.net.connection.authLevel == 0) return;

			float rad = 5f;
			string name = string.Empty;
			if (args.Length == 0) return;
			List<BasePlayer> pList = FindPlayerByName(args[0]);
			if (pList.Count > 1)
			{
				ReplyChat(player, _messages["MatchOverflow"]);
				return;
			}
			if (pList.Count == 0)
			{
				ReplyChat(player, _messages["MatchNoone"]);
				return;
			}
			var target = pList.First();
			if (args.Length > 1) float.TryParse(args[1], out rad);
			if (args.Length > 2) name = args[2];
			List<KillData> tempList;
			if (name == string.Empty)
				tempList =
					_killLog.Where(
						x =>
							GetDistance(x.KillerV3(), player.transform.position) < rad ||
							GetDistance(x.VictimV3(), player.transform.position) < rad).ToList();
			else
				tempList =
					_killLog.OrderByDescending(o => o.TimeSpamp)
						.Where(
							x =>
								(GetDistance(x.KillerV3(), player.transform.position) < rad ||
								 GetDistance(x.VictimV3(), player.transform.position) < rad) && x.CheckNameVictim(name))
						.Take(7)
						.ToList();

			if (tempList.Count == 0)
			{
				ReplyChat(player, _messages["NoData"]);
			}
			else if (tempList.Count > 0 && tempList.Count < 30)
			{
				foreach (KillData kill in tempList)
				{
					ShowKillInfo(player, kill, false, 60);
					ShowKillInfo(target, kill, false, 60);
				}
			}
			else
			{
				ReplyChat(player, _messages["TooMuch"]);
			}
		}

		private List<BasePlayer> FindPlayerByName(string playerName = "")
		{
			// Check if a player name was supplied.
			if (playerName == "") return null;

			// Set the player name to lowercase to be able to search case insensitive.
			playerName = playerName.ToLower();

			// Setup some variables to save the matching BasePlayers with that partial
			// name.
			var matches = new List<BasePlayer>();

			// Iterate through the online player list and check for a match.
			foreach (BasePlayer player in BasePlayer.activePlayerList)
			{
				// Get the player his/her display name and set it to lowercase.
				string displayName = player.displayName.ToLower();

				// Look for a match.
				if (displayName.Contains(playerName))
				{
					matches.Add(player);
				}
			}

			// Return all the matching players.
			return matches;
		}

		private void ShowKillInfo(BasePlayer player, KillData killData, bool single, float drawTimeCustom = 0)
		{
			Vector3 killer = killData.KillerV3();
			Vector3 victim = killData.VictimV3();
			if (single)
				ReplyChat(player,
					string.Format(_messages["KillEntry"], killData.killer_Name, killData.killerID, killData.victim_Name,
						killData.victimID, GetDistance(killData.VictimV3(), killData.KillerV3()),
						(killData.HeadShot ? "HeadShot" : "Normal Hit"), killData.Date));
			if (killer != Vector3.zero && victim != Vector3.zero)
			{
				string head = killData.HeadShot ? "[H]" : string.Empty;
				if (killData.HeadShot)
					player.SendConsoleCommand("ddraw.arrow",
						new object[] {drawTimeCustom > 0 ? drawTimeCustom : DrawTime, Color.red, killer + _eyesAdjust, victim, 1f});
				else
					player.SendConsoleCommand("ddraw.arrow",
						new object[] {drawTimeCustom > 0 ? drawTimeCustom : DrawTime, Color.green, killer + _eyesAdjust, victim, 1f});

				player.SendConsoleCommand("ddraw.text", drawTimeCustom > 0 ? drawTimeCustom : DrawTime, Color.white,
					killer + _eyesAdjust, "K:[{kill_data.killer_Name}] -> V:[{kill_data.victim_Name}] ({GetDistance(kill_data.VictimV3(), kill_data.KillerV3()).ToString(\"N2\")}m) {head}");
			}
		}

		[ChatCommand("hidlist")]
		private void cmdList(BasePlayer player, string cmd, string[] args)
		{
			if (player.net.connection.authLevel == 0) return;
			if (args.Length == 0) return;
			int listIndx = 0;
			BasePlayer target = null;
			int.TryParse(args[0], out listIndx);
			if (args.Length > 1)
			{
				List<BasePlayer> pList = FindPlayerByName(args[1]);
				if (pList.Count > 1)
				{
					ReplyChat(player, _messages["MatchOverflow"]);
				}
				else if (pList.Count == 0)
				{
					ReplyChat(player, _messages["MatchNoone"]);
				}
				else
				{
					target = pList.First();
					ReplyChat(player, string.Format(_messages["DataShow"], target.displayName));
				}
			}

			if (_localKillList.ContainsKey(player.userID))
			{
				if (listIndx < _localKillList[player.userID].Count && listIndx >= 0)
				{
					ReplyChat(player, "Showind ID: " + listIndx);
					ShowKillInfo(player, _localKillList[player.userID][listIndx], true);
					if (target != null)
						ShowKillInfo(target, _localKillList[player.userID][listIndx], true);
				}
				else
				{
					ShowKillInfo(player, _localKillList[player.userID].Last(), true);
					if (target != null)
						ShowKillInfo(target, _localKillList[player.userID].Last(), true);
				}
			}
		}

		private float GetDistance(Vector3 v3, float x, float y, float z)
		{
			var distance = (float) Math.Pow(Math.Pow(v3.x - x, 2) + Math.Pow(v3.y - y, 2), 0.5);
			distance = (float) Math.Pow(Math.Pow(distance, 2) + Math.Pow(v3.z - z, 2), 0.5);

			return distance;
		}

		private float GetDistance(Vector3 v3_1, Vector3 v3_2)
		{
			var distance = (float) Math.Pow(Math.Pow(v3_1.x - v3_2.x, 2) + Math.Pow(v3_1.y - v3_2.y, 2), 0.5);
			distance = (float) Math.Pow(Math.Pow(distance, 2) + Math.Pow(v3_1.z - v3_2.z, 2), 0.5);

			return distance;
		}

		private void ReplyChat(BasePlayer player, string msg)
		{
			player.ChatMessage(string.Format("<color=#81D600>{0}</color>: {1}", Title, msg));
		}

		#region VARS

		private const float DrawTime = 30f;

		private readonly Dictionary<string, string> _defMsg = new Dictionary<string, string>
		{
			{"NoData", "No KillData found!"},
			{"RemovedRows", "Removed {0} rows older than {1} hours, {2} rows total"},
			{"KillList", "Next KillInfo avaliable please type /hidlist with selected index"},
			{
				"KillEntry",
				"<color=#4F9BFF>{0}</color><color=#F5D400>[{1}]</color> killed <color=#4F9BFF>{2}</color><color=#F5D400>[{3}]</color>, with distance <color=#F5D400>{4}</color>, with <i>{5}</i>, at <color=#F80>{6}</color>"
			},
			{
				"KillIndex",
				"<color=#F5D400>[{0}]</color><color=#F80>({1})</color> <color=#4F9BFF>{2}</color> killed <color=#4F9BFF>{3}</color>"
			},
			{"TooMuch", "<color=#F00>Too much data found, please use lower radius or use /hid RADIUS Victim_NAME</color>"},
			{"MatchOverflow", "More than one match!"},
			{"MatchNoone", "No players with that name found!"},
			{"DataShow", "Showing KillData to {0}!"}
		};

		private readonly Dictionary<ulong, List<KillData>> _localKillList = new Dictionary<ulong, List<KillData>>();
		private readonly Time _time = new Time();
		private Vector3 _eyesAdjust;
		private List<KillData> _killLog = new List<KillData>();
		private Dictionary<string, string> _messages;
		private float _minimumDistance = 3f;
		private int _removeHours = 48;
		private int _saveTimer = 600;

		#endregion

		public class KillData
		{
			public string Date;
			public bool HeadShot;
			public uint TimeSpamp;
			public ulong killerID;
			public string killer_Name;
			public float killer_X = 0, killer_Y = 0, killer_Z = 0;
			public ulong victimID;
			public string victim_Name;
			public float victim_X = 0, victim_Y = 0, victim_Z = 0;

			public KillData()
			{
				TimeSpamp = 0;
				killer_X = 0;
				killer_Y = 0;
				killer_Z = 0;
				victim_X = 0;
				victim_Y = 0;
				victim_Z = 0;
				Date = string.Empty;
				killer_Name = string.Empty;
				victim_Name = string.Empty;
				killerID = 0;
				victimID = 0;
				HeadShot = false;
			}

			public KillData(uint stamp, string date, float kx, float ky, float kz, float vx, float vy, float vz,
				ulong killerid, ulong victimid, string killername, string victimname, bool headshot)
			{
				TimeSpamp = stamp;
				killer_X = kx;
				killer_Y = ky;
				killer_Z = kz;
				victim_X = vx;
				victim_Y = vy;
				victim_Z = vz;
				Date = date;
				killer_Name = string.Empty;
				victim_Name = string.Empty;
				killerID = 0;
				victimID = 0;
				killer_Name = killername;
				victim_Name = victimname;
				killerID = killerid;
				victimID = victimid;
				HeadShot = headshot;
			}

			public Vector3 KillerV3()
			{
				return new Vector3(killer_X, killer_Y, killer_Z);
			}

			public Vector3 VictimV3()
			{
				return new Vector3(victim_X, victim_Y, victim_Z);
			}

			public bool CheckNameVictim(string name)
			{
				return victim_Name.ToLower().Contains(name.ToLower());
			}

			public bool CheckNameKiller(string name)
			{
				return killer_Name.ToLower().Contains(name.ToLower());
			}

			public bool CheckName(string name)
			{
				return victim_Name.ToLower().Contains(name.ToLower()) || killer_Name.ToLower().Contains(name.ToLower());
			}
		}

		public class Tuple<T, U>
		{
			public Tuple(T item1, U item2)
			{
				Item1 = item1;
				Item2 = item2;
			}

			public T Item1 { get; set; }
			public U Item2 { get; set; }
		}
	}
}