using Oxide.Core;

namespace Oxide.Plugins
{
	[Info("Server List Info", "baton", "1.0.0", ResourceId = 1500)]
	[Description("Customizable server list information")]
	public class WeatherConfig : RustPlugin
	{
		private void OnServerInitialized()
		{
			LoadConfig();

			var rain = Config.Get<string>("rain");
			var fog = Config.Get<string>("fog");

			var rustLib = Interface.Oxide.GetLibrary<Game.Rust.Libraries.Rust>();
			rustLib.RunServerCommand("weather.rain", rain);
			rustLib.RunServerCommand("weather.fog", fog);
		}

		protected override void LoadDefaultConfig()
		{
			Config["rain"] = "0";
			Config["fog"] = "0";
		}
	}
}
