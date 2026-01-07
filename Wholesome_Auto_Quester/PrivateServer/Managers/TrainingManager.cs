using robotManager.Helpful;
using System.Collections.Generic;
using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;
using Wholesome_Auto_Quester.PrivateServer.Models;

namespace Wholesome_Auto_Quester.PrivateServer.Managers
{
    public class TrainingManager
    {
        public enum TrainingPhase
        {
            Idle,
            RecordingPosition,
            UsingHearthstone,
            TravelingToTrainer,
            InteractingWithTrainer,
            TeleportingBack,
            ResumingProduct
        }
        
        private TrainingPhase _currentPhase = TrainingPhase.Idle;
        private float _savedPosX;
        private float _savedPosY;
        private float _savedPosZ;
        private int _savedMapId;
        private TrainingConfig _config;
        private HashSet<int> _trainedLevels = new HashSet<int>();
        
        // Public properties for states to access
        public bool IsActive => _currentPhase != TrainingPhase.Idle;
        public TrainingPhase CurrentTrainingPhase => _currentPhase;
        
        public float SavedPosX => _savedPosX;
        public float SavedPosY => _savedPosY;
        public float SavedPosZ => _savedPosZ;
        public int SavedMapId => _savedMapId;
        
        public int HearthstoneEntry => _config.HearthstoneEntry;
        public int TeleportItemEntry => _config.TeleportItemEntry;
        public bool UseCustomTeleport => _config.UseCustomTeleport;
        public List<string> TeleportMenuPath => _config.TeleportMenuPath;
        
        public TrainingManager(TrainingConfig config)
        {
            _config = config;
        }
        
        public bool HasTrainedAtLevel(int level)
        {
            return _trainedLevels.Contains(level);
        }
        
        public void StartTraining()
        {
            if (IsActive)
            {
                Logging.Write("[WAQ-Private] Training already in progress");
                return;
            }
            
            // 记录当前位置
            _savedPosX = ObjectManager.Me.Position.X;
            _savedPosY = ObjectManager.Me.Position.Y;
            _savedPosZ = ObjectManager.Me.Position.Z;
            _savedMapId = Usefuls.ContinentId;
            
            Logging.Write($"[WAQ-Private] Saved position: ({_savedPosX:F1}, {_savedPosY:F1}, {_savedPosZ:F1}) on map {_savedMapId}");
            
            _currentPhase = TrainingPhase.UsingHearthstone;
        }
        
        public void SetPhase(TrainingPhase phase)
        {
            _currentPhase = phase;
            Logging.Write($"[WAQ-Private] Training phase: {phase}");
        }
        
        public void CompleteTraining()
        {
            int level = (int)ObjectManager.Me.Level;
            _trainedLevels.Add(level);
            
            _currentPhase = TrainingPhase.Idle;
            Logging.Write("[WAQ-Private] Training complete");
        }
    }
}
