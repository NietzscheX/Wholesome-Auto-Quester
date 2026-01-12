using robotManager.FiniteStateMachine;
using robotManager.Helpful;
using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;
using System.Threading;

namespace Wholesome_Auto_Quester.PrivateServer.States.Equipment
{
    public class EquipItemsState : State
    {
        private Managers.EquipmentManager _equipmentManager;
        private int _retryCount = 0;
        private const int MAX_RETRIES = 3;
        
        public EquipItemsState(Managers.EquipmentManager equipmentManager)
        {
            _equipmentManager = equipmentManager;
            Priority = 14;
        }
        
        public override string DisplayName => "WAQ-Private - Equip Items";
        
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
                
                return _equipmentManager.CurrentEquipmentPhase == Managers.EquipmentManager.EquipmentPhase.EquippingItems;
            }
        }
        
        public override void Run()
        {
            Logging.Write("[WAQ-Private] Step 3: Equipping items");
            
            _equipmentManager.ExecuteEquipItems();
            
            // 等待一下让游戏更新装备状态
            Thread.Sleep(1000);
            
            // 验证武器是否正确装备
            if (_equipmentManager.NeedsWeaponCheck())
            {
                _retryCount++;
                
                if (_retryCount >= MAX_RETRIES)
                {
                    Logging.WriteError($"[WAQ-Private] ✗ Equipment failed after {MAX_RETRIES} attempts! Aborting refresh cycle.");
                    _retryCount = 0;
                    _equipmentManager.MarkRefreshComplete(false);
                    _equipmentManager.SetPhase(Managers.EquipmentManager.EquipmentPhase.Idle);
                    return;
                }
                
                Logging.Write($"[WAQ-Private] ⚠ Weapon check failed after equipping! Retry {_retryCount}/{MAX_RETRIES}...");
                Logging.Write("[WAQ-Private] Returning to purchase phase to re-acquire missing weapons...");
                
                // 回到购买阶段重新购买
                _equipmentManager.SetPhase(Managers.EquipmentManager.EquipmentPhase.PurchasingEquipment);
                return;
            }
            
            // 装备成功,重置重试计数
            _retryCount = 0;
            
            // 切换到传送返回阶段（如果有保存的返回点）
            if (_equipmentManager.HasSavedReturnLocation)
            {
                Logging.Write("[WAQ-Private] ✓ Equipment complete, preparing to teleport back...");
                _equipmentManager.SetPhase(Managers.EquipmentManager.EquipmentPhase.TeleportingBack);
            }
            else
            {
                Logging.Write("[WAQ-Private] ✓ Equipment refresh complete (no return teleport)");
                _equipmentManager.MarkRefreshComplete(true);
                _equipmentManager.SetPhase(Managers.EquipmentManager.EquipmentPhase.Idle);
            }
        }
    }
}
