using robotManager.Helpful;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;
using Wholesome_Auto_Quester.PrivateServer.Models;

namespace Wholesome_Auto_Quester.PrivateServer.Managers
{
    /// <summary>
    /// 传送管理器 - 负责智能传送逻辑
    /// </summary>
    public class TeleportManager
    {
        internal TeleportConfig _config; // 内部访问，允许 SmartTravelState 访问配置
        private DateTime _lastTeleportTime = DateTime.MinValue;
        
        public TeleportManager(TeleportConfig config)
        {
            _config = config;
        }
        
        /// <summary>
        /// 判断是否应该使用传送
        /// </summary>
        public bool ShouldUseTeleport(Vector3 currentPos, 
                                      Vector3 targetPos, 
                                      int currentContinent, 
                                      int targetContinent)
        {
            if (!_config.TeleportSettings.EnableSmartTeleport)
            {
                return false;
            }
            
            // 1. 检查冷却
            if (IsOnCooldown())
            {
                Logging.Write("[WAQ-Teleport] 传送在冷却中");
                return false;
            }
            
            // 2. 检查炉石是否在背包
            var hearthstone = Bag.GetBagItem()
                .FirstOrDefault(item => item.Entry == _config.TeleportSettings.HearthstoneItemEntry);
            if (hearthstone == null)
            {
                Logging.Write("[WAQ-Teleport] 炉石未在背包中,无法使用传送");
                return false;
            }
            
            // 3. 跨大陆检查
            if (currentContinent != targetContinent && 
                _config.TeleportSettings.CrossContinentAlwaysTeleport)
            {
                Logging.Write($"[WAQ-Teleport] 跨大陆旅行 (当前: {currentContinent}, 目标: {targetContinent}), 使用传送");
                return true;
            }
            
            // 4. 距离检查
            float distance = currentPos.DistanceTo(targetPos);
            if (distance >= _config.TeleportSettings.MinDistanceForTeleport)
            {
                Logging.Write($"[WAQ-Teleport] 距离 {distance:F1} 码 >= 传送阈值 {_config.TeleportSettings.MinDistanceForTeleport:F0} 码, 使用传送");
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 查找最优传送点
        /// </summary>
        /// <param name="targetPos">目标位置</param>
        /// <param name="targetContinent">目标大陆ID</param>
        /// <param name="playerFaction">玩家阵营</param>
        /// <param name="skipWalkDistanceCheck">跳过步行距离检查（用于返回传送，只检查是否在同一大陆）</param>
        public TeleportLocation FindBestTeleportLocation(Vector3 targetPos, 
                                                          int targetContinent,
                                                          string playerFaction,
                                                          bool skipWalkDistanceCheck = false)
        {
            if (_config.TeleportLocations == null || _config.TeleportLocations.Count == 0)
            {
                Logging.WriteError("[WAQ-Teleport] 传送点配置为空,请配置 teleport_locations.yml");
                return null;
            }
            
            // 过滤可用的传送点 (同大陆 + 阵营匹配)
            var validLocations = _config.TeleportLocations
                .Where(loc => loc.Continent == targetContinent)
                .Where(loc => loc.Faction == "Neutral" || loc.Faction == playerFaction)
                .ToList();
            
            if (!validLocations.Any())
            {
                Logging.Write($"[WAQ-Teleport] 未找到适合的传送点 (大陆: {targetContinent}, 阵营: {playerFaction})");
                return null;
            }
            
            // 找到距离目标最近的传送点
            var bestLocation = validLocations
                .OrderBy(loc => {
                    var locPos = new Vector3(
                        loc.Position.X, 
                        loc.Position.Y, 
                        loc.Position.Z
                    );
                    return locPos.DistanceTo(targetPos);
                })
                .First();
            
            var bestPos = new Vector3(
                bestLocation.Position.X, 
                bestLocation.Position.Y, 
                bestLocation.Position.Z
            );
            float walkDistance = bestPos.DistanceTo(targetPos);
            
            // 对于返回传送，跳过步行距离检查（只要在同一大陆就行）
            if (skipWalkDistanceCheck)
            {
                Logging.Write($"[WAQ-Teleport] ✓ 选择返回传送点: {bestLocation.Name} (传送后距原位置: {walkDistance:F1} 码)");
                return bestLocation;
            }
            
            // 确保传送后的步行距离在合理范围内
            if (walkDistance > _config.TeleportSettings.MaxWalkDistanceAfterTeleport)
            {
                Logging.Write($"[WAQ-Teleport] 最近传送点 {bestLocation.Name} 仍需步行 {walkDistance:F1} 码, 超过阈值 {_config.TeleportSettings.MaxWalkDistanceAfterTeleport:F0} 码, 不使用传送");
                return null;
            }
            
            Logging.Write($"[WAQ-Teleport] ✓ 选择传送点: {bestLocation.Name} (传送后步行距离: {walkDistance:F1} 码)");
            return bestLocation;
        }
        
        /// <summary>
        /// 执行传送
        /// </summary>
        public bool ExecuteTeleport(TeleportLocation location)
        {
            try
            {
                Logging.Write($"[WAQ-Teleport] ========== 开始传送到: {location.Name} ==========");
                
                // 使用炉石
                var hearthstone = Bag.GetBagItem()
                    .FirstOrDefault(item => item.Entry == _config.TeleportSettings.HearthstoneItemEntry);
                
                if (hearthstone == null)
                {
                    Logging.WriteError("[WAQ-Teleport] 炉石未找到,传送失败");
                    return false;
                }
                
                // 停止移动
                Logging.Write("[WAQ-Teleport] 停止移动中...");
                wManager.Wow.Helpers.MovementManager.StopMove();
                Thread.Sleep(500);
                
                // 使用炉石打开菜单
                Logging.Write($"[WAQ-Teleport] 使用 {hearthstone.Name} 打开传送菜单");
                ItemsManager.UseItem(hearthstone.Name);
                Thread.Sleep(1000);
                
                // 等待菜单出现
                bool menuAppeared = WaitForGossipFrame();
                if (!menuAppeared)
                {
                    Logging.WriteError("[WAQ-Teleport] 传送菜单未打开,传送失败");
                    return false;
                }
                
                // 导航菜单路径
                if (location.MenuPath != null && location.MenuPath.Count > 0)
                {
                    foreach (var menuOption in location.MenuPath)
                    {
                        Logging.Write($"[WAQ-Teleport] 选择菜单: {menuOption}");
                        bool success = SelectTeleportMenu(menuOption);
                        
                        if (!success)
                        {
                            Logging.WriteError($"[WAQ-Teleport] 菜单选择失败: {menuOption}");
                            return false;
                        }
                        
                        Thread.Sleep(1500); // 等待下一个菜单加载
                    }
                }
                else
                {
                    Logging.WriteError("[WAQ-Teleport] 菜单路径为空,无法传送");
                    return false;
                }
                
                // 等待传送完成
                Logging.Write("[WAQ-Teleport] 等待传送加载中...");
                Thread.Sleep(3000);
                WaitForLoadingScreen();
                
                _lastTeleportTime = DateTime.Now;
                Logging.Write($"[WAQ-Teleport] ========== 传送到 {location.Name} 完成! ==========");
                return true;
            }
            catch (Exception ex)
            {
                Logging.WriteError($"[WAQ-Teleport] 传送失败: {ex.Message}");
                Logging.WriteError($"[WAQ-Teleport] 堆栈: {ex.StackTrace}");
                return false;
            }
        }
        
        /// <summary>
        /// 等待 Gossip/Quest 窗口出现
        /// </summary>
        private bool WaitForGossipFrame()
        {
            for (int i = 0; i < 10; i++) // 最多等待5秒
            {
                bool hasMenu = Lua.LuaDoString<bool>(@"
                    return (GossipFrame and GossipFrame:IsVisible()) or 
                           (QuestFrame and QuestFrame:IsVisible())
                ");
                
                if (hasMenu)
                {
                    Logging.Write("[WAQ-Teleport] ✓ 传送菜单已打开");
                    return true;
                }
                Thread.Sleep(500);
            }
            return false;
        }
        
        /// <summary>
        /// 选择传送菜单选项
        /// </summary>
        private bool SelectTeleportMenu(string menuText)
        {
            // 清理特殊字符
            string escapedText = menuText.Replace("'", "\\'");
            
            string luaScript = $@"
                local targetText = '{escapedText}'
                
                -- 文本清理函数: 去除所有空格和特殊字符
                local function cleanText(text)
                    if not text then return '' end
                    return string.gsub(text, '%s', '')
                end
                
                local cleanTarget = cleanText(targetText)
                
                -- 方法1: 尝试 GossipTitleButton (传送界面常用)
                for i = 1, 32 do
                    local button = _G['GossipTitleButton' .. i]
                    if button and button:IsVisible() then
                        local text = cleanText(button:GetText() or '')
                        if string.find(text, cleanTarget) then
                            button:Click()
                            DEFAULT_CHAT_FRAME:AddMessage('[WAQ-Teleport] ✓ 点击了: ' .. (button:GetText() or ''))
                            return true
                        end
                    end
                end
                
                -- 方法2: 尝试 Gossip 选项
                local numOptions = GetNumGossipOptions()
                if numOptions then
                    for i = 1, numOptions do
                        local option = select(i * 2 - 1, GetGossipOptions())
                        if option then
                            local cleanOption = cleanText(option)
                            if string.find(cleanOption, cleanTarget) then
                                SelectGossipOption(i)
                                DEFAULT_CHAT_FRAME:AddMessage('[WAQ-Teleport] ✓ 选择了: ' .. option)
                                return true
                            end
                        end
                    end
                end
                
                DEFAULT_CHAT_FRAME:AddMessage('[WAQ-Teleport] ✗ 未找到菜单: ' .. targetText)
                return false
            ";
            
            return Lua.LuaDoString<bool>(luaScript);
        }
        
        /// <summary>
        /// 等待加载屏幕完成
        /// </summary>
        private void WaitForLoadingScreen()
        {
            int timeout = 30;
            while (Usefuls.IsLoadingOrConnecting && timeout > 0)
            {
                Logging.Write($"[WAQ-Teleport] 正在加载... ({timeout}s)");
                Thread.Sleep(1000);
                timeout--;
            }
            
            if (timeout == 0)
            {
                Logging.WriteError("[WAQ-Teleport] 加载超时");
            }
            else
            {
                Logging.Write("[WAQ-Teleport] ✓ 加载完成");
            }
        }
        
        /// <summary>
        /// 检查是否在冷却中
        /// </summary>
        private bool IsOnCooldown()
        {
            if (_config.TeleportSettings.TeleportCooldown == 0)
                return false;
                
            var elapsed = (DateTime.Now - _lastTeleportTime).TotalSeconds;
            return elapsed < _config.TeleportSettings.TeleportCooldown;
        }
        
        /// <summary>
        /// 获取玩家阵营字符串
        /// </summary>
        public static string GetPlayerFaction()
        {
            // PlayerFaction 直接返回 "Alliance" 或 "Horde" 字符串
            return ObjectManager.Me.PlayerFaction;
        }
    }
}
