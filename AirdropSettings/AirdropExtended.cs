using System.Globalization;
using AirdropExtended;
using AirdropExtended.Airdrop.Services;
using AirdropExtended.Airdrop.Settings;
using AirdropExtended.Airdrop.Settings.Generate;
using AirdropExtended.Behaviors;
using AirdropExtended.Commands;
using AirdropExtended.Diagnostics;
using AirdropExtended.Permissions;
using AirdropExtended.WeightedSearch;
using JetBrains.Annotations;
using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using AirdropExtended.PluginSettings;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Libraries;
using Oxide.Plugins;
using Rust;
using UnityEngine;
using Constants = AirdropExtended.Constants;
using LogType = Oxide.Core.Logging.LogType;
using Timer = Oxide.Core.Libraries.Timer;

// ReSharper disable once CheckNamespace

namespace Oxide.Plugins
{
	[Info(Constants.PluginName, "baton", "0.5.3", ResourceId = 1210)]
	[Description("Customizable airdrop")]
	public class AirdropExtended : RustPlugin
	{
		private readonly SettingsContext _settingsContext;
		private readonly AirdropController _airdropController;
		private PluginSettingsRepository _pluginSettingsRepository;
		private Dictionary<string, AirdropExtendedCommand> _commands;

		public AirdropExtended()
		{
			_settingsContext = new SettingsContext();
			_airdropController = new AirdropController(_settingsContext);
		}

		private void OnServerInitialized()
		{
			LoadConfig();
			Bootstrap();
			Save();
		}

		private void Bootstrap()
		{
			_pluginSettingsRepository = new PluginSettingsRepository(Config, SaveConfig);
			Load();

			var commands = CommandFactory.Create(_settingsContext, _pluginSettingsRepository, _airdropController);
			PermissionService.RegisterPermissions(this, commands);

			_commands = commands.ToDictionary(c => c.Name, c => c);
			var consoleSystem = Interface.Oxide.GetLibrary<Command>();
			foreach (var command in commands)
			{
				var command1 = command;
				consoleSystem.AddConsoleCommand(command.Name, this, arg =>
				{
					command1.Execute(arg, null);
					return true;
				});
			}
		}

		private void Load()
		{
			_settingsContext.SettingsName = _pluginSettingsRepository.LoadSettingsName();
			Diagnostics.MessageToServer("Loaded settings:{0}", _settingsContext.SettingsName);
			_settingsContext.Settings = AidropSettingsRepository.LoadFrom(_settingsContext.SettingsName);
			_airdropController.ApplySettings();
		}

		private void Save()
		{
			Diagnostics.MessageToServer("Saving settings:{0}", _settingsContext.SettingsName);
			_pluginSettingsRepository.SaveSettingsName(_settingsContext.SettingsName);
			AidropSettingsRepository.SaveTo(_settingsContext.SettingsName, _settingsContext.Settings);

			SaveConfig();
		}

		private void Unload()
		{
			_airdropController.Cleanup();
			Save();
		}

		void OnPlayerLoot(PlayerLoot lootInventory, BaseEntity targetEntity)
		{
			var supplyDrop = targetEntity as SupplyDrop;
			if (lootInventory == null || targetEntity == null || supplyDrop == null)
				return;

			var commonSettings = _settingsContext.Settings.CommonSettings;

			Diagnostics.MessageTo(
				commonSettings.NotifyOnPlayerLootingStartedMessage,
				commonSettings.NotifyOnPlayerLootingStarted,
				lootInventory.GetComponent<BasePlayer>().displayName,
				supplyDrop.net.ID);
		}

		private void OnEntitySpawned(BaseEntity entity)
		{
			if (_airdropController.IsInitialized())
				_airdropController.OnEntitySpawned(entity);
		}

		private void OnWeaponThrown(BasePlayer player, BaseEntity entity)
		{
			if (entity is SupplySignal)
				_airdropController.OnSupplySignal(player, entity);
		}

		#region command handlers

		[ChatCommand("aire.load")]
		private void LoadSettingsChatCommand(BasePlayer player, string command, string[] args)
		{
			_commands["aire.load"].ExecuteFromChat(player, command, args);
		}

		[ChatCommand("aire.save")]
		private void SaveSettingsChatCommand(BasePlayer player, string command, string[] args)
		{
			_commands["aire.save"].ExecuteFromChat(player, command, args);
		}

		[ChatCommand("aire.generate")]
		private void GenerateSettingsChatCommand(BasePlayer player, string command, string[] args)
		{
			_commands["aire.generate"].ExecuteFromChat(player, command, args);
		}

		[ChatCommand("aire.reload")]
		private void ReloadSettingsChatCommand(BasePlayer player, string command, string[] args)
		{
			_commands["aire.reload"].ExecuteFromChat(player, command, args);
		}

		[ChatCommand("aire.players")]
		private void SetPlayersChatCommand(BasePlayer player, string command, string[] args)
		{
			_commands["aire.players"].ExecuteFromChat(player, command, args);
		}

		[ChatCommand("aire.event")]
		private void SetEventEnabledChatCommand(BasePlayer player, string command, string[] args)
		{
			_commands["aire.event"].ExecuteFromChat(player, command, args);
		}

		[ChatCommand("aire.supply")]
		private void SetSupplyEnabledChatCommand(BasePlayer player, string command, string[] args)
		{
			_commands["aire.supply"].ExecuteFromChat(player, command, args);
		}

		[ChatCommand("aire.timer")]
		private void SetTimerEnabledChatCommand(BasePlayer player, string command, string[] args)
		{
			_commands["aire.timer"].ExecuteFromChat(player, command, args);
		}

		[ChatCommand("aire.despawntime")]
		private void SetDespawnTimeChatCommand(BasePlayer player, string command, string[] args)
		{
			_commands["aire.despawntime"].ExecuteFromChat(player, command, args);
		}

		[ChatCommand("aire.minfreq")]
		private void SetMinFrequencyChatCommand(BasePlayer player, string command, string[] args)
		{
			_commands["aire.minfreq"].ExecuteFromChat(player, command, args);
		}

		[ChatCommand("aire.maxfreq")]
		private void SetMaxFrequencyChatCommand(BasePlayer player, string command, string[] args)
		{
			_commands["aire.maxfreq"].ExecuteFromChat(player, command, args);
		}

		[ChatCommand("aire.setitem")]
		private void SetItemChatCommand(BasePlayer player, string command, string[] args)
		{
			_commands["aire.setitem"].ExecuteFromChat(player, command, args);
		}

		[ChatCommand("aire.setitemgroup")]
		private void SetItemGroupChatCommand(BasePlayer player, string command, string[] args)
		{
			_commands["aire.setitemgroup"].ExecuteFromChat(player, command, args);
		}

		[ChatCommand("aire.capacity")]
		private void SetDropCapacityChatCommand(BasePlayer player, string command, string[] args)
		{
			_commands["aire.capacity"].ExecuteFromChat(player, command, args);
		}

		#endregion
	}
}

namespace AirdropExtended
{
	public sealed class Constants
	{
		public const string PluginName = "AirdropExtended";
	}

	public sealed class SettingsContext
	{
		public string SettingsName { get; set; }
		public AirdropSettings Settings { get; set; }
	}
}

namespace AirdropExtended.Behaviors
{
	public sealed class CollisionCheckBehavior : MonoBehaviour
	{
		private const string DefaultNotifyOnCollisionMessage = "Supply drop {0} has landed at {1},{2},{3}";

		private bool _isTriggered;

		public string NotifyOnCollisionMessage { get; set; }
		public bool NotifyOnCollision { get; set; }

		public CollisionCheckBehavior()
		{
			NotifyOnCollisionMessage = DefaultNotifyOnCollisionMessage;
			NotifyOnCollision = true;
		}

		void OnCollisionEnter(Collision col)
		{
			if (_isTriggered)
				return;

			_isTriggered = true;

			var baseEntity = GetComponent<BaseEntity>();

			var landedX = baseEntity.transform.localPosition.x;
			var landedY = baseEntity.transform.localPosition.y;
			var landedZ = baseEntity.transform.localPosition.z;

			Diagnostics.Diagnostics.MessageTo(NotifyOnCollisionMessage, NotifyOnCollision, baseEntity.net.ID, landedX, landedY, landedZ);

			var despawnBehavior = baseEntity.GetComponent<DespawnBehavior>();
			if (despawnBehavior != null)
				despawnBehavior.Despawn();
		}

		public static void AddTo(BaseEntity entity, CommonSettings settings)
		{
			if (entity == null) throw new ArgumentNullException("entity");
			if (settings == null) throw new ArgumentNullException("settings");
			entity.gameObject.AddComponent<CollisionCheckBehavior>();

			var dropCollisionCheck = entity.gameObject.GetComponent<CollisionCheckBehavior>();

			dropCollisionCheck.NotifyOnCollision = settings.NotifyOnCollision;
			dropCollisionCheck.NotifyOnCollisionMessage = settings.NotifyOnCollisionMessage;
		}

	}

	public sealed class DespawnBehavior : MonoBehaviour
	{
		private const string DefaultNotifyOnDespawnMessage = "Supply drop {0} has been despawned at {1},{2},{3}";
		private const float DefaultSupplyStayTime = 300.0f;

		private bool _isTriggered;
		private Timer.TimerInstance _timerInstance;
		private BaseEntity _baseEntity;

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

			_baseEntity = gameObject.GetComponent<BaseEntity>();
			_isTriggered = true;

			_timerInstance = Interface.Oxide.GetLibrary<Timer>("Timer").Once(TimeoutInSeconds, DespawnCallback);
		}

		private void DespawnCallback()
		{
			var x = _baseEntity.transform.position.x;
			var y = _baseEntity.transform.position.y;
			var z = _baseEntity.transform.position.z;

			Diagnostics.Diagnostics.MessageTo(NotifyOnDespawnMessage, NotifyOnDespawn, _baseEntity.net.ID, x, y, z);
			_baseEntity.KillMessage();

			OnDestroy();
		}

		void OnDestroy()
		{
			_baseEntity = null;

			if (_timerInstance == null || _timerInstance.Destroyed)
				return;

			_timerInstance.Destroy();
			_timerInstance = null;
			_isTriggered = false;
		}

		public static void AddTo(BaseEntity supplyDrop, CommonSettings settings)
		{
			if (supplyDrop == null) throw new ArgumentNullException("supplyDrop");
			if (settings == null) throw new ArgumentNullException("settings");

			if (settings.SupplyCrateDespawnTime <= TimeSpan.Zero)
				return;

			var timeoutInSeconds = Convert.ToSingle(settings.SupplyCrateDespawnTime.TotalSeconds);

			supplyDrop.gameObject.AddComponent<DespawnBehavior>();
			var despawnBehavior = supplyDrop.gameObject.GetComponent<DespawnBehavior>();

			despawnBehavior.TimeoutInSeconds = timeoutInSeconds;
			despawnBehavior.NotifyOnDespawn = settings.NotifyOnDespawn;
			despawnBehavior.NotifyOnDespawnMessage = settings.NotifyOnDespawnMessage;
		}
	}

	public static class SupplyDropBehaviorService
	{
		public static void AttachCustomBehaviorsToSupplyDrops(AirdropSettings settings)
		{
			if (settings == null)
				throw new ArgumentNullException("settings");

			var supplyDrops = UnityEngine.Object.FindObjectsOfType<SupplyDrop>();

			foreach (var supplyDrop in supplyDrops)
			{
				var despawnBehavior = supplyDrop.GetComponent<DespawnBehavior>();
				if (Equals(despawnBehavior, null))
					DespawnBehavior.AddTo(supplyDrop, settings.CommonSettings);

				despawnBehavior = supplyDrop.GetComponent<DespawnBehavior>();
				var hasParachute = HasParachute(supplyDrop);

				var collisionBehavior = supplyDrop.GetComponent<CollisionCheckBehavior>();
				if (hasParachute && Equals(collisionBehavior, null))
					CollisionCheckBehavior.AddTo(supplyDrop, settings.CommonSettings);

				if (hasParachute)
					continue;

				despawnBehavior.Despawn();
			}
		}

		private static bool HasParachute(SupplyDrop supplyDrop)
		{
			var parachuteField = typeof(SupplyDrop).GetField("parachute",
				Interface.Oxide.GetLibrary<Oxide.Game.Rust.Libraries.Rust>().PrivateBindingFlag());
			if (parachuteField == null)
				return false;

			return parachuteField.GetValue(supplyDrop) != null;
		}

		public static void RemoveCustomBehaviorsFromSupplyDrops()
		{
			var despawnBehaviors = UnityEngine.Object.FindObjectsOfType<DespawnBehavior>();
			if (despawnBehaviors != null && despawnBehaviors.Any())
			{
				foreach (var despawnBehavior in despawnBehaviors)
					UnityEngine.Object.Destroy(despawnBehavior);
			}

			var landBehaviors = UnityEngine.Object.FindObjectsOfType<CollisionCheckBehavior>();
			if (landBehaviors != null && landBehaviors.Any())
			{
				foreach (var landBehavior in landBehaviors)
					UnityEngine.Object.Destroy(landBehavior);
			}
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
			ConsoleSystem.Broadcast("chat.add \"SERVER\" " + string.Format(message, args).QuoteSafe() + " 1.0", new object[0]);
		}

		public static void MessageToServer(string message, params object[] args)
		{
			Interface.GetMod().RootLogger.Write(LogType.Info, string.Format("aire:{0}", message), args);
		}

		public static void MessageToServerAndPlayer(BasePlayer player, string message, params object[] args)
		{
			if (player != null)
				MessageToPlayer(player, message, args);
			MessageToServer(message, args);
		}
	}
}

namespace AirdropExtended.Permissions
{
	public static class PermissionService
	{
		public static Permission Permission = Interface.GetMod().GetLibrary<Permission>();

		public static bool HasPermission(BasePlayer player, string permissionName)
		{
			if (player == null || string.IsNullOrEmpty(permissionName))
				return false;

			var uid = Convert.ToString(player.userID);
			if (Permission.UserHasPermission(uid, permissionName))
				return true;

			Diagnostics.Diagnostics.MessageToPlayer(player, "You have no permission to use this command!");
			return false;
		}

		public static void RegisterPermissions(Plugin owner, List<AirdropExtendedCommand> commands)
		{
			if (owner == null) throw new ArgumentNullException("owner");
			if (commands == null) throw new ArgumentNullException("commands");

			foreach (var permissionName in commands.Select(c => c.PermissionName))
			{
				if (!Permission.PermissionExists(permissionName))
					Permission.RegisterPermission(permissionName, owner);
			}
		}
	}
}

namespace AirdropExtended.Commands
{
	public abstract class AirdropExtendedCommand
	{
		public string Name { get; private set; }
		public string PermissionName { get; private set; }

		protected AirdropExtendedCommand(string name, string permissionName = "")
		{
			if (string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");

			Name = name;
			PermissionName = permissionName;
		}

		public virtual void ExecuteFromChat(BasePlayer player, string command, string[] args)
		{
			if (!PermissionService.HasPermission(player, PermissionName))
			{
				Diagnostics.Diagnostics.MessageToPlayer(player, "You have are required to have permission \"{0}\" to run command:{1}", PermissionName, Name);
				return;
			}

			var commandString = args.Aggregate(command, (s, s1) => s + " " + s1.QuoteSafe());
			Diagnostics.Diagnostics.MessageToServer("'{0}' called by {1}", commandString, player.displayName);
			var commandArgs = new ConsoleSystem.Arg(commandString);
			Execute(commandArgs, player);
		}

		public abstract void Execute(ConsoleSystem.Arg arg, BasePlayer player);

		protected void PrintUsage(BasePlayer player)
		{
			var message = GetUsageString();
			if (player != null)
				Diagnostics.Diagnostics.MessageToPlayer(player, message);
			Diagnostics.Diagnostics.MessageToServer(message);
		}

		protected virtual string GetUsageString()
		{
			return GetDefaultUsageString();
		}

		protected string GetDefaultUsageString(params string[] parameters)
		{
			var parameterString = string.Join(" ", parameters);
			return string.Format("Command use {0} {1}", Name, parameterString);
		}
	}

	public class LoadSettingsCommand : AirdropExtendedCommand
	{
		private readonly SettingsContext _context;
		private readonly AirdropController _controller;
		private readonly PluginSettingsRepository _repository;

		public LoadSettingsCommand(
			SettingsContext context, 
			PluginSettingsRepository repository,
			AirdropController controller)
			: base("aire.load", "aire.canLoad")
		{
			if (context == null) throw new ArgumentNullException("context");
			if (repository == null) throw new ArgumentNullException("repository");
			if (controller == null) throw new ArgumentNullException("controller");

			_context = context;
			_repository = repository;
			_controller = controller;
		}

		public override void Execute(ConsoleSystem.Arg arg, BasePlayer player)
		{
			if (!arg.HasArgs())
			{
				PrintUsage(player);
				return;
			}

			var settingsName = arg.GetString(0);
			if (string.IsNullOrEmpty(settingsName))
			{
				PrintUsage(player);
				return;
			}

			Diagnostics.Diagnostics.MessageToServerAndPlayer(player, "Loading settings: {0}", settingsName);

			_context.SettingsName = settingsName;
			_context.Settings = AidropSettingsRepository.LoadFrom(settingsName);
			_repository.SaveSettingsName(_context.SettingsName);
			_controller.ApplySettings();
		}

		protected override string GetUsageString()
		{
			return GetDefaultUsageString("settingsName");
		}
	}

	public class ReloadSettingsCommand : AirdropExtendedCommand
	{
		private readonly SettingsContext _context;
		private readonly PluginSettingsRepository _repository;
		private readonly AirdropController _controller;

		public ReloadSettingsCommand(
			SettingsContext context,
			PluginSettingsRepository repository,
			AirdropController controller)
			: base("aire.reload", "aire.canReload")
		{
			if (context == null) throw new ArgumentNullException("context");
			if (repository == null) throw new ArgumentNullException("repository");
			if (controller == null) throw new ArgumentNullException("controller");
			_context = context;
			_repository = repository;
			_controller = controller;
		}

		public override void Execute(ConsoleSystem.Arg arg, BasePlayer player)
		{
			Diagnostics.Diagnostics.MessageToServerAndPlayer(player, "Reloading plugin");

			_context.SettingsName = _repository.LoadSettingsName();
			_context.Settings = AidropSettingsRepository.LoadFrom(_context.SettingsName);
			_controller.ApplySettings();
		}

		protected override string GetUsageString()
		{
			return GetDefaultUsageString("settingsName");
		}
	}

	public class SaveSettingsCommand : AirdropExtendedCommand
	{
		private readonly SettingsContext _context;
		private readonly PluginSettingsRepository _pluginSettingsRepository;

		public SaveSettingsCommand(SettingsContext context, PluginSettingsRepository pluginSettingsRepository)
			: base("aire.save", "aire.canSave")
		{
			if (context == null) throw new ArgumentNullException("context");
			if (pluginSettingsRepository == null) throw new ArgumentNullException("pluginSettingsRepository");
			_context = context;
			_pluginSettingsRepository = pluginSettingsRepository;
		}

		public override void Execute(ConsoleSystem.Arg arg, BasePlayer player)
		{
			if (!arg.HasArgs())
			{
				PrintUsage(player);
				return;
			}

			var settingsName = arg.GetString(0);
			if (string.IsNullOrEmpty(settingsName))
			{
				PrintUsage(player);
				return;
			}

			Diagnostics.Diagnostics.MessageToServerAndPlayer(player, "Saving settings to: {0}", settingsName);

			_pluginSettingsRepository.SaveSettingsName(settingsName);
			AidropSettingsRepository.SaveTo(settingsName, _context.Settings);
		}

		protected override string GetUsageString()
		{
			return GetDefaultUsageString("settingsName");
		}
	}

	public class GenerateDefaultSettingsAndSaveCommand : AirdropExtendedCommand
	{
		public GenerateDefaultSettingsAndSaveCommand()
			: base("aire.generate", "aire.canGenerate")
		{ }

		public override void Execute(ConsoleSystem.Arg arg, BasePlayer player)
		{
			if (!arg.HasArgs())
			{
				PrintUsage(player);
				return;
			}

			var settingsName = arg.GetString(0);
			if (string.IsNullOrEmpty(settingsName))
			{
				PrintUsage(player);
				return;
			}

			Diagnostics.Diagnostics.MessageToServerAndPlayer(player, "Generating default settings to {0}", settingsName);

			var settings = AirdropSettingsFactory.CreateDefault();
			AidropSettingsRepository.SaveTo(settingsName, settings);
		}

		protected override string GetUsageString()
		{
			return GetDefaultUsageString("settingsName");
		}
	}

	public class SetDropMinFrequencyCommand : AirdropExtendedCommand
	{
		private readonly SettingsContext _context;
		private readonly AirdropController _controller;

		public SetDropMinFrequencyCommand(SettingsContext context, AirdropController controller)
			: base("aire.minfreq", "aire.canMinFreq")
		{
			if (context == null) throw new ArgumentNullException("context");
			if (controller == null) throw new ArgumentNullException("controller");
			_context = context;
			_controller = controller;
		}

		public override void Execute(ConsoleSystem.Arg arg, BasePlayer player)
		{
			if (!arg.HasArgs())
			{
				PrintUsage(player);
				return;
			}

			var frequency = arg.GetInt(0);
			frequency = frequency < 0 ? 0 : frequency;
			_context.Settings.CommonSettings.MinDropFrequency = TimeSpan.FromSeconds(frequency);

			Diagnostics.Diagnostics.MessageToServerAndPlayer(player, "Setting min frequency to {0}", frequency);
			_controller.ApplySettings();
		}

		protected override string GetUsageString()
		{
			return GetDefaultUsageString("3600");
		}
	}

	public class SetDropMaxFrequencyCommand : AirdropExtendedCommand
	{
		private readonly SettingsContext _context;
		private readonly AirdropController _controller;

		public SetDropMaxFrequencyCommand(SettingsContext context, AirdropController controller)
			: base("aire.maxfreq", "aire.canMaxFreq")
		{
			if (context == null) throw new ArgumentNullException("context");
			if (controller == null) throw new ArgumentNullException("controller");

			_context = context;
			_controller = controller;
		}

		public override void Execute(ConsoleSystem.Arg arg, BasePlayer player)
		{
			if (!arg.HasArgs())
			{
				PrintUsage(player);
				return;
			}

			var frequency = arg.GetInt(0);
			frequency = frequency < 0 ? 0 : frequency;
			_context.Settings.CommonSettings.MaxDropFrequency = TimeSpan.FromSeconds(frequency);

			Diagnostics.Diagnostics.MessageToServerAndPlayer(player, "Setting max frequency to {0}", frequency);
			_controller.ApplySettings();
		}

		protected override string GetUsageString()
		{
			return GetDefaultUsageString("3600");
		}
	}

	public class SetPlayersCommand : AirdropExtendedCommand
	{
		private readonly SettingsContext _context;
		private readonly AirdropController _controller;

		public SetPlayersCommand(SettingsContext context, AirdropController controller)
			: base("aire.players", "aire.canPlayers")
		{
			if (context == null) throw new ArgumentNullException("context");
			if (controller == null) throw new ArgumentNullException("controller");
			_context = context;
			_controller = controller;
		}

		public override void Execute(ConsoleSystem.Arg arg, BasePlayer player)
		{
			if (!arg.HasArgs())
			{
				PrintUsage(player);
				return;
			}

			var players = arg.GetInt(0);
			players = players < 0 ? 0 : players;
			_context.Settings.CommonSettings.MinimumPlayerCount = players;

			Diagnostics.Diagnostics.MessageToServerAndPlayer(player, "Setting min players to {0}", players);
			_controller.ApplySettings();
		}

		protected override string GetUsageString()
		{
			return GetDefaultUsageString("25");
		}
	}

	public class SetTimerEnabledCommand : AirdropExtendedCommand
	{
		private readonly SettingsContext _context;
		private readonly AirdropController _controller;

		public SetTimerEnabledCommand(SettingsContext context, AirdropController controller)
			: base("aire.timer", "aire.canTimer")
		{
			if (context == null) throw new ArgumentNullException("context");
			if (controller == null) throw new ArgumentNullException("controller");
			_context = context;
			_controller = controller;
		}

		public override void Execute(ConsoleSystem.Arg arg, BasePlayer player)
		{
			if (!arg.HasArgs())
			{
				PrintUsage(player);
				return;
			}

			var pluginAirdropTimerEnabled = arg.GetBool(0);

			_context.Settings.CommonSettings.PluginAirdropTimerEnabled = pluginAirdropTimerEnabled;

			Diagnostics.Diagnostics.MessageToServerAndPlayer(player, "Setting plugin timer enabled to {0}", pluginAirdropTimerEnabled);
			_controller.ApplySettings();
		}

		protected override string GetUsageString()
		{
			return GetDefaultUsageString("true");
		}
	}

	public class SetEventEnabledCommand : AirdropExtendedCommand
	{
		private readonly SettingsContext _context;
		private readonly AirdropController _controller;

		public SetEventEnabledCommand(SettingsContext context, AirdropController controller)
			: base("aire.event", "aire.canEvent")
		{
			if (context == null) throw new ArgumentNullException("context");
			if (controller == null) throw new ArgumentNullException("controller");
			_context = context;
			_controller = controller;
		}

		public override void Execute(ConsoleSystem.Arg arg, BasePlayer player)
		{
			if (!arg.HasArgs())
			{
				PrintUsage(player);
				return;
			}

			var builtInAirdropEnabled = arg.GetBool(0);

			_context.Settings.CommonSettings.BuiltInAirdropEnabled = builtInAirdropEnabled;

			Diagnostics.Diagnostics.MessageToServerAndPlayer(player, "Setting built in airdrop enabled to {0}", builtInAirdropEnabled);
			_controller.ApplySettings();
		}

		protected override string GetUsageString()
		{
			return GetDefaultUsageString("true");
		}
	}

	public class SetSupplyDropEnabledCommand : AirdropExtendedCommand
	{
		private readonly SettingsContext _context;
		private readonly AirdropController _controller;

		public SetSupplyDropEnabledCommand(SettingsContext context, AirdropController controller)
			: base("aire.supply", "aire.canSupply")
		{
			if (context == null) throw new ArgumentNullException("context");
			if (controller == null) throw new ArgumentNullException("controller");
			_context = context;
			_controller = controller;
		}

		public override void Execute(ConsoleSystem.Arg arg, BasePlayer player)
		{
			if (!arg.HasArgs())
			{
				PrintUsage(player);
				return;
			}

			var supplyDropTimerEnabled = arg.GetBool(0);

			_context.Settings.CommonSettings.SupplySignalsEnabled = supplyDropTimerEnabled;

			Diagnostics.Diagnostics.MessageToServerAndPlayer(player, "Setting supply signals enabled to {0}", supplyDropTimerEnabled);
			_controller.ApplySettings();
		}

		protected override string GetUsageString()
		{
			return GetDefaultUsageString("true");
		}
	}

	public class SetDespawnTimeCommand : AirdropExtendedCommand
	{
		private readonly SettingsContext _context;
		private readonly AirdropController _controller;

		public SetDespawnTimeCommand(SettingsContext context, AirdropController controller)
			: base("aire.despawntime", "aire.canDespawnTime")
		{
			if (context == null) throw new ArgumentNullException("context");
			if (controller == null) throw new ArgumentNullException("controller");
			_context = context;
			_controller = controller;
		}

		public override void Execute(ConsoleSystem.Arg arg, BasePlayer player)
		{
			if (!arg.HasArgs())
			{
				PrintUsage(player);
				return;
			}

			var despawnTimeInSeconds = arg.GetInt(0);
			despawnTimeInSeconds = despawnTimeInSeconds < 0 ? 0 : despawnTimeInSeconds;
			_context.Settings.CommonSettings.SupplyCrateDespawnTime = TimeSpan.FromSeconds(despawnTimeInSeconds);

			Diagnostics.Diagnostics.MessageToServerAndPlayer(player, "Set SupplyCrateDespawnTime to {0}", _context.Settings.CommonSettings.SupplyCrateDespawnTime);
			_controller.ApplySettings();
		}

		protected override string GetUsageString()
		{
			return GetDefaultUsageString("300");
		}
	}

	public class SetItemSettingsCommand : AirdropExtendedCommand
	{
		private readonly SettingsContext _context;
		private readonly AirdropController _controller;
		private readonly string _usageString;

		public SetItemSettingsCommand(SettingsContext context, AirdropController controller)
			: base("aire.setitem", "aire.canSetItem")
		{
			if (context == null) throw new ArgumentNullException("context");
			if (controller == null) throw new ArgumentNullException("controller");
			_context = context;
			_controller = controller;

			var usageStrings = new[]
			{
				GetDefaultUsageString("item_name [category] [chance] [min] [max] [is_blueprint]"),
				string.Format("Example: {0} Weapon rocket_launcher 15 1 1 false.", Name),
				"default chance=0, min=0, max=0, is_blueprint=false."
			};

			_usageString = string.Join(Environment.NewLine, usageStrings);
		}

		public override void Execute(ConsoleSystem.Arg arg, BasePlayer player)
		{
			if (!arg.HasArgs())
			{
				PrintUsage(player);
				return;
			}

			string categoryName;
			string itemName;
			float chance;
			int minAmount;
			int maxAmount;
			bool isBlueprint;

			try
			{
				categoryName = arg.GetString(0);
				itemName = arg.GetString(1);
				chance = arg.GetFloat(2);
				minAmount = arg.GetInt(3);
				maxAmount = arg.GetInt(4);
				isBlueprint = arg.GetBool(5);
			}
			catch (Exception)
			{
				PrintUsage(player);
				return;
			}

			var item = _context.Settings.FindItem(categoryName, itemName, isBlueprint);

			item.ChanceInPercent = chance;
			item.MinAmount = minAmount;
			item.MaxAmount = maxAmount;

			Diagnostics.Diagnostics.MessageToServerAndPlayer(player,
			"Set settings to item:{0}, chance:{1}, min_amount:{2}, max_amount:{3}, is blueprint:{4}",
			itemName,
			chance,
			minAmount,
			maxAmount,
			isBlueprint);

			_controller.ApplySettings();
		}

		protected override string GetUsageString()
		{
			return _usageString;
		}
	}

	public class SetItemGroupSettingsCommand : AirdropExtendedCommand
	{
		private readonly SettingsContext _context;
		private readonly AirdropController _controller;
		private readonly string _usageString;

		public SetItemGroupSettingsCommand(SettingsContext context, AirdropController controller)
			: base("aire.setitemgroup", "aire.canSetItemGroup")
		{
			if (context == null) throw new ArgumentNullException("context");
			if (controller == null) throw new ArgumentNullException("controller");
			_context = context;
			_controller = controller;

			var usageStrings = new[]
			{
				GetDefaultUsageString("group_name 2"),
				string.Format("Example: {0} Attire 2", Name)
			};

			_usageString = string.Join(Environment.NewLine, usageStrings);
		}

		public override void Execute(ConsoleSystem.Arg arg, BasePlayer player)
		{
			if (!arg.HasArgs())
			{
				PrintUsage(player);
				return;
			}

			string groupName;
			int maxAmount;

			try
			{
				groupName = arg.GetString(0);
				if (string.IsNullOrEmpty(groupName))
				{
					PrintUsage(player);
					return;
				}

				maxAmount = arg.GetInt(1);
			}
			catch (Exception)
			{
				PrintUsage(player);
				return;
			}

			var airdropItemGroup = _context.Settings.ItemGroups.FirstOrDefault(g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));
			if (airdropItemGroup == null)
			{
				Diagnostics.Diagnostics.MessageToServerAndPlayer(player, " command {0} error - group not found", Name);
				return;
			}

			airdropItemGroup.MaximumAmountInLoot = maxAmount;

			Diagnostics.Diagnostics.MessageToServerAndPlayer(player,
				"Set item group {0}, max_amount:{1}",
				groupName,
				maxAmount);

			_controller.ApplySettings();
		}

		protected override string GetUsageString()
		{
			return _usageString;
		}
	}

	public class SetAirdropCapacityCommand : AirdropExtendedCommand
	{
		private readonly SettingsContext _context;
		private readonly AirdropController _controller;

		public SetAirdropCapacityCommand(SettingsContext context, AirdropController controller)
			: base("aire.capacity", "aire.canCapacity")
		{
			if (context == null) throw new ArgumentNullException("context");
			if (controller == null) throw new ArgumentNullException("controller");
			_context = context;
			_controller = controller;
		}

		public override void Execute(ConsoleSystem.Arg arg, BasePlayer player)
		{
			if (!arg.HasArgs())
			{
				PrintUsage(player);
				return;
			}

			var capacity = arg.GetInt(0);
			capacity = capacity < 0 ? 0 : capacity;
			_context.Settings.Capacity = capacity;

			Diagnostics.Diagnostics.MessageToServerAndPlayer(player, "Setting airdrop capacity to {0}", capacity);
			_controller.ApplySettings();
		}

		protected override string GetUsageString()
		{
			return GetDefaultUsageString("18");
		}
	}

	public static class CommandFactory
	{
		public static List<AirdropExtendedCommand> Create(
			SettingsContext context,
			PluginSettingsRepository settingsRepository,
			AirdropController controller)
		{
			if (context == null) throw new ArgumentNullException("context");
			if (settingsRepository == null) throw new ArgumentNullException("settingsRepository");
			if (controller == null) throw new ArgumentNullException("controller");

			return new List<AirdropExtendedCommand>
				{
					new LoadSettingsCommand(context, settingsRepository, controller),
					new ReloadSettingsCommand(context, settingsRepository, controller),
					new SaveSettingsCommand(context, settingsRepository),
					new GenerateDefaultSettingsAndSaveCommand(),
					new SetDropMinFrequencyCommand(context, controller),
					new SetDropMaxFrequencyCommand(context, controller),
					new SetPlayersCommand(context, controller),
					new SetEventEnabledCommand(context, controller),
					new SetTimerEnabledCommand(context, controller),
					new SetSupplyDropEnabledCommand(context, controller),
					new SetDespawnTimeCommand(context, controller),
					new SetItemSettingsCommand(context, controller),
					new SetItemGroupSettingsCommand(context, controller),
					new SetAirdropCapacityCommand(context, controller)
				};
		}
	}
}

namespace AirdropExtended.PluginSettings
{
	public sealed class PluginSettingsRepository
	{
		private readonly DynamicConfigFile _config;
		private readonly Action _saveConfigDelegate;

		public PluginSettingsRepository(DynamicConfigFile config, Action saveConfigDelegate)
		{
			if (config == null) throw new ArgumentNullException("config");
			if (saveConfigDelegate == null) throw new ArgumentNullException("saveConfigDelegate");
			_config = config;
			_saveConfigDelegate = saveConfigDelegate;
		}

		private const string DefaultSettingsName = "defaultSettings";

		public string LoadSettingsName(string defaultName = DefaultSettingsName)
		{
			string settingsName;
			try
			{
				settingsName = (string)_config["settingsName"];
			}
			catch (Exception)
			{
				settingsName = string.Empty;
			}

			settingsName = string.IsNullOrEmpty(settingsName) ? defaultName : settingsName;
			return settingsName;
		}

		public void SaveSettingsName(string settingsName)
		{
			if (string.IsNullOrEmpty(settingsName))
				settingsName = DefaultSettingsName;

			_config["settingsName"] = settingsName;
			_saveConfigDelegate();
		}
	}
}

namespace AirdropExtended.WeightedSearch
{
	public static class Algorithms
	{
		public static int BinarySearchClosestIndex<T>(List<T> inputArray, Func<T, float> selector, float number)
		{
			if (inputArray.Count == 0)
				return -1;

			var left = 0;
			var right = inputArray.Count - 1;
			//find the closest range
			while ((right - left) > 1)
			{
				var mid = left + (right - left) / 2;

				if (selector(inputArray[mid]) > number)
					right = mid;
				else
					left = mid;
			}

			var diffWithLeft = number - selector(inputArray[left]);
			var diffWithRight = selector(inputArray[right]) - number;

			//closest is the one with the lesser difference
			var result = diffWithLeft < diffWithRight
				? left
				: right;

			return result;
		}
	}

	public struct Weighted<T>
	{
		public T Value { get; set; }
		public float Weight { get; set; }
	}
}

namespace AirdropExtended.Airdrop.Services
{
	public sealed class AirdropTimerService
	{
		public static TimeSpan DefaultTimerInterval = TimeSpan.FromHours(1);

		private Timer.TimerInstance _aidropTimer;
		private AirdropSettings _settings;

		public void StartAirdropTimer(AirdropSettings settings)
		{
			if (settings == null) throw new ArgumentNullException("settings");
			_settings = settings;

			if (!settings.CommonSettings.PluginAirdropTimerEnabled)
				return;

			SetupNextTick();
		}

		private void SetupNextTick()
		{
			StopAirdropTimer();

			var minDropFreq = Convert.ToSingle(_settings.CommonSettings.MinDropFrequency.TotalSeconds);
			var maxDropFreq = Convert.ToSingle(_settings.CommonSettings.MaxDropFrequency.TotalSeconds);

			var delay = Oxide.Core.Random.Range(minDropFreq, maxDropFreq);

			Diagnostics.Diagnostics.MessageToServer("next airdrop in:{0}", delay);
			_aidropTimer = Interface.GetMod().GetLibrary<Timer>().Once(delay, Tick);
		}

		private void Tick()
		{
			var playerCount = BasePlayer.activePlayerList.Count;
			if (playerCount >= _settings.CommonSettings.MinimumPlayerCount)
			{
				Diagnostics.Diagnostics.MessageToServer("running timed airdrop");

				var plane = GameManager.server.CreateEntity(
					"assets/bundled/prefabs/events/cargo_plane.prefab",
					new Vector3(),
					new Quaternion());
				if (plane != null)
					plane.Spawn();
			}
			else
			{
				Diagnostics.Diagnostics.MessageTo(
					_settings.CommonSettings.NotifyOnPlaneRemovedMessage,
					_settings.CommonSettings.NotifyOnPlaneRemoved,
					playerCount);
			}

			SetupNextTick();
		}

		public void StopAirdropTimer()
		{
			if (_aidropTimer == null || _aidropTimer.Destroyed)
				return;

			_aidropTimer.Destroy();
			_aidropTimer = null;
		}
	}

	public sealed class AirdropController
	{
		private readonly SettingsContext _context;
		private readonly AirdropTimerService _timerService = new AirdropTimerService();

		public AirdropController(SettingsContext context)
		{
			if (context == null) throw new ArgumentNullException("context");
			_context = context;
		}

		public bool IsInitialized()
		{
			return _context != null && _context.Settings != null;
		}

		public void ApplySettings()
		{
			var settings = _context.Settings ?? AirdropSettingsFactory.CreateDefault();
			AirdropSettingsValidator.Validate(settings);

			CleanupServices();
			_context.Settings = settings;
			InitializeServices(settings);
		}

		private void CleanupServices()
		{
			_timerService.StopAirdropTimer();
			SupplyDropBehaviorService.RemoveCustomBehaviorsFromSupplyDrops();
		}

		private void InitializeServices(AirdropSettings settings)
		{
			SetupBuiltInAirdrop();
			SupplyDropBehaviorService.AttachCustomBehaviorsToSupplyDrops(settings);
			_timerService.StartAirdropTimer(settings);
		}

		public void OnEntitySpawned(BaseEntity entity)
		{
			if (entity == null)
				return;

			if (entity is SupplyDrop)
				HandleSupply(entity as SupplyDrop);

			if (entity is CargoPlane)
				NotifyOnPlane();
		}

		private void HandleSupply(SupplyDrop entity)
		{
			var supplyDrop = entity.GetComponent<LootContainer>();
			if (supplyDrop == null)
				return;

			var itemContainer = supplyDrop.inventory;
			if (itemContainer == null || itemContainer.itemList == null)
				return;

			FillAirdropContainer(itemContainer);

			var x = entity.transform.position.x;
			var y = entity.transform.position.y;
			var z = entity.transform.position.z;

			Diagnostics.Diagnostics.MessageTo(_context.Settings.CommonSettings.NotifyOnDropStartedMessage, _context.Settings.CommonSettings.NotifyOnDropStarted, entity.net.ID, x, y, z);

			CollisionCheckBehavior.AddTo(supplyDrop, _context.Settings.CommonSettings);
			DespawnBehavior.AddTo(entity, _context.Settings.CommonSettings);
		}

		private void FillAirdropContainer(ItemContainer itemContainer)
		{
			if (_context == null || _context.Settings == null)
				return;
			itemContainer.itemList.Clear();
			itemContainer.capacity = _context.Settings.Capacity;

			var itemList = _context.Settings.CreateItemList();
			foreach (var item in itemList)
				item.MoveToContainer(itemContainer, -1, false);
		}

		private void NotifyOnPlane()
		{
			Diagnostics.Diagnostics.MessageTo(_context.Settings.CommonSettings.NotifyOnPlaneSpawnedMessage, _context.Settings.CommonSettings.NotifyOnPlaneSpawned);
		}

		private void SetupBuiltInAirdrop()
		{
			var schedule = UnityEngine.Object.FindObjectsOfType<EventSchedule>().First();

			schedule.CancelInvoke("RunSchedule");
			if (_context.Settings.CommonSettings.BuiltInAirdropEnabled)
				schedule.InvokeRepeating("RunSchedule", 1f, 1f);
		}

		public void Cleanup()
		{
			_timerService.StopAirdropTimer();
			SupplyDropBehaviorService.RemoveCustomBehaviorsFromSupplyDrops();
		}

		public void OnSupplySignal(BasePlayer player, BaseEntity entity)
		{
			if (_context.Settings.CommonSettings.SupplySignalsEnabled)
				return;

			entity.KillMessage();
			Diagnostics.Diagnostics.MessageToPlayer(player, "Supply signals are disabled by server.");
		}
	}
}

namespace AirdropExtended.Airdrop.Settings
{
	public enum PickStrategy
	{
		Capacity,
		GroupSize
	}

	public sealed class AirdropSettings
	{
		public const int MaxCapacity = 18;

		private int _capacity = MaxCapacity;

		public AirdropSettings()
		{
			Capacity = MaxCapacity;
			PickStrategy = PickStrategy.Capacity;
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

		public PickStrategy PickStrategy { get; set; }

		public List<AirdropItemGroup> ItemGroups { get; set; }

		public CommonSettings CommonSettings { get; set; }

		public List<Item> CreateItemList()
		{
			var groups = ItemGroups.Where(g => g.CanDrop());
			List<Item> items;
			switch (PickStrategy)
			{
				case PickStrategy.Capacity:
					items = CapacityWeightedPick(groups);
					break;
				case PickStrategy.GroupSize:
					items = GroupSizeWeightedPick(groups);
					break;
				default:
					items = CapacityWeightedPick(groups);
					break;
			}
			return items;
		}

		private List<Item> CapacityWeightedPick(IEnumerable<AirdropItemGroup> groups)
		{
			var items = new List<Item>(Capacity);

			var weightedGroups = groups
				.OrderBy(i => i.MaximumAmountInLoot)
				.ToList();

			var groupWeightAccumulator = 0.0f;
			var fractionCapacity = (float)Capacity;
			var groupWeightArray = weightedGroups.Aggregate(new List<Weighted<AirdropItemGroup>>(), (list, @group) =>
			{
				groupWeightAccumulator += @group.MaximumAmountInLoot / fractionCapacity;
				list.Add(new Weighted<AirdropItemGroup> { Value = @group, Weight = groupWeightAccumulator });
				return list;
			});

			for (var pickIteration = 0; pickIteration < Capacity; pickIteration++)
			{
				var groupRandomValue = (float)Oxide.Core.Random.Range(0.0d, 1.0d) * groupWeightAccumulator;
				var indexOfGroupToPick = Algorithms.BinarySearchClosestIndex(groupWeightArray, g => g.Weight, groupRandomValue);
				var weightedGroup = weightedGroups[indexOfGroupToPick];

				var item = PickItemWeightedOrDefault(weightedGroup);
				if (item == null)
					continue;

				items.Add(item);
			}
			return items;
		}

		private static Item PickItemWeightedOrDefault(AirdropItemGroup weightedGroup)
		{
			var itemWeightAccumulator = 0.0f;
			var itemWeightedArray = weightedGroup.ItemSettings
				.OrderByDescending(i => i.ChanceInPercent)
				.Aggregate(new List<Weighted<AirdropItem>>(), (list, itm) =>
				{
					itemWeightAccumulator += itm.ChanceInPercent;
					list.Add(new Weighted<AirdropItem> { Value = itm, Weight = itemWeightAccumulator });
					return list;
				});

			var itemRandomValue = (float)Oxide.Core.Random.Range(0.0d, 1.0d) * itemWeightAccumulator;
			var indexOfItemToPick = Algorithms.BinarySearchClosestIndex(itemWeightedArray, setting => setting.Weight, itemRandomValue);
			var item = itemWeightedArray[indexOfItemToPick].Value;
			var amount = Oxide.Core.Random.Range(item.MinAmount, item.MaxAmount);
			if (amount == 0)
				return null;

			var i1 = ItemManager.CreateByName(item.Name, amount);
			if (weightedGroup.Name.Equals("Blueprint", StringComparison.OrdinalIgnoreCase))
				i1.SetFlag(Item.Flag.Blueprint, true);
			return i1;
		}

		private List<Item> GroupSizeWeightedPick(IEnumerable<AirdropItemGroup> groups)
		{
			var list = new List<Item>(Capacity);
			foreach (var group in groups)
			{
				for (var i = 0; i < group.MaximumAmountInLoot; i++)
				{
					var item = PickItemWeightedOrDefault(@group);
					if (item == null)
						continue;

					list.Add(item);
				}
			}
			return list;
		}

		public AirdropItem FindItem(string categoryName, string itemName, bool isBlueprint)
		{
			if (categoryName == null) throw new ArgumentNullException("categoryName");
			if (itemName == null) throw new ArgumentNullException("itemName");

			return ItemGroups.Select(@group =>
				@group.ItemSettings.FirstOrDefault(f =>
					f.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase) &&
					f.IsBlueprint == isBlueprint))
						.FirstOrDefault(item => item != null);
		}
	}

	public sealed class AirdropItemGroup
	{
		private int _maximumAmountInLoot;
		private bool _canDrop;
		private List<AirdropItem> _itemSettings;

		public string Name { get; set; }

		public int MaximumAmountInLoot
		{
			get { return _maximumAmountInLoot; }
			set
			{
				_maximumAmountInLoot = value < 0
					? 0
					: value;
				RefreshCanDrop();
			}
		}

		private void RefreshCanDrop()
		{
			_canDrop = _maximumAmountInLoot > 0 && _itemSettings != null && _itemSettings.Any(i => i.CanDrop());
		}

		public bool CanDrop()
		{
			return _canDrop;
		}

		public List<AirdropItem> ItemSettings
		{
			get { return _itemSettings; }
			set
			{
				_itemSettings = (value ?? new List<AirdropItem>())
					.OrderByDescending(i => i.MaxAmount)
					.ThenByDescending(i => i.ChanceInPercent).ToList();
				RefreshCanDrop();
			}
		}
	}

	public sealed class AirdropItem
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
			set { _minAmount = value < 0 ? 0 : value; }
		}
		
		public int MaxAmount
		{
			get { return _maxAmount; }
			set { _maxAmount = value < 0 ? 0 : value; }
		}

		public bool IsBlueprint { get; set; }

		public bool CanDrop()
		{
			return ChanceInPercent > 0.0f && MaxAmount > 0;
		}
	}

	public sealed class CommonSettings
	{
		public static readonly TimeSpan DefaultDropFrequency = TimeSpan.FromHours(1);

		public const string DefaultNotifyOnPlaneSpawnedMessage = "Cargo Plane has been spawned.";
		public const string DefaultNotifyOnPlaneRemovedMessage = "Cargo Plane has been removed, due to insufficient player count: {0}.";
		public const string DefaultNotifyOnDropStartedMessage = "Supply Drop {0} has been spawned at {1},{2},{3}.";
		public const string DefaultNotifyOnPlayerLootingStartedMessage = "Player {0} started looting the Supply Drop {1}.";
		private const string DefaultNotifyOnCollisionMessage = "Supply drop {0} has landed at {1},{2},{3}";
		private const string DefaultNotifyOnDespawnMessage = "Supply drop {0} has been despawned at {1},{2},{3}";

		public Boolean SupplySignalsEnabled { get; set; }
		public Boolean BuiltInAirdropEnabled { get; set; }
		public Boolean PluginAirdropTimerEnabled { get; set; }

		public TimeSpan MinDropFrequency { get; set; }
		public TimeSpan MaxDropFrequency { get; set; }

		public int MinimumPlayerCount { get; set; }
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

			SupplySignalsEnabled = true;
			BuiltInAirdropEnabled = false;
			PluginAirdropTimerEnabled = true;
		}

		public static CommonSettings CreateDefault()
		{
			return new CommonSettings
			{
				MinDropFrequency = DefaultDropFrequency,
				MaxDropFrequency = DefaultDropFrequency,
				MinimumPlayerCount = 25,
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
				NotifyOnDespawnMessage = DefaultNotifyOnDespawnMessage,

				SupplySignalsEnabled = true,
				BuiltInAirdropEnabled = false,
				PluginAirdropTimerEnabled = true
			};
		}
	}

	public sealed class AirdropSettingsValidator
	{
		public static void Validate(AirdropSettings settings)
		{
			if (settings.ItemGroups == null)
				settings.ItemGroups = AirdropSettingsFactory.CreateDefault().ItemGroups;

			var countOfItems = settings.ItemGroups.Sum(g => g.MaximumAmountInLoot);
			var diff = countOfItems - AirdropSettings.MaxCapacity;
			if (diff > 0 && settings.PickStrategy == PickStrategy.GroupSize)
				AdjustGroupMaxAmount(settings.ItemGroups, diff);

			ValidateFrequency(settings);
		}

		private static void AdjustGroupMaxAmount(List<AirdropItemGroup> value, int diff)
		{
			Diagnostics.Diagnostics.MessageToServer("adjusting groups amount: substracting {1} from total", diff);
			var groupsOrderedByDescending = value.OrderByDescending(g => g.MaximumAmountInLoot);
			for (var i = diff; i > 0; i--)
			{
				var airdropItemGroup = groupsOrderedByDescending.Skip(diff - i).Take(1).First();
				value.First(g => g.Name == airdropItemGroup.Name).MaximumAmountInLoot--;

				foreach (var item in airdropItemGroup.ItemSettings.Where(item => item.MinAmount > item.MaxAmount))
					item.MinAmount = item.MaxAmount;
			}
		}

		private static void ValidateFrequency(AirdropSettings settings)
		{
			if (settings.CommonSettings.MinDropFrequency <= TimeSpan.Zero)
				settings.CommonSettings.MinDropFrequency = CommonSettings.DefaultDropFrequency;
			if (settings.CommonSettings.MaxDropFrequency <= TimeSpan.Zero)
				settings.CommonSettings.MaxDropFrequency = CommonSettings.DefaultDropFrequency;

			if (settings.CommonSettings.MinDropFrequency > settings.CommonSettings.MaxDropFrequency)
			{
				settings.CommonSettings.MinDropFrequency = settings.CommonSettings.MaxDropFrequency;
				Diagnostics.Diagnostics.MessageToServer("adjusting minfreq to :{0}", settings.CommonSettings.MinDropFrequency.TotalSeconds);
			}
			else if (settings.CommonSettings.MaxDropFrequency < settings.CommonSettings.MinDropFrequency)
			{
				settings.CommonSettings.MaxDropFrequency = settings.CommonSettings.MinDropFrequency;
				Diagnostics.Diagnostics.MessageToServer("adjusting maxfreq to :{0}", settings.CommonSettings.MaxDropFrequency.TotalSeconds);
			}
		}
	}

	public static class AidropSettingsRepository
	{
		public static AirdropSettings LoadFrom(string settingsName)
		{
			AirdropSettings settings;
			try
			{
				var fileName = "airdropExtended_" + settingsName;
				settings = Interface.GetMod().DataFileSystem.ReadObject<AirdropSettings>(fileName);

				var oxideGeneratedDefaultSettingsFile = string.IsNullOrEmpty(settingsName) || settings == null || settings.CommonSettings == null || settings.ItemGroups == null;
				if (oxideGeneratedDefaultSettingsFile)
				{
					Diagnostics.Diagnostics.MessageToServer("Not found settings in:{0}, generating default", settingsName);
					settings = AirdropSettingsFactory.CreateDefault();
					SaveTo(settingsName, settings);
				}
			}
			catch (Exception ex)
			{
				Diagnostics.Diagnostics.MessageToServer("exception during read:{0}", ex);
				Diagnostics.Diagnostics.MessageToServer("error. Creating default settings.");
				settings = AirdropSettingsFactory.CreateDefault();
			}

			return settings;
		}

		public static void SaveTo(string settingsName, AirdropSettings airdropSettings)
		{
			if (airdropSettings == null) throw new ArgumentNullException("airdropSettings");
			if (string.IsNullOrEmpty(settingsName)) throw new ArgumentException("Should not be blank", "settingsName");

			var fileName = "airdropExtended_" + settingsName;
			Interface.GetMod().DataFileSystem.WriteObject(fileName, airdropSettings);
		}
	}
}

namespace AirdropExtended.Airdrop.Settings.Generate
{
	public static class AirdropSettingsFactory
	{
		public static List<string> DefaultExcludedItems = new List<string>
		{
			//Construction
			"generator.wind.scrap",
			"lock.key",
			//Food
			"wolfmeat.spoiled",
			"wolfmeat.burned",
			"chicken.spoiled",
			"chicken.burned",
			"apple.spoiled",
			"humanmeat.spoiled",
			"humanmeat.burned",
			//Misc
			"book.accident",
			"note",
			//Resources
			"salt.water",
			"skull.human",
			"skull.wolf",
			"water",
			//Tools
			"tool.camera",
			"rock"
		};

		private static readonly Dictionary<string, Func<ItemDefinition, int[]>> DefaultAmountByCategoryMapping = new Dictionary
			<string, Func<ItemDefinition, int[]>>
			{
				{"Food", GenerateAmountMappingForFood},
				{"Attire", def => new[] {1, 1}},
				{"Items", def => new[] {1, 1}},
				{"Ammunition", GenerateAmountMappingForAmmunition},
				{"Misc", GenerateAmountMappingForMisc},
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
			if (singleStackItems.Contains(itemDefinition.shortname))
				return new[] { 1, 1 };

			return new[] { 5, 10 };
		}

		private static int[] GenerateAmountMappingForAmmunition(ItemDefinition def)
		{
			return def.shortname.Contains("rocket", CompareOptions.OrdinalIgnoreCase)
				? new[] { 1, 3 }
				: new[] { 32, 48 };
		}

		private static int[] GenerateAmountMappingForMisc(ItemDefinition def)
		{
			return def.shortname.Contains("blueprint", CompareOptions.OrdinalIgnoreCase)
				? new[] { 5, 10 }
				: new[] { 1, 1 };
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

			if (largeAmountItems.Contains(itemDefinition.shortname))
				return new[] { 3, 5 };

			return new[] { 1, 1 };
		}

		private static int[] GenerateMappingForResource(ItemDefinition itemDefinition)
		{
			var largeStackItems = new[] { "wood", "sulfur_ore", "sulfur", "stones", "metal_ore", "metal_fragments", "fat_animal", "cloth", "bone_fragments" };
			var smallStackItems = new[] { "paper", "gunpowder", "lowgradefuel", "explosives", "can_tuna_empty", "can_beans_empty" };
			var zeroStackItems = new[] { "skull_wolf", "skull_human", "water", "salt_water", "charcoal", "battery_small" };

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

		public static AirdropSettings CreateDefault()
		{
			var itemGroups = GenerateDefaultItemGroups();
			return new AirdropSettings
			{
				ItemGroups = itemGroups,
				Capacity = AirdropSettings.MaxCapacity,
				CommonSettings = CommonSettings.CreateDefault()
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
							.Where(i => !DefaultExcludedItems.Contains(i.shortname, StringComparer.OrdinalIgnoreCase))
							.Select(itemDefinition =>
							{
								Func<ItemDefinition, int[]> amountFunc;
								DefaultAmountByCategoryMapping.TryGetValue(categoryName, out amountFunc);
								var amountMappingArray = amountFunc == null
									? new[] { 0, 0 }
									: amountFunc(itemDefinition);

								return new AirdropItem
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
			var notDefaultBlueprints = ItemManager.bpList.Where(bp => !bp.defaultBlueprint);
			var bpItems = notDefaultBlueprints.Select(b => b.targetItem).ToList();
			return new AirdropItemGroup
			{
				ItemSettings = bpItems.Select(itemDef => new AirdropItem
				{
					Name = itemDef.shortname,
					MinAmount = 1,
					MaxAmount = 1,
					ChanceInPercent = CalculateChanceByRarity(itemDef.rarity),
					IsBlueprint = true
				}).ToList(),
				MaximumAmountInLoot = 2,
				Name = "Blueprint"
			};
		}
	}
}
