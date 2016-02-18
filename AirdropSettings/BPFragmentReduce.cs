using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
	[Info("BPFragmentReduce", "Nogrod", "1.0.9")]
	internal class BPFragmentReduce : RustPlugin
	{
		private static readonly List<Item> _itemsToTake = new List<Item>();

		void OnServerInitialized()
		{
			var item = ItemManager.CreateByName("blueprint_fragment");
			_itemsToTake.Add(item);
		}

		void OnItemAddedToContainer(ItemContainer container, Item item)
		{
			if (container == null || item == null)
				return;

			var lootContainer = container.entityOwner as LootContainer;
			if (lootContainer == null)
				return;

			if (!lootContainer.distributeFragments)
				return;

			if (!item.info.name.Equals("blueprint_fragment.item", StringComparison.OrdinalIgnoreCase))
				return;

			foreach (var containerItem in container.itemList)
			{
				if (!containerItem.info.name.Equals("blueprint_fragment.item", StringComparison.OrdinalIgnoreCase))
					continue;

				if (lootContainer.LookupPrefab().name.Contains("barrel"))
					containerItem.amount = Core.Random.Range(1, 4);
				else
					containerItem.amount = Core.Random.Range(3, 12);

				containerItem.MarkDirty();

				break;
			}
		}
	}
}
