//#define CALC_OLD
#define CALC_NEW
using System.Collections.Generic;
using System.Linq;
using Network;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Random = UnityEngine.Random;
//using ObjectList = Facepunch.ObjectList;

namespace Oxide.Plugins
{
    [Info("Reclaimer", "DefaultPlayer", "1.1")]
    public sealed class Reclaimer : RustPlugin
    {
        private static List<string> _bpBlacklist = new List<string>();
        private static List<string> _ingridientBlacklist = new List<string>();
        private static readonly Facepunch.ObjectList ObjBtn = new Facepunch.ObjectList("ReclaimBtnA");
        #region Utils
        private static BasePlayer GetPlayerFromContainer(ItemContainer container, Item item) =>
            item.GetOwnerPlayer() ??
            BasePlayer.activePlayerList.FirstOrDefault(
                p => p.inventory.loot.IsLooting() && p.inventory.loot.entitySource == container.entityOwner);
        private enum FxType { ITEM_BROKE, BP_BROKE, FAIL }
        #endregion

        protected override void LoadDefaultConfig()
        {
            Config["bpBlacklist"] = _bpBlacklist;
            Config["ingBlacklist"] = _ingridientBlacklist;
        }

        private void Loaded()
        {
            _bpBlacklist = Config.Get<List<string>>("bpBlacklist");
            _ingridientBlacklist = Config.Get<List<string>>("ingBlacklist");
        }

        private static void ShowReclaimButton(BasePlayer player, bool isBP = false)
        {
            var elements = new CuiElementContainer();
            var closeButton = new CuiButton
            {
                Button =
                    {
                        /*Close = "ReclaimBtn",*/
                        Command = "reclaim.do",
                        Color = !isBP ? "0.43 0.53 0.30" : "0.109 0.41 0.62",
                    },
                RectTransform =
                    {
                        AnchorMin = "0.6756176 0.1837503",
                        AnchorMax = "0.7821605 0.2328347"
                    },
                Text =
                    {
                        Text = !isBP ? "Разобрать" : "Разорвать",
                        FontSize = 22,
                        Align = TextAnchor.MiddleCenter
                    }
            };
            elements.Add(closeButton, name: "ReclaimBtnA", parent: "HUD/Overlay");
            CuiHelper.AddUi(player, elements);
        }

        private static void HideReclaimButton(BasePlayer player)
        {
            CommunityEntity.ServerInstance.ClientRPCEx(new SendInfo { connection = player.net.connection }, null, "DestroyUI", ObjBtn);
        }

        private class ButtonDestroyer : MonoBehaviour
        {
            private void PlayerStoppedLooting(BasePlayer player)
            {
                HideReclaimButton(player);
                Destroy(this);  
            }

            void OnDisable()
            {
            }
        }

        private static void Fx(BaseEntity target, FxType type)
        {
            string fx;
            switch (type)
            {
                case FxType.ITEM_BROKE:
                    fx = "assets/bundled/prefabs/fx/item_break.prefab";
                    break;
                case FxType.BP_BROKE:
                    fx = "assets/bundled/prefabs/fx/gestures/blueprint_fail.prefab";
                    break;
                case FxType.FAIL:
                default:
                    fx = "assets/bundled/prefabs/fx/build/repair_failed.prefab";
                    break;
            }
            EffectNetwork.Send(new Effect(fx, target, 0, Vector3.up, Vector3.zero) { scale = Random.Range(0f, 1f) });
        }

        private static bool IsReclaimable(Item item) => item != null /*&& !item.IsBlueprint()*/ && !item.IsBusy() && item.IsValid() && !_bpBlacklist.Contains(item.info.shortname) && ItemManager.FindBlueprint(item.info);

        [ConsoleCommand("reclaim.do")]
        private void CmdReclaimDo(ConsoleSystem.Arg arg)
        {
            var plr = arg.Player();
            if (!plr || plr.IsSleeping() || plr.isStalled || plr.IsWounded() || !plr.inventory.loot.IsLooting() || !plr.inventory.loot.entitySource.name.Contains("researchtable"))
                return;
            var container = plr.inventory.loot.containers[0];
            var table = (ResearchTable) container.entityOwner;
            if (!container.SlotTaken(0) || !IsReclaimable(container.GetSlot(0)) || table.IsResearching())
            {
                Fx(plr, FxType.FAIL);
                return;
            }
            var item = container.GetSlot(0);
            var bp = ItemManager.FindBlueprint(item.info);
            if (!bp)
            {
                Fx(plr, FxType.FAIL);
                return;
            }
            item.RemoveFromContainer();
            item.Remove(0f);

            if (item.GetOwnerPlayer() || item.GetWorldEntity() || item.IsValid())
                return;

            if (!item.IsBlueprint())
                ReturnIngridients(item, bp, plr);
            else
            {
                var pieces = Random.Range(5, 11);
                plr.GiveItem(ItemManager.Create(ItemManager.FindItemDefinition("blueprint_fragment"), pieces));
                Fx(plr, FxType.BP_BROKE);
                plr.ChatMessage($"Вы разорвали чертёж {item.info.displayName.translated} на <color=#00FF00>{pieces}</color> кусков.");
            }

        }

        private static void ReturnIngridients(Item item, ItemBlueprint bp, BasePlayer plr)
        {
            var percentList = new List<float>(bp.ingredients.Count);
            foreach (var ingredient in bp.ingredients.Where(ingredient => !_ingridientBlacklist.Contains(ingredient.itemDef.shortname)))
            {
                float percent;
                if (item.hasCondition && item.MaxStackable() == 1)
                {
                    var cond = item.conditionNormalized;
                    if (item.isBroken || cond.InRange(0f, .2f))
                        percent = .3f; // 30%
                    else if (cond.InRange(.2f, .7f))
                        percent = .5f; // 50%
                    else if (cond.InRange(.7f, .9f))
                        percent = .7f; // 70%
                    else
                        percent = .9f; // 90%
                }
                else
                    percent = Random.Range(.5f, .9f); // Обычные вещи от 50 до 90%.

                percentList.Add(percent);
	            var amountOfIngridientForStackOfItems = GetAmountOfIngridientForStackOfItems(item, bp, ingredient);
				
	            var amount = Mathf.Floor(amountOfIngridientForStackOfItems * percent);

                if (amount <= 0 || amount >= amountOfIngridientForStackOfItems || amount >= int.MaxValue)
                    amount = 1;

                var ret = ItemManager.Create(ingredient.itemDef, (int)amount);
                plr.GiveItem(ret);
            }
            Fx(plr, FxType.ITEM_BROKE);
            plr.ChatMessage($"Вы вытащили из <color=#00FF00>х{item.amount}</color> {item.info.displayName.translated} ~<color=#00FF00>{Mathf.Round(percentList.Average() * 100)}%</color> ресурсов.");
        }

	    private static float GetAmountOfIngridientForStackOfItems(Item item, ItemBlueprint bp, ItemAmount ingredient)
	    {
		    return ingredient.amount * item.amount / bp.amountToCreate;
	    }

	    private static bool ProcessContainerEvent(ItemContainer container, Item item, out BasePlayer player)
        {
            player = null;
            if (!container.entityOwner || !container.entityOwner.name.Contains("researchtable"))
                return false;
            return (player = GetPlayerFromContainer(container, item));
        }

        private void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            BasePlayer plr;
            if (!ProcessContainerEvent(container, item, out plr) || !IsReclaimable(container.GetSlot(0)))
                return;
            HideReclaimButton(plr);
            ShowReclaimButton(plr, item.IsBlueprint());
        }

        private void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            BasePlayer plr;
            if (!ProcessContainerEvent(container, item, out plr))
                return;
            if (!container.SlotTaken(0))
                HideReclaimButton(plr);
        }

        private void OnPlayerLoot(PlayerLoot lootInventory, BaseEntity targetEntity)
        {
            var plr = lootInventory.GetComponent<BasePlayer>();
            if (!plr || !targetEntity.name.Contains("researchtable"))
                return;
            lootInventory.entitySource.gameObject.AddComponent<ButtonDestroyer>();
            var container = (StorageContainer) lootInventory.entitySource;
            if (!IsReclaimable(container.inventory.GetSlot(0)))
                return;
            HideReclaimButton(plr);
            ShowReclaimButton(plr, container.inventory.GetSlot(0).IsBlueprint());
        }

    }
}
static class NumExt
{
    public static bool InRange(this float value, float minimum, float maximum) => value >= minimum && value <= maximum;
}
