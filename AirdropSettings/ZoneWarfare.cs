using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
	[Info("ZoneWarfare", "baton", "0.0.1", ResourceId = 1210)]
	[Description("Customizable airdrop")]
	public class ZoneWarfare : RustPlugin
	{
		private static string[] _capturableZoneNames;
		private static TimeSpan _timeToCaptureZone;

		void OnServerInitialized()
		{
			LoadConfig();
			_capturableZoneNames = (Config["capturableZoneNames"] ?? new string[0]).ToString().Split(',');
			var timeToCaptureZone = (string)Config["timeToCaptureZone"] ?? string.Empty;
			if (!TimeSpan.TryParse(timeToCaptureZone, out _timeToCaptureZone))
				_timeToCaptureZone = TimeSpan.FromMinutes(30);
		}

		void Unloaded()
		{
			SaveConfig();

			Config["capturableZoneNames"] = string.Join(",", _capturableZoneNames);
			Config["timeToCaptureZone"] = _timeToCaptureZone.ToString();
		}
	}
}

namespace ZoneWarfare.Domain
{
	public sealed class Zone
	{
		public string Name { get; set; }

		public List<BasePlayer> GetPlayersInZone()
		{
			return null;
		}
	}

	public sealed class Clan
	{
		public string Name { get; set; }

		public List<BasePlayer> GetMembers()
		{
			return null;
		}
	}
}
