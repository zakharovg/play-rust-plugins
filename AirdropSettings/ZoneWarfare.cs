using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
	public sealed class CaptureTeam
	{
		public string ClanName { get; set; }
		public List<BasePlayer> ClanMembers { get; set; }
		public TimeSpan TimePassedSinceCapture { get; set; }
	}

	[Info("ZoneWarfare", "baton", "0.0.1", ResourceId = 1210)]
	[Description("Customizable airdrop")]
	public class ZoneWarfare : RustPlugin
	{
		private static string[] _capturableZoneNames;
		private static TimeSpan _timeToCaptureZone;
		private static Dictionary<string, CaptureTeam> _captureTeams = new Dictionary<string, CaptureTeam>();

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
