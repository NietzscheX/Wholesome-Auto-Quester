using robotManager.FiniteStateMachine;
using robotManager.Helpful;
using System.Threading;
using wManager.Wow.Bot.Tasks;
using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;
using Wholesome_Auto_Quester.PrivateServer.Models;

namespace Wholesome_Auto_Quester.PrivateServer.States.Training
{
    public class TravelToTrainerState : State
    {
        private Managers.TrainingManager _trainingManager;
        private Managers.TeleportManager _teleportManager;
        private TrainingConfig _config;
        private bool _teleportAttempted = false;
        
        public TravelToTrainerState(Managers.TrainingManager trainingManager, 
                                    TrainingConfig config,
                                    Managers.TeleportManager teleportManager = null)
        {
            _trainingManager = trainingManager;
            _config = config;
            _teleportManager = teleportManager;
            Priority = 15;
        }
        
        public override string DisplayName => "WAQ-Private - Travel to Trainer";
        
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
                
                return _trainingManager.CurrentTrainingPhase == Managers.TrainingManager.TrainingPhase.TravelingToTrainer;
            }
        }
        
        public override void Run()
        {
            Vector3 trainerPos = new Vector3(
                _config.TrainerPosition.X,
                _config.TrainerPosition.Y,
                _config.TrainerPosition.Z
            );
            
            float distance = ObjectManager.Me.Position.DistanceTo(trainerPos);
            
            if (distance < 5f)
            {
                Logging.Write("[WAQ-Private] Arrived at trainer location");
                _trainingManager.SetPhase(Managers.TrainingManager.TrainingPhase.InteractingWithTrainer);
                _teleportAttempted = false;
                return;
            }
            
            // 智能传送逻辑
            if (_teleportManager != null && !_teleportAttempted)
            {
                int currentContinent = Usefuls.ContinentId;
                int trainerContinent = _config.TrainerMapId;
                
                if (_teleportManager.ShouldUseTeleport(
                    ObjectManager.Me.Position,
                    trainerPos,
                    currentContinent,
                    trainerContinent))
                {
                    Logging.Write($"[WAQ-Private] ========================================");
                    Logging.Write($"[WAQ-Private] 距离训练师 {distance:F1} 码, 将使用传送");
                    Logging.Write($"[WAQ-Private] ========================================");
                    
                    string faction = Managers.TeleportManager.GetPlayerFaction();
                    
                    var teleportLocation = _teleportManager.FindBestTeleportLocation(
                        trainerPos,
                        trainerContinent,
                        faction
                    );
                    
                    if (teleportLocation != null)
                    {
                        bool success = _teleportManager.ExecuteTeleport(teleportLocation);
                        
                        if (success)
                        {
                            Logging.Write($"[WAQ-Private] ✓ 传送成功! 从传送点前往训练师");
                            Thread.Sleep(2000);
                            _teleportAttempted = true;
                        }
                        else
                        {
                            Logging.WriteError("[WAQ-Private] 传送失败,使用普通方式前往");
                            _teleportAttempted = true;
                        }
                    }
                    else
                    {
                        Logging.Write("[WAQ-Private] 未找到合适的传送点,使用普通方式前往");
                        _teleportAttempted = true;
                    }
                }
            }
            
            // 普通跑路逻辑
            Logging.Write($"[WAQ-Private] Moving to trainer ({distance:F1} yards away)");
            GoToTask.ToPosition(trainerPos, 3f);
        }
    }
}
