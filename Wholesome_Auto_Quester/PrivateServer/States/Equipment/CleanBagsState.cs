using robotManager.FiniteStateMachine;
using robotManager.Helpful;
using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;
using Wholesome_Auto_Quester.PrivateServer.Managers;

namespace Wholesome_Auto_Quester.PrivateServer.States.Equipment
{
    public class CleanBagsState : State
    {
        private EquipmentManager _equipmentManager;
        private TeleportManager _teleportManager;
        
        public CleanBagsState(EquipmentManager equipmentManager, TeleportManager teleportManager = null)
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
                
                // 检查是否需要装备刷新并且还没开始
                if (!_equipmentManager.IsActive && _equipmentManager.NeedsRefresh())
                {
                    // 触发刷新流程，传入 TeleportManager 以便保存返回点
                    _equipmentManager.TriggerRefresh(_teleportManager);
                }
                
                return _equipmentManager.CurrentEquipmentPhase == EquipmentManager.EquipmentPhase.CleaningBags;
            }
        }
        
        public override void Run()
        {
            Logging.Write("[WAQ-Private] ========================================");
            Logging.Write("[WAQ-Private] Starting equipment refresh cycle");
            Logging.Write("[WAQ-Private] Step 1: Cleaning bags and removing damaged equipment");
            Logging.Write("[WAQ-Private] ========================================");
            
            _equipmentManager.ExecuteCleanBags();
            _equipmentManager.SetPhase(EquipmentManager.EquipmentPhase.PurchasingEquipment);
        }
    }
}
