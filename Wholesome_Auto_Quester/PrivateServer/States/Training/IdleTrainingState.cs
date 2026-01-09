using robotManager.FiniteStateMachine;
using robotManager.Helpful;
using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;
using Wholesome_Auto_Quester.PrivateServer.Models;

namespace Wholesome_Auto_Quester.PrivateServer.States.Training
{
    public class IdleTrainingState : State
    {
        private Managers.TrainingManager _trainingManager;
        private TrainingConfig _config;
        
        public IdleTrainingState(Managers.TrainingManager trainingManager, TrainingConfig config)
        {
            _trainingManager = trainingManager;
            _config = config;
            Priority = 15;
        }
        
        public override string DisplayName => "WAQ-Private - Training Idle";
        
        public override bool NeedToRun
        {
            get
            {
                if (_trainingManager.IsActive
                    || Fight.InFight
                    || ObjectManager.Me.IsOnTaxi
                    || ObjectManager.Me.IsDead
                    || !ObjectManager.Me.IsValid
                    || Conditions.InGameAndConnectedAndAliveAndProductStartedNotInPause == false)
                {
                    return false;
                }
                    
                int level = (int)ObjectManager.Me.Level;
                
                // 使用 TrainingManager 的综合检查方法
                // 这会检查职业技能、武器训练、骑术、双天赋等所有类型
                return _trainingManager.NeedsAnyTraining(level);
            }
        }
        
        public override void Run()
        {
            int level = (int)ObjectManager.Me.Level;
            Logging.Write($"[WAQ-Private] =============================================");
            Logging.Write($"[WAQ-Private] Level {level} reached! Starting auto-training...");
            Logging.Write($"[WAQ-Private] =============================================");
            
            _trainingManager.StartTraining();
        }
    }
}
