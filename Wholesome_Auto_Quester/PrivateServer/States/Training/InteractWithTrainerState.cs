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
            Logging.Write("[WAQ-Private] Interacting with trainer and learning all skills");
            
            int playerLevel = (int)ObjectManager.Me.Level;
            
            // 1. 学习职业技能
            LearnSkillsOfType(TrainingType.ClassSkills, _config.TrainerGossipOption);
            
            // 2. 学习武器技能（如果启用）
            if (_config.EnableWeaponTraining)
            {
                LearnSkillsOfType(TrainingType.WeaponSkills, _config.WeaponTrainerGossipOption);
            }
            
            // 3. 学习骑术（如果启用且达到等级）
            if (_config.EnableRidingTraining )
            {
                LearnSkillsOfType(TrainingType.RidingSkills, _config.RidingTrainerGossipOption);
            }
            
            // 4. 购买双天赋（如果启用且达到等级）
            if (_config.EnableDualTalent )
            {
                TryBuyDualTalent();
            }
            
            // 更新法术书
            SpellManager.UpdateSpellBook();
            Thread.Sleep(500);
            
            Logging.Write("[WAQ-Private] All training completed successfully");
            _trainingManager.MarkCurrentTrainingComplete();
        }
        
        /// <summary>
        /// 学习指定类型的技能
        /// </summary>
        private void LearnSkillsOfType(TrainingType type, string gossipOption)
        {
            Logging.Write($"[WAQ-Private] Learning {type} (gossip: {gossipOption})");
            
            for (int attempt = 0; attempt <= 3; attempt++)
            {
                try
                {
                    // 与NPC交互并选择对话选项
                    Helpers.NpcInteractionHelper.InteractWithTrainer(
                        _config.TrainerNpcEntry,
                        gossipOption
                    );
                    
                    Thread.Sleep(1500 + Usefuls.Latency);
                    
                    // 检查训练师窗口是否打开
                    int isTrainerOpen = Lua.LuaDoString<int>("return ClassTrainerFrame and ClassTrainerFrame:IsVisible() and 1 or 0");
                    if (isTrainerOpen > 0)
                    {
                        Logging.Write($"[WAQ-Private] Trainer window opened for {type}");
                        
                        // 切换到"可用"标签页,只显示可学习的技能
                        Lua.LuaDoString(@"
                            if ClassTrainerFrame and ClassTrainerFrame:IsVisible() then
                                -- 设置过滤器为只显示可用技能
                                -- available = 可用, unavailable = 不可用, used = 已学习
                                SetTrainerServiceTypeFilter('available', 1)
                                SetTrainerServiceTypeFilter('unavailable', 0)
                                SetTrainerServiceTypeFilter('used', 0)
                            end
                        ");
                        Thread.Sleep(300); // 等待过滤器生效
                        
                        // 学习所有可用技能
                        LearnAllAvailableServices();
                        
                        Thread.Sleep(800 + Usefuls.Latency);
                        
                        // 关闭窗口
                        Lua.LuaDoString("if ClassTrainerFrame and ClassTrainerFrame:IsVisible() then ClassTrainerFrame:Hide() end");
                        Thread.Sleep(500);
                        
                        Logging.Write($"[WAQ-Private] ✓ {type} training completed");
                        return;
                    }
                    else
                    {
                        Logging.Write($"[WAQ-Private] Trainer window did not open for {type}, attempt {attempt + 1}");
                    }
                }
                catch (Exception ex)
                {
                    Logging.WriteError($"[WAQ-Private] Error during {type} training: {ex.Message}");
                }
                
                Thread.Sleep(1000);
            }
            
            Logging.WriteError($"[WAQ-Private] Failed to learn {type} after attempts");
        }
        
        /// <summary>
        /// 学习训练师窗口中所有可用的技能
        /// </summary>
        private void LearnAllAvailableServices()
        {
            try
            {
                // 使用 Lua 一次性学习所有可用技能
                // 这样更高效，避免逐个检查
                int learnedCount = Lua.LuaDoString<int>(@"
                    local learned = 0
                    local availableFound = true
                    local maxIterations = 50  -- 防止无限循环
                    local iteration = 0
                    
                    while availableFound and iteration < maxIterations do
                        availableFound = false
                        iteration = iteration + 1
                        
                        local numServices = GetNumTrainerServices() or 0
                        
                        for i = 1, numServices do
                            local _, _, serviceType = GetTrainerServiceInfo(i)
                            if serviceType == 'available' then
                                BuyTrainerService(i)
                                learned = learned + 1
                                availableFound = true
                                -- 学习一个后列表可能变化，需要重新扫描
                                break
                            end
                        end
                    end
                    
                    return learned
                ");
                
                if (learnedCount > 0)
                {
                    Logging.Write($"[WAQ-Private] ✓ Learned {learnedCount} skills");
                }
                else
                {
                    Logging.Write("[WAQ-Private] No available skills to learn");
                }
                
                Thread.Sleep(500 + Usefuls.Latency);
            }
            catch (Exception ex)
            {
                Logging.WriteError($"[WAQ-Private] Error learning services: {ex.Message}");
                // 降级：尝试直接使用 BuyTrainerService(0)
                Lua.LuaDoString("BuyTrainerService(0)");
            }
        }
        
        /// <summary>
        /// 尝试购买双天赋
        /// </summary>
        private void TryBuyDualTalent()
        {
            Logging.Write("[WAQ-Private] Attempting to purchase Dual Talent...");
            
            // 检查是否已有双天赋
            int numGroups = Lua.LuaDoString<int>("return GetNumTalentGroups() or 1");
            if (numGroups > 1)
            {
                Logging.Write("[WAQ-Private] Dual Talent already purchased, skipping");
                return;
            }
            
            try
            {
                // 与NPC交互并选择双天赋选项
                Helpers.NpcInteractionHelper.InteractWithTrainer(
                    _config.TrainerNpcEntry,
                    _config.DualTalentGossipOption
                );
                
                Thread.Sleep(1500 + Usefuls.Latency);
                
                // 尝试购买双天赋
                Lua.LuaDoString("BuyDualTalentSpec()");
                Thread.Sleep(500);
                
                // 确认弹窗（如果有）
                Lua.LuaDoString(@"
                    if StaticPopup1 and StaticPopup1:IsVisible() then
                        local button = StaticPopup1.button1
                        if button then button:Click() end
                    end
                ");
                
                Thread.Sleep(1000);
                
                // 验证购买成功
                numGroups = Lua.LuaDoString<int>("return GetNumTalentGroups() or 1");
                if (numGroups > 1)
                {
                    Logging.Write("[WAQ-Private] ✓ Dual Talent purchased successfully!");
                }
                else
                {
                    Logging.Write("[WAQ-Private] Dual Talent purchase may have failed or not available");
                }
            }
            catch (Exception ex)
            {
                Logging.WriteError($"[WAQ-Private] Error purchasing Dual Talent: {ex.Message}");
            }
        }
    }
}
