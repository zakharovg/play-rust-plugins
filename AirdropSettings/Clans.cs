// Reference: Newtonsoft.Json

using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Linq;
using Oxide.Core.Libraries.Covalence;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("Clans", "playrust.io / dcode", "1.5.5", ResourceId = 842)]
	public class Clans : RustPlugin
	{

		#region Rust:IO Bindings

		private Library lib;
		private MethodInfo isInstalled;
		private MethodInfo hasFriend;
		private MethodInfo addFriend;
		private MethodInfo deleteFriend;
		internal static Clans Instance = null;

		FieldInfo displayName = typeof(BasePlayer).GetField("_displayName", (BindingFlags.Instance | BindingFlags.NonPublic));

		private void InitializeRustIO()
		{
			lib = Interface.GetMod().GetLibrary<Library>("RustIO");
			if (lib == null || (isInstalled = lib.GetFunction("IsInstalled")) == null || (hasFriend = lib.GetFunction("HasFriend")) == null || (addFriend = lib.GetFunction("AddFriend")) == null || (deleteFriend = lib.GetFunction("DeleteFriend")) == null)
			{
				lib = null;
				Puts("{0}: {1}", Title, "Rust:IO is not present. You need to install Rust:IO first in order to use this plugin!");
			}
		}

		private bool IsInstalled()
		{
			if (lib == null) return false;
			return (bool)isInstalled.Invoke(lib, new object[] { });
		}

		private bool HasFriend(string playerId, string friendId)
		{
			if (lib == null) return false;
			return (bool)hasFriend.Invoke(lib, new object[] { playerId, friendId });
		}

		private bool AddFriend(string playerId, string friendId)
		{
			if (lib == null) return false;
			return (bool)addFriend.Invoke(lib, new object[] { playerId, friendId });
		}

		private bool DeleteFriend(string playerId, string friendId)
		{
			if (lib == null) return false;
			return (bool)deleteFriend.Invoke(lib, new object[] { playerId, friendId });
		}

		#endregion

		private Dictionary<string, Clan> clans = new Dictionary<string, Clan>();
		private Dictionary<string, string> originalNames = new Dictionary<string, string>();
		private Regex tagRe = new Regex("^[a-zA-Zа-яА-Я0-9]{2,6}$");
		private Dictionary<string, string> messages = new Dictionary<string, string>();
		private Dictionary<string, Clan> lookup = new Dictionary<string, Clan>();
		private bool addClanMatesAsFriends = true;
		private int limitMembers = -1;
		private int limitModerators = -1;

		// Loads the data file
		private void LoadData()
		{
			clans.Clear();
			var data = Interface.GetMod().DataFileSystem.GetDatafile("rustio_clans");
			if (data["clans"] != null)
			{
				var clansData = (Dictionary<string, object>)Convert.ChangeType(data["clans"], typeof(Dictionary<string, object>));
				foreach (var iclan in clansData)
				{
					string tag = iclan.Key;
					var clanData = iclan.Value as Dictionary<string, object>;
					string description = (string)clanData["description"];
					string owner = (string)clanData["owner"];
					List<string> moderators = new List<string>();
					foreach (var imoderator in clanData["moderators"] as List<object>)
					{
						moderators.Add((string)imoderator);
					}
					List<string> members = new List<string>();
					foreach (var imember in clanData["members"] as List<object>)
					{
						members.Add((string)imember);
					}
					List<string> invited = new List<string>();
					foreach (var iinvited in clanData["invited"] as List<object>)
					{
						invited.Add((string)iinvited);
					}
					Clan clan;
					clans.Add(tag, clan = new Clan()
					{
						tag = tag,
						description = description,
						owner = owner,
						moderators = moderators,
						members = members,
						invited = invited
					});
					lookup[owner] = clan;
					foreach (var member in members)
						lookup[member] = clan;
				}
			}
		}

		// Saves the data file
		private void SaveData()
		{
			var data = Interface.GetMod().DataFileSystem.GetDatafile("rustio_clans");
			var clansData = new Dictionary<string, object>();
			foreach (var clan in clans)
			{
				var clanData = new Dictionary<string, object>();
				clanData.Add("tag", clan.Value.tag);
				clanData.Add("description", clan.Value.description);
				clanData.Add("owner", clan.Value.owner);
				var moderators = new List<object>();
				foreach (var imoderator in clan.Value.moderators)
					moderators.Add(imoderator);
				var members = new List<object>();
				foreach (var imember in clan.Value.members)
					members.Add(imember);
				var invited = new List<object>();
				foreach (var iinvited in clan.Value.invited)
					invited.Add(iinvited);
				clanData.Add("moderators", moderators);
				clanData.Add("members", members);
				clanData.Add("invited", invited);
				clansData.Add(clan.Value.tag, clanData);
			}
			data["clans"] = clansData;
			Interface.GetMod().DataFileSystem.SaveDatafile("rustio_clans");
		}

		// A list of all translateable texts
		private List<string> texts = new List<string>() {
            "%NAME% has come online!",
            "%NAME% has gone offline.",

            "You are currently not a member of a clan.",
            "You are the owner of:",
            "You are a moderator of:",
            "You are a member of:",
            "Members online:",
            "Pending invites:",
            "To learn more about clans, type: <color=\"#ffd479\">/clan help</color>",

            "Usage: <color=\"#ffd479\">/clan create \"TAG\" \"Description\"</color>",
            "You are already a member of a clan.",
            "Clan tags must be 2 to 6 characters long and may contain standard letters and numbers only",
            "Please provide a short description of your clan.",
            "There is already a clan with this tag.",
            "You are now the owner of your new clan:",
            "To invite new members, type: <color=\"#ffd479\">/clan invite \"Player name\"</color>",

            "Usage: <color=\"#ffd479\">/clan invite \"Player name\"</color>",
            "You need to be a moderator of your clan to use this command.",
            "No such player or player name not unique:",
            "This player is already a member of your clan:",
            "This player is not a member of your clan:",
            "This player has already been invited to your clan:",
            "This player is already a moderator of your clan:",
            "This player is not a moderator of your clan:",
            "%MEMBER% invited %PLAYER% to the clan.",
            "Usage: <color=\"#ffd479\">/clan join \"TAG\"</color>",
            "You have not been invited to join this clan.",
            "%NAME% has joined the clan!",
            "You have been invited to join the clan:",
            "To join, type: <color=#ffd479>/clan join \"%TAG%\"</color>",

            "Usage: <color=\"#ffd479\">/clan promote \"Player name\"</color>",
            "You need to be the owner of your clan to use this command.",
            "%OWNER% promoted %MEMBER% to moderator.",

            "Usage: <color=\"#ffd479\">/clan demote \"Player name\"</color>",

            "Usage: <color=\"#ffd479\">/clan leave</color>",
            "You have left your current clan.",
            "%NAME% has left the clan.",

            "Usage: <color=\"#ffd479\">/clan kick \"Player name\"</color>",
            "This player is an owner or moderator and cannot be kicked:",
            "%NAME% kicked %MEMBER% from the clan.",

            "Usage: <color=\"#ffd479\">/clan disband forever</color>",
            "Your current clan has been disbanded forever.",

            "Usage: <color=\"#ffd479\">/clan delete \"TAG\"</color>",
            "You need to be a server owner to delete clans.",
            "There is no clan with that tag:",
            "Your clan has been deleted by the server owner.",
            "You have deleted the clan:",

            "Available commands:",
            "<color=#ffd479>/clan</color> - Displays relevant information about your current clan",
            "<color=#ffd479>/c Message...</color> - Sends a message to all online clan members",
            "<color=#ffd479>/clan create \"TAG\" \"Description\"</color> - Creates a new clan you own",
            "<color=#ffd479>/clan join \"TAG\"</color> - Joins a clan you have been invited to",
            "<color=#ffd479>/clan leave</color> - Leaves your current clan",
            "<color=#74c6ff>Moderator</color> commands:",
            "<color=#ffd479>/clan invite \"Player name\"</color> - Invites a player to your clan",
            "<color=#ffd479>/clan kick \"Player name\"</color> - Kicks a member from your clan",
            "<color=#a1ff46>Owner</color> commands:",
            "<color=#ffd479>/clan promote \"Name\"</color> - Promotes a member to moderator",
            "<color=#ffd479>/clan demote \"Name\"</color> - Demotes a moderator to member",
            "<color=#ffd479>/clan disband forever</color> - Disbands your clan (no undo)",
            "<color=#cd422b>Server owner</color> commands:",
            "<color=#ffd479>/clan delete \"TAG\"</color> - Deletes a clan (no undo)",

            "<color=\"#ffd479\">/clan</color> - Displays your current clan status",
            "<color=\"#ffd479\">/clan help</color> - Learn how to create or join a clan"
        };

		// Loads the default configuration
		protected override void LoadDefaultConfig()
		{
			var messages = new Dictionary<string, object>();
			foreach (var text in texts)
			{
				if (messages.ContainsKey(text))
					Puts("{0}: {1}", Title, "Duplicate translation string: " + text);
				else
					messages.Add(text, text);
			}
			Config["messages"] = messages;
			Config.Set("addClanMatesAsFriends", true);
			Config.Set("limit", "members", -1);
			Config.Set("limit", "moderators", -1);
		}

		// Translates a string
		private string _(string text, Dictionary<string, string> replacements = null)
		{
			if (messages.ContainsKey(text) && messages[text] != null)
				text = messages[text];
			if (replacements != null)
				foreach (var replacement in replacements)
					text = text.Replace("%" + replacement.Key + "%", replacement.Value);
			return text;
		}

		// Finds a clan by tag
		private Clan FindClan(string tag)
		{
			Clan clan;
			if (clans.TryGetValue(tag, out clan))
				return clan;
			return null;
		}

		// Finds a user's clan
		private Clan FindClanByUser(string userId)
		{
			Clan clan;
			if (lookup.TryGetValue(userId, out clan))
				return clan;
			return null;
		}

		// Finds a player by partial name
		private BasePlayer FindPlayerByPartialName(string name)
		{
			if (string.IsNullOrEmpty(name))
				return null;
			BasePlayer player = null;
			name = name.ToLower();
			var allPlayers = BasePlayer.activePlayerList.ToArray();
			// Try to find an exact match first
			foreach (var p in allPlayers)
			{
				if (p.displayName == name)
				{
					if (player != null)
						return null; // Not unique
					player = p;
				}
			}
			if (player != null)
				return player;
			// Otherwise try to find a partial match
			foreach (var p in allPlayers)
			{
				if (p.displayName.ToLower().IndexOf(name) >= 0)
				{
					if (player != null)
						return null; // Not unique
					player = p;
				}
			}
			return player;
		}

		// Strips the tag from a player's name
		private string StripTag(string name, Clan clan)
		{
			if (clan == null)
				return name;
			var re = new Regex(@"^\[" + clan.tag + @"\]\s");
			while (re.IsMatch(name))
				name = name.Substring(clan.tag.Length + 3);
			return name;
		}


		private void SetupPlayer(BasePlayer player)
		{
			var prevName = player.displayName;
			var playerId = player.userID.ToString();
			var clan = FindClanByUser(playerId);
			displayName.SetValue(player, StripTag(player.displayName, clan));
			string originalName = null;
			if (!originalNames.ContainsKey(playerId))
			{
				originalNames.Add(playerId, originalName = player.displayName);
			}
			else
			{
				originalName = originalNames[playerId];
			}
			if (clan == null)
			{
				displayName.SetValue(player, originalName);
			}
			else
			{
				var tag = "[" + clan.tag + "]" + " ";
				if (!player.displayName.StartsWith(tag))
					displayName.SetValue(player, tag + originalName);
			}
			if (player.displayName != prevName)
			{
				var bp = BasePlayer.FindByID(player.userID);
				if (bp != null)
					bp.SendNetworkUpdate();
			}
		}
		// Sets up a player to use the correct clan tag
		private void SetupPlayer(IPlayer player)
		{
			var prevName = player.Nickname;
			var playerId = player.UniqueID;
			var clan = FindClanByUser(playerId);
			displayName.SetValue(player, StripTag(player.Nickname, clan));
			string originalName = null;
			if (!originalNames.ContainsKey(playerId))
			{
				originalNames.Add(playerId, originalName = player.Nickname);
			}
			else
			{
				originalName = originalNames[playerId];
			}
			if (clan == null)
			{
				displayName.SetValue(player, originalName);
			}
			else
			{
				var tag = "[" + clan.tag + "]" + " ";
				if (!player.Nickname.StartsWith(tag))
					displayName.SetValue(player, tag + originalName);
			}
			if (player.Nickname != prevName)
			{
				var bp = BasePlayer.FindByID(ulong.Parse(player.UniqueID));
				if (bp != null)
					bp.SendNetworkUpdate();
			}
		}

		// Sets up all players contained in playerIds
		private void SetupPlayers(List<string> playerIds)
		{
			foreach (var playerId in playerIds)
			{
				var uid = Convert.ToUInt64(playerId);
				var player = BasePlayer.FindByID(uid);
				if (player != null)
					SetupPlayer(player);
				else
				{
					player = BasePlayer.FindSleeping(uid);
					if (player != null)
						SetupPlayer(player);
				}
			}
		}

		[HookMethod("Init")]
		void Init()
		{
			Instance = this;
		}

		[HookMethod("OnServerInitialized")]
		void OnServerInitialized()
		{
			try
			{
				InitializeRustIO();
				LoadConfig();
				try
				{
					var customMessages = Config.Get<Dictionary<string, object>>("messages");
					if (customMessages != null)
						foreach (var pair in customMessages)
							messages[pair.Key] = (string)pair.Value;
					LoadData();
				}
				catch (Exception ex2)
				{
					Warn("oxide/config/Clans.json seems to contain an invalid 'messages' structure. Please delete the config file once and reload the plugin.");
				}
				foreach (var player in BasePlayer.activePlayerList)
					SetupPlayer(player);
				foreach (var player in BasePlayer.sleepingPlayerList)
					SetupPlayer(player);
				try { addClanMatesAsFriends = Config.Get<bool>("addClanMatesAsFriends"); }
				catch { }
				try { limitMembers = Config.Get<int>("limits", "members"); }
				catch { }
				try { limitModerators = Config.Get<int>("limits", "moderators"); }
				catch { }
			}
			catch (Exception ex)
			{
				Error("OnServerInitialized failed", ex);
			}
		}

		[HookMethod("OnUserApprove")]
		void OnUserApprove(Network.Connection connection)
		{
			// Override whatever there is
			originalNames[connection.userid.ToString()] = connection.username;
		}

		[HookMethod("OnPlayerInit")]
		void OnPlayerInit(BasePlayer player)
		{
			string originalName;
			if (originalNames.TryGetValue(player.userID.ToString(), out originalName))
				displayName.SetValue(player, originalName);
			try
			{
				SetupPlayer(player);
				var clan = FindClanByUser(player.userID.ToString());
				if (clan != null)
					clan.Broadcast(_("%NAME% has come online!", new Dictionary<string, string>() { { "NAME", StripTag(player.displayName, clan) } }));
			}
			catch (Exception ex)
			{
				Error("OnPlayerInit failed", ex);
			}
		}

		[HookMethod("OnPlayerDisconnected")]
		void OnPlayerDisconnected(BasePlayer player)
		{
			try
			{
				var clan = FindClanByUser(player.userID.ToString());
				if (clan != null)
					clan.Broadcast(_("%NAME% has gone offline.", new Dictionary<string, string>() { { "NAME", StripTag(player.displayName, clan) } }));
			}
			catch (Exception ex)
			{
				Error("OnPlayerDisconnected failed", ex);
			}
		}

		[HookMethod("Unload")]
		void OnUnload()
		{
			try
			{
				// Reset player names to originals
				foreach (var pair in originalNames)
				{
					var playerId = Convert.ToUInt64(pair.Key);
					var player = BasePlayer.FindByID(playerId);
					if (player != null)
						displayName.SetValue(player, pair.Value);
					else
					{
						player = BasePlayer.FindSleeping(playerId);
						if (player != null)
							displayName.SetValue(player, pair.Value);
					}
				}
			}
			catch (Exception ex)
			{
				Error("Unload failed", ex);
			}
		}

		[HookMethod("SendHelpText")]
		private void SendHelpText(BasePlayer player)
		{
			var sb = new StringBuilder()
			   .Append("<size=18>Clans</size> by <color=#ce422b>http://playrust.io</color>\n")
			   .Append("  ").Append(_("<color=\"#ffd479\">/clan</color> - Displays your current clan status")).Append("\n")
			   .Append("  ").Append(_("<color=\"#ffd479\">/clan help</color> - Learn how to create or join a clan"));
			player.ChatMessage(sb.ToString());
		}

		[HookMethod("BuildServerTags")]
		private void BuildServerTags(IList<string> taglist)
		{
			taglist.Add("clans");
		}

		[ChatCommand("clan")]
		private void cmdChatClan(BasePlayer player, string command, string[] args)
		{
			var userId = player.userID.ToString();
			var myClan = FindClanByUser(userId);
			var sb = new StringBuilder();
			// No arguments: List clans and get help how to create one
			if (args.Length == 0)
			{
				sb.Append("<size=22>Банды</size>\n");
				if (myClan == null)
				{
					sb.Append(_("You are currently not a member of a clan.")).Append("\n");
				}
				else
				{
					if (myClan.IsOwner(userId))
					{
						sb.Append(_("You are the owner of:"));
					}
					else if (myClan.IsModerator(userId))
						sb.Append(_("You are a moderator of:"));
					else
						sb.Append(_("You are a member of:"));
					sb.Append(" [").Append(myClan.tag).Append("] ").Append(myClan.description).Append("\n");
					sb.Append(_("Members online:")).Append(" ");
					List<string> onlineMembers = new List<string>();
					int n = 0;
					foreach (var memberId in myClan.members)
					{
						var p = BasePlayer.FindByID(Convert.ToUInt64(memberId));
						if (p != null)
						{
							if (n > 0) sb.Append(", ");
							if (myClan.IsOwner(memberId))
							{
								sb.Append("<color=#a1ff46>").Append(StripTag(p.displayName, myClan)).Append("</color>");
							}
							else if (myClan.IsModerator(memberId))
							{
								sb.Append("<color=#74c6ff>").Append(StripTag(p.displayName, myClan)).Append("</color>");
							}
							else
							{
								sb.Append(p.displayName);
							}
							++n;
						}
					}
					sb.Append("\n");
					if ((myClan.IsOwner(userId) || myClan.IsModerator(userId)) && myClan.invited.Count > 0)
					{
						sb.Append(_("Pending invites:")).Append(" ");
						int m = 0;
						foreach (var inviteId in myClan.invited)
						{
							var p = BasePlayer.FindByID(Convert.ToUInt64(inviteId));
							if (p != null)
							{
								if (m > 0) sb.Append(", ");
								sb.Append(p.displayName);
								++m;
							}
						}
						sb.Append("\n");
					}
				}
				sb.Append(_("To learn more about clans, type: <color=\"#ffd479\">/clan help</color>"));
				SendReply(player, "{0}", sb.ToString());
				return;
			}
			switch (args[0])
			{
				case "create":
					if (args.Length != 3)
					{
						sb.Append(_("Usage: <color=\"#ffd479\">/clan create \"TAG\" \"Description\"</color>"));
						break;
					}
					if (myClan != null)
					{
						sb.Append(_("You are already a member of a clan."));
						break;
					}
					if (!tagRe.IsMatch(args[1]))
					{
						sb.Append(_("Clan tags must be 2 to 6 characters long and may contain standard letters and numbers only"));
						break;
					}
					args[2] = args[2].Trim();
					if (args[2].Length < 2 || args[2].Length > 30)
					{
						sb.Append(_("Please provide a short description of your clan."));
						break;
					}
					if (clans.ContainsKey(args[1]))
					{
						sb.Append(_("There is already a clan with this tag."));
						break;
					}
					myClan = Clan.Create(args[1], args[2], userId);
					clans.Add(myClan.tag, myClan);
					SaveData();
					lookup[userId] = myClan;
					SetupPlayer(player); // Add clan tag
					sb.Append(_("You are now the owner of your new clan:")).Append(" ");
					sb.Append("[").Append(myClan.tag).Append("] ").Append(myClan.description).Append("\n");
					sb.Append(_("To invite new members, type: <color=\"#ffd479\">/clan invite \"Player name\"</color>"));
					break;
				case "invite":
					if (args.Length != 2)
					{
						sb.Append(_("Usage: <color=\"#ffd479\">/clan invite \"Player name\"</color>"));
						break;
					}
					if (myClan == null)
					{
						sb.Append(_("You are currently not a member of a clan."));
						break;
					}
					if (!myClan.IsOwner(userId) && !myClan.IsModerator(userId))
					{
						sb.Append(_("You need to be a moderator of your clan to use this command."));
						break;
					}
					var invPlayer = FindPlayerByPartialName(args[1]);
					if (invPlayer == null)
					{
						sb.Append(_("No such player or player name not unique:")).Append(" ").Append(args[1]);
						break;
					}
					if (myClan.members.Contains(invPlayer.UserIDString))
					{
						sb.Append(_("This player is already a member of your clan:")).Append(" ").Append(invPlayer.displayName);
						break;
					}
					if (myClan.invited.Contains(invPlayer.UserIDString))
					{
						sb.Append(_("This player has already been invited to your clan:")).Append(" ").Append(invPlayer.displayName);
						break;
					}
					myClan.invited.Add(invPlayer.UserIDString);
					SaveData();
					myClan.Broadcast(_("%MEMBER% invited %PLAYER% to the clan.", new Dictionary<string, string>() { { "MEMBER", StripTag(player.displayName, myClan) }, { "PLAYER", invPlayer.UserIDString } }));
					var basePlayer = BasePlayer.FindByID(ulong.Parse(invPlayer.UserIDString));
					basePlayer.SendConsoleCommand("chat.add", "",
						_("You have been invited to join the clan:") + " [" + myClan.tag + "] " + myClan.description + "\n" +
						_("To join, type: <color=#ffd479>/clan join \"%TAG%\"</color>", new Dictionary<string, string>() { { "TAG", myClan.tag } }));
					break;
				case "join":
					if (args.Length != 2)
					{
						sb.Append(_("Usage: <color=\"#ffd479\">/clan join \"TAG\"</color>"));
						break;
					}
					if (myClan != null)
					{
						sb.Append(_("You are already a member of a clan."));
						break;
					}
					myClan = FindClan(args[1]);
					if (myClan == null || !myClan.IsInvited(userId))
					{
						sb.Append(_("You have not been invited to join this clan."));
						break;
					}
					if (limitMembers >= 0 && myClan.members.Count > limitMembers)
					{
						sb.Append(_("This clan has already reached the maximum number of members."));
						break;
					}
					myClan.invited.Remove(userId);
					myClan.members.Add(userId);
					SaveData();
					lookup[userId] = myClan;
					SetupPlayer(player);
					myClan.Broadcast(_("%NAME% has joined the clan!", new Dictionary<string, string>() { { "NAME", StripTag(player.displayName, myClan) } }));
					foreach (var memberId in myClan.members)
					{
						if (memberId != userId && IsInstalled() && addClanMatesAsFriends)
						{
							AddFriend(memberId, userId);
							AddFriend(userId, memberId);
						}
					}
					break;
				case "promote":
					if (args.Length != 2)
					{
						sb.Append(_("Usage: <color=\"#ffd479\">/clan promote \"Player name\"</color>"));
						break;
					}
					if (myClan == null)
					{
						sb.Append(_("You are currently not a member of a clan."));
						break;
					}
					if (!myClan.IsOwner(userId))
					{
						sb.Append(_("You need to be the owner of your clan to use this command."));
						break;
					}
					var promotePlayer = FindPlayerByPartialName(args[1]);
					if (promotePlayer == null)
					{
						sb.Append(_("No such player or player name not unique:") + " " + args[1]);
						break;
					}
					var promotePlayerUserId = promotePlayer.UserIDString;
					if (!myClan.IsMember(promotePlayerUserId))
					{
						sb.Append(_("This player is not a member of your clan:") + " " + promotePlayer.displayName);
						break;
					}
					if (myClan.IsModerator(promotePlayerUserId))
					{
						sb.Append(_("This player is already a moderator of your clan:") + " " + promotePlayer.displayName);
						break;
					}
					myClan.moderators.Add(promotePlayerUserId);
					SaveData();
					myClan.Broadcast(_("%OWNER% promoted %MEMBER% to moderator.", new Dictionary<string, string> { { "OWNER", StripTag(player.displayName, myClan) }, { "MEMBER", StripTag(promotePlayer.displayName, myClan) } }));
					break;
				case "demote":
					if (args.Length != 2)
					{
						sb.Append(_("Usage: <color=\"#ffd479\">/clan demote \"Player name\"</color>"));
						break;
					}
					if (myClan == null)
					{
						sb.Append(_("You are currently not a member of a clan."));
						break;
					}
					if (!myClan.IsOwner(userId))
					{
						sb.Append(_("You need to be the owner of your clan to use this command."));
						break;
					}
					var demotePlayer = FindPlayerByPartialName(args[1]);
					if (demotePlayer == null)
					{
						sb.Append(_("No such player or player name not unique:") + " " + args[1]);
						break;
					}
					var demotePlayerUserId = demotePlayer.UserIDString;
					if (!myClan.IsMember(demotePlayerUserId))
					{
						sb.Append(_("This player is not a member of your clan:") + " " + demotePlayer.displayName);
						break;
					}
					if (!myClan.IsModerator(demotePlayerUserId))
					{
						sb.Append(_("This player is not a moderator of your clan:") + " " + demotePlayer.displayName);
						break;
					}
					myClan.moderators.Remove(demotePlayerUserId);
					SaveData();
					myClan.Broadcast(player.displayName + " понизил " + demotePlayer.displayName + " до обычного бандита");
					break;
				case "leave":
					if (args.Length != 1)
					{
						sb.Append(_("Usage: <color=\"#ffd479\">/clan leave</color>"));
						break;
					}
					if (myClan == null)
					{
						sb.Append(_("You are currently not a member of a clan."));
						break;
					}
					if (myClan.members.Count == 1)
					{ // Remove the clan once the last member leaves
						clans.Remove(myClan.tag);
					}
					else
					{
						myClan.moderators.Remove(userId);
						myClan.members.Remove(userId);
						myClan.invited.Remove(userId);
						if (myClan.IsOwner(userId) && myClan.members.Count > 0)
						{ // Make the first member the new owner
							myClan.owner = myClan.members[0];
						}
					}

					foreach (var memberId in myClan.members)
					{
						if (memberId != userId && IsInstalled())
						{
							DeleteFriend(memberId, userId);
							DeleteFriend(userId, memberId);
						}
					}
					SaveData();
					lookup.Remove(userId);
					SetupPlayer(player); // Remove clan tag
					sb.Append(_("You have left your current clan."));
					myClan.Broadcast(_("%NAME% has left the clan.", new Dictionary<string, string>() { { "NAME", player.displayName } }));
					break;
				case "kick":
					if (args.Length != 2)
					{
						sb.Append(_("Usage: <color=\"#ffd479\">/clan kick \"Player name\"</color>"));
						break;
					}
					if (myClan == null)
					{
						sb.Append(_("You are currently not a member of a clan."));
						break;
					}
					if (!myClan.IsOwner(userId) && !myClan.IsModerator(userId))
					{
						sb.Append(_("You need to be a moderator of your clan to use this command."));
						break;
					}
					var kickPlayer = FindPlayerByPartialName(args[1]);
					if (kickPlayer == null)
					{
						sb.Append(_("No such player or player name not unique:") + " " + args[1]);
						break;
					}
					var kickPlayerUserId = kickPlayer.UserIDString;
					if (!myClan.IsMember(kickPlayerUserId) && !myClan.IsInvited(kickPlayerUserId))
					{
						sb.Append(_("This player is not a member of your clan:") + " " + kickPlayer.displayName);
						break;
					}
					if (myClan.IsOwner(kickPlayerUserId) || myClan.IsModerator(kickPlayerUserId))
					{
						sb.Append(_("This player is an owner or moderator and cannot be kicked:") + " " + kickPlayer.displayName);
						break;
					}
					myClan.members.Remove(kickPlayerUserId);
					myClan.invited.Remove(kickPlayerUserId);
					SaveData();
					lookup.Remove(kickPlayerUserId);
					foreach (var memberId in myClan.members)
					{
						if (memberId != userId && IsInstalled())
						{
							DeleteFriend(memberId, userId);
							DeleteFriend(userId, memberId);
						}
					}
					SetupPlayer(kickPlayer); // Remove clan tag
					myClan.Broadcast(_("%NAME% kicked %MEMBER% from the clan.", new Dictionary<string, string> { { "NAME", StripTag(player.displayName, myClan) }, { "MEMBER", kickPlayer.displayName } }));
					break;
				case "disband":
					if (args.Length != 2)
					{
						sb.Append(_("Usage: <color=\"#ffd479\">/clan disband forever</color>"));
						break;
					}
					if (myClan == null)
					{
						sb.Append(_("You are currently not a member of a clan."));
						break;
					}
					if (!myClan.IsOwner(userId))
					{
						sb.Append(_("You need to be the owner of your clan to use this command."));
						break;
					}
					clans.Remove(myClan.tag);
					SaveData();
					foreach (var memberId in myClan.members)
					{
						foreach (var otherMemberId in myClan.members.Where(m => m != memberId))
						{
							if (memberId != otherMemberId && IsInstalled())
							{
								DeleteFriend(memberId, otherMemberId);
								DeleteFriend(otherMemberId, memberId);
							}
						}
					}

					foreach (var member in myClan.members)
						lookup.Remove(member);
					myClan.Broadcast(_("Your current clan has been disbanded forever."));
					SetupPlayers(myClan.members); // Remove clan tags
					break;
				case "delete":
					if (args.Length != 2)
					{
						sb.Append(_("Usage: <color=\"#ffd479\">/clan delete \"TAG\"</color>"));
						break;
					}
					if (player.net.connection.authLevel < 2)
					{
						sb.Append(_("You need to be a server owner to delete clans."));
						break;
					}
					Clan clan;
					if (!clans.TryGetValue(args[1], out clan))
					{
						sb.Append(_("There is no clan with that tag:")).Append(" ").Append(args[1]);
						break;
					}
					clan.Broadcast(_("Your clan has been deleted by the server owner."));
					clans.Remove(args[1]);
					SaveData();
					foreach (var memberId in clan.members)
					{
						foreach (var otherMemberId in clan.members.Where(m => m != memberId))
						{
							if (memberId != otherMemberId && IsInstalled())
							{
								DeleteFriend(memberId, otherMemberId);
								DeleteFriend(otherMemberId, memberId);
							}
						}
					}
					foreach (var member in clan.members)
						lookup.Remove(member);
					SetupPlayers(clan.members);
					sb.Append(_("You have deleted the clan:")).Append(" [").Append(clan.tag).Append("] ").Append(clan.description);
					break;
				// case "list":
				// foreach (var cl in clans.Values)
				// {
				// var leaderData = Interface.GetMod().GetLibrary<Covalence>().Players.GetPlayer(cl.owner);
				// if(leaderData == null)
				// continue;
				// sb.Append("<color=orange>[").Append(cl.tag).Append("]</color> - ").Append(cl.description).Append(". Главарь - <color=orange>").Append(leaderData.Nickname).Append("</color>").Append("\n");
				// }
				// break;
				default:
					sb.Append(_("Available commands:")).Append("\n");
					sb.Append("  ").Append(_("<color=#ffd479>/clan</color> - Displays relevant information about your current clan")).Append("\n");
					sb.Append("  ").Append(_("<color=#ffd479>/c Message...</color> - Sends a message to all online clan members")).Append("\n");
					sb.Append("  ").Append(_("<color=#ffd479>/clan create \"TAG\" \"Description\"</color> - Creates a new clan you own")).Append("\n");
					sb.Append("  ").Append(_("<color=#ffd479>/clan join \"TAG\"</color> - Joins a clan you have been invited to")).Append("\n");
					sb.Append("  ").Append(_("<color=#ffd479>/clan leave</color> - Leaves your current clan")).Append("\n");
					// sb.Append("  ").Append("<color=#ffd479>/clan list</color> - Список банд сервера").Append("\n");
					sb.Append(_("<color=#74c6ff>Moderator</color> commands:")).Append("\n");
					sb.Append("  ").Append(_("<color=#ffd479>/clan invite \"Player name\"</color> - Invites a player to your clan")).Append("\n");
					sb.Append("  ").Append(_("<color=#ffd479>/clan kick \"Player name\"</color> - Kicks a member from your clan")).Append("\n");
					sb.Append(_("<color=#a1ff46>Owner</color> commands:")).Append("\n");
					sb.Append("  ").Append(_("<color=#ffd479>/clan promote \"Name\"</color> - Promotes a member to moderator")).Append("\n");
					sb.Append("  ").Append(_("<color=#ffd479>/clan demote \"Name\"</color> - Demotes a moderator to member")).Append("\n");
					sb.Append("  ").Append(_("<color=#ffd479>/clan disband forever</color> - Disbands your clan (no undo)")).Append("\n");

					break;
			}
			SendReply(player, "{0}", sb.ToString().TrimEnd());
		}

		[ChatCommand("c")]
		private void cmdChatClanchat(BasePlayer player, string command, string[] args)
		{
			var myClan = FindClanByUser(player.userID.ToString());
			if (myClan == null)
			{
				SendReply(player, "{0}", _("You are currently not a member of a clan."));
				return;
			}
			var message = string.Join(" ", args);
			if (string.IsNullOrEmpty(message))
				return;
			myClan.Broadcast(StripTag(player.displayName, myClan) + ": " + message);
			var playerId = player.userID.ToString();
			var playerName = originalNames.ContainsKey(playerId) ? originalNames[playerId] : player.displayName;
			Puts("[CLANCHAT] {0} - {1}: {2}", myClan.tag, playerName, message);
		}

		// Represents a clan
		public class Clan
		{
			public string tag;
			public string description;
			public string owner;
			public List<string> moderators = new List<string>();
			public List<string> members = new List<string>();
			public List<string> invited = new List<string>();

			public static Clan Create(string tag, string description, string ownerId)
			{
				var clan = new Clan() { tag = tag, description = description, owner = ownerId };
				clan.members.Add(ownerId);
				return clan;
			}

			public bool IsOwner(string userId)
			{
				return userId == owner;
			}

			public bool IsModerator(string userId)
			{
				return moderators.Contains(userId);
			}

			public bool IsMember(string userId)
			{
				return members.Contains(userId);
			}

			public bool IsInvited(string userId)
			{
				return invited.Contains(userId);
			}

			public void Broadcast(string message)
			{
				foreach (var memberId in members)
				{
					var player = BasePlayer.FindByID(Convert.ToUInt64(memberId));
					if (player == null)
						continue;
					player.SendConsoleCommand("chat.add", "", "<color=#a1ff46>(Банда)</color> " + message);
				}
			}

			internal JObject ToJObject()
			{
				var obj = new JObject();
				obj["tag"] = tag;
				obj["description"] = description;
				obj["owner"] = owner;
				var jmoderators = new JArray();
				foreach (var moderator in moderators)
					jmoderators.Add(moderator);
				obj["moderators"] = jmoderators;
				var jmembers = new JArray();
				foreach (var member in members)
					jmembers.Add(member);
				obj["members"] = jmembers;
				var jinvited = new JArray();
				foreach (var invite in invited)
					jinvited.Add(invite);
				obj["invited"] = jinvited;
				return obj;
			}
		}

		#region Plugin API

		[LibraryFunction("GetClan")]
		public JObject GetClan(string tag)
		{
			var clan = FindClan(tag);
			if (clan == null)
				return null;
			return clan.ToJObject();
		}

		[LibraryFunction("GetAllClans")]
		public JArray GetAllClans()
		{
			return new JArray(clans.Keys);
		}

		[LibraryFunction("GetClanOf")]
		public string GetClanOf(object player)
		{
			if (player == null)
				throw new ArgumentException("player");
			if (player is ulong)
				player = ((ulong)player).ToString();
			else if (player is BasePlayer)
				player = (player as BasePlayer).userID.ToString();
			if (!(player is string))
				throw new ArgumentException("player");
			var clan = FindClanByUser((string)player);
			if (clan == null)
				return null;
			return clan.tag;
		}

		#endregion

		#region Utility Methods

		private void Log(string message)
		{
			Interface.Oxide.LogInfo("{0}: {1}", Title, message);
		}

		private void Warn(string message)
		{
			Interface.Oxide.LogWarning("{0}: {1}", Title, message);
		}

		private void Error(string message, Exception ex = null)
		{
			if (ex != null)
				Interface.Oxide.LogException(string.Format("{0}: {1}", Title, message), ex);
			else
				Interface.Oxide.LogError("{0}: {1}", Title, message);
		}

		#endregion
	}
}
