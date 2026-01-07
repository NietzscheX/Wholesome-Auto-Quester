using robotManager.FiniteStateMachine;
using robotManager.Helpful;
using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;

namespace Wholesome_Auto_Quester.PrivateServer.States.Equipment
{
    public class IdleEquipmentState : State
    {
        private Managers.EquipmentManager _equipmentManager;
        
        public IdleEquipmentState(Managers.EquipmentManager equipmentManager)
        {
            _equipmentManager = equipmentManager;
            Priority = 14;
        }
        
        public override string DisplayName => "WAQ-Private - Equipment Idle";
        
        public override bool NeedToRun
        {
            get
            {
                // 空闲状态永远不应该运行
                // 当装备系统没有工作时，应该把控制权交还给主任务系统
                return false;
            }
        }
        
        public override void Run()
        {
            // 空闲状态,不做任何操作
        }
    }
}
