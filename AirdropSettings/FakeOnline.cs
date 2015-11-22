using System;
using System.Reflection;
using UnityEngine;
using System.Collections.Generic;
using Steamworks;

namespace Oxide.Plugins
{
	[Info("FakeOnline", "Eat", "0.0.2")]
	class FakeOnline : RustPlugin
	{
		private static readonly MethodInfo _updateMethod = typeof(ServerMgr).GetMethod("UpdateServerInformation",
			BindingFlags.NonPublic | BindingFlags.Instance);

		// Стартовое значение фейкового онлайна
		static int FAKE_ONLINE = 121;

		// Максимальный возможный фейк онлайн
		const int MAX_FAKE_ONLINE = 130;
		// Минимальный возможный фейк онлайн
		const int MIN_FAKE_ONLINE = 120;

		// Максимальное изменение онлайна, при обновлении
		const int MAX_STEP_FAKE = 5;
		// Минимальное изменение онлайна, при обновлении
		const int MIN_STEP_FAKE = -5;

		#region Vars

		static readonly System.Random random = new System.Random();

		#endregion

		bool Status = false;

		void OnServerInitialized()
		{
			FakeOn();
		}

		void FakeOn()
		{
			var currentTime = DateTime.Now;
			var minutes = currentTime.Minute;
			if (minutes >= 0 && minutes <= 14)
			{
				var timeToStart = 14 - minutes;
				timer.Once(timeToStart, FakeOn);
				return;
			}
			
			minutes = 60 - minutes;

			Status = true;

			int step = random.Next(MIN_STEP_FAKE, MAX_STEP_FAKE + 1);

			FAKE_ONLINE = Mathf.Clamp(FAKE_ONLINE + step, MIN_FAKE_ONLINE, MAX_FAKE_ONLINE);
			SteamGameServer.SetBotPlayerCount(FAKE_ONLINE);
			_updateMethod.Invoke(ServerMgr.Instance, new object[0]);


			timer.Once(minutes * 60, FakeOff);
			Puts("Fake online ON");
		}

		void FakeOff()
		{
			SteamGameServer.SetBotPlayerCount(0);
			_updateMethod.Invoke(ServerMgr.Instance, new object[0]);
			Status = false;
			timer.Once(15 * 60, FakeOn);
			Puts("Fake online OFF");
		}

		#region Logic
		void BuildServerTags(IList<string> tags)
		{
			if (!Status)
				return;

			tags[1] = "cp" + FAKE_ONLINE.ToString();
		}
		#endregion

		#region Status fake
		object OnRunCommand(ConsoleSystem.Arg arg)
		{
			if (arg.cmd != null && arg.connection != null && arg.connection.authLevel < 2 && arg.cmd.namefull == "global.status")
				return true;
			return null;
		}
		#endregion
	}
}