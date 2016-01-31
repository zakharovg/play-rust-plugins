using System.Linq;
using Rust;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("TwigsDecay", "playrust.io/dcode/Sanlerus", "0.0.1", ResourceId = 899)]
	public class TwigsDecay : RustPlugin
	{
		private List<string> buildingBlocks = new List<string>();
		private Dictionary<string, int> buildingBlocksDamage = new Dictionary<string, int>();
		private Dictionary<string, int> itemList = new Dictionary<string, int>();
		private Dictionary<string, string> messages = new Dictionary<string, string>();
		private int decayInterval;
		private int multiDamage;
		private Timer timerTwigs;

		protected override void LoadDefaultConfig()
		{
			Puts("Создан файл конфигурации по умолчанию");
			var buildingBlocks = new List<object>()
			{
				"assets/prefabs/building core/foundation.steps/foundation.steps.prefab",
				"assets/prefabs/building core/foundation.triangle/foundation.triangle.prefab",
				"assets/prefabs/building core/foundation/foundation.prefab"
			};
			Config["BuildingBlocks"] = buildingBlocks;
			var buildingBlocksDamage = new Dictionary<string, object>()
			{
				{"Twigs", 3}, // health: 5
				{"Wood", 4}, // health: 250
				{"Stone", 4}, // health: 500
				{"Metal", 2}, // health: 300
				{"TopTier", 3} // health: 1000
			};

			Config["BuildingBlocksDamage"] = buildingBlocksDamage;
			var itemList = new Dictionary<string, object>
			{
				{"assets/prefabs/building/gates.external.high/gates.external.high.stone/gates.external.high.stone.prefab", 25 }, //2000
				{"assets/prefabs/building/gates.external.high/gates.external.high.wood/gates.external.high.wood.prefab",   20 }, //1500
				{"assets/prefabs/building/ladder.wall.wood/ladder.wooden.wall.prefab",									   4 }, //100
				{"assets/prefabs/building/wall.external.high.stone/wall.external.high.stone.prefab",					   25 }, //2000
				{"assets/prefabs/building/wall.external.high.wood/wall.external.high.wood.prefab",						   20 }, //1500
				{"assets/prefabs/deployable/barricades/barricade.concrete.prefab",										   12 }, //200
				{"assets/prefabs/deployable/barricades/barricade.metal.prefab",											   24 }, //600
				{"assets/prefabs/deployable/barricades/barricade.sandbags.prefab",										   6 }, //100
				{"assets/prefabs/deployable/barricades/barricade.stone.prefab",											   6 }, //100
				{"assets/prefabs/deployable/barricades/barricade.wood.prefab",											   12 }, //200
				{"assets/prefabs/deployable/barricades/barricade.woodwire.prefab",										   18 }, //400
				{"assets/prefabs/deployable/bear trap/beartrap.prefab",													   12 }, //200
				{"assets/prefabs/deployable/campfire/campfire.prefab",													   3 }, //50
				{"assets/prefabs/deployable/floor spikes/spikes.floor.prefab",											   3 }, //50
				{"assets/prefabs/deployable/furnace.large/furnace.large.prefab",										   15 }, //1500
				{"assets/prefabs/deployable/furnace/furnace.prefab",													   7 }, //500
				{"assets/prefabs/deployable/jack o lantern/jackolantern.angry.prefab",									   15 },
				{"assets/prefabs/deployable/jack o lantern/jackolantern.happy.prefab",									   15 },
				{"assets/prefabs/deployable/landmine/landmine.prefab",													   3 }, //50
				{"assets/prefabs/deployable/lantern/lantern.deployed.prefab",											   3 }, //50
				{"assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab",											   7 }, //150
				{"assets/prefabs/deployable/large wood storage/box.wooden.large.prefab",								   12 }, //300
				{"assets/prefabs/deployable/repair bench/repairbench_deployed.prefab",									   9 }, //200
				{"assets/prefabs/deployable/research table/researchtable_deployed.prefab",								   9 }, //200
				{"assets/prefabs/deployable/signs/sign.hanging.banner.large.prefab",									   6 }, 
				{"assets/prefabs/deployable/signs/sign.hanging.ornate.prefab",											   6 },
				{"assets/prefabs/deployable/signs/sign.hanging.prefab",													   6 },
				{"assets/prefabs/deployable/signs/sign.huge.wood.prefab",												   6 },
				{"assets/prefabs/deployable/signs/sign.large.wood.prefab",												   6 },
				{"assets/prefabs/deployable/signs/sign.medium.wood.prefab",												   3 },
				{"assets/prefabs/deployable/signs/sign.pictureframe.landscape.prefab",									   3 },
				{"assets/prefabs/deployable/signs/sign.pictureframe.portrait.prefab",									   3 },
				{"assets/prefabs/deployable/signs/sign.pictureframe.tall.prefab",										   3 },
				{"assets/prefabs/deployable/signs/sign.pictureframe.xl.prefab",											   3 },
				{"assets/prefabs/deployable/signs/sign.pictureframe.xxl.prefab",										   3 },
				{"assets/prefabs/deployable/signs/sign.pole.banner.large.prefab",										   6 },
				{"assets/prefabs/deployable/signs/sign.post.double.prefab",												   3 },
				{"assets/prefabs/deployable/signs/sign.post.single.prefab",												   6 },
				{"assets/prefabs/deployable/signs/sign.post.town.prefab",												   6 },
				{"assets/prefabs/deployable/signs/sign.post.town.roof.prefab",											   6 },
				{"assets/prefabs/deployable/signs/sign.small.wood.prefab",												   3 },

				{"assets/prefabs/deployable/sleeping bag/sleepingbag_deployed.prefab",									   12 }, //200
				{"assets/prefabs/deployable/sleeping bag/sleepingbag_leather_deployed.prefab",							   12 }, //200

				{"assets/prefabs/deployable/water catcher/water_catcher_large.prefab",									   12 }, //200
				{"assets/prefabs/deployable/water catcher/water_catcher_small.prefab",									   3 }, //50

				{"assets/prefabs/misc/burlap sack/generic_world.prefab",												   100 },
				{"assets/prefabs/npc/bear/bear_corpse.prefab",															   100 },
				{"assets/prefabs/npc/boar/boar_corpse.prefab",															   100 },
				{"assets/prefabs/npc/chicken/chicken_corpse.prefab",													   100 },
				{"assets/prefabs/npc/horse/horse_corpse.prefab",														   100 },
				{"assets/prefabs/npc/stag/stag_corpse.prefab",															   100 },
				{"assets/prefabs/npc/wolf/wolf_corpse.prefab",															   100 },
				{"assets/prefabs/player/player_corpse.prefab}",															   100 },
			};

			Config["ItemsName/Damage"] = itemList;
			var messages = new Dictionary<string, object>();
			//messages.Add("TaskStart", "Очищаем карту от ненужных объектов. <color=#ffaaaa>Возможны небольшие подвисания!</color>");
			//messages.Add("TaskEnd", "Очистка карты завершена. Обработанно объектов: <color=#ffaaaa>{0}</color> Разрушенно объектов: <color=#ffaaaa>{1}</color>");
			Config["Messages"] = messages;
			Config["DecayIntervalInHours"] = 1;
			Config["MultiplierDamageBlocksWithoutCupboard"] = 5;
		}

		void Loaded()
		{
			try
			{
				int val;
				var blocksConfig = (List<object>)Config["BuildingBlocks"];
				foreach (var cfg in blocksConfig) buildingBlocks.Add(Convert.ToString(cfg));
				var damageConfig = (Dictionary<string, object>)Config["BuildingBlocksDamage"];
				foreach (var cfg in damageConfig) buildingBlocksDamage.Add(cfg.Key, (val = Convert.ToInt32(cfg.Value)) >= 0 ? val : 0);
				var itemConfig = (Dictionary<string, object>)Config["ItemsName/Damage"];
				foreach (var cfg in itemConfig) itemList.Add(cfg.Key, (val = Convert.ToInt32(cfg.Value)) >= 0 ? val : 0);
				var messagesConfig = (Dictionary<string, object>)Config["Messages"];
				foreach (var cfg in messagesConfig) messages[cfg.Key] = Convert.ToString(cfg.Value);
				decayInterval = Convert.ToInt32(Config["DecayIntervalInHours"]);
				if (decayInterval <= 0) decayInterval = 1;
				multiDamage = Convert.ToInt32(Config["MultiplierDamageBlocksWithoutCupboard"]);
				timerTwigs = timer.Repeat((decayInterval * 3600), 0, TwigsStart);
				if (ConVar.Decay.scale > 0f) ConVar.Decay.scale = 0f;
			}
			catch (Exception ex)
			{
				PrintError("Не удалось загрузить файл конфигурации: {0}", ex.Message);
			}
		}

		private void TwigsStart()
		{
			//MessageToAll(messages["TaskStart"]);

			int entitesCheck = 0;
			int entitesDestroyed = 0;
			string entityName;
			int amount;

			var allEntites = UnityEngine.Object.FindObjectsOfType<BaseCombatEntity>();

			foreach (var entity in allEntites)
			{
				if (entity.isDestroyed) continue;

				entitesCheck++;
				entityName = entity.name;

				if (entityName.Contains("tool cupboard"))
				{
					if (itemList.TryGetValue(entityName, out amount) && amount != 0)
					{
						if (entityName == "assets/prefabs/deployable/tool cupboard/cupboard.tool.deployed.prefab")
						{
							if (FindBuildingBlockSphere(entity.transform.position, 4))
								continue;
						}
						else if (FindCupboardToolSphere(entity.transform.position, 30))
							continue;

						entity.Hurt(amount, DamageType.Decay, null, false);

						if (entity.isDestroyed)
							entitesDestroyed++;
					}
				}
				else if (entityName.Contains("build"))
				{
					if (!buildingBlocks.Contains(entityName)) continue;
					var block = entity as BuildingBlock;
					if (block == null)
						continue;

					string gradeName = block.grade.ToString();
					if (buildingBlocksDamage.TryGetValue(gradeName, out amount) && amount != 0)
					{
						if (gradeName != "Twigs" && !FindCupboardToolSphere(entity.transform.position, 30))
							amount *= multiDamage;

						block.Hurt(amount, DamageType.Decay, null, false);
						if (block.isDestroyed) entitesDestroyed++;
					}
				}
				else if (entityName.Contains("signs"))
				{
					if (itemList.TryGetValue(entityName, out amount) && amount != 0)
					{
						if (FindCupboardToolSphere(entity.transform.position, 30)) continue;
						entity.Hurt(amount, DamageType.Decay, null, false);
						if (entity.isDestroyed) entitesDestroyed++;
					}
				}
			}
			MessageToAll(string.Format(messages["TaskEnd"], entitesCheck.ToString(), entitesDestroyed.ToString()));
		}

		void Unload()
		{
			timerTwigs.Destroy();
		}

		private bool FindCupboardToolSphere(Vector3 position, int radius)
		{
			var allColliders = Physics.OverlapSphere(position, radius);
			foreach (Collider collider in allColliders)
			{
				if (collider.name == "assets/prefabs/deployable/tool cupboard/cupboard.tool.deployed.prefab")
					return true;
			}
			return false;
		}

		private bool FindBuildingBlockSphere(Vector3 position, int radius)
		{
			var ray = new Ray(position, Vector3.down);

			var raycastHits = Physics.SphereCastAll(position, radius, Vector3.down, 4, 2097152);
			if (!raycastHits.Any())
				return false;

			foreach (var raycastHit in raycastHits)
			{
				var hitEntity = raycastHit.GetEntity();
				Puts("	collision :{0}", raycastHit.collider.name);
				if (hitEntity == null)
					return false;

				var buildingBlock = hitEntity.GetComponentInParent<BuildingBlock>() ?? hitEntity.GetComponent<BuildingBlock>();
				if (buildingBlock != null)
					return true;
			}
			return false;
		}

		private void MessageToAll(string message)
		{
			ConsoleSystem.Broadcast("chat.add", 0, string.Format(messages["ChatFormat"], message));
		}
	}
}
