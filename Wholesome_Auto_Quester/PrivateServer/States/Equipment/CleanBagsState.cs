using robotManager.FiniteStateMachine;
using robotManager.Helpful;
using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;

namespace Wholesome_Auto_Quester.PrivateServer.States.Equipment
{
    public class CleanBagsState : State
    {
        private Managers.EquipmentManager _equipmentManager;
        
        public CleanBagsState(Managers.EquipmentManager equipmentManager)
        {
            _equipmentManager = equipmentManager;
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
            _equipmentManager.SetPhase(Managers.EquipmentManager.EquipmentPhase.PurchasingEquipment);
        }
    }
}
