using robotManager.Helpful;
using System;
using System.Collections.Generic;
using System.Threading;
using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;
using Wholesome_Auto_Quester.PrivateServer.Managers;
using Wholesome_Auto_Quester.PrivateServer.Models;

namespace Wholesome_Auto_Quester.PrivateServer.Helpers
{
    /// <summary>
    /// 瞬移助手 - 支持同大陆瞬移和跨大陆传送后瞬移
    /// </summary>
    public class FlyHelper
    {
        private static readonly Random _random = new Random();
        
        // 最大单次瞬移距离（超过这个距离会分步瞬移）
        private const float MAX_SINGLE_TELEPORT_DISTANCE = 500f;
        
        /// <summary>
        /// 直接瞬移到指定坐标（仅同大陆有效）
        /// </summary>
        public static bool FlyTo(Vector3 pos)
        {
            if (!WholesomeAQSettings.CurrentSetting.Fly)
            {
                Logging.WriteError("[FlyHelper] 瞬移开关未打开");
                return false;
            }
            
            // 检查玩家是否死亡
            if (ObjectManager.Me.IsDead)
            {
                Logging.WriteError("[FlyHelper] 玩家已死亡，无法瞬移");
                return false;
            }
            
            // 验证坐标是否有效
            if (pos == null || pos == Vector3.Empty || (pos.X == 0 && pos.Y == 0 && pos.Z == 0))
            {
                Logging.WriteError($"[FlyHelper] 无效的目标坐标: ({pos?.X}, {pos?.Y}, {pos?.Z})");
                return false;
            }
            
            // 计算距离，如果太远则分步瞬移
            float distance = ObjectManager.Me.Position.DistanceTo(pos);
            if (distance > MAX_SINGLE_TELEPORT_DISTANCE)
            {
                Logging.Write($"[FlyHelper] 距离 {distance:F0} 码较远，将分步瞬移");
                return StepTeleport(pos);
            }

            return DoSingleTeleport(pos);
        }
        
        /// <summary>
        /// 分步瞬移（减少被检测风险）
        /// </summary>
        private static bool StepTeleport(Vector3 finalPos)
        {
            Vector3 currentPos = ObjectManager.Me.Position;
            float totalDistance = currentPos.DistanceTo(finalPos);
            int steps = (int)Math.Ceiling(totalDistance / MAX_SINGLE_TELEPORT_DISTANCE);
            
            Logging.Write($"[FlyHelper] 分 {steps} 步瞬移");
            
            for (int i = 1; i <= steps; i++)
            {
                if (ObjectManager.Me.IsDead)
                {
                    Logging.WriteError("[FlyHelper] 分步瞬移中角色死亡");
                    return false;
                }
                
                // 计算中间点
                float progress = (float)i / steps;
                Vector3 stepTarget;
                
                if (i == steps)
                {
                    stepTarget = finalPos;
                }
                else
                {
                    stepTarget = new Vector3(
                        currentPos.X + (finalPos.X - currentPos.X) * progress,
                        currentPos.Y + (finalPos.Y - currentPos.Y) * progress,
                        currentPos.Z + (finalPos.Z - currentPos.Z) * progress
                    );
                }
                
                Logging.Write($"[FlyHelper] 步骤 {i}/{steps}");
                
                if (!DoSingleTeleport(stepTarget))
                {
                    return false;
                }
                
                // 步骤之间随机延迟（减少检测）
                if (i < steps)
                {
                    int delay = _random.Next(500, 1500);
                    Thread.Sleep(delay);
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// 执行单次瞬移
        /// </summary>
        private static bool DoSingleTeleport(Vector3 pos)
        {
            try
            {
                // 停止当前移动
                wManager.Wow.Helpers.MovementManager.StopMove();
                
                // 随机等待（减少检测）
                int preDelay = _random.Next(100, 300);
                Thread.Sleep(preDelay);
                
                int processId = (int)wManager.Wow.Memory.WowMemory.Memory.GetProcess().Id;
                MemoryRobot.Memory memory = new MemoryRobot.Memory(processId);
                uint BaseAddress = (uint)memory.ReadInt32(0xCD87A8);
                BaseAddress = (uint)memory.ReadInt32(BaseAddress + 0x34);
                BaseAddress = (uint)memory.ReadInt32(BaseAddress + 0x24);
                
                // 在 Z 坐标上增加偏移量，防止模型穿透地面导致掉落
                const float Z_OFFSET = 3.0f;
                float safeZ = pos.Z + Z_OFFSET;
                
                memory.WriteFloat(BaseAddress + 0x798, pos.X);
                memory.WriteFloat(BaseAddress + 0x79C, pos.Y);
                memory.WriteFloat(BaseAddress + 0x7A0, safeZ);
                
                Logging.Write($"[FlyHelper] 瞬移中... 目标: ({pos.X:F1}, {pos.Y:F1}, {safeZ:F1}) [Z+{Z_OFFSET}]");

                wManager.Wow.Helpers.Move.JumpOrAscend(wManager.Wow.Helpers.Move.MoveAction.PressKey, 100);

                // 等待稳定（随机延迟）
                int stabilizeDelay = _random.Next(1200, 1800);
                Thread.Sleep(stabilizeDelay);
                
                // 检查是否死亡（瞬移后可能掉落死亡）
                if (ObjectManager.Me.IsDead)
                {
                    Logging.WriteError("[FlyHelper] 瞬移后角色死亡！");
                    return false;
                }
                
                Logging.Write($"[FlyHelper] ✓ 瞬移到 ({pos.X:F1}, {pos.Y:F1}, {pos.Z:F1})");
                
                // 额外等待让 PathFinder 初始化（随机延迟）
                Logging.Write("[FlyHelper] 等待世界同步...");
                int syncDelay = _random.Next(1500, 2500);
                Thread.Sleep(syncDelay);
                
                return true;
            }
            catch (Exception ex)
            {
                Logging.WriteError($"[FlyHelper] 瞬移失败: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 智能旅行：根据大陆决定使用瞬移或传送+瞬移
        /// </summary>
        /// <param name="targetPos">目标位置</param>
        /// <param name="targetContinent">目标大陆ID</param>
        /// <param name="teleportManager">传送管理器（用于跨大陆传送）</param>
        /// <returns>是否成功</returns>
        public static bool SmartTravelTo(Vector3 targetPos, int targetContinent, TeleportManager teleportManager = null)
        {
            if (!WholesomeAQSettings.CurrentSetting.Fly)
            {
                Logging.Write("[FlyHelper] 瞬移功能未启用");
                return false;
            }
            
            // 检查玩家是否死亡
            if (ObjectManager.Me.IsDead)
            {
                Logging.WriteError("[FlyHelper] 玩家已死亡，无法瞬移");
                return false;
            }
            
            // 验证目标坐标
            if (targetPos == null || targetPos == Vector3.Empty || 
                (targetPos.X == 0 && targetPos.Y == 0 && targetPos.Z == 0))
            {
                Logging.WriteError($"[FlyHelper] 无效的目标坐标: ({targetPos?.X}, {targetPos?.Y}, {targetPos?.Z})");
                return false;
            }
            
            // 验证目标大陆
            if (targetContinent < 0)
            {
                Logging.WriteError($"[FlyHelper] 无效的目标大陆: {targetContinent}");
                return false;
            }
            
            int currentContinent = Usefuls.ContinentId;
            
            Logging.Write($"[FlyHelper] SmartTravel: 当前大陆={currentContinent}, 目标大陆={targetContinent}");
            
            // 同大陆：直接瞬移
            if (currentContinent == targetContinent)
            {
                Logging.Write("[FlyHelper] 同大陆，直接瞬移");
                return FlyTo(targetPos);
            }
            
            // 跨大陆：先传送，再瞬移
            Logging.Write("[FlyHelper] 跨大陆，需要先传送到目标大陆");
            
            if (teleportManager == null)
            {
                Logging.WriteError("[FlyHelper] 跨大陆瞬移需要 TeleportManager，但未提供");
                return false;
            }
            
            // 查找目标大陆的传送点
            string faction = TeleportManager.GetPlayerFaction();
            var teleportLocation = teleportManager.FindBestTeleportLocation(
                targetPos, targetContinent, faction, skipWalkDistanceCheck: true);
            
            if (teleportLocation == null)
            {
                Logging.WriteError($"[FlyHelper] 未找到目标大陆 {targetContinent} 的传送点");
                return false;
            }
            
            Logging.Write($"[FlyHelper] 使用传送点 '{teleportLocation.Name}' 前往目标大陆");
            
            // 执行传送
            bool teleportSuccess = teleportManager.ExecuteTeleport(teleportLocation);
            
            if (!teleportSuccess)
            {
                Logging.WriteError("[FlyHelper] 传送失败");
                return false;
            }
            
            // 等待世界同步
            Thread.Sleep(2000);
            
            // 确认已到达目标大陆
            if (Usefuls.ContinentId != targetContinent)
            {
                Logging.WriteError($"[FlyHelper] 传送后大陆ID不匹配: 期望={targetContinent}, 实际={Usefuls.ContinentId}");
                return false;
            }
            
            // 瞬移到最终目标
            Logging.Write($"[FlyHelper] 已到达目标大陆，瞬移到最终位置");
            return FlyTo(targetPos);
        }
        
        /// <summary>
        /// 智能旅行到 NPC 位置
        /// </summary>
        public static bool SmartTravelToNpc(Vector3Position npcPos, int npcContinent, TeleportManager teleportManager = null)
        {
            if (npcPos == null)
            {
                Logging.WriteError("[FlyHelper] NPC 位置为空");
                return false;
            }
            
            var targetPos = new Vector3(npcPos.X, npcPos.Y, npcPos.Z);
            return SmartTravelTo(targetPos, npcContinent, teleportManager);
        }
        
        /// <summary>
        /// 检查是否启用了瞬移
        /// </summary>
        public static bool IsEnabled => WholesomeAQSettings.CurrentSetting.Fly;
    }
}