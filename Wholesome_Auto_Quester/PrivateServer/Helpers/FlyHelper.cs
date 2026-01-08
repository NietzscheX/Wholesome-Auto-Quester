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

            try
            {
                int processId = (int)wManager.Wow.Memory.WowMemory.Memory.GetProcess().Id;
                MemoryRobot.Memory memory = new MemoryRobot.Memory(processId);
                uint BaseAddress = (uint)memory.ReadInt32(0xCD87A8);
                BaseAddress = (uint)memory.ReadInt32(BaseAddress + 0x34);
                BaseAddress = (uint)memory.ReadInt32(BaseAddress + 0x24);
                
                memory.WriteFloat(BaseAddress + 0x798, pos.X);
                memory.WriteFloat(BaseAddress + 0x79C, pos.Y);
                memory.WriteFloat(BaseAddress + 0x7A0, pos.Z);

                wManager.Wow.Helpers.Move.JumpOrAscend(wManager.Wow.Helpers.Move.MoveAction.PressKey, 100);

                Thread.Sleep(1000);
                
                Logging.Write($"[FlyHelper] ✓ 瞬移到 ({pos.X:F1}, {pos.Y:F1}, {pos.Z:F1})");
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