using robotManager.FiniteStateMachine;
using robotManager.Helpful;
using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;

namespace Wholesome_Auto_Quester.PrivateServer.States.Training
{
    public class ResumeProductState : State
    {
        private Managers.TrainingManager _trainingManager;
        
        public ResumeProductState(Managers.TrainingManager trainingManager)
        {
            _trainingManager = trainingManager;
            Priority = 15;
        }
        
        public override string DisplayName => "WAQ-Private - Resume Product";
        
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
                
                return _trainingManager.CurrentTrainingPhase == Managers.TrainingManager.TrainingPhase.ResumingProduct;
            }
        }
        
        public override void Run()
        {
            Logging.Write("[WAQ-Private] Training complete, resuming product");
            _trainingManager.CompleteTraining();
        }
    }
}
