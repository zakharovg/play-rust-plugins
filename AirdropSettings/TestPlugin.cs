using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("Test", "baton", "0.0.1", ResourceId = 100500)]
	[Description("test.")]
	public sealed class TestPlugin : RustPlugin
	{
		private void OnPlayerChat(ConsoleSystem.Arg arg)
		{
			var message = arg.GetString(0, "text");
			MessageToAll("OnPlayerChat:got this message:" + message);
		}

		private void OnRunCommand(ConsoleSystem.Arg arg)
		{
			Puts("OnRunCommand");
			var cmdName = arg.cmd == null ? string.Empty : arg.cmd.name;
			MessageToAll("command called: {0}", cmdName);
			if (cmdName == "chat.add")
				MessageToAll("chat add called:{0}" + arg.GetString(0, "text"));
		}

		[ConsoleCommand("one")]
		private void ConsoleCmd()
		{
			Interface.GetMod().GetLibrary<Game.Rust.Libraries.Rust>().BroadcastChat("two", "three");
			MessageToAll("three four");
		}

		public static void MessageToAll(string message, params object[] args)
		{
			var msg = string.Format(message, args);
			ConsoleSystem.Broadcast("chat.add \"SERVER\" " + msg.QuoteSafe() + " 1.0", new object[0]);
		}
	}
}