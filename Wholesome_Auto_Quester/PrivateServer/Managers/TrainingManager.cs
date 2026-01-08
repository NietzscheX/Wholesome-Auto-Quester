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
            var pos = ObjectManager.Me.Position;
            
            // 如果坐标是 (0,0,0)，尝试重试
            if (pos.X == 0 && pos.Y == 0 && pos.Z == 0)
            {
                Logging.Write("[WAQ-Private] ⚠ Warning: Player position is (0,0,0), retrying...");
                for (int i = 0; i < 10; i++)
                {
                    System.Threading.Thread.Sleep(200);
                    pos = ObjectManager.Me.Position;
                    if (pos.X != 0 || pos.Y != 0 || pos.Z != 0) break;
                }
            }

            // 如果仍然是 (0,0,0)，尝试使用 自身的地址
            if (pos.X == 0 && pos.Y == 0 && pos.Z == 0)
            {
                pos = ObjectManager.Me.Position;
            }

            _savedPosX = pos.X;
            _savedPosY = pos.Y;
            _savedPosZ = pos.Z;
            _savedMapId = Usefuls.ContinentId;

            if (_savedPosX == 0 && _savedPosY == 0)
            {
                Logging.WriteError("[WAQ-Private] ✗ Critical: Failed to get player position! Teleport back will be disabled.");
            }
            
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
