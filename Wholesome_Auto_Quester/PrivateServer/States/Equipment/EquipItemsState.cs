using robotManager.FiniteStateMachine;
using robotManager.Helpful;
using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;

namespace Wholesome_Auto_Quester.PrivateServer.States.Equipment
{
    public class EquipItemsState : State
    {
        private Managers.EquipmentManager _equipmentManager;
        
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
            
            Logging.Write("[WAQ-Private] Equipment refresh complete");
            _equipmentManager.SetPhase(Managers.EquipmentManager.EquipmentPhase.Idle);
        }
    }
}
