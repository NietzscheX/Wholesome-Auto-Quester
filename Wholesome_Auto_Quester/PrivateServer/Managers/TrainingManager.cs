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
            InteractingWithTrainer
            // 不再需要 TeleportingBack 和 ResumingProduct - FSM 会自动处理
        }
        
        private TrainingPhase _currentPhase = TrainingPhase.Idle;
        private float _savedPosX;
        private float _savedPosY;
        private float _savedPosZ;
        private int _savedMapId;
        private TrainingConfig _config;
        private HashSet<int> _trainedLevels = new HashSet<int>();
        
        // 训练冷却时间(防止重连后反复触发)
        private System.DateTime _lastTrainingTime = System.DateTime.MinValue;
        private const int TRAINING_COOLDOWN_MINUTES = 30; // 30分钟内不重复训练
        
        // === 新增：多类型训练支持 ===
        private Queue<TrainingType> _pendingTrainings = new Queue<TrainingType>();
        private TrainingType _currentTrainingType = TrainingType.ClassSkills;
        
        // 记录已完成的训练（用于避免重复）
        private bool _hasLearnedWeaponSkills = false;
        private HashSet<int> _ridingTrainedLevels = new HashSet<int>();
        private bool _hasDualTalent = false;
        
        // Public properties for states to access
        public bool IsActive => _currentPhase != TrainingPhase.Idle;
        public TrainingPhase CurrentTrainingPhase => _currentPhase;
        public TrainingType CurrentTrainingType => _currentTrainingType;
        public TrainingConfig Config => _config;
        
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
            // 检查是否已有双天赋
            _hasDualTalent = CheckHasDualTalent();
        }
        
        public bool HasTrainedAtLevel(int level)
        {
            return _trainedLevels.Contains(level);
        }
        
        /// <summary>
        /// 检查是否需要进行任何类型的训练
        /// </summary>
        public bool NeedsAnyTraining(int playerLevel)
        {
            return NeedsClassTraining(playerLevel) 
                || NeedsWeaponTraining(playerLevel) 
                || NeedsRidingTraining(playerLevel) 
                || NeedsDualTalent(playerLevel);
        }
        
        /// <summary>
        /// 检查是否需要职业技能训练
        /// </summary>
        public bool NeedsClassTraining(int playerLevel)
        {
            // 检查训练冷却时间(防止重连后反复触发)
            var timeSinceLastTraining = (System.DateTime.Now - _lastTrainingTime).TotalMinutes;
            if (timeSinceLastTraining < TRAINING_COOLDOWN_MINUTES)
            {
                return false;
            }
            
            if (HasTrainedAtLevel(playerLevel)) return false;
            
            if (_config.TrainOnEvenLevels && playerLevel % 2 == 0)
                return true;
                
            if (_config.TrainAtLevels != null && _config.TrainAtLevels.Contains(playerLevel))
                return true;
                
            return false;
        }
        
        /// <summary>
        /// 检查是否需要武器训练（只在首次时学一次）
        /// </summary>
        public bool NeedsWeaponTraining(int playerLevel)
        {
            if (!_config.EnableWeaponTraining) return false;
            if (_hasLearnedWeaponSkills) return false;
            // 武器训练通常在低等级就学完，设个最低等级限制
            if (playerLevel < 2) return false;
            return true;
        }
        
        /// <summary>
        /// 检查是否需要骑术训练
        /// </summary>
        public bool NeedsRidingTraining(int playerLevel)
        {
            if (!_config.EnableRidingTraining) return false;
            if (_config.RidingTrainAtLevels == null) return false;
            
            foreach (int level in _config.RidingTrainAtLevels)
            {
                if (playerLevel >= level && !_ridingTrainedLevels.Contains(level))
                {
                    return true;
                }
            }
            return false;
        }
        
        /// <summary>
        /// 检查是否需要双天赋
        /// </summary>
        public bool NeedsDualTalent(int playerLevel)
        {
            if (!_config.EnableDualTalent) return false;
            if (_hasDualTalent) return false;
            if (playerLevel < _config.DualTalentMinLevel) return false;
            
            // 重新检查游戏中是否已购买
            _hasDualTalent = CheckHasDualTalent();
            return !_hasDualTalent;
        }
        
        /// <summary>
        /// 检查是否已购买双天赋
        /// </summary>
        private bool CheckHasDualTalent()
        {
            try
            {
                int numGroups = Lua.LuaDoString<int>("return GetNumTalentGroups() or 1");
                return numGroups > 1;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// 开始训练流程，收集所有需要的训练类型
        /// </summary>
        public void StartTraining()
        {
            if (IsActive)
            {
                Logging.Write("[WAQ-Private] Training already in progress");
                return;
            }
            
            int playerLevel = (int)ObjectManager.Me.Level;
            
            // 收集所有需要的训练类型
            _pendingTrainings.Clear();
            
            if (NeedsClassTraining(playerLevel))
            {
                _pendingTrainings.Enqueue(TrainingType.ClassSkills);
                Logging.Write("[WAQ-Private] Queued: Class Skills training");
            }
            
            if (NeedsWeaponTraining(playerLevel))
            {
                _pendingTrainings.Enqueue(TrainingType.WeaponSkills);
                Logging.Write("[WAQ-Private] Queued: Weapon Skills training");
            }
            
            if (NeedsRidingTraining(playerLevel))
            {
                _pendingTrainings.Enqueue(TrainingType.RidingSkills);
                Logging.Write("[WAQ-Private] Queued: Riding Skills training");
            }
            
            if (NeedsDualTalent(playerLevel))
            {
                _pendingTrainings.Enqueue(TrainingType.DualTalent);
                Logging.Write("[WAQ-Private] Queued: Dual Talent purchase");
            }
            
            if (_pendingTrainings.Count == 0)
            {
                Logging.Write("[WAQ-Private] No training needed");
                return;
            }
            
            // 设置第一个训练类型
            _currentTrainingType = _pendingTrainings.Dequeue();
            Logging.Write($"[WAQ-Private] Starting with: {_currentTrainingType}");
            
            // 记录训练时间
            _lastTrainingTime = System.DateTime.Now;
            
            // 记录当前位置
            SaveCurrentPosition();
            
            _currentPhase = TrainingPhase.UsingHearthstone;
        }
        
        private void SaveCurrentPosition()
        {
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

            _savedPosX = pos.X;
            _savedPosY = pos.Y;
            _savedPosZ = pos.Z;
            _savedMapId = Usefuls.ContinentId;

            if (_savedPosX == 0 && _savedPosY == 0)
            {
                Logging.WriteError("[WAQ-Private] ✗ Critical: Failed to get player position! Teleport back will be disabled.");
            }
            
            Logging.Write($"[WAQ-Private] Saved position: ({_savedPosX:F1}, {_savedPosY:F1}, {_savedPosZ:F1}) on map {_savedMapId}");
        }
        
        public void SetPhase(TrainingPhase phase)
        {
            _currentPhase = phase;
            Logging.Write($"[WAQ-Private] Training phase: {phase}");
        }
        
        /// <summary>
        /// 标记当前训练类型完成，检查是否有更多训练
        /// </summary>
        public void MarkCurrentTrainingComplete()
        {
            int level = (int)ObjectManager.Me.Level;
            
            switch (_currentTrainingType)
            {
                case TrainingType.ClassSkills:
                    _trainedLevels.Add(level);
                    Logging.Write($"[WAQ-Private] ✓ Class skills trained at level {level}");
                    break;
                    
                case TrainingType.WeaponSkills:
                    _hasLearnedWeaponSkills = true;
                    Logging.Write("[WAQ-Private] ✓ Weapon skills trained");
                    break;
                    
                case TrainingType.RidingSkills:
                    // 标记当前等级以下的所有骑术等级为已训练
                    foreach (int ridingLevel in _config.RidingTrainAtLevels)
                    {
                        if (level >= ridingLevel)
                        {
                            _ridingTrainedLevels.Add(ridingLevel);
                        }
                    }
                    Logging.Write($"[WAQ-Private] ✓ Riding skills trained at level {level}");
                    break;
                    
                case TrainingType.DualTalent:
                    _hasDualTalent = true;
                    Logging.Write("[WAQ-Private] ✓ Dual Talent purchased");
                    break;
            }
            
            // 检查是否还有更多训练
            if (_pendingTrainings.Count > 0)
            {
                _currentTrainingType = _pendingTrainings.Dequeue();
                Logging.Write($"[WAQ-Private] Next training: {_currentTrainingType}");
                // 继续与训练师交互
                _currentPhase = TrainingPhase.InteractingWithTrainer;
            }
            else
            {
                // 所有训练完成，直接回到 Idle
                // FSM 会自动让其他状态处理导航
                _currentPhase = TrainingPhase.Idle;
                Logging.Write("[WAQ-Private] ========================================");
                Logging.Write("[WAQ-Private] ✓ All training complete!");
                Logging.Write("[WAQ-Private] ========================================");
            }
        }
        
        public void CompleteTraining()
        {
            _pendingTrainings.Clear();
            _currentPhase = TrainingPhase.Idle;
            Logging.Write("[WAQ-Private] All training complete");
        }
    }
}
