using System.Linq;
using Rust;
using Rust;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using Debug = System.Diagnostics.Debug;

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
				"assets/bundled/prefabs/build/floor.prefab",
				"assets/bundled/prefabs/build/floor.triangle.prefab",
				"assets/bundled/prefabs/build/foundation.prefab",
				"assets/bundled/prefabs/build/foundation.steps.prefab",
				"assets/bundled/prefabs/build/foundation.triangle.prefab"
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
			var itemList = new Dictionary<string, object>()
			{
				{"assets/bundled/prefabs/items/cupboard.tool.deployed.prefab", 31}, // health: 500
				{"assets/bundled/prefabs/items/large_woodbox_deployed.prefab", 18}, // health: 300
				{"assets/bundled/prefabs/items/sleepingbag_leather_deployed.prefab", 12}, // health: 200
				{"assets/bundled/prefabs/items/woodbox_deployed.prefab", 9}, // health: 150
				{"assets/bundled/prefabs/items/campfire_deployed.prefab", 12}, // health: 50
				{"assets/bundled/prefabs/items/furnace_deployed.prefab", 23}, // health: 500
				{"assets/bundled/prefabs/items/lantern_deployed.prefab", 3}, // health: 50
				{"assets/bundled/prefabs/items/researchtable_deployed.prefab", 9}, // health: 200
				{"assets/bundled/prefabs/items/repairbench_deployed.prefab", 9}, // health: 200
				{"assets/bundled/prefabs/items/ladders/ladder.wooden.wall.prefab", 3}, // health: 50
				{"assets/bundled/prefabs/items/beartrap.prefab", 12}, // health: 200
				{"assets/bundled/prefabs/items/floor_spikes.prefab", 3}, // health: 50
				{"assets/bundled/prefabs/items/barricades/barricade.sandbags.prefab", 6}, // health: 100
				{"assets/bundled/prefabs/items/barricades/barricade.metal.prefab", 37}, // health: 600
				{"assets/bundled/prefabs/items/barricades/barricade.stone.prefab", 6}, // health: 100
				{"assets/bundled/prefabs/items/barricades/barricade.woodwire.prefab", 25}, // health: 400
				{"assets/bundled/prefabs/items/barricades/barricade.wood.prefab", 12}, // health: 200
				{"assets/bundled/prefabs/items/barricades/barricade.concrete.prefab", 12}, // health: 200
				{"assets/bundled/prefabs/signs/sign.medium.wood.prefab", 6}, // health: 100
				{"assets/bundled/prefabs/signs/sign.large.wood.prefab", 9}, // health: 150
				{"assets/bundled/prefabs/signs/sign.huge.wood.prefab", 12}, // health: 200
				{"assets/bundled/prefabs/signs/sign.small.wood.prefab", 3} // health: 50
			};
			Config["ItemsName/Damage"] = itemList;
			var messages = new Dictionary<string, object>();
			messages.Add("ChatFormat", "<size=16><color=#aaffaa>RusTme.ru</color></size>: {0}");
			messages.Add("TaskStart", "Запущена очистка карты от ненужных объектов. <color=#ffaaaa>Возможны небольшие подвисания!</color>");
			messages.Add("TaskEnd", "Очистка карты завершена. Обработанно объектов: <color=#ffaaaa>{0}</color> Разрушенно объектов: <color=#ffaaaa>{1}</color>");
			Config["Messages"] = messages;
			Config["DecayIntervalInHours"] = 3;
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
				if (decayInterval <= 0) decayInterval = 3;
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
			MessageToAll(messages["TaskStart"]);

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

				if (entityName.Contains("items"))
				{
					if (itemList.TryGetValue(entityName, out amount) && amount != 0)
					{
						if (entityName == "assets/bundled/prefabs/items/cupboard.tool.deployed.prefab")
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
				if (collider.name == "assets/bundled/prefabs/items/cupboard.tool.deployed.prefab")
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
