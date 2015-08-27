using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using ZoneWarfare.Configuration;
using ZoneWarfare.Domain.Services;

namespace Oxide.Plugins
{
	[Info("ZoneWarfare", "baton", "0.0.1", ResourceId = 1210)]
	[Description("Customizable airdrop")]
	public class ZoneWarfare : RustPlugin
	{
		private static PluginSettingsRepository _settingsRepository;
		private static PluginSettings _settings;
		private static WarfareZoneService _service;

		void OnServerInitialized()
		{
			LoadConfig();
			_settingsRepository = new PluginSettingsRepository(Config);
			_settings = _settingsRepository.Load();

			Bootstrap();
			_service.StartWarfare();
		}

		private void Bootstrap()
		{
			var clansPlugin = plugins.Find("Clans");
			if (clansPlugin == null)
				throw new InvalidOperationException("Plugin Clans not found!");
			var clanService = new ClanService(clansPlugin);
			_service = new WarfareZoneService(_settings, clanService);
		}

		void Unloaded()
		{
			SaveConfig();
			_service.StopWarfare();
			_settingsRepository.Save(_settings);
		}
	}
}

namespace ZoneWarfare.Configuration
{
	public sealed class PluginSettings
	{
		public CommonSettings Settings { get; set; }
		public LocalizationSettings Localization { get; set; }

		public PluginSettings()
		{
			Settings = new CommonSettings();
			Localization = new LocalizationSettings();
		}
	}

	public sealed class CommonSettings
	{
		public string CaptureZones { get; set; }
		public TimeSpan CaptureTime { get; set; }
		public TimeSpan ContestedTime { get; set; }
		public int MinimumPlayersToStartCapture { get; set; }
		public int CapturePrize { get; set; }

		public CommonSettings()
		{
			CaptureZones = string.Empty;
			CaptureTime = TimeSpan.FromMinutes(15);
			ContestedTime = TimeSpan.FromMinutes(2);
			MinimumPlayersToStartCapture = 2;
			CapturePrize = 1000;
		}
	}

	public sealed class LocalizationSettings
	{
		public string ClanStartedAttack { get; set; }
		public string ClanCapturedZone { get; set; }
		public string ClanLostZone { get; set; }
		public string ClanHoldZone { get; set; }
	}

	public sealed class PluginSettingsRepository
	{
		private readonly DynamicConfigFile _configFile;

		public PluginSettingsRepository(DynamicConfigFile configFile)
		{
			if (configFile == null) throw new ArgumentNullException("configFile");
			_configFile = configFile;
		}

		public PluginSettings Load()
		{
			PluginSettings settings;
			if (!_configFile.Exists())
			{
				settings = new PluginSettings();
				_configFile.WriteObject(settings);
			}
			else
			{
				settings = _configFile.ReadObject<PluginSettings>();
			}

			return settings;
		}

		public void Save(PluginSettings settings)
		{
			if (settings == null) throw new ArgumentNullException("settings");
			_configFile.WriteObject(settings);
		}
	}
}

namespace ZoneWarfare.Domain
{
	public sealed class Zone
	{
		public Zone(string name)
		{
			if (string.IsNullOrEmpty(name)) throw new ArgumentException("Should not be empty", "name");
			Name = name;
		}

		public string Name { get; private set; }
	}

	public sealed class CaptureTeam
	{
		public CaptureTeam(string clanName)
		{
			if (string.IsNullOrEmpty(clanName)) throw new ArgumentNullException("clanName");
			ClanName = clanName;
			Players = new List<BasePlayer>();
		}

		public string ClanName { get; set; }
		public List<BasePlayer> Players { get; set; }
	}

	public sealed class WarfareZone
	{
		public WarfareZone(Zone zone)
		{
			if (zone == null) throw new ArgumentNullException("zone");

			Zone = zone;
			Teams = new List<CaptureTeam>();
		}

		public Zone Zone { get; private set; }
		public List<CaptureTeam> Teams { get; private set; }

		public void AddToCaptureTeam(string clanName, BasePlayer player)
		{
			if (string.IsNullOrEmpty(clanName)) throw new ArgumentNullException("clanName");
			if (player == null) throw new ArgumentNullException("player");

			var captureTeam = Teams.FirstOrDefault(t => t.ClanName.Equals(clanName, StringComparison.OrdinalIgnoreCase));
			if (captureTeam == null)
			{
				var team = new CaptureTeam(clanName);
				team.Players.Add(player);
				Teams.Add(team);
			}
			else
				captureTeam.Players.Add(player);
		}

		public void RemoveFromCaptureTeam(string clanName, BasePlayer player)
		{
			if (string.IsNullOrEmpty(clanName)) throw new ArgumentNullException("clanName");
			if (player == null) throw new ArgumentNullException("player");

			var captureTeam = Teams.FirstOrDefault(t => t.ClanName.Equals(clanName, StringComparison.OrdinalIgnoreCase));
			if (captureTeam != null)
				captureTeam.Players.Remove(player);
		}

		public void OnTimePassed(TimeSpan time)
		{
			
		}
	}
}

namespace ZoneWarfare.Domain.Services
{
	public sealed class ClanService
	{
		private readonly Plugin _clansPlugin;

		public ClanService(Plugin clansPlugin)
		{
			if (clansPlugin == null) throw new ArgumentNullException("clansPlugin");
			_clansPlugin = clansPlugin;
		}

		public string GetClanName(BasePlayer player)
		{
			if (player == null) throw new ArgumentNullException("player");

			return _clansPlugin.Call<string>("GetClanOf", player);
		}
	}

	public sealed class WarfareZoneRepository
	{
		public IEnumerable<WarfareZone> GetAll()
		{
			return Enumerable.Empty<WarfareZone>();
		}

		public bool Exists(string name)
		{
			return false;
		}

		public void Save(WarfareZone warfareZone)
		{
			if (warfareZone == null) throw new ArgumentNullException("warfareZone");
		}

		public void Delete(IEnumerable<WarfareZone> zonesToRemove)
		{
			if (zonesToRemove == null) throw new ArgumentNullException("zonesToRemove");
		}
	}

	public sealed class WarfareZoneService
	{
		private static readonly WarfareZoneRepository WarfareZoneRepository = new WarfareZoneRepository();
		private readonly ClanService _clanService;

		private readonly PluginSettings _settings;
		private Timer.TimerInstance _timer;

		private static List<WarfareZone> _zones;

		public WarfareZoneService(PluginSettings settings, ClanService clanService)
		{
			if (settings == null) throw new ArgumentNullException("settings");
			if (clanService == null) throw new ArgumentNullException("clanService");
			_settings = settings;
			_clanService = clanService;
		}

		public void StartWarfare()
		{
			var warfareZoneNames = _settings.Settings.CaptureZones.Split(';', ',').Where(z => !string.IsNullOrEmpty(z));
			var warfareZones = WarfareZoneRepository.GetAll().ToList();
			var zonesToRemove = warfareZones.Where(z => !warfareZoneNames.Contains(z.Zone.Name));
			WarfareZoneRepository.Delete(zonesToRemove);

			foreach (var warfareZoneName in warfareZoneNames)
			{
				if (warfareZones.Any(z => z.Zone.Name.Equals(warfareZoneName, StringComparison.OrdinalIgnoreCase)))
					continue;

				CreateNewZone(warfareZoneName);
			}

			_zones = WarfareZoneRepository.GetAll().ToList();
			_timer = Interface.Oxide.GetLibrary<Timer>().Repeat(1, 0, Update);
		}

		private static void CreateNewZone(string warfareZoneName)
		{
			var zone = new Zone(warfareZoneName);
			var warfareZone = new WarfareZone(zone);
			WarfareZoneRepository.Save(warfareZone);
		}

		private void Update()
		{

		}

		public void OnPlayerEnteredZone(Zone zone, BasePlayer player)
		{
			var warfareZone = _zones.FirstOrDefault(z => z.Zone.Name.Equals(zone.Name));
			if (warfareZone == null)
				return;

			var clanName = _clanService.GetClanName(player);
			if (string.IsNullOrEmpty(clanName))
				return;

			warfareZone.AddToCaptureTeam(clanName, player);
		}

		public void OnPlayerLeftZone(Zone zone, BasePlayer player)
		{
			var warfareZone = _zones.FirstOrDefault(z => z.Zone.Name.Equals(zone.Name));
			if (warfareZone == null)
				return;

			var clanName = _clanService.GetClanName(player);
			if (string.IsNullOrEmpty(clanName))
				return;

			warfareZone.RemoveFromCaptureTeam(clanName, player);
		}

		public void StopWarfare()
		{
			if (_timer == null || _timer.Destroyed)
				return;

			_timer.Destroy();
			_timer = null;
		}
	}
}
