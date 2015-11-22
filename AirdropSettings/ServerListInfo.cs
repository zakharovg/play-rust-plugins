using Oxide.Core;

namespace Oxide.Plugins
{
	[Info("Server List Info", "baton", "1.0.0", ResourceId = 1500)]
	[Description("Customizable server list information")]
	public class ServerListInfo : RustPlugin
	{
		private void OnServerInitialized()
		{
			LoadConfig();

			var headerImage = Config.Get<string>("header");
			var description = Config.Get<string>("description").Replace("NEWLINE", "\n");

			var rustLib = Interface.Oxide.GetLibrary<Game.Rust.Libraries.Rust>();
			rustLib.RunServerCommand("server.headerimage", headerImage);
			rustLib.RunServerCommand("server.description", string.Format("{0}", description));
		}

		protected override void LoadDefaultConfig()
		{
			Config["header"] = string.Empty;
			Config["description"] = string.Empty;
		}
	}
}
