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

using System.Globalization;
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

			var supplyDrops = UnityEngine.Object.FindObjectsOfType<SupplyDrop>();
			foreach (var supplyDrop in supplyDrops)
			{
				var despawnBehavior = supplyDrop.GetComponent<DespawnBehavior>();
				if (despawnBehavior == null)
					continue;

				Diagnostics.MessageToServer("Adding despawn behavior to supply drop:{0}", supplyDrop.net.ID);
				AddDespawnBehavior(supplyDrop);
				despawnBehavior.Despawn();
			}
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
			ApplySettings();
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

			Diagnostics.MessageToServer(_settings.Settings.ToString());
		}

		private void ApplySettings()
		{
			if (_settings.Settings.DropFrequency <= TimeSpan.Zero || _settings.Settings.ConsoleStartOnly)
				return;
			StartAirdropTimer();
		}

		private void StartAirdropTimer()
		{
			var dropFrequencyInSeconds = Convert.ToSingle(_settings.Settings.DropFrequency.TotalSeconds);
			_aidropTimer = timer.Repeat(dropFrequencyInSeconds, 0, SpawnPlane);
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
				lootInventory.GetComponent<BasePlayer>().userID,
				supplyDrop.net.ID);
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
			supplyDrop.inventory.capacity = _settings.Capacity;

			if (itemContainer == null || itemContainer.itemList == null)
				return;

			itemContainer.itemList.Clear();
			var itemList = _settings.CreateItemList();

			Diagnostics.MessageToServer("capacity:{0}", itemContainer.capacity);
			foreach (var item in itemList)
			{
				Diagnostics.MessageToServer("aire: add item {0}, amount {1} to airdrop container", item.info.name, item.amount);
				item.MoveToContainer(itemContainer, -1, false);
			}

			var x = entity.transform.position.x;
			var y = entity.transform.position.y;
			var z = entity.transform.position.z;

			Diagnostics.MessageTo(_settings.Settings.NotifyOnDropStartedMessage, _settings.Settings.NotifyOnDropStarted, entity.net.ID, x, y, z);

			AddCollisionBehaviorTo(entity);
			AddDespawnBehavior(entity);
		}

		private void AddCollisionBehaviorTo(SupplyDrop supplyDrop)
		{
			supplyDrop.gameObject.AddComponent<CollisionCheck>();


			var dropCollisionCheck = supplyDrop.gameObject.GetComponent<CollisionCheck>();


			dropCollisionCheck.NotifyOnCollision = _settings.Settings.NotifyOnCollision;
			dropCollisionCheck.NotifyOnCollisionMessage = _settings.Settings.NotifyOnCollisionMessage;
		}

		private void AddDespawnBehavior(SupplyDrop supplyDrop)
		{
			if (_settings.Settings.SupplyCrateDespawnTime <= TimeSpan.Zero)
				return;

			var timeoutInSeconds = Convert.ToSingle(_settings.Settings.SupplyCrateDespawnTime.TotalSeconds);

			supplyDrop.gameObject.AddComponent<DespawnBehavior>();
			var despawnBehavior = supplyDrop.gameObject.GetComponent<DespawnBehavior>();

			despawnBehavior.TimeoutInSeconds = timeoutInSeconds;
			despawnBehavior.NotifyOnDespawn = _settings.Settings.NotifyOnDespawn;
			despawnBehavior.NotifyOnDespawnMessage = _settings.Settings.NotifyOnDespawnMessage;
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

		private void SpawnPlane()
		{
			if (_settings.Settings.ConsoleStartOnly)
				return;
			Diagnostics.MessageToServer("plane spawned by timer");
			var plane = GameManager.server.CreateEntity("events/cargo_plane", new Vector3(), new Quaternion());
			if (plane != null)
				plane.Spawn();
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
		// ReSharper disable once UnusedParameter.Local
		void ReloadConfigConsole(ConsoleSystem.Arg arg)
		{
			Diagnostics.MessageToServer("aire:Reloading settings");
			Cleanup();
			Initialize();
		}

		[ChatCommand("aire.reload")]
		void ReloadConfigChat(BasePlayer player, string command, string[] args)
		{
			if (!HasPermission(player, "aire.canReload"))
				return;

			Diagnostics.MessageToServer("aire: reload called by {0}", player.name);
			ReloadConfigConsole(new ConsoleSystem.Arg(command));
		}

		private bool HasPermission(BasePlayer player, string permissionName)
		{
			if (player == null)
				return false;
			var uid = Convert.ToString(player.userID);
			if (permission.UserHasPermission(uid, permissionName))
				return true;

			Diagnostics.MessageToPlayer(player, "You have no permission to use this command!");
			return false;
		}

		[ConsoleCommand("aire.load")]
		void LoadConfigConsole(ConsoleSystem.Arg arg)
		{
			if (!arg.HasArgs())
			{
				Diagnostics.MessageToServer("aire:Command use aire.load my_settings_name");
				return;
			}

			var settingsName = arg.Args[0];
			if (string.IsNullOrEmpty(settingsName))
			{
				Diagnostics.MessageToServer("aire:Command use aire.load my_settings_name");
				return;
			}

			Diagnostics.MessageToServer("aire:Loading settings: {0}", settingsName);
			Cleanup();
			PluginSettingsManager.Save(this, settingsName);
			Initialize();
		}

		[ChatCommand("aire.load")]
		void LoadConfigChat(BasePlayer player, string command, string[] args)
		{
			if (!HasPermission(player, "aire.canLoad"))
				return;

			Diagnostics.MessageToServer("aire: load called by {0}", player.name);
			LoadConfigConsole(new ConsoleSystem.Arg(command));
		}

		[ChatCommand("aire.save")]
		void SaveConfigChat(BasePlayer player, string command, string[] args)
		{
			if (!HasPermission(player, "aire.canLoad"))
				return;

			Diagnostics.MessageToServer("aire: save called by {0}", player.name);
			SaveConfigConsole(new ConsoleSystem.Arg(command));
		}

		[ConsoleCommand("aire.save")]
		void SaveConfigConsole(ConsoleSystem.Arg arg)
		{
			var settingsName = !arg.HasArgs()
				? string.Empty
				: arg.Args[0];
			settingsName = string.IsNullOrEmpty(settingsName)
				? _settingsName
				: settingsName;

			AirdropSettingsFactory.SaveTo(settingsName, _settings);
			PluginSettingsManager.Save(this, settingsName);
		}

		[ConsoleCommand("aire.generate")]
		void GenerateConfigConsole(ConsoleSystem.Arg arg)
		{
			if (!arg.HasArgs())
			{
				Diagnostics.MessageToServer("aire:Command use aire.generate my_settings_name");
				return;
			}

			var settingsName = arg.GetString(0);
			if (string.IsNullOrEmpty(settingsName))
			{
				Diagnostics.MessageToServer("aire:Command use aire.generate my_settings_name");
				return;
			}

			Diagnostics.MessageToServer("aire:Generate default settings to {0}", settingsName);
			var settings = AirdropSettingsFactory.CreateDefault();
			AirdropSettingsFactory.SaveTo(settingsName, settings);
		}

		[ChatCommand("aire.generate")]
		void GenerateConfigChat(BasePlayer player, string command, string[] args)
		{
			if (!HasPermission(player, "aire.canGenerate"))
				return;

			Diagnostics.MessageToServer("aire: generate called by {0}", player.name);
			GenerateConfigConsole(new ConsoleSystem.Arg(command));
		}

		[ConsoleCommand("aire.freq")]
		void SetFrequencyConsole(ConsoleSystem.Arg arg)
		{
			if (!arg.HasArgs())
			{
				Diagnostics.MessageToServer("aire:Command use: aire.frequency 100");
				return;
			}

			var frequency = arg.GetInt(0);
			_settings.Settings.DropFrequency = TimeSpan.FromSeconds(frequency);
			Diagnostics.MessageToServer("aire:Set frequency to {0} seconds", _settings.Settings.DropFrequency.TotalSeconds);

			Cleanup();
			ApplySettings();
		}

		[ChatCommand("aire.freq")]
		void SetFrequencyChat(BasePlayer player, string command, string[] args)
		{
			if (!HasPermission(player, "aire.canFreq"))
				return;

			Diagnostics.MessageToServer("aire: freq called by {0}", player.name);
			SetFrequencyConsole(new ConsoleSystem.Arg(command));
		}

		[ConsoleCommand("aire.players")]
		void SetMinimumPlayerCountConsole(ConsoleSystem.Arg arg)
		{
			if (!arg.HasArgs())
			{
				Diagnostics.MessageToServer("aire: Command use: aire.players 100");
				return;
			}

			var minimumPlayerCount = arg.GetInt(0);
			_settings.Settings.MinimumPlayerCount = minimumPlayerCount;
			Diagnostics.MessageToServer("aire:Set minimumPlayerCount to {0}", _settings.Settings.MinimumPlayerCount);

			Cleanup();
			ApplySettings();
		}

		[ChatCommand("aire.players")]
		void SetMinimumPlayerCountChat(BasePlayer player, string command, string[] args)
		{
			if (!HasPermission(player, "aire.canPlayers"))
				return;

			Diagnostics.MessageToServer("aire: players called by {0}", player.name);
			SetMinimumPlayerCountConsole(new ConsoleSystem.Arg(command));
		}

		[ConsoleCommand("aire.console")]
		void SetConsoleStartOnlyConsole(ConsoleSystem.Arg arg)
		{
			if (!arg.HasArgs())
			{
				Diagnostics.MessageToServer("aire:Command use: aire.console true");
				return;
			}

			var consoleStartOnly = arg.GetBool(0);

			Diagnostics.MessageToServer("aire:Set ConsoleStartOnly to {0}", consoleStartOnly);
			_settings.Settings.ConsoleStartOnly = consoleStartOnly;

			Cleanup();
			ApplySettings();
		}

		[ChatCommand("aire.console")]
		void SetConsoleStartOnlyChat(BasePlayer player, string command, string[] args)
		{
			if (!HasPermission(player, "aire.canConsole"))
				return;

			Diagnostics.MessageToServer("aire: console called by {0}", player.name);
			SetConsoleStartOnlyConsole(new ConsoleSystem.Arg(command));
		}

		[ConsoleCommand("aire.despawn")]
		void SetSupplyCrateDespawnTimeConsole(ConsoleSystem.Arg arg)
		{
			if (!arg.HasArgs())
			{
				Diagnostics.MessageToServer("aire:Command use: aire.despawn 100");
				return;
			}

			var despawnTimeInSeconds = arg.GetInt(0);
			_settings.Settings.SupplyCrateDespawnTime = TimeSpan.FromSeconds(despawnTimeInSeconds);
			Diagnostics.MessageToServer("aire:Set SupplyCrateDespawnTime to {0} seconds", _settings.Settings.SupplyCrateDespawnTime.TotalSeconds);

			Cleanup();
			ApplySettings();
		}

		[ChatCommand("aire.despawn")]
		void SetSupplyCrateDespawnTimeChat(BasePlayer player, string command, string[] args)
		{
			if (!HasPermission(player, "aire.canDespawn"))
				return;

			Diagnostics.MessageToServer("aire: despawn called by {0}", player.name);
			SetSupplyCrateDespawnTimeConsole(new ConsoleSystem.Arg(command));
		}

		[ConsoleCommand("aire.set")]
		void SetItemSettingsConsole(ConsoleSystem.Arg arg)
		{
			if (!arg.HasArgs())
			{
				PrintSetItemSettingsUsage();
				return;
			}

			string itemName;
			float chance;
			int minAmount;
			int maxAmount;

			try
			{
				itemName = arg.GetString(0);
				chance = arg.GetFloat(1);
				minAmount = arg.GetInt(2);
				maxAmount = arg.GetInt(3);
			}
			catch (Exception)
			{
				PrintSetItemSettingsUsage();
				return;
			}

			foreach (var group in _settings.ItemsByGroups)
			{
				var item = group.ItemSettings.FirstOrDefault(f => f.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase));
				if (item == null)
					continue;

				item.ChanceInPercent = chance;
				item.MinAmount = minAmount;
				item.MaxAmount = maxAmount;

				Diagnostics.MessageToServer(
				"aire:Set settings to item:{0}, chance:{1}, min_amount:{2}, max_amount:{3}",
				itemName,
				chance,
				minAmount,
				maxAmount);
			}
		}

		private static void PrintSetItemSettingsUsage()
		{
			Diagnostics.MessageToServer("aire:Command use: aire.set item_name [chance] [min] [max].");
			Diagnostics.MessageToServer("aire:Example: aire.set rocket_launcher 15 1 1.");
			Diagnostics.MessageToServer("aire:default chance=0, min=0, max=0.");
		}

		[ChatCommand("aire.set")]
		void SetItemSettingsChat(BasePlayer player, string command, string[] args)
		{
			if (!HasPermission(player, "aire.canSet"))
				return;

			Diagnostics.MessageToServer("aire: set called by {0}", player.name);
			SetItemSettingsConsole(new ConsoleSystem.Arg(command));
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
		private const string DefaultNotifyOnCollisionMessage = "Supply drop {0} has landed at {1},{2},{3}";

		private bool _isTriggered;

		public string NotifyOnCollisionMessage { get; set; }
		public bool NotifyOnCollision { get; set; }

		public CollisionCheck()
		{
			NotifyOnCollisionMessage = DefaultNotifyOnCollisionMessage;
			NotifyOnCollision = true;
		}

		// ReSharper disable once UnusedMember.Local
		// ReSharper disable once UnusedParameter.Local
		void OnCollisionEnter(UnityEngine.Collision col)
		{
			if (_isTriggered)
				return;

			_isTriggered = true;

			var baseEntity = GetComponent<BaseEntity>();
			if (baseEntity.model != null)
			{
				Diagnostics.Diagnostics.MessageToServer("name:{0}", baseEntity.model.name);
				Diagnostics.Diagnostics.MessageToServer("tag:{0}", baseEntity.model.tag);
			}

			Diagnostics.Diagnostics.MessageToServer("baseentity name:{0}", baseEntity.name);

			var landedX = baseEntity.transform.localPosition.x;
			var landedY = baseEntity.transform.localPosition.y;
			var landedZ = baseEntity.transform.localPosition.z;

			Diagnostics.Diagnostics.MessageTo(NotifyOnCollisionMessage, NotifyOnCollision, baseEntity.net.ID, landedX, landedY, landedZ);

			var despawnBehavior = baseEntity.GetComponent<DespawnBehavior>();
			if (despawnBehavior != null)
				despawnBehavior.Despawn();
		}
	}

	public sealed class DespawnBehavior : MonoBehaviour
	{
		private const string DefaultNotifyOnDespawnMessage = "Supply drop {0} has been despawned at {1},{2},{3}";
		private const float DefaultSupplyStayTime = 300.0f;

		private bool _isTriggered;

		public float TimeoutInSeconds { get; set; }

		public string NotifyOnDespawnMessage { get; set; }
		public bool NotifyOnDespawn { get; set; }

		public DespawnBehavior()
		{
			TimeoutInSeconds = DefaultSupplyStayTime;
			NotifyOnDespawnMessage = DefaultNotifyOnDespawnMessage;
			NotifyOnDespawn = false;
		}

		public void Despawn()
		{
			if (_isTriggered)
				return;

			var baseEntity = GetComponent<BaseEntity>();

			Diagnostics.Diagnostics.MessageToServer("starting timer:{0}", TimeoutInSeconds);
			_isTriggered = true;

			Interface.Oxide.GetLibrary<Timer>("Timer").Once(TimeoutInSeconds, () =>
			{
				Diagnostics.Diagnostics.MessageToServer("destruction callback has been called");
				Diagnostics.Diagnostics.MessageToServer("destruction callback called for {0}", baseEntity.net.ID);
				var x = baseEntity.transform.position.x;
				var y = baseEntity.transform.position.y;
				var z = baseEntity.transform.position.z;

				Diagnostics.Diagnostics.MessageTo(NotifyOnDespawnMessage, NotifyOnDespawn, baseEntity.net.ID, x, y, z);
				baseEntity.KillMessage();
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
		public const int MaxCapacity = 18;

		private int _capacity = MaxCapacity;

		public AirdropSettings()
		{
			Capacity = MaxCapacity;
		}

		public int Capacity
		{
			get { return _capacity; }
			set
			{
				_capacity = value > MaxCapacity || value < 0
					? MaxCapacity
					: value;
			}
		}

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

				var pickedItem = ItemManager.CreateByName(airdropItemSetting.Name, amount);
				if (airdropItemGroup.Name.Equals("Blueprint", StringComparison.OrdinalIgnoreCase))
					pickedItem.isBlueprint = true;
				pickedItems[pickedItemsNumber] = pickedItem;

				++pickedItemsNumber;
			} while (pickedItemsNumber < airdropItemGroup.MaximumAmountInLoot);

			return pickedItems;
		}
	}

	public sealed class AirdropItemGroup
	{
		private int _maximumAmountInLoot;
		public string Name { get; set; }

		public int MaximumAmountInLoot
		{
			get { return _maximumAmountInLoot; }
			set { _maximumAmountInLoot = value < 0 ? 0 : value; }
		}

		public List<AirdropItemSetting> ItemSettings { get; set; }
	}

	public class AirdropItemSetting
	{
		private float _chanceInPercent;
		private string _name;
		private int _minAmount;
		private int _maxAmount;

		public string Name
		{
			get { return _name; }
			set
			{
				if (string.IsNullOrEmpty(value))
					throw new ArgumentException("Item name should not be null", "value");
				_name = value;
			}
		}

		public float ChanceInPercent
		{
			get { return _chanceInPercent; }
			set { _chanceInPercent = value < 0 ? 0 : value; }
		}

		public int MinAmount
		{
			get { return _minAmount; }
			set
			{
				if (value < 0)
					_minAmount = 0;
				else if (value > MaxAmount)
					_minAmount = MaxAmount;
				else
					_minAmount = value;
			}
		}

		public int MaxAmount
		{
			get { return _maxAmount; }
			set
			{
				if (value < 0)
					_maxAmount = 0;
				else if (value < MinAmount)
					_maxAmount = MinAmount;
				else
					_maxAmount = value;
			}
		}
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
				{"Ammunition", GenerateAmountMappingForAmmunition},
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

		private static int[] GenerateAmountMappingForAmmunition(ItemDefinition def)
		{
			return def.shortname.Contains("rocket", CompareOptions.OrdinalIgnoreCase)
				? new[] { 1, 3 }
				: new[] { 32, 48 };
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
			var defaultItems = new[] { "rock", "torch", "camera" };

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
				{"Attire", 2},
				{"Items", 1},
				{"Ammunition", 1},
				{"Construction", 2},
				{"Medical", 2},
				{"Tool", 2},
				{"Traps", 1},
				{"Misc", 0},
				{"Weapon", 2},
				{"Resources", 2},
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
					int defaultAmount;
					DefaultCategoryAmountMapping.TryGetValue(categoryName, out defaultAmount);
					return new AirdropItemGroup
					{
						Name = categoryName,
						ItemSettings = group
							.Select(itemDefinition =>
							{
								Func<ItemDefinition, int[]> amountFunc;
								DefaultAmountByCategoryMapping.TryGetValue(categoryName, out amountFunc);
								var amountMappingArray = amountFunc == null ? new[] { 0, 0 } : amountFunc(itemDefinition);
								return new AirdropItemSetting
								{

									ChanceInPercent = CalculateChanceByRarity(itemDefinition.rarity),
									Name = itemDefinition.shortname,
									MinAmount = amountMappingArray[0],
									MaxAmount = amountMappingArray[1]
								};
							})
							.ToList(),
						MaximumAmountInLoot = defaultAmount
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
