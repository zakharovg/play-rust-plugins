using System.Linq;
using Oxide.Core;

namespace Oxide.Plugins
{
	[Info("PrintItemNames", "ZakBelg", 0.1, ResourceId = 1)]
	[Description("Print item info to file")]
	public class PrintItemNames : RustPlugin
	{
		void OnServerInitialized()
		{
			var items = ItemManager.GetItemDefinitions();
			var infos = items.Select(i =>
			new[]{
				i.shortname,
				i.category.ToString(),
				i.itemType.ToString(),
				i.itemid.ToString()
			}).ToArray();
			Interface.Oxide.DataFileSystem.WriteObject("item_infos", infos);
		}
	}
}
