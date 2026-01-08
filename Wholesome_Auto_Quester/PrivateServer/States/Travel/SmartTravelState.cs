using robotManager.FiniteStateMachine;
using robotManager.Helpful;
using System;
using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;
using Wholesome_Auto_Quester.PrivateServer.Managers;

namespace Wholesome_Auto_Quester.PrivateServer.States.Travel
{
    /// <summary>
    /// 全局智能传送状态 - 周期性监控移动距离
    /// 策略：如果启用瞬移，直接瞬移到目标；否则使用传送点
    /// </summary>
    public class SmartTravelState : State
    {
        private TeleportManager _teleportManager;
        private Vector3 _lastCheckPosition = Vector3.Empty;
        private DateTime _lastCheckTime = DateTime.MinValue;
        private DateTime _lastTeleportAttempt = DateTime.MinValue;
        private const int CHECK_INTERVAL_SECONDS = 3;
        private const int TELEPORT_COOLDOWN_SECONDS = 10;
        
        // 从设置读取瞬移阈值
        private float MinDistanceForFly => WholesomeAQSettings.CurrentSetting.FlyMinDistance > 0 
            ? WholesomeAQSettings.CurrentSetting.FlyMinDistance 
            : 200f;
        
        public SmartTravelState(TeleportManager teleportManager)
        {
            _teleportManager = teleportManager;
            // 设置较高优先级，确保能拦截长距离移动
            Priority = 10; 
        }
        
        public override string DisplayName => "WAQ-Private - Smart Travel";
        
        public override bool NeedToRun
        {
            get
            {
                // 基本状态检查
                if (Fight.InFight ||
                    ObjectManager.Me.IsOnTaxi ||
                    ObjectManager.Me.IsDead ||
                    ObjectManager.Me.IsCast ||
                    !ObjectManager.Me.IsValid ||
                    Conditions.InGameAndConnectedAndAliveAndProductStartedNotInPause == false)
                {
                    return false;
                }
                
                // 检查是否启用了瞬移功能
                bool flyEnabled = Helpers.FlyHelper.IsEnabled;
                if (!flyEnabled)
                {
                    // 如果没启用瞬移，使用原来的传送点逻辑
                    return CheckForTeleportTravel();
                }
                
                // 冷却检查
                var cooldownElapsed = (DateTime.Now - _lastTeleportAttempt).TotalSeconds;
                if (cooldownElapsed < TELEPORT_COOLDOWN_SECONDS)
                {
                    return false;
                }
                
                // 检查间隔
                var checkElapsed = (DateTime.Now - _lastCheckTime).TotalSeconds;
                if (checkElapsed < CHECK_INTERVAL_SECONDS)
                {
                    return false;
                }
                
                _lastCheckTime = DateTime.Now;
                
                // 检查任务位置提供者
                if (!WholesomeToolbox.QuestLocationBridge.IsProviderAvailable())
                {
                    Logging.Write("[WAQ-SmartTravel] 任务位置提供者不可用");
                    return false;
                }
                
                var provider = WholesomeToolbox.QuestLocationBridge.GetProvider();
                if (!provider.HasActiveQuestTarget())
                {
                    return false;
                }
                
                var questTarget = provider.GetCurrentQuestTarget();
                if (questTarget == null || !questTarget.IsValid)
                {
                    return false;
                }
                
                // 检查距离是否足够远，需要瞬移
                float distance = ObjectManager.Me.Position.DistanceTo(questTarget.Location);
                Logging.Write($"[WAQ-SmartTravel] 目标距离: {distance:F0} 码, 阈值: {MinDistanceForFly} 码");
                
                if (distance >= MinDistanceForFly)
                {
                    Logging.Write($"[WAQ-SmartTravel] 距离足够远，准备瞬移");
                    return true;
                }
                
                return false;
            }
        }
        
        /// <summary>
        /// 原来的传送点检查逻辑（当 Fly 未启用时使用）
        /// </summary>
        private bool CheckForTeleportTravel()
        {
            if (_teleportManager == null)
                return false;
                
            // 传送冷却检查
            if ((DateTime.Now - _lastTeleportAttempt).TotalSeconds < 30)
            {
                return false;
            }
            
            // 检查间隔
            if ((DateTime.Now - _lastCheckTime).TotalSeconds < 5)
            {
                return false;
            }
            
            _lastCheckTime = DateTime.Now;
            
            // 检查任务位置提供者
            if (!WholesomeToolbox.QuestLocationBridge.IsProviderAvailable())
                return false;
                
            var provider = WholesomeToolbox.QuestLocationBridge.GetProvider();
            if (!provider.HasActiveQuestTarget())
                return false;
            
            // 检查是否在移动
            Vector3 currentPos = ObjectManager.Me.Position;
            
            if (_lastCheckPosition == Vector3.Empty)
            {
                _lastCheckPosition = currentPos;
                return false;
            }
            
            float movedDistance = _lastCheckPosition.DistanceTo(currentPos);
            _lastCheckPosition = currentPos;
            
            // 如果移动距离很小,说明没在赶路
            if (movedDistance < 50f)
            {
                return false;
            }
            
            Logging.Write($"[WAQ-SmartTravel] 检测到玩家正在移动 ({movedDistance:F1} 码/5秒), 检查是否需要传送");
            return true;
        }
        
        public override void Run()
        {
            try
            {
                if (!WholesomeToolbox.QuestLocationBridge.IsProviderAvailable())
                {
                    Logging.Write("[WAQ-SmartTravel] 任务位置提供者未注册");
                    _lastTeleportAttempt = DateTime.Now;
                    return;
                }
                
                var provider = WholesomeToolbox.QuestLocationBridge.GetProvider();
                if (!provider.HasActiveQuestTarget())
                {
                    return;
                }
                
                var questTarget = provider.GetCurrentQuestTarget();
                if (questTarget == null || !questTarget.IsValid)
                {
                    return;
                }
                
                Vector3 currentPos = ObjectManager.Me.Position;
                int currentContinent = Usefuls.ContinentId;
                
                Logging.Write($"[WAQ-SmartTravel] 当前任务目标: {questTarget.TargetName}");
                Logging.Write($"[WAQ-SmartTravel] 目标位置: {questTarget.Location}, 大陆: {questTarget.Continent}");
                
                // 优先使用瞬移功能
                if (Helpers.FlyHelper.IsEnabled)
                {
                    Logging.Write("[WAQ-SmartTravel] ========================================");
                    Logging.Write("[WAQ-SmartTravel] 使用瞬移前往任务目标");
                    Logging.Write("[WAQ-SmartTravel] ========================================");
                    
                    bool success = Helpers.FlyHelper.SmartTravelTo(
                        questTarget.Location, 
                        questTarget.Continent, 
                        _teleportManager
                    );
                    
                    if (success)
                    {
                        Logging.Write($"[WAQ-SmartTravel] ✓ 瞬移成功! 已到达 {questTarget.TargetName} 附近");
                    }
                    else
                    {
                        Logging.WriteError("[WAQ-SmartTravel] ✗ 瞬移失败");
                    }
                    
                    _lastTeleportAttempt = DateTime.Now;
                    return;
                }
                
                // 传统传送点逻辑
                string faction = TeleportManager.GetPlayerFaction();
                
                // 判断是否应该传送
                if (!_teleportManager.ShouldUseTeleport(currentPos, questTarget.Location, 
                                                         currentContinent, questTarget.Continent))
                {
                    return;
                }
                
                // 查找最优传送点
                var bestLocation = _teleportManager.FindBestTeleportLocation(
                    questTarget.Location, questTarget.Continent, faction);
                
                if (bestLocation == null)
                {
                    Logging.Write("[WAQ-SmartTravel] 未找到合适的传送点");
                    _lastTeleportAttempt = DateTime.Now;
                    return;
                }
                
                // 执行传送
                Logging.Write($"[WAQ-SmartTravel] ========================================");
                Logging.Write($"[WAQ-SmartTravel] 准备传送以加速前往: {questTarget.TargetName}");
                Logging.Write($"[WAQ-SmartTravel] ========================================");
                
                bool teleportSuccess = _teleportManager.ExecuteTeleport(bestLocation);
                
                if (teleportSuccess)
                {
                    Logging.Write($"[WAQ-SmartTravel] ✓ 传送成功! 已到达 {bestLocation.Name}");
                }
                else
                {
                    Logging.WriteError("[WAQ-SmartTravel] ✗ 传送失败");
                }
                
                _lastTeleportAttempt = DateTime.Now;
            }
            catch (Exception ex)
            {
                Logging.WriteError($"[WAQ-SmartTravel] 错误: {ex.Message}");
                Logging.WriteError($"[WAQ-SmartTravel] 堆栈: {ex.StackTrace}");
            }
        }
    }
}
