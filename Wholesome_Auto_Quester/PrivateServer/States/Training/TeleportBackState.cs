using robotManager.FiniteStateMachine;
using robotManager.Helpful;
using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;
using Wholesome_Auto_Quester.PrivateServer.Models;

namespace Wholesome_Auto_Quester.PrivateServer.States.Training
{
    public class TeleportBackState : State
    {
        private Managers.TrainingManager _trainingManager;
        private TrainingConfig _config;
        
        public TeleportBackState(Managers.TrainingManager trainingManager, TrainingConfig config)
        {
            _trainingManager = trainingManager;
            _config = config;
            Priority = 15;
        }
        
        public override string DisplayName => "WAQ-Private - Teleport Back";
        
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
                
                return _trainingManager.CurrentTrainingPhase == Managers.TrainingManager.TrainingPhase.TeleportingBack;
            }
        }
        
        public override void Run()
        {
            Logging.Write("[WAQ-Private] Teleporting back to saved position");

            if (_trainingManager.SavedPosX == 0 && _trainingManager.SavedPosY == 0)
            {
                Logging.WriteError("[WAQ-Private] Invalid saved position (0,0,0)! Cannot teleport back.");
                _trainingManager.SetPhase(Managers.TrainingManager.TrainingPhase.ResumingProduct);
                return;
            }
            
            if (Helpers.TeleportHelper.TeleportTo(
                _trainingManager.SavedPosX,
                _trainingManager.SavedPosY,
                _trainingManager.SavedPosZ,
                _trainingManager.SavedMapId,
                _config))
            {
                _trainingManager.SetPhase(Managers.TrainingManager.TrainingPhase.ResumingProduct);
            }
            else
            {
                Logging.WriteError("[WAQ-Private] Teleport back failed, resuming product anyway");
                _trainingManager.SetPhase(Managers.TrainingManager.TrainingPhase.ResumingProduct);
            }
        }
    }
}
