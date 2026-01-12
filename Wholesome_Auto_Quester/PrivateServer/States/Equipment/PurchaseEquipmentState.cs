using robotManager.FiniteStateMachine;
using robotManager.Helpful;
using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;
using Wholesome_Auto_Quester.PrivateServer.Models;
using System.Threading;

namespace Wholesome_Auto_Quester.PrivateServer.States.Equipment
{
    public class PurchaseEquipmentState : State
    {
        private Managers.EquipmentManager _equipmentManager;
        private EquipmentConfig _config;
        private int _purchaseRetries = 0;
        private const int MAX_PURCHASE_RETRIES = 3;
        
        public PurchaseEquipmentState(Managers.EquipmentManager equipmentManager, EquipmentConfig config)
        {
            _equipmentManager = equipmentManager;
            _config = config;
            Priority = 14;
        }
        
        public override string DisplayName => "WAQ-Private - Purchase Equipment";
        
        public override bool NeedToRun
        {
            get
            {
                if (Fight.InFight
                    || ObjectManager.Me.IsOnTaxi
                    || ObjectManager.Me.IsDead
                    || !ObjectManager.Me.IsValid
                    || Conditions.InGameAndConnectedAndAliveAndProductStartedNotInPause == false)
                {
                    return false;
                }
                
                return _equipmentManager.CurrentEquipmentPhase == Managers.EquipmentManager.EquipmentPhase.PurchasingEquipment;
            }
        }
        
        public override void Run()
        {
            Logging.Write("[WAQ-Private] Step 2: Purchasing equipment and supplies");
            
            // 记录购买前的背包物品数量(武器槽位相关)
            int mainHandItemId = 0;
            int offHandItemId = 0;
            
            if (_equipmentManager.CurrentClassProfile?.Slots != null)
            {
                if (_equipmentManager.CurrentClassProfile.Slots.ContainsKey("MainHand"))
                    mainHandItemId = _equipmentManager.CurrentClassProfile.Slots["MainHand"].ItemId;
                if (_equipmentManager.CurrentClassProfile.Slots.ContainsKey("OffHand"))
                    offHandItemId = _equipmentManager.CurrentClassProfile.Slots["OffHand"].ItemId;
            }
            
            int mainHandCountBefore = mainHandItemId > 0 ? GetItemCount(mainHandItemId) : 0;
            int offHandCountBefore = offHandItemId > 0 ? GetItemCount(offHandItemId) : 0;
            
            Logging.Write($"[WAQ-Private] Before purchase: MainHand({mainHandItemId}) count={mainHandCountBefore}, OffHand({offHandItemId}) count={offHandCountBefore}");
            
            // 执行购买
            _equipmentManager.ExecutePurchaseEquipment();
            
            // 等待购买完成
            Thread.Sleep(1000);
            
            // 验证购买结果
            int mainHandCountAfter = mainHandItemId > 0 ? GetItemCount(mainHandItemId) : 0;
            int offHandCountAfter = offHandItemId > 0 ? GetItemCount(offHandItemId) : 0;
            
            Logging.Write($"[WAQ-Private] After purchase: MainHand({mainHandItemId}) count={mainHandCountAfter}, OffHand({offHandItemId}) count={offHandCountAfter}");
            
            // 检查是否购买成功(对于需要的武器)
            bool needsMainHand = _equipmentManager.NeedsWeaponCheck() && mainHandItemId > 0;
            bool purchaseFailed = false;
            
            if (needsMainHand)
            {
                // 检查主手武器是否已装备或已在背包中
                int equippedMainHand = GetEquippedItemId(16);
                if (equippedMainHand != mainHandItemId && mainHandCountAfter == 0)
                {
                    Logging.Write($"[WAQ-Private] ⚠ MainHand purchase may have failed! Equipped: {equippedMainHand}, InBag: {mainHandCountAfter}");
                    purchaseFailed = true;
                }
            }
            
            if (purchaseFailed)
            {
                _purchaseRetries++;
                if (_purchaseRetries >= MAX_PURCHASE_RETRIES)
                {
                    Logging.WriteError($"[WAQ-Private] ✗ Purchase failed after {MAX_PURCHASE_RETRIES} attempts! Check if NPC has the item.");
                    _purchaseRetries = 0;
                    _equipmentManager.MarkRefreshComplete(false);
                    _equipmentManager.SetPhase(Managers.EquipmentManager.EquipmentPhase.Idle);
                    return;
                }
                
                Logging.Write($"[WAQ-Private] Retrying purchase ({_purchaseRetries}/{MAX_PURCHASE_RETRIES})...");
                Thread.Sleep(2000);
                // 保持在 PurchasingEquipment 阶段,下一轮会重试
                return;
            }
            
            // 购买成功,重置重试计数,进入装备阶段
            _purchaseRetries = 0;
            _equipmentManager.SetPhase(Managers.EquipmentManager.EquipmentPhase.EquippingItems);
        }
        
        private int GetItemCount(int itemId)
        {
            return Lua.LuaDoString<int>($@"
                local count = GetItemCount({itemId});
                return count or 0;
            ");
        }
        
        private int GetEquippedItemId(int slotId)
        {
            return Lua.LuaDoString<int>($@"
                local itemLink = GetInventoryItemLink('player', {slotId});
                if not itemLink then return 0; end
                local itemId = tonumber(itemLink:match('item:(%d+)'));
                return itemId or 0;
            ");
        }
    }
}
