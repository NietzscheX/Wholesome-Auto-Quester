using robotManager.FiniteStateMachine;
using robotManager.Helpful;
using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;
using Wholesome_Auto_Quester.PrivateServer.Models;

namespace Wholesome_Auto_Quester.PrivateServer.States.Equipment
{
    public class PurchaseEquipmentState : State
    {
        private Managers.EquipmentManager _equipmentManager;
        private EquipmentConfig _config;
        
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
            
            _equipmentManager.ExecutePurchaseEquipment();
            _equipmentManager.SetPhase(Managers.EquipmentManager.EquipmentPhase.EquippingItems);
        }
    }
}
