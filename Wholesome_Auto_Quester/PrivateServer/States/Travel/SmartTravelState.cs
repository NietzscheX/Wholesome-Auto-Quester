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
    /// 策略：监控玩家移动距离，超过阈值时提供传送选项
    /// </summary>
    public class SmartTravelState : State
    {
        private TeleportManager _teleportManager;
        private Vector3 _lastCheckPosition = Vector3.Empty;
        private DateTime _lastCheckTime = DateTime.MinValue;
        private DateTime _lastTeleportAttempt = DateTime.MinValue;
        private const int CHECK_INTERVAL_SECONDS = 5;
        private const int TELEPORT_COOLDOWN_SECONDS = 30;
        private const float MOVEMENT_THRESHOLD = 50f;
        
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
                if (_teleportManager == null ||
                    Fight.InFight ||
                    ObjectManager.Me.IsOnTaxi ||
                    ObjectManager.Me.IsDead ||
                    ObjectManager.Me.IsCast ||
                    !ObjectManager.Me.IsValid ||
                    Conditions.InGameAndConnectedAndAliveAndProductStartedNotInPause == false)
                {
                    return false;
                }
                
                // 检查任务位置提供者是否可用
                if (!WholesomeToolbox.QuestLocationBridge.IsProviderAvailable())
                {
                    return false;
                }
                
                // 传送冷却检查
                if ((DateTime.Now - _lastTeleportAttempt).TotalSeconds < TELEPORT_COOLDOWN_SECONDS)
                {
                    return false;
                }
                
                // 检查间隔
                if ((DateTime.Now - _lastCheckTime).TotalSeconds < CHECK_INTERVAL_SECONDS)
                {
                    return false;
                }
                
                _lastCheckTime = DateTime.Now;
                
                // 检查是否有活动任务
                var provider = WholesomeToolbox.QuestLocationBridge.GetProvider();
                if (!provider.HasActiveQuestTarget())
                {
                    return false;
                }
                
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
                if (movedDistance < MOVEMENT_THRESHOLD)
                {
                    return false;
                }
                
                Logging.Write($"[WAQ-SmartTravel] 检测到玩家正在移动 ({movedDistance:F1} 码/5秒), 检查是否需要传送");
                return true;
            }
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
                string faction = TeleportManager.GetPlayerFaction();
                
                Logging.Write($"[WAQ-SmartTravel] 当前任务目标: {questTarget.TargetName}");
                Logging.Write($"[WAQ-SmartTravel] 目标位置: {questTarget.Location}, 大陆: {questTarget.Continent}");
                
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
                
                bool success = _teleportManager.ExecuteTeleport(bestLocation);
                
                if (success)
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
