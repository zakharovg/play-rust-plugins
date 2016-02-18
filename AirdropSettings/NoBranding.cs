namespace Oxide.Plugins
{
    [Info("NoBranding","DefaultPlayer","1.0")]
    public class NoBranding : RustPlugin
    {
        private void OnPlayerInit(BasePlayer plr) {
			plr.SendConsoleCommand("global.branding false");
			plr.SendConsoleCommand("bind m \"/map\"");
			}
    }
}
