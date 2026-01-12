using robotManager.FiniteStateMachine;
using robotManager.Helpful;
using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;
using System.Threading;

namespace Wholesome_Auto_Quester.PrivateServer.States.Equipment
{
    /// <summary>
    /// 装备物品状态 - 将购买的物品装备到角色身上
    /// 完成后验证,如果武器仍缺失则重试购买
    /// </summary>
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
            
            // 等待装备生效
            Thread.Sleep(1500);
            
            // 验证武器是否正确装备
            if (_equipmentManager.NeedsWeaponCheck())
            {
                _retryCount++;
                
                if (_retryCount >= MAX_RETRIES)
                {
                    Logging.WriteError($"[WAQ-Private] ✗ Equipment failed after {MAX_RETRIES} attempts! Check NPC inventory.");
                    _retryCount = 0;
                    _equipmentManager.MarkRefreshComplete(false);
                    _equipmentManager.SetPhase(Managers.EquipmentManager.EquipmentPhase.Idle);
                    return;
                }
                
                Logging.Write($"[WAQ-Private] ⚠ Weapon check failed! Retry {_retryCount}/{MAX_RETRIES}...");
                
                // 回到购买阶段重新尝试
                _equipmentManager.SetPhase(Managers.EquipmentManager.EquipmentPhase.PurchasingEquipment);
                return;
            }
            
            // 成功!
            _retryCount = 0;
            _equipmentManager.MarkRefreshComplete(true);
            
            Logging.Write("[WAQ-Private] ========================================");
            Logging.Write("[WAQ-Private] ✓ Equipment refresh complete!");
            Logging.Write("[WAQ-Private] ========================================");
            
            // 直接回到 Idle 状态,让 FSM 决定下一步做什么
            // 不需要手动传送回去,其他任务状态会自动接管
            _equipmentManager.SetPhase(Managers.EquipmentManager.EquipmentPhase.Idle);
        }
    }
}
