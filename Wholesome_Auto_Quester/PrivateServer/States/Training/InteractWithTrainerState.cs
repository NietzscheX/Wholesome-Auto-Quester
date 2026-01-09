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
            if (_config.EnableRidingTraining && playerLevel >= 40)
            {
                LearnSkillsOfType(TrainingType.RidingSkills, _config.RidingTrainerGossipOption);
            }
            
            // 4. 购买双天赋（如果启用且达到等级）
            if (_config.EnableDualTalent && playerLevel >= _config.DualTalentMinLevel)
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
            // BuyTrainerService(0) 会学习所有可学技能
            // 但有些服务器可能需要逐个学习
            try
            {
                // 获取可用服务数量
                int numServices = Lua.LuaDoString<int>(@"
                    local count = 0
                    if GetNumTrainerServices then
                        count = GetNumTrainerServices() or 0
                    end
                    return count
                ");
                
                if (numServices > 0)
                {
                    Logging.Write($"[WAQ-Private] Found {numServices} trainer services");
                    
                    // 尝试学习所有可用服务
                    for (int i = 1; i <= numServices; i++)
                    {
                        // 检查该服务是否可学习
                        string serviceType = Lua.LuaDoString<string>($"return select(3, GetTrainerServiceInfo({i})) or 'unavailable'");
                        if (serviceType == "available")
                        {
                            Lua.LuaDoString($"BuyTrainerService({i})");
                            Thread.Sleep(300 + Usefuls.Latency);
                        }
                    }
                }
                else
                {
                    // 降级：使用 BuyTrainerService(0) 一次性学习
                    Lua.LuaDoString("BuyTrainerService(0)");
                }
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
