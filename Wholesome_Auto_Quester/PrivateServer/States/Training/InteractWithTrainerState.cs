using robotManager.FiniteStateMachine;
using robotManager.Helpful;
using System;
using System.Threading;
using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;
using Wholesome_Auto_Quester.PrivateServer.Models;

namespace Wholesome_Auto_Quester.PrivateServer.States.Training
{
    public class InteractWithTrainerState : State
    {
        private Managers.TrainingManager _trainingManager;
        private TrainingConfig _config;
        
        public InteractWithTrainerState(Managers.TrainingManager trainingManager, TrainingConfig config)
        {
            _trainingManager = trainingManager;
            _config = config;
            Priority = 15;
        }
        
        public override string DisplayName => "WAQ-Private - Interact with Trainer";
        
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
                
                return _trainingManager.CurrentTrainingPhase == Managers.TrainingManager.TrainingPhase.InteractingWithTrainer;
            }
        }
        
        public override void Run()
        {
            Logging.Write("[WAQ-Private] Interacting with trainer and learning skills");
            
            for (int attempt = 0; attempt <= 5; attempt++)
            {
                Logging.Write($"[WAQ-Private] Attempt {attempt + 1} to interact with trainer");
                
                try
                {
                    Helpers.NpcInteractionHelper.InteractWithTrainer(
                        _config.TrainerNpcEntry,
                        _config.TrainerGossipOption
                    );
                    
                    Thread.Sleep(1500 + Usefuls.Latency);
                    
                    int isTrainerOpen = Lua.LuaDoString<int>("return ClassTrainerFrame:IsVisible() and 1 or 0");
                    if (isTrainerOpen > 0)
                    {
                        Logging.Write("[WAQ-Private] Trainer window opened successfully");
                        
                        Lua.LuaDoString("BuyTrainerService(0)");
                        Thread.Sleep(800 + Usefuls.Latency);
                        
                        SpellManager.UpdateSpellBook();
                        Thread.Sleep(500);
                        
                        Lua.LuaDoString("if ClassTrainerFrame:IsVisible() then ClassTrainerFrame:Hide() end");
                        
                        Logging.Write("[WAQ-Private] Training completed successfully");
                        _trainingManager.SetPhase(Managers.TrainingManager.TrainingPhase.TeleportingBack);
                        return;
                    }
                    else
                    {
                        Logging.Write("[WAQ-Private] Trainer window did not open, retrying...");
                    }
                }
                catch (Exception ex)
                {
                    Logging.WriteError($"[WAQ-Private] Error during trainer interaction: {ex.Message}");
                }
                
                Thread.Sleep(1000);
            }
            
            Logging.WriteError("[WAQ-Private] Failed to interact with trainer after 6 attempts");
            _trainingManager.SetPhase(Managers.TrainingManager.TrainingPhase.TeleportingBack);
        }
    }
}
