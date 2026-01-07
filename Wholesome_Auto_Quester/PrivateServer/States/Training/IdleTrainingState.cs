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
                
                if (_trainingManager.HasTrainedAtLevel(level))
                    return false;
                
                // 优先检查 TrainAtLevels 列表
                if (_config.TrainAtLevels != null && _config.TrainAtLevels.Count > 0)
                {
                    return _config.TrainAtLevels.Contains(level);
                }
                
                // 如果 TrainAtLevels 为空，则使用 TrainOnEvenLevels 模式
                if (_config.TrainOnEvenLevels)
                {
                    return level % 2 == 0;
                }
                    
                return false;
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
