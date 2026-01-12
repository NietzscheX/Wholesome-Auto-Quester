using robotManager.FiniteStateMachine;
using robotManager.Helpful;
using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;

namespace Wholesome_Auto_Quester.PrivateServer.States.Equipment
{
    /// <summary>
    /// 清理背包状态 - 删除损坏和不匹配的装备
    /// 此状态在检测到需要装备刷新时激活
    /// </summary>
    public class CleanBagsState : State
    {
        private Managers.EquipmentManager _equipmentManager;
        private Managers.TeleportManager _teleportManager;
        
        public CleanBagsState(Managers.EquipmentManager equipmentManager, Managers.TeleportManager teleportManager = null)
        {
            _equipmentManager = equipmentManager;
            _teleportManager = teleportManager;
            Priority = 14;
        }
        
        public override string DisplayName => "WAQ-Private - Clean Bags";
        
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
                
                // 当装备刷新进入清理阶段时激活
                return _equipmentManager.CurrentEquipmentPhase == Managers.EquipmentManager.EquipmentPhase.CleaningBags;
            }
        }
        
        public override void Run()
        {
            Logging.Write("[WAQ-Private] ========================================");
            Logging.Write("[WAQ-Private] Starting equipment refresh cycle");
            Logging.Write("[WAQ-Private] Step 1: Cleaning bags and removing damaged equipment");
            Logging.Write("[WAQ-Private] ========================================");
            
            _equipmentManager.ExecuteCleanBags();
            
            // 进入购买阶段
            _equipmentManager.SetPhase(Managers.EquipmentManager.EquipmentPhase.PurchasingEquipment);
        }
    }
}
