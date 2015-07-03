/*
 * user story:
		Airdrop loot settings - 
			player count
			frequency
			supply crate stay time
create default config
check player authentication levels
diagnostics
	- notifications by groups
	- server notification
 */

using AirdropExtended.Collision;
using AirdropExtended.Diagnostics;
using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using AirdropExtended;
using AirdropExtended.Airdrop.Settings;
using AirdropExtended.PluginSettings;
using Oxide.Plugins;
using Rust;
using UnityEngine;
using LogType = Oxide.Core.Logging.LogType;
using Timer = Oxide.Core.Libraries.Timer;


// ReSharper disable once CheckNamespace

namespace Oxide.Plugins
{
	[Info(Constants.PluginName, "ZakBelg", 0.1, ResourceId = 714)]
	[Description("Customizable airdrop: loot table, timers, auth")]
	public class AirdropExtendedPlugin : RustPlugin
	{
		private static readonly PluginSettingsManager PluginSettingsManager = new PluginSettingsManager();
		private static readonly AirdropSettingsFactory AirdropSettingsFactory = new AirdropSettingsFactory();

		private AirdropSettings _settings;
		private string _settingsName;
		private Timer _aidropTimer;

		// ReSharper disable once UnusedMember.Local
		private void OnServerInitialized()
		{
			Load();
			Puts(_settings.Settings.ToString());
		}

		// ReSharper disable once UnusedMember.Local
		void OnPlayerLoot(PlayerLoot lootInventory, BaseEntity targetEntity)
		{
			var supplyDrop = targetEntity as SupplyDrop;
			if (lootInventory == null || targetEntity == null || supplyDrop == null)
				return;

			Diagnostics.MessageTo(
				_settings.Settings.NotifyOnPlayerLootingStartedMessage,
				_settings.Settings.NotifyOnPlayerLootingStarted,
				supplyDrop.net.ID,
				lootInventory.GetComponent<BasePlayer>().userID);
		}

		// ReSharper disable once UnusedMember.Local
		private void OnEntitySpawned(BaseEntity entity)
		{
			if (entity == null || _settings == null)
				return;

			if (entity is SupplyDrop)
				HandleSupply(entity as SupplyDrop);

			if (entity is CargoPlane)
				HandlePlane(entity as CargoPlane);
		}

		private void HandleSupply(SupplyDrop entity)
		{
			var supplyDrop = entity.GetComponent<LootContainer>();
			if (supplyDrop == null)
				return;

			var itemContainer = supplyDrop.inventory;
			if (itemContainer == null || itemContainer.itemList == null)
				return;

			itemContainer.itemList.Clear();
			var itemList = _settings.CreateItemList();

			foreach (var item in itemList)
			{
				Diagnostics.MessageToServer("aire: add item {0}, amount {1} to airdrop container", item.info.name, item.amount);
				item.MoveToContainer(itemContainer, -1, false);
			}

			var x = entity.transform.position.x;
			var y = entity.transform.position.y;
			var z = entity.transform.position.z;

			Diagnostics.MessageTo(_settings.Settings.NotifyOnDropStartedMessage, _settings.Settings.NotifyOnDropStarted, entity.net.ID, x, y, z);
			if (_settings.Settings.SupplyCrateDespawnTime <= TimeSpan.Zero)
				return;

			var timeoutInSeconds = Convert.ToSingle(_settings.Settings.SupplyCrateDespawnTime.TotalSeconds);
			supplyDrop.gameObject.AddComponent<CollisionCheck>();

			var dropCollisionCheck = supplyDrop.gameObject.GetComponent<CollisionCheck>();
			dropCollisionCheck.TimeoutInSeconds = timeoutInSeconds;

			dropCollisionCheck.NotifyOnCollision = _settings.Settings.NotifyOnCollision;
			dropCollisionCheck.NotifyOnCollisionMessage = _settings.Settings.NotifyOnCollisionMessage;

			dropCollisionCheck.NotifyOnDespawn = _settings.Settings.NotifyOnDespawn;
			dropCollisionCheck.NotifyOnDespawnMessage = _settings.Settings.NotifyOnDespawnMessage;
		}

		private void HandlePlane(CargoPlane plane)
		{
			var playerCount = BasePlayer.activePlayerList.Count;
			if (playerCount < _settings.Settings.MinimumPlayerCount)
			{
				Diagnostics.MessageTo(_settings.Settings.NotifyOnPlaneRemovedMessage, _settings.Settings.NotifyOnPlaneRemoved, playerCount);
				plane.Kill();
				return;
			}

			Diagnostics.MessageTo(_settings.Settings.NotifyOnPlaneSpawnedMessage, _settings.Settings.NotifyOnPlaneSpawned);
		}

		private void Load()
		{
			LoadConfig();
			RegisterPermissions();
			Initialize();
		}

		private void RegisterPermissions()
		{
			if (!permission.PermissionExists("aire.canReload")) permission.RegisterPermission("aire.canReload", this);
			if (!permission.PermissionExists("aire.canLoad")) permission.RegisterPermission("aire.canLoad", this);
			if (!permission.PermissionExists("aire.canGenerate")) permission.RegisterPermission("aire.canGenerate", this);
			if (!permission.PermissionExists("aire.canFreq")) permission.RegisterPermission("aire.canFreq", this);
			if (!permission.PermissionExists("aire.canPlayers")) permission.RegisterPermission("aire.canPlayers", this);
			if (!permission.PermissionExists("aire.canConsole")) permission.RegisterPermission("aire.canConsole", this);
			if (!permission.PermissionExists("aire.canDespawn")) permission.RegisterPermission("aire.canDespawn", this);
		}

		private void Initialize()
		{
			LoadSettings();

			if (_settings.Settings.DropFrequency <= TimeSpan.Zero || _settings.Settings.ConsoleStartOnly)
				return;
			StartAirdropTimer();
		}

		private void StartAirdropTimer()
		{
			var dropFrequencyInSeconds = Convert.ToSingle(_settings.Settings.DropFrequency.TotalSeconds);
			_aidropTimer = timer.Repeat(dropFrequencyInSeconds, 0, SpawnPlane);
		}

		private void SpawnPlane()
		{
			if (_settings.Settings.ConsoleStartOnly)
				return;

			var plane = GameManager.server.CreateEntity("events/cargo_plane", new Vector3(), new Quaternion());
			if (plane != null)
				plane.Spawn();
		}

		private void LoadSettings()
		{
			try
			{
				_settingsName = PluginSettingsManager.LoadSettingsName(this);
				Diagnostics.MessageToServer("settings file to use:{0}", _settingsName);
				var loadedSettingsOrDefault = AirdropSettingsFactory.LoadFrom(_settingsName);

				_settings = string.IsNullOrEmpty(_settingsName) || loadedSettingsOrDefault.ItemsByGroups == null
					? AirdropSettingsFactory.CreateDefault()
					: loadedSettingsOrDefault;
			}
			catch (Exception)
			{
				Diagnostics.MessageToServer("error. creating default file name {0}", _settingsName);
				_settings = AirdropSettingsFactory.CreateDefault();
			}
			Diagnostics.MessageToServer("validating settings {0}", _settingsName);
			AirdropSettingsValidator.Validate(_settings);
			Diagnostics.MessageToServer("validation ++ {0}", _settingsName);
			_settingsName = string.IsNullOrEmpty(_settingsName)
				? "defaultSettings"
				: _settingsName;
		}

		// ReSharper disable once UnusedMember.Local
		private void Unload()
		{
			Cleanup();
			PluginSettingsManager.Save(this, _settingsName);
			SaveConfig();
			AirdropSettingsFactory.SaveTo(_settingsName, _settings);
		}

		private void Cleanup()
		{
			StopAirdropTimer();
		}

		private void StopAirdropTimer()
		{
			if (_aidropTimer != null && !_aidropTimer.Destroyed)
				_aidropTimer.Destroy();
		}

		[ConsoleCommand("aire.reload")]
		public void ReloadConfigConsole(ConsoleSystem.Arg arg)
		{
			if (arg.connection == null || arg.connection.player == null)
				return;

			var player = arg.connection.player as BasePlayer;
			ReloadConfigChat(player, arg.cmd.name, arg.Args);
		}

		[ChatCommand("aire.reload")]
		public void ReloadConfigChat(BasePlayer player, string command, string[] args)
		{
			if (!HasPermission(player, "aire.canReload")) 
				return;

			Diagnostics.MessageToServer("aire:Reloading settings, Player:{0}", player.userID);
			Cleanup();
			Initialize();
		}

		private bool HasPermission(BasePlayer player, string permissionName)
		{
			var uid = Convert.ToString(player.userID);
			if (permission.UserHasPermission(uid, permissionName)) 
				return true;

			Diagnostics.MessageToPlayer(player, "You have no permission to use this command!");
			return false;
		}

		[ConsoleCommand("aire.load")]
		public void LoadConfigConsole(ConsoleSystem.Arg arg)
		{
			if (arg.connection == null || arg.connection.player == null)
				return;

			var player = arg.connection.player as BasePlayer;
			LoadConfigChat(player, arg.cmd.name, arg.Args);
		}

		[ChatCommand("aire.load")]
		public void LoadConfigChat(BasePlayer player, string command, string[] args)
		{
			if (!HasPermission(player, "aire.canLoad"))
				return;

			if (args.Length < 1)
			{
				Diagnostics.MessageToServer("aire:Command use aire.load my_settings_name");
				return;
			}

			var settingsName = args[0];
			if (string.IsNullOrEmpty(settingsName))
			{
				Diagnostics.MessageToServer("aire:Command use aire.load my_settings_name");
				return;
			}

			Diagnostics.MessageToServer("aire:Loading settings, Player:{0}", player.userID);

			Cleanup();
			PluginSettingsManager.Save(this, args[0]);
			Initialize();
		}

		[ConsoleCommand("aire.generate")]
		public void GenerateConfigConsole(ConsoleSystem.Arg arg)
		{
			if (arg.connection == null || arg.connection.player == null)
				return;

			var player = arg.connection.player as BasePlayer;
			GenerateConfigChat(player, arg.cmd.name, arg.Args);
		}

		[ChatCommand("aire.generate")]
		public void GenerateConfigChat(BasePlayer player, string command, string[] args)
		{
			if (!HasPermission(player, "aire.canGenerate"))
				return;

			if (args.Length < 1)
			{
				Diagnostics.MessageToServer("aire:Command use aire.generate my_settings_name");
				return;
			}

			var settingsName = args[0];
			if (string.IsNullOrEmpty(settingsName))
			{
				Diagnostics.MessageToServer("aire:Command use aire.generate my_settings_name");
				return;
			}

			Diagnostics.MessageToServer("aire:Generate default settings to {0}", settingsName);
			var settings = AirdropSettingsFactory.CreateDefault();
			AirdropSettingsFactory.SaveTo(settingsName, settings);
		}

		[ConsoleCommand("aire.freq")]
		public void SetFrequencyConsole(ConsoleSystem.Arg arg)
		{
			if (arg.connection == null || arg.connection.player == null)
				return;

			var player = arg.connection.player as BasePlayer;
			SetFrequencyChat(player, arg.cmd.name, arg.Args);
		}

		[ChatCommand("aire.freq")]
		public void SetFrequencyChat(BasePlayer player, string command, string[] args)
		{
			if (!HasPermission(player, "aire.canFreq"))
				return;

			if (args.Length < 1)
			{
				Diagnostics.MessageToServer("aire:Command use: aire.frequency 100");
				return;
			}

			int frequency;
			if (!int.TryParse(args[0], out frequency))
			{
				Diagnostics.MessageToServer("aire:Command use: aire.frequency 100");
				return;
			}

			Diagnostics.MessageToServer("aire:Set frequency to {0} seconds", frequency);
			_settings.Settings.DropFrequency = TimeSpan.FromSeconds(frequency);

			Cleanup();
			Initialize();
		}

		[ConsoleCommand("aire.players")]
		public void SetMinimumPlayerCountConsole(ConsoleSystem.Arg arg)
		{
			if (arg.connection == null || arg.connection.player == null)
				return;

			var player = arg.connection.player as BasePlayer;
			SetMinimumPlayerCountChat(player, arg.cmd.name, arg.Args);
		}

		[ChatCommand("aire.players")]
		public void SetMinimumPlayerCountChat(BasePlayer player, string command, string[] args)
		{
			if (!HasPermission(player, "aire.canPlayers"))
				return;

			if (args.Length < 1)
			{
				Diagnostics.MessageToServer("aire:Command use: aire.players 100");
				return;
			}

			int minimumPlayerCount;
			if (!int.TryParse(args[0], out minimumPlayerCount))
			{
				Diagnostics.MessageToServer("aire:Command use: aire.players 100");
				return;
			}

			Diagnostics.MessageToServer("aire:Set minimumPlayerCount to {0}", minimumPlayerCount);
			_settings.Settings.MinimumPlayerCount = minimumPlayerCount;

			Cleanup();
			Initialize();
		}

		[ConsoleCommand("aire.console")]
		public void SetConsoleStartOnlyConsole(ConsoleSystem.Arg arg)
		{
			if (arg.connection == null || arg.connection.player == null)
				return;

			var player = arg.connection.player as BasePlayer;
			SetConsoleStartOnlyChat(player, arg.cmd.name, arg.Args);
		}

		[ChatCommand("aire.console")]
		public void SetConsoleStartOnlyChat(BasePlayer player, string command, string[] args)
		{
			if (!HasPermission(player, "aire.canConsole"))
				return;

			if (args.Length < 1)
			{
				Diagnostics.MessageToServer("aire:Command use: aire.console 1");
				return;
			}

			bool consoleStartOnly;
			if (!bool.TryParse(args[0], out consoleStartOnly))
			{
				Diagnostics.MessageToServer("aire:Command use: aire.console 1");
				return;
			}

			Diagnostics.MessageToServer("aire:Set ConsoleStartOnly to {0}", consoleStartOnly);
			_settings.Settings.ConsoleStartOnly = consoleStartOnly;

			Cleanup();
			Initialize();
		}

		[ConsoleCommand("aire.despawn")]
		public void SetSupplyCrateDespawnTimeConsole(ConsoleSystem.Arg arg)
		{
			if (arg.connection == null || arg.connection.player == null)
				return;

			var player = arg.connection.player as BasePlayer;
			SetSupplyCrateDespawnTimeChat(player, arg.cmd.name, arg.Args);
		}

		[ChatCommand("aire.despawn")]
		public void SetSupplyCrateDespawnTimeChat(BasePlayer player, string command, string[] args)
		{
			if (!HasPermission(player, "aire.canDespawn"))
				return;

			if (args.Length < 1)
			{
				Diagnostics.MessageToServer("aire:Command use: aire.despawn 100");
				return;
			}

			int despawnTimeInSeconds;
			if (!int.TryParse(args[0], out despawnTimeInSeconds))
			{
				Diagnostics.MessageToServer("aire:Command use: aire.despawn 100");
				return;
			}

			Diagnostics.MessageToServer("aire:Set SupplyCrateDespawnTime to {0} seconds", despawnTimeInSeconds);
			_settings.Settings.SupplyCrateDespawnTime = TimeSpan.FromSeconds(despawnTimeInSeconds);

			Cleanup();
			Initialize();
		}
	}
}

namespace AirdropExtended
{
	public sealed class Constants
	{
		public const string PluginName = "AirdropSettings";
	}
}

namespace AirdropExtended.Collision
{
	public sealed class CollisionCheck : MonoBehaviour
	{
		private const float DefaultSupplyStayTime = 300.0f;
		private const string DefaultNotifyOnCollisionMessage = "Supply drop {0} has landed at {1},{2},{3}";
		private const string DefaultNotifyOnDespawnMessage = "Supply drop {0} has been despawned at {1},{2},{3}";

		private readonly Timer _timer = Interface.Oxide.GetLibrary<Timer>("Timer");
		private Timer.TimerInstance _destructionTimer;
		public float TimeoutInSeconds { get; set; }

		public string NotifyOnCollisionMessage { get; set; }
		public bool NotifyOnCollision { get; set; }

		public string NotifyOnDespawnMessage { get; set; }
		public bool NotifyOnDespawn { get; set; }

		public CollisionCheck()
		{
			TimeoutInSeconds = DefaultSupplyStayTime;

			NotifyOnCollisionMessage = DefaultNotifyOnCollisionMessage;
			NotifyOnCollision = true;

			NotifyOnDespawnMessage = DefaultNotifyOnDespawnMessage;
			NotifyOnDespawn = true;
		}

		// ReSharper disable once UnusedMember.Local
		// ReSharper disable once UnusedParameter.Local
		void OnCollisionEnter(UnityEngine.Collision col)
		{
			var baseEntity = GetComponent<BaseEntity>();
			var landedX = col.gameObject.transform.localPosition.x;
			var landedY = col.gameObject.transform.localPosition.y;
			var landedZ = col.gameObject.transform.localPosition.z;

			Diagnostics.Diagnostics.MessageTo(NotifyOnCollisionMessage, NotifyOnCollision, baseEntity.net.ID, landedX, landedY, landedZ);
			_destructionTimer = _timer.Once(TimeoutInSeconds * 60, () =>
			{
				var x = baseEntity.transform.position.x;
				var y = baseEntity.transform.position.y;
				var z = baseEntity.transform.position.z;

				Diagnostics.Diagnostics.MessageTo(NotifyOnDespawnMessage, NotifyOnDespawn, baseEntity.net.ID, x, y, z);
				baseEntity.KillMessage();
				_destructionTimer.Destroy();
				_destructionTimer = null;
			});
		}
	}
}

namespace AirdropExtended.Diagnostics
{
	public static class Diagnostics
	{
		public static void MessageTo(string message, bool sendToAll, params object[] args)
		{
			if (sendToAll)
				MessageToAll(message, args);
			MessageToServer(message, args);
		}

		public static void MessageToPlayer(BasePlayer player, string message, params object[] args)
		{
			const string format = "<color=orange>aire</color>:";
			player.SendConsoleCommand("chat.add", new object[] { 0, format + string.Format(message, args), 1f });
		}

		public static void MessageToAll(string message, params object[] args)
		{
			ConsoleSystem.Broadcast(message, args);
		}

		public static void MessageToServer(string message, params object[] args)
		{
			Interface.GetMod().RootLogger.Write(LogType.Info, message, args);
		}
	}
}

namespace AirdropExtended.PluginSettings
{
	public sealed class PluginSettingsManager
	{
		public string LoadSettingsName(AirdropExtendedPlugin plugin)
		{
			if (plugin == null) throw new ArgumentNullException("plugin");
			string settingsName;
			try
			{
				settingsName = (string)plugin.Config["settingsName"];
			}
			catch (Exception)
			{
				settingsName = string.Empty;
			}
			return settingsName;
		}

		public void Save(AirdropExtendedPlugin plugin, string settingsName)
		{
			if (plugin == null) throw new ArgumentNullException("plugin");
			if (string.IsNullOrEmpty(settingsName)) throw new ArgumentException("Should not be blank", "settingsName");

			plugin.Config["settingsName"] = settingsName;
		}
	}
}

namespace AirdropExtended.Airdrop.Settings
{
	public sealed class AirdropSettings
	{
		public const int MaxCapacity = 24;

		public AirdropSettings()
		{
			Capacity = MaxCapacity;
		}

		public int Capacity { get; set; }

		public List<AirdropItemGroup> ItemsByGroups { get; set; }

		public CommonSettings Settings { get; set; }

		public List<Item> CreateItemList()
		{
			return ItemsByGroups.SelectMany(PickItemsFromGroup).ToList();
		}

		private static IEnumerable<Item> PickItemsFromGroup(AirdropItemGroup airdropItemGroup)
		{
			if (airdropItemGroup.MaximumAmountInLoot <= 0)
				return Enumerable.Empty<Item>();

			var pickedItemsNumber = 0;
			var pickedItems = new Item[airdropItemGroup.MaximumAmountInLoot];

			do
			{
				var itemIndex = Oxide.Core.Random.Range(1, airdropItemGroup.ItemSettings.Count) - 1;
				var airdropItemSetting = airdropItemGroup.ItemSettings[itemIndex];
				var pickedChance = Oxide.Core.Random.Range(1, 100);
				if (pickedChance < airdropItemSetting.ChanceInPercent)
					continue;

				var amount = Oxide.Core.Random.Range(airdropItemSetting.MinAmount, airdropItemSetting.MaxAmount);
				if (amount == 0)
					continue;

				Diagnostics.Diagnostics.MessageToServer("created item {0}, amount:{1}", airdropItemSetting.Name, amount);
				var pickedItem = ItemManager.CreateByName(airdropItemSetting.Name, amount);
				if (airdropItemGroup.Name == "Blueprint")
					pickedItem.isBlueprint = true;
				pickedItems[pickedItemsNumber] = pickedItem;

				++pickedItemsNumber;
			} while (pickedItemsNumber < airdropItemGroup.MaximumAmountInLoot);

			return pickedItems;
		}
	}

	public sealed class AirdropItemGroup
	{
		public string Name { get; set; }
		public int MaximumAmountInLoot { get; set; }
		public List<AirdropItemSetting> ItemSettings { get; set; }
	}

	public struct AirdropItemSetting
	{
		public string Name { get; set; }
		public float ChanceInPercent { get; set; }
		public int MinAmount { get; set; }
		public int MaxAmount { get; set; }
	}

	public sealed class CommonSettings
	{
		public const string DefaultNotifyOnPlaneSpawnedMessage = "Cargo Plane has been spawned.";
		public const string DefaultNotifyOnPlaneRemovedMessage = "Cargo Plane has been removed, due to insufficient player count: {0}.";
		public const string DefaultNotifyOnDropStartedMessage = "Supply Drop {0} has been spawned at {1},{2},{3}.";
		public const string DefaultNotifyOnPlayerLootingStartedMessage = "Player {0} started looting the Supply Drop {1}.";
		private const string DefaultNotifyOnCollisionMessage = "Supply drop {0} has landed at {1},{2},{3}";
		private const string DefaultNotifyOnDespawnMessage = "Supply drop {0} has been despawned at {1},{2},{3}";

		public TimeSpan DropFrequency { get; set; }
		public int MinimumPlayerCount { get; set; }
		public Boolean ConsoleStartOnly { get; set; }
		public TimeSpan SupplyCrateDespawnTime { get; set; }

		public Boolean NotifyOnPlaneSpawned { get; set; }
		public string NotifyOnPlaneSpawnedMessage { get; set; }

		public Boolean NotifyOnPlaneRemoved { get; set; }
		public string NotifyOnPlaneRemovedMessage { get; set; }

		public Boolean NotifyOnDropStarted { get; set; }
		public string NotifyOnDropStartedMessage { get; set; }

		public Boolean NotifyOnPlayerLootingStarted { get; set; }
		public string NotifyOnPlayerLootingStartedMessage { get; set; }

		public string NotifyOnCollisionMessage { get; set; }
		public bool NotifyOnCollision { get; set; }

		public string NotifyOnDespawnMessage { get; set; }
		public bool NotifyOnDespawn { get; set; }

		public CommonSettings()
		{
			NotifyOnPlaneSpawned = false;
			NotifyOnPlaneSpawnedMessage = DefaultNotifyOnPlaneSpawnedMessage;

			NotifyOnPlaneRemoved = false;
			NotifyOnPlaneRemovedMessage = DefaultNotifyOnPlaneRemovedMessage;

			NotifyOnDropStarted = false;
			NotifyOnDropStartedMessage = DefaultNotifyOnDropStartedMessage;

			NotifyOnPlayerLootingStarted = false;
			NotifyOnPlayerLootingStartedMessage = DefaultNotifyOnPlayerLootingStartedMessage;

			NotifyOnCollisionMessage = DefaultNotifyOnCollisionMessage;
			NotifyOnCollision = false;

			NotifyOnDespawnMessage = DefaultNotifyOnDespawnMessage;
			NotifyOnDespawn = false;
		}

		public static CommonSettings CreateDefault()
		{
			return new CommonSettings
			{
				DropFrequency = TimeSpan.FromHours(1),
				MinimumPlayerCount = 25,
				ConsoleStartOnly = false,
				SupplyCrateDespawnTime = TimeSpan.FromMinutes(5),
				NotifyOnPlaneSpawned = false,
				NotifyOnPlaneSpawnedMessage = DefaultNotifyOnPlaneSpawnedMessage,
				NotifyOnPlaneRemoved = false,
				NotifyOnPlaneRemovedMessage = DefaultNotifyOnPlaneRemovedMessage,
				NotifyOnDropStarted = false,
				NotifyOnDropStartedMessage = DefaultNotifyOnDropStartedMessage,
				NotifyOnPlayerLootingStarted = false,
				NotifyOnPlayerLootingStartedMessage = DefaultNotifyOnPlayerLootingStartedMessage,
				NotifyOnCollision = false,
				NotifyOnCollisionMessage = DefaultNotifyOnCollisionMessage,
				NotifyOnDespawn = false,
				NotifyOnDespawnMessage = DefaultNotifyOnDespawnMessage
			};
		}

		public override string ToString()
		{
			return string.Format("DropFrequency: {0}, MinimumPlayerCount: {1}, ConsoleStartOnly: {2}, SupplyCrateDespawnTime: {3}, \n NotifyOnPlaneSpawned: {4}, NotifyOnPlaneSpawnedMessage: {5},\n NotifyOnPlaneRemoved: {6}, NotifyOnPlaneRemovedMessage: {7},\n NotifyOnDropStarted: {8}, NotifyOnDropStartedMessage: {9}, \n NotifyOnPlayerLootingStarted: {10}, NotifyOnPlayerLootingStartedMessage: {11}", DropFrequency, MinimumPlayerCount, ConsoleStartOnly, SupplyCrateDespawnTime, NotifyOnPlaneSpawned, NotifyOnPlaneSpawnedMessage, NotifyOnPlaneRemoved, NotifyOnPlaneRemovedMessage, NotifyOnDropStarted, NotifyOnDropStartedMessage, NotifyOnPlayerLootingStarted, NotifyOnPlayerLootingStartedMessage);
		}
	}

	public sealed class AirdropSettingsValidator
	{
		public static void Validate(AirdropSettings settings)
		{
			if (settings.ItemsByGroups == null)
				settings.ItemsByGroups = AirdropSettingsFactory.CreateDefault().ItemsByGroups;

			var countOfItems = settings.ItemsByGroups.Sum(g => g.MaximumAmountInLoot);
			var diff = countOfItems - AirdropSettings.MaxCapacity;
			if (diff > 0)
				AdjustGroupMaxAmount(settings.ItemsByGroups, diff);
		}

		private static void AdjustGroupMaxAmount(List<AirdropItemGroup> value, int diff)
		{
			Diagnostics.Diagnostics.MessageToServer("adjusting group amount {0}");
			var groupsOrderedByDescending = value.OrderByDescending(g => g.MaximumAmountInLoot);
			for (var i = diff; i > 0; i--)
			{
				var airdropItemGroup = groupsOrderedByDescending.Skip(diff - i).Take(1).First();
				value.First(g => g.Name == airdropItemGroup.Name).MaximumAmountInLoot--;
			}
		}
	}

	public sealed class AirdropSettingsFactory
	{
		private static readonly Dictionary<string, Func<ItemDefinition, int[]>> DefaultAmountByCategoryMapping = new Dictionary
			<string, Func<ItemDefinition, int[]>>
			{
				{"Food", GenerateAmountMappingForFood},
				{"Attire", def => new[] {1, 1}},
				{"Items", def => new[] {1, 1}},
				{"Ammunition", def => new[] {32, 48}},
				{"Misc", def => new[] {1, 1}},
				{"Construction", GenerateAmountMappingForConstruction},
				{"Medical", GenerateMappingForMedical},
				{"Tool", GenerateMappingForTool},
				{"Traps", def => new[] {1, 1}},
				{"Weapon", def => new[] {1, 1}},
				{"Resources", GenerateMappingForResource},
				{"Blueprint", def => new[] {1, 1}}
			};

		private static int[] GenerateAmountMappingForFood(ItemDefinition itemDefinition)
		{
			var singleStackItems = new[] { "smallwaterbottle" };
			var spoiledFood = new[]
				{
					"wolfmeat_spoiled", "wolfmeat_burned", "humanmeat_spoiled", "humanmeat_burned", "chicken_spoiled", "chicken_burned"
				};
			if (singleStackItems.Contains(itemDefinition.shortname))
				return new[] { 1, 1 };
			if (spoiledFood.Contains(itemDefinition.shortname))
				return new[] { 0, 0 };

			return new[] { 5, 10 };
		}

		private static int[] GenerateAmountMappingForConstruction(ItemDefinition itemDefinition)
		{
			var blueprint = ItemManager.FindBlueprint(itemDefinition);
			if (blueprint == null)
				return new[] { 0, 0 };
			if (itemDefinition.shortname.Equals("lock.key", StringComparison.OrdinalIgnoreCase))
				return new[] { 0, 0 };
			return new[] { 1, 1 };
		}

		private static int[] GenerateMappingForMedical(ItemDefinition itemDefinition)
		{
			var largeStackItems = new[] { "antiradpills", "blood" };
			return largeStackItems.Contains(itemDefinition.shortname)
				? new[] { 5, 10 }
				: new[] { 1, 1 };
		}

		private static int[] GenerateMappingForTool(ItemDefinition itemDefinition)
		{
			var largeAmountItems = new[] { "explosive.timed" };
			var defaultItems = new[] { "rock", "torch", "camera_tool" };

			if (largeAmountItems.Contains(itemDefinition.shortname))
				return new[] { 3, 5 };
			if (defaultItems.Contains(itemDefinition.shortname))
				return new[] { 0, 0 };

			return new[] { 1, 1 };
		}

		private static int[] GenerateMappingForResource(ItemDefinition itemDefinition)
		{
			var filterItems = new[] { "metal_refined", "tool_camera" };
			var largeStackItems = new[] { "wood", "sulfur_ore", "sulfur", "stones", "metal_ore", "metal_fragments", "fat_animal", "cloth", "bone_fragments" };
			var smallStackItems = new[] { "paper", "gunpowder", "lowgradefuel", "explosives", "can_tuna_empty", "can_beans_empty" };
			var zeroStackItems = new[] { "skull_wolf", "skull_human", "water", "salt_water", "charcoal", "battery_small" };

			if (filterItems.Contains(itemDefinition.shortname))
				return new[] { 0, 0 };
			if (largeStackItems.Contains(itemDefinition.shortname))
				return new[] { 750, 1000 };
			if (smallStackItems.Contains(itemDefinition.shortname))
				return new[] { 25, 50 };
			if (zeroStackItems.Contains(itemDefinition.shortname))
				return new[] { 0, 0 };
			return new[] { 750, 1000 };
		}

		private static readonly Dictionary<string, int> DefaultCategoryAmountMapping = new Dictionary<string, int>
			{
				{"Food", 1},
				{"Attire", 4},
				{"Items", 3},
				{"Ammunition", 2},
				{"Construction", 3},
				{"Medical", 2},
				{"Tool", 2},
				{"Traps", 1},
				{"Misc", 0},
				{"Weapon", 2},
				{"Resources", 3},
				{"Blueprint", 2}
			};

		public const string TemplatePath = "";

		public AirdropSettings LoadFrom(string settingsName)
		{
			Diagnostics.Diagnostics.MessageToServer("loading file with:{0}", settingsName);
			var fileName = "airdropExtended_" + settingsName;
			Diagnostics.Diagnostics.MessageToServer("loading template:{0}", fileName);
			return Interface.GetMod().DataFileSystem.ReadObject<AirdropSettings>(fileName);
		}

		public void SaveTo(string settingsName, AirdropSettings airdropSettings)
		{
			var fileName = "airdropExtended_" + settingsName;
			Diagnostics.Diagnostics.MessageToServer("saving template:{0}", fileName);
			Interface.GetMod().DataFileSystem.WriteObject(fileName, airdropSettings);
		}

		public static AirdropSettings CreateDefault()
		{
			var itemGroups = GenerateDefaultItemGroups();
			return new AirdropSettings
			{
				ItemsByGroups = itemGroups,
				Capacity = AirdropSettings.MaxCapacity,
				Settings = CommonSettings.CreateDefault()
			};
		}

		private static List<AirdropItemGroup> GenerateDefaultItemGroups()
		{
			var itemGroups = ItemManager
				.GetItemDefinitions()
				.GroupBy(i => i.category)
				.Select(group =>
				{
					var categoryName = group.Key.ToString();
					return new AirdropItemGroup
					{
						Name = categoryName,
						ItemSettings = group
							.Select(itemDefinition =>
							{
								var amountMappingArray = DefaultAmountByCategoryMapping[categoryName](itemDefinition);
								return new AirdropItemSetting
								{

									ChanceInPercent = CalculateChanceByRarity(itemDefinition.rarity),
									Name = itemDefinition.shortname,
									MinAmount = amountMappingArray[0],
									MaxAmount = amountMappingArray[1]
								};
							})
							.ToList(),
						MaximumAmountInLoot = DefaultCategoryAmountMapping[categoryName]
					};
				})
				.ToList();
			var blueprintItemGroup = GenerateBlueprintItemGroup();
			itemGroups.Add(blueprintItemGroup);
			return itemGroups;
		}

		private static float CalculateChanceByRarity(Rarity rarity)
		{
			return 100 - ((int)rarity + 1) * 12.5f;
		}

		private static AirdropItemGroup GenerateBlueprintItemGroup()
		{
			var notDefaultBlueprints = ItemManager.Instance.bpList.Where(bp => !bp.defaultBlueprint);
			var bpItems = notDefaultBlueprints.Select(b => b.targetItem).ToList();
			return new AirdropItemGroup
			{
				ItemSettings = bpItems.Select(itemDef => new AirdropItemSetting
				{
					Name = itemDef.shortname,
					MinAmount = 1,
					MaxAmount = 1,
					ChanceInPercent = CalculateChanceByRarity(itemDef.rarity)
				}).ToList(),
				MaximumAmountInLoot = 2,
				Name = "Blueprint"
			};
		}
	}
}
