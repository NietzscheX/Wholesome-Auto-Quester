using robotManager.Helpful;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using wManager.Wow.Bot.Tasks;
using wManager.Wow.Enums;
using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;
using Wholesome_Auto_Quester.PrivateServer.Models;
using Wholesome_Auto_Quester.PrivateServer.Config;

namespace Wholesome_Auto_Quester.PrivateServer.Managers
{
    public class EquipmentManager
    {
        public enum EquipmentPhase
        {
            Idle,
            CleaningBags,
            PurchasingEquipment,
            EquippingItems
            // 不再需要 TeleportingBack - FSM 会自动处理
        }
        
        private EquipmentPhase _currentPhase = EquipmentPhase.Idle;
        private System.Threading.Timer _checkTimer;
        private const int CHECK_INTERVAL_MS = 60000; // 每60秒检查一次
        
        // YAML 配置
        private EquipmentConfig _config;
        private ClassProfile _currentClassProfile;
        
        // TeleportManager 引用（用于保存返回传送点）
        private TeleportManager _teleportManager;
        
        // 保存的返回传送点（用于装备更换后返回）
        private TeleportLocation _savedReturnLocation;
        private float _savedPosX;
        private float _savedPosY;
        private float _savedPosZ;
        private int _savedMapId;
        
        // 冷却和失败跟踪
        private DateTime _lastRefreshTime = DateTime.MinValue;
        private const int REFRESH_COOLDOWN_SECONDS = 300; // 5分钟冷却
        private int _consecutiveFailures = 0;
        private const int MAX_CONSECUTIVE_FAILURES = 3;
        
        public bool IsActive => _currentPhase != EquipmentPhase.Idle;
        public EquipmentPhase CurrentEquipmentPhase => _currentPhase;
        public EquipmentConfig Config => _config;
        public ClassProfile CurrentClassProfile => _currentClassProfile;
        
        // 暴露保存的位置和传送点
        public float SavedPosX => _savedPosX;
        public float SavedPosY => _savedPosY;
        public float SavedPosZ => _savedPosZ;
        public int SavedMapId => _savedMapId;
        public TeleportLocation SavedReturnLocation => _savedReturnLocation;
        public bool HasSavedReturnLocation => _savedReturnLocation != null;
        
        public void Initialize(string yamlPath, TeleportManager teleportManager = null)
        {
            _teleportManager = teleportManager;
            
            if (!File.Exists(yamlPath))
            {
                Logging.WriteError($"[WAQ-Equipment] YAML config file not found at: {yamlPath}");
                Logging.Write("[WAQ-Equipment] Please create equipment.yml based on equipment.yml.example");
                return;
            }
            
            _config = YamlConfigLoader.Load(yamlPath);
            
            if (_config == null)
            {
                Logging.WriteError("[WAQ-Equipment] Failed to load YAML configuration");
                return;
            }
            
            // 检测玩家职业
            int playerClass = (int)ObjectManager.Me.WowClass;
            _currentClassProfile = _config.Classes?.FirstOrDefault(c => c.ClassId == playerClass);
            
            if (_currentClassProfile == null)
            {
                Logging.Write($"[WAQ-Equipment] No class profile found for class ID {playerClass}, equipment manager disabled");
                return;
            }
            
            Logging.Write($"[WAQ-Equipment] Loaded class profile: {_currentClassProfile.Name}");
            
            // 启动定时器，定期检查装备耐久度
            int interval = _config.GlobalSettings?.CheckIntervalMs ?? CHECK_INTERVAL_MS;
            _checkTimer = new System.Threading.Timer(CheckDurabilityCallback, null, 10000, interval);
            Logging.Write($"[WAQ-Equipment] Equipment durability check timer started (interval: {interval}ms)");
        }
        
        public void Dispose()
        {
            if (_checkTimer != null)
            {
                _checkTimer.Dispose();
                _checkTimer = null;
            }
        }
        
        private void CheckDurabilityCallback(object state)
        {
            try
            {
                if (_config == null || _currentClassProfile == null) return;
                if (IsActive) return;
                
                // 首先检查武器状态(最高优先级,无视冷却)
                if (NeedsWeaponCheck())
                {
                    Logging.Write("[WAQ-Equipment] ⚠ CRITICAL: Missing weapon detected! Triggering emergency equipment refresh...");
                    TriggerRefresh(_teleportManager);
                    return;
                }
                
                if (NeedsRefresh() || NeedsSupplies())
                {
                    Logging.Write("[WAQ-Equipment] ⏰ Periodic check: Maintenance needed!");
                    // 使用存储的 TeleportManager 以便保存返回传送点
                    TriggerRefresh(_teleportManager);
                }
            }
            catch (Exception e)
            {
                Logging.WriteError($"[WAQ-Equipment] Error in durability check: {e.Message}");
            }
        }
        
        public bool NeedsRefresh()
        {
            if (_config == null || _currentClassProfile == null) return false;
            
            // 武器检查无视冷却 - 最高优先级
            if (NeedsWeaponCheck())
            {
                Logging.Write("[WAQ-Equipment] ⚠ Weapon check failed - refresh needed regardless of cooldown");
                return true;
            }
            
            // 检查冷却
            var elapsed = (DateTime.Now - _lastRefreshTime).TotalSeconds;
            if (elapsed < REFRESH_COOLDOWN_SECONDS)
            {
                return false;
            }
            
            // 检查连续失败次数
            if (_consecutiveFailures >= MAX_CONSECUTIVE_FAILURES)
            {
                // 超过最大失败次数，等待30分钟后重置
                if (elapsed < 1800)
                {
                    return false;
                }
                _consecutiveFailures = 0; // 重置
            }
            
            foreach (var slot in _currentClassProfile.Slots)
            {
                if (slot.Value.Strategy == "Ignore") continue;
                if (NeedsSlotEquipment(slot.Key, slot.Value)) return true;
            }

            if (NeedsSupplies()) return true;
            
            return false;
        }
        
        /// <summary>
        /// 检查武器状态 - 确保角色装备的武器与配置匹配
        /// 规则:
        /// 1. 主手必须装备配置中指定的武器(如果配置中有MainHand)
        /// 2. 如果主手装备的是双手武器,副手可以为空
        /// 3. 如果主手装备的是单手武器且配置了副手,副手也需要装备正确的物品
        /// </summary>
        public bool NeedsWeaponCheck()
        {
            if (_config == null || _currentClassProfile == null) return false;
            if (_currentClassProfile.Slots == null) return false;
            
            // 检查配置中是否有主手武器配置
            bool hasMainHandConfig = _currentClassProfile.Slots.ContainsKey("MainHand") 
                && _currentClassProfile.Slots["MainHand"].Strategy != "Ignore"
                && _currentClassProfile.Slots["MainHand"].ItemId > 0;
            
            if (!hasMainHandConfig)
            {
                // 没有配置主手武器,跳过检查
                return false;
            }
            
            int expectedMainHandId = _currentClassProfile.Slots["MainHand"].ItemId;
            int currentMainHandId = GetEquippedItemId(16); // 16 = 主手槽位
            
            // 检查主手是否装备了正确的武器
            if (currentMainHandId == 0)
            {
                Logging.Write($"[WAQ-Equipment] ✗ Main hand is EMPTY! Expected: {expectedMainHandId}. Combat will fail without weapon.");
                return true;
            }
            
            if (currentMainHandId != expectedMainHandId)
            {
                Logging.Write($"[WAQ-Equipment] ✗ Main hand weapon mismatch! Current: {currentMainHandId}, Expected: {expectedMainHandId}.");
                return true;
            }
            
            // 检查副手配置
            bool hasOffHandConfig = _currentClassProfile.Slots.ContainsKey("OffHand")
                && _currentClassProfile.Slots["OffHand"].Strategy != "Ignore"
                && _currentClassProfile.Slots["OffHand"].ItemId > 0
                && !_currentClassProfile.Slots["OffHand"].AllowEmpty;
            
            if (hasOffHandConfig)
            {
                // 检查主手是否是双手武器
                if (IsTwoHandWeaponEquipped())
                {
                    // 双手武器装备时,副手会自动为空,这是正常的
                    // 不需要日志输出,避免刷屏
                    return false;
                }
                
                int expectedOffHandId = _currentClassProfile.Slots["OffHand"].ItemId;
                int currentOffHandId = GetEquippedItemId(17); // 17 = 副手槽位
                
                // 主手是单手武器,检查副手
                if (currentOffHandId == 0)
                {
                    Logging.Write($"[WAQ-Equipment] ✗ Off-hand is EMPTY! Expected: {expectedOffHandId}.");
                    return true;
                }
                
                if (currentOffHandId != expectedOffHandId)
                {
                    Logging.Write($"[WAQ-Equipment] ✗ Off-hand weapon mismatch! Current: {currentOffHandId}, Expected: {expectedOffHandId}.");
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// 获取指定槽位装备的物品ID
        /// </summary>
        /// <param name="slotId">槽位ID (16=主手, 17=副手)</param>
        /// <returns>物品ID,如果槽位为空返回0</returns>
        private int GetEquippedItemId(int slotId)
        {
            return Lua.LuaDoString<int>($@"
                local itemLink = GetInventoryItemLink('player', {slotId});
                if not itemLink then return 0; end
                local itemId = tonumber(itemLink:match('item:(%d+)'));
                return itemId or 0;
            ");
        }
        
        /// <summary>
        /// 检查当前装备的主手武器是否是双手武器
        /// </summary>
        private bool IsTwoHandWeaponEquipped()
        {
            return Lua.LuaDoString<bool>(@"
                local itemLink = GetInventoryItemLink('player', 16);
                if not itemLink then return false; end
                
                local _, _, _, _, _, _, _, _, equipLoc = GetItemInfo(itemLink);
                if not equipLoc then return false; end
                
                -- 双手武器类型: INVTYPE_2HWEAPON
                return equipLoc == 'INVTYPE_2HWEAPON';
            ");
        }

        public bool NeedsSupplies()
        {
            if (_config == null || _currentClassProfile == null) return false;
            if (_currentClassProfile.Supplies == null || _currentClassProfile.Supplies.Count == 0) return false;

            foreach (var supplyEntry in _currentClassProfile.Supplies)
            {
                var supply = supplyEntry.Value;
                if (supply.ItemId <= 0) continue;

                if (!ShouldBuySupply(supply)) continue;

                int count = GetItemCountInBags(supply.ItemId);
                if (count < supply.MinCount)
                {
                    Logging.Write($"[WAQ-Equipment] Low supplies: {supplyEntry.Key} ({supply.ItemId}) (Current: {count}, Min: {supply.MinCount})");
                    return true;
                }
            }

            return false;
        }
        
        public void TriggerRefresh(TeleportManager teleportManager = null)
        {
            if (IsActive)
            {
                Logging.Write("[WAQ-Equipment] Equipment refresh already in progress, skipping");
                return;
            }
            
            if (_config == null || _currentClassProfile == null)
            {
                Logging.WriteError("[WAQ-Equipment] Cannot refresh: no config or class profile loaded");
                return;
            }
            
            // 不再保存位置 - FSM 会自动让其他状态处理导航
            _currentPhase = EquipmentPhase.CleaningBags;
            Logging.Write("[WAQ-Equipment] Equipment refresh triggered");
        }
        
        /// <summary>
        /// 保存当前位置并查找最佳返回传送点
        /// </summary>
        public void SaveCurrentPosition(TeleportManager teleportManager = null)
        {
            var pos = ObjectManager.Me.Position;
            
            // 如果坐标是 (0,0,0)，尝试重试
            if (pos.X == 0 && pos.Y == 0 && pos.Z == 0)
            {
                Logging.Write("[WAQ-Equipment] ⚠ Warning: Player position is (0,0,0), retrying...");
                for (int i = 0; i < 10; i++)
                {
                    Thread.Sleep(200);
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

            // 再次验证，如果还是 (0,0,0)，记录错误且不执行后续
            if (_savedPosX == 0 && _savedPosY == 0)
            {
                Logging.WriteError("[WAQ-Equipment] ✗ Critical: Failed to get player position! Teleport back will be disabled.");
                return;
            }
            
            Logging.Write($"[WAQ-Equipment] Saved original position: ({_savedPosX:F1}, {_savedPosY:F1}, {_savedPosZ:F1}) MapId: {_savedMapId}");
            
            // 从 teleport_locations.yml 查找当前位置附近的最佳传送点
            // 对于返回传送，跳过步行距离检查（只要在同一大陆就行）
            if (teleportManager != null)
            {
                string faction = TeleportManager.GetPlayerFaction();
                _savedReturnLocation = teleportManager.FindBestTeleportLocation(
                    pos, _savedMapId, faction, 
                    skipWalkDistanceCheck: true  // 返回传送不限制距离
                );
                
                if (_savedReturnLocation != null)
                {
                    Logging.Write($"[WAQ-Equipment] ✓ Found return teleport point: {_savedReturnLocation.Name}");
                    Logging.Write($"[WAQ-Equipment]   Menu path: [{string.Join(" > ", _savedReturnLocation.MenuPath ?? new List<string>())}]");
                }
                else
                {
                    Logging.Write("[WAQ-Equipment] ⚠ No suitable return teleport point found for current location");
                }
            }
            else
            {
                Logging.Write("[WAQ-Equipment] ⚠ TeleportManager not available, return teleport disabled");
            }
        }
        
        /// <summary>
        /// 清除保存的位置
        /// </summary>
        public void ClearSavedPosition()
        {
            _savedReturnLocation = null;
            _savedPosX = 0;
            _savedPosY = 0;
            _savedPosZ = 0;
            _savedMapId = 0;
            Logging.Write("[WAQ-Equipment] Cleared saved position and return location");
        }
        
        /// <summary>
        /// 标记刷新完成（更新时间戳）
        /// </summary>
        public void MarkRefreshComplete(bool success = true)
        {
            _lastRefreshTime = DateTime.Now;
            
            if (success)
            {
                // 验证装备是否真的正确
                bool stillNeedsRefresh = false;
                foreach (var slot in _currentClassProfile.Slots)
                {
                    if (slot.Value.Strategy == "Ignore") continue;
                    if (NeedsSlotEquipment(slot.Key, slot.Value))
                    {
                        stillNeedsRefresh = true;
                        Logging.Write($"[WAQ-Equipment] ⚠ Slot {slot.Key} still needs equipment after refresh");
                    }
                }
                
                if (stillNeedsRefresh)
                {
                    _consecutiveFailures++;
                    Logging.Write($"[WAQ-Equipment] ⚠ Equipment refresh may have failed. Failure count: {_consecutiveFailures}/{MAX_CONSECUTIVE_FAILURES}");
                }
                else
                {
                    _consecutiveFailures = 0;
                    Logging.Write("[WAQ-Equipment] ✓ All equipment slots verified successfully");
                }
            }
            else
            {
                _consecutiveFailures++;
                Logging.Write($"[WAQ-Equipment] Equipment refresh failed. Failure count: {_consecutiveFailures}/{MAX_CONSECUTIVE_FAILURES}");
            }
        }
        
        public void SetPhase(EquipmentPhase phase)
        {
            _currentPhase = phase;
            Logging.Write($"[WAQ-Equipment] Equipment phase: {phase}");
        }
        
        public void ExecuteCleanBags()
        {
            DeleteDamagedEquipment();
            DeleteNonMatchingEquipment();  // 删除不匹配配置的装备（如出生自带的垃圾武器）
            EnsureBagSpace(10);
        }
        
        public void ExecutePurchaseEquipment()
        {
            GetNewEquipment();
        }
        
        public void ExecuteEquipItems()
        {
            UseAndEquipItems();
        }
        
        private void DeleteDamagedEquipment()
        {
            int threshold = _config.GlobalSettings?.DurabilityThreshold ?? 10;
            
            string protectedItemsLua = "{}";
            if (_config.GlobalSettings?.ProtectedItems != null && _config.GlobalSettings.ProtectedItems.Count > 0)
            {
                protectedItemsLua = "{" + string.Join(",", _config.GlobalSettings.ProtectedItems) + "}";
            }
            
            Lua.LuaDoString($@"
                local threshold = {threshold};
                local protectedItems = {protectedItemsLua};
                
                local protected = {{}};
                for _, id in ipairs(protectedItems) do
                    protected[id] = true;
                end
                
                for i=1,18 do
                    local durability, max = GetInventoryItemDurability(i);
                    if durability ~= nil and max ~= nil and max > 0 then
                        local percent = (durability / max) * 100;
                        if percent <= threshold then
                            local itemLink = GetInventoryItemLink('player', i);
                            if itemLink then
                                local itemId = tonumber(itemLink:match('item:(%d+)'));
                                if itemId and not protected[itemId] then
                                    local itemName = GetItemInfo(itemLink);
                                    DEFAULT_CHAT_FRAME:AddMessage('[WAQ-Equipment] Deleting: ' .. (itemName or 'Unknown') .. ' (' .. math.floor(percent) .. '% durability)');
                                    PickupInventoryItem(i);
                                    DeleteCursorItem();
                                end
                            end
                        end
                    end
                end
            ");
            
            Thread.Sleep(1000);
            Logging.Write("[WAQ-Equipment] Damaged equipment deleted");
        }
        
        /// <summary>
        /// 删除不匹配配置的装备（如出生自带的垃圾武器）
        /// </summary>
        private void DeleteNonMatchingEquipment()
        {
            if (_currentClassProfile?.Slots == null) return;
            
            // 构建期望的装备映射: slotId -> expectedItemId
            var expectedItems = new Dictionary<int, int>();
            foreach (var slotEntry in _currentClassProfile.Slots)
            {
                int slotId = GetSlotId(slotEntry.Key);
                if (slotId > 0 && slotEntry.Value.ItemId > 0)
                {
                    expectedItems[slotId] = slotEntry.Value.ItemId;
                }
            }
            
            if (expectedItems.Count == 0) return;
            
            string protectedItemsLua = "{}";
            if (_config.GlobalSettings?.ProtectedItems != null && _config.GlobalSettings.ProtectedItems.Count > 0)
            {
                protectedItemsLua = "{" + string.Join(",", _config.GlobalSettings.ProtectedItems) + "}";
            }
            
            // 构建期望装备的 Lua 表
            string expectedItemsLua = "{";
            foreach (var kvp in expectedItems)
            {
                expectedItemsLua += $"[{kvp.Key}]={kvp.Value},";
            }
            expectedItemsLua += "}";
            
            Logging.Write("[WAQ-Equipment] Checking for non-matching equipped items...");
            
            int deleted = Lua.LuaDoString<int>($@"
                local expectedItems = {expectedItemsLua};
                local protectedItems = {protectedItemsLua};
                
                local protected = {{}};
                for _, id in ipairs(protectedItems) do
                    protected[id] = true;
                end
                
                local deletedCount = 0;
                
                -- 遍历装备槽
                for slotId, expectedId in pairs(expectedItems) do
                    local itemLink = GetInventoryItemLink('player', slotId);
                    if itemLink then
                        local currentId = tonumber(itemLink:match('item:(%d+)'));
                        if currentId then
                            DEFAULT_CHAT_FRAME:AddMessage('[WAQ-Equipment] Slot ' .. slotId .. ': current=' .. currentId .. ', expected=' .. expectedId);
                            if currentId ~= expectedId and not protected[currentId] then
                                local itemName = GetItemInfo(itemLink);
                                DEFAULT_CHAT_FRAME:AddMessage('[WAQ-Equipment] DELETING from slot ' .. slotId .. ': ' .. (itemName or currentId));
                                PickupInventoryItem(slotId);
                                DeleteCursorItem();
                                deletedCount = deletedCount + 1;
                            end
                        end
                    end
                end
                
                return deletedCount;
            ");
            
            if (deleted > 0)
            {
                Thread.Sleep(1000);
                Logging.Write($"[WAQ-Equipment] Removed {deleted} non-matching equipped items");
            }
            else
            {
                Logging.Write("[WAQ-Equipment] No non-matching items to remove");
            }
        }
        
        private void GoToNpcSource(string sourceKey)
        {
            if (!_config.Sources.ContainsKey(sourceKey))
            {
                Logging.WriteError($"[WAQ-Equipment] NPC source '{sourceKey}' not found in config");
                return;
            }
            
            var source = _config.Sources[sourceKey];
            var npcPos = new robotManager.Helpful.Vector3(source.Position.X, source.Position.Y, source.Position.Z);

            // 检查是否需要传送
            UseTeleportIfFar(npcPos, source.MapId);
            
            Logging.Write($"[WAQ-Equipment] Moving to {source.Name} at ({source.Position.X:F1}, {source.Position.Y:F1}, {source.Position.Z:F1})");
            
            while (ObjectManager.Me.Position.DistanceTo(npcPos) > 4)
            {
                if (ObjectManager.Me.IsDeadMe || !ObjectManager.Me.IsValid) return;
                
                GoToTask.ToPosition(npcPos, 3f);
                Thread.Sleep(500);
            }
            
            wManager.Wow.Helpers.MovementManager.StopMove();
            Logging.Write($"[WAQ-Equipment] Arrived at {source.Name}");
        }

        private void UseTeleportIfFar(robotManager.Helpful.Vector3 targetPos, int targetMapId)
        {
            float distance = ObjectManager.Me.Position.DistanceTo(targetPos);
            bool differentContinent = (int)wManager.Wow.Helpers.Usefuls.ContinentId != targetMapId;
            
            // 检查是否启用瞬移功能
            if (Helpers.FlyHelper.IsEnabled)
            {
                Logging.Write($"[WAQ-Equipment] 瞬移功能已启用，使用智能旅行");
                
                if (Helpers.FlyHelper.SmartTravelTo(targetPos, targetMapId, _teleportManager))
                {
                    Logging.Write("[WAQ-Equipment] ✓ 瞬移成功");
                    return;
                }
                else
                {
                    Logging.Write("[WAQ-Equipment] 瞬移失败，尝试使用传统传送方式");
                }
            }

            // 传统传送逻辑
            if (_config.Training == null || !_config.Training.UseCustomTeleport) return;

            if (differentContinent || distance > 1000)
            {
                int teleportItem = _config.Training.TeleportItemEntry;
                if (teleportItem > 0 && GetItemCountInBags(teleportItem) > 0)
                {
                    Logging.Write($"[WAQ-Equipment] Target is far ({distance:F1}y). Using teleport item {teleportItem}...");
                    
                    wManager.Wow.Helpers.MovementManager.StopMove();
                    Thread.Sleep(500);

                    Lua.RunMacroText("/use item:" + teleportItem);
                    
                    Logging.Write("[WAQ-Equipment] Waiting for teleport menu or cast completion...");
                    bool menuOpen = false;
                    for (int i = 0; i < 40; i++)
                    {
                        if (Lua.LuaDoString<bool>("return (GossipFrame and GossipFrame:IsVisible()) or (QuestFrame and QuestFrame:IsVisible())"))
                        {
                            menuOpen = true;
                            Logging.Write("[WAQ-Equipment] Teleport menu detected!");
                            break;
                        }

                        if (ObjectManager.Me.CastingSpellId == 0 && i > 10) 
                        {
                            // Cast finished but no menu yet
                        }

                        Thread.Sleep(500);
                    }

                    if (_config.Training.TeleportMenuPath != null && _config.Training.TeleportMenuPath.Count > 0)
                    {
                        if (menuOpen)
                        {
                            Logging.Write($"[WAQ-Equipment] Navigating teleport menu: [{string.Join(", ", _config.Training.TeleportMenuPath)}]");
                            Helpers.NpcInteractionHelper.NavigateMenuPath(_config.Training.TeleportMenuPath);
                            Thread.Sleep(5000);
                        }
                        else
                        {
                            Logging.WriteError("[WAQ-Equipment] Teleport item used but NO interaction window appeared!");
                        }
                    }
                    
                    Logging.Write("[WAQ-Equipment] Teleport cast complete. Waiting for world synchronization...");
                    Thread.Sleep(8000);
                    
                    for (int i = 0; i < 30; i++)
                    {
                        bool posSynced = ObjectManager.Me.Position.X != 0 || ObjectManager.Me.Position.Y != 0;
                        if (Usefuls.ContinentId == targetMapId && posSynced)
                        {
                            Logging.Write("[WAQ-Equipment] World synchronized. Stabilizing 2 sec...");
                            Thread.Sleep(2000);
                            break;
                        }
                        Thread.Sleep(1000);
                    }
                }
            }
        }
        
        private int GetItemCountInBags(int itemId)
        {
            return Lua.LuaDoString<int>($@"
                local count = GetItemCount({itemId});
                return count or 0;
            ");
        }

        private void GetNewEquipment()
        {
            if (_currentClassProfile == null)
            {
                Logging.WriteError("[WAQ-Equipment] No class profile loaded");
                return;
            }
            
            Logging.Write($"[WAQ-Equipment] ========================================");
            Logging.Write($"[WAQ-Equipment] Acquiring equipment for {_currentClassProfile.Name}");
            Logging.Write($"[WAQ-Equipment] ========================================");
            
            EnsureBagSpace(10);
            
            var purchaseDict = new Dictionary<int, PurchaseTask>();
            bool hasItemsToEquip = false;
            
            Logging.Write($"[WAQ-Equipment] Scanning {_currentClassProfile.Slots.Count} slots for equipment needs...");

            foreach (var slotEntry in _currentClassProfile.Slots)
            {
                var slotName = slotEntry.Key;
                var slot = slotEntry.Value;
                if (slot.Strategy == "Ignore") continue;

                if (!NeedsSlotEquipment(slotName, slot)) continue;

                if (GetItemCountInBags(slot.ItemId) > 0)
                {
                    Logging.Write($"[WAQ-Equipment] Slot {slotName}: Item {slot.ItemId} already in bags.");
                    hasItemsToEquip = true;
                    continue;
                }

                if (slot.BundleId.HasValue && GetItemCountInBags(slot.BundleId.Value) > 0)
                {
                    Logging.Write($"[WAQ-Equipment] Slot {slotName}: Bundle {slot.BundleId.Value} already in bags.");
                    hasItemsToEquip = true;
                    continue;
                }

                int purchaseId = slot.BundleId ?? slot.ItemId;
                if (purchaseDict.ContainsKey(purchaseId)) continue;

                if (string.IsNullOrEmpty(slot.SourceKey))
                {
                    Logging.Write($"[WAQ-Equipment] Slot {slotName}: Needs purchase but no SourceKey configured!");
                    continue;
                }

                List<string> path = null;
                if (slot.BundleId.HasValue)
                {
                    var bundle = _config.Bundles?.FirstOrDefault(b => b.Id == slot.BundleId.Value);
                    if (bundle != null) path = bundle.MenuPath;
                }
                if (path == null) path = slot.MenuPath;

                Logging.Write($"[WAQ-Equipment] Slot {slotName}: Adding {purchaseId} to purchase list.");
                purchaseDict[purchaseId] = new PurchaseTask {
                    SourceKey = slot.SourceKey,
                    PurchaseId = purchaseId,
                    Quantity = 1,
                    MenuPath = path
                };
            }

            var itemsToPurchase = purchaseDict.Values.ToList();

            // 扫描消耗品
            if (_currentClassProfile.Supplies != null && _currentClassProfile.Supplies.Count > 0)
            {
                foreach (var supplyEntry in _currentClassProfile.Supplies)
                {
                    var supply = supplyEntry.Value;
                    if (supply.ItemId <= 0) continue;

                    if (!ShouldBuySupply(supply)) continue;

                    int currentCount = GetItemCountInBags(supply.ItemId);
                    if (currentCount < supply.MinCount)
                    {
                        int buyAmount = (supply.MaxCount > 0 ? supply.MaxCount : 80) - currentCount;
                        if (buyAmount > 0)
                        {
                            Logging.Write($"[WAQ-Equipment] Adding supply to purchase list: {supplyEntry.Key} (ID: {supply.ItemId}) x{buyAmount}");
                            itemsToPurchase.Add(new PurchaseTask {
                                SourceKey = supply.SourceKey,
                                PurchaseId = supply.ItemId,
                                Quantity = buyAmount,
                                MenuPath = supply.MenuPath
                            });
                        }
                    }
                }
            }

            // 执行购买
            if (itemsToPurchase.Count > 0)
            {
                var groupedPurchases = itemsToPurchase
                    .GroupBy(x => new { x.SourceKey, PathKey = string.Join(">", x.MenuPath ?? new List<string>()) });

                foreach (var group in groupedPurchases)
                {
                    var firstTask = group.First();
                    var taskList = group.ToList();
                    
                    Logging.Write($"[WAQ-Equipment] Navigating to {firstTask.SourceKey} for path: [{string.Join(", ", firstTask.MenuPath ?? new List<string>())}]");
                    PurchaseGroupFromSource(firstTask.SourceKey, firstTask.MenuPath, taskList);
                }
                hasItemsToEquip = true;
            }

            if (hasItemsToEquip)
            {
                Thread.Sleep(1000);
                UseAndEquipItems();
            }
            else
            {
                Logging.Write("[WAQ-Equipment] No equipment actions needed.");
            }
        }

        private class PurchaseTask
        {
            public string SourceKey { get; set; }
            public int PurchaseId { get; set; }
            public int Quantity { get; set; }
            public List<string> MenuPath { get; set; }
        }

        private void PurchaseGroupFromSource(string sourceKey, List<string> menuPath, List<PurchaseTask> tasks)
        {
            GoToNpcSource(sourceKey);
            var source = _config.Sources[sourceKey];
            
            Helpers.NpcInteractionHelper.InteractWithNpc(source.Id, "");
            Thread.Sleep(1000);
            
            if (menuPath != null && menuPath.Count > 0)
            {
                Helpers.NpcInteractionHelper.NavigateMenuPath(menuPath);
                Thread.Sleep(500);
            }
            
            foreach (var task in tasks)
            {
                Logging.Write($"[WAQ-Equipment] Purchasing item {task.PurchaseId} x{task.Quantity}");
                Helpers.ShopHelper.PurchaseItemById(task.PurchaseId, task.Quantity);
                Thread.Sleep(300);
            }
            
            Helpers.ShopHelper.CloseMerchant();
            Thread.Sleep(1000);
        }
        
        private bool NeedsSlotEquipment(string slotName, EquipmentSlot slotConfig)
        {
            int slotId = GetSlotId(slotName);
            
            if (slotId == -1)
            {
                Logging.WriteError($"[WAQ-Equipment] Unknown slot name: {slotName}");
                return false;
            }
            
            string itemLink = Lua.LuaDoString<string>($@"
                local itemLink = GetInventoryItemLink('player', {slotId});
                return itemLink or '';
            ");
            
            if (string.IsNullOrEmpty(itemLink))
            {
                if (!slotConfig.AllowEmpty)
                {
                    Logging.Write($"[WAQ-Equipment] Slot {slotName} is empty, needs equipment");
                    return true;
                }
                return false;
            }
            
            int durability = Lua.LuaDoString<int>($@"
                local durability, max = GetInventoryItemDurability({slotId});
                if durability and max and max > 0 then
                    return math.floor((durability / max) * 100);
                end
                return 100;
            ");
            
            bool idMismatch = false;
            if (slotConfig.ItemId > 0)
            {
                int currentId = Lua.LuaDoString<int>($@"
                    local itemLink = GetInventoryItemLink('player', {slotId});
                    if itemLink then
                        local id = itemLink:match('item:(%d+)');
                        return tonumber(id) or 0;
                    end
                    return 0;
                ");
                if (currentId != slotConfig.ItemId)
                {
                    idMismatch = true;
                }
            }

            int threshold = _config.GlobalSettings?.DurabilityThreshold ?? 10;
            if (durability <= threshold || idMismatch)
            {
                if (idMismatch)
                    Logging.Write($"[WAQ-Equipment] Slot {slotName} ID mismatch, needs replacement");
                else
                    Logging.Write($"[WAQ-Equipment] Slot {slotName} durability {durability}% <= {threshold}%, needs replacement");
                return true;
            }
            
            return false;
        }

        private int GetSlotId(string slotName)
        {
            var slotMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "Head", 1 },
                { "Neck", 2 },
                { "Shoulder", 3 },
                { "Shirt", 4 },
                { "Chest", 5 },
                { "Waist", 6 },
                { "Legs", 7 },
                { "Feet", 8 },
                { "Wrist", 9 },
                { "Hands", 10 },
                { "Finger1", 11 },
                { "Finger2", 12 },
                { "Trinket1", 13 },
                { "Trinket2", 14 },
                { "Back", 15 },
                { "MainHand", 16 },
                { "OffHand", 17 },
                { "Ranged", 18 }
            };
            
            return slotMap.ContainsKey(slotName) ? slotMap[slotName] : -1;
        }
        
        private int GetFreeBagSlots()
        {
            return Lua.LuaDoString<int>(@"
                local free = 0;
                for bag = 0, 4 do
                    local freeSlots, bagType = GetContainerNumFreeSlots(bag);
                    if bagType == 0 then
                        free = free + freeSlots;
                    end
                end
                return free;
            ");
        }
        
        private void EnsureBagSpace(int requiredSlots = 5)
        {
            int freeSlots = GetFreeBagSlots();
            
            if (freeSlots >= requiredSlots)
            {
                Logging.Write($"[WAQ-Equipment] Bag space sufficient: {freeSlots} free slots");
                return;
            }
            
            Logging.Write($"[WAQ-Equipment] Insufficient bag space ({freeSlots}/{requiredSlots}), cleaning bags...");
            
            SellJunkItems();
            Thread.Sleep(1000);
            
            freeSlots = GetFreeBagSlots();
            if (freeSlots >= requiredSlots)
            {
                Logging.Write($"[WAQ-Equipment] Bag space after selling: {freeSlots} free slots");
                return;
            }
            
            DestroyUnsellableItems();
            Thread.Sleep(500);
            
            freeSlots = GetFreeBagSlots();
            Logging.Write($"[WAQ-Equipment] Bag space after cleanup: {freeSlots} free slots");
        }
        
        private void SellJunkItems()
        {
            string protectedLua = "{}";
            if (_config.GlobalSettings?.ProtectedItems != null && _config.GlobalSettings.ProtectedItems.Count > 0)
            {
                protectedLua = "{" + string.Join(",", _config.GlobalSettings.ProtectedItems) + "}";
            }
            
            Lua.LuaDoString($@"
                local protected = {protectedLua};
                local protectedSet = {{}};
                for _, id in ipairs(protected) do
                    protectedSet[id] = true;
                end
                
                local sold = 0;
                for bag = 0, 4 do
                    for slot = 1, GetContainerNumSlots(bag) do
                        local itemLink = GetContainerItemLink(bag, slot);
                        if itemLink then
                            local itemId = tonumber(itemLink:match('item:(%d+)'));
                            local _, _, quality = GetItemInfo(itemLink);
                            
                            if quality and quality <= 1 and not protectedSet[itemId] then
                                UseContainerItem(bag, slot);
                                sold = sold + 1;
                            end
                        end
                    end
                end
                
                if sold > 0 then
                    print('[WAQ-Equipment] Sold ' .. sold .. ' items');
                end
            ");
        }
        
        private void DestroyUnsellableItems()
        {
            string protectedLua = "{}";
            if (_config.GlobalSettings?.ProtectedItems != null && _config.GlobalSettings.ProtectedItems.Count > 0)
            {
                protectedLua = "{" + string.Join(",", _config.GlobalSettings.ProtectedItems) + "}";
            }
            
            Lua.LuaDoString($@"
                local protected = {protectedLua};
                local protectedSet = {{}};
                for _, id in ipairs(protected) do
                    protectedSet[id] = true;
                end
                
                local destroyed = 0;
                for bag = 0, 4 do
                    for slot = 1, GetContainerNumSlots(bag) do
                        local itemLink = GetContainerItemLink(bag, slot);
                        if itemLink then
                            local itemId = tonumber(itemLink:match('item:(%d+)'));
                            local _, _, quality, _, _, _, _, _, _, _, vendorPrice = GetItemInfo(itemLink);
                            
                            if quality and quality <= 1 and vendorPrice and vendorPrice < 1000 and not protectedSet[itemId] then
                                PickupContainerItem(bag, slot);
                                DeleteCursorItem();
                                destroyed = destroyed + 1;
                            end
                        end
                    end
                end
                
                if destroyed > 0 then
                    print('[WAQ-Equipment] Destroyed ' .. destroyed .. ' unsellable items');
                end
            ");
        }
        
        private void UseAndEquipItems()
        {
            Logging.Write("[WAQ-Equipment] Using purchased items and equipping...");
            
            // 步骤1: 使用礼包
            Logging.Write("[WAQ-Equipment] Step 1: Opening configured bundles...");
            
            string bundleIdsLua = "{}";
            if (_config.Bundles != null && _config.Bundles.Count > 0)
            {
                bundleIdsLua = "{" + string.Join(",", _config.Bundles.Select(b => b.Id)) + "}";
            }
            
            int usedItems = Lua.LuaDoString<int>($@"
                local bundleIds = {bundleIdsLua};
                local bundleSet = {{}};
                for _, id in ipairs(bundleIds) do
                    bundleSet[id] = true;
                end
                
                local used = 0;
                for bag = 0, 4 do
                    for slot = 1, GetContainerNumSlots(bag) do
                        local itemLink = GetContainerItemLink(bag, slot);
                        if itemLink then
                            local itemId = tonumber(itemLink:match('item:(%d+)'));
                            
                            if bundleSet[itemId] then
                                local itemName = GetItemInfo(itemLink);
                                DEFAULT_CHAT_FRAME:AddMessage('[WAQ-Equipment] Opening bundle: ' .. (itemName or itemId));
                                UseContainerItem(bag, slot);
                                used = used + 1;
                            end
                        end
                    end
                end
                return used;
            ");
            
            Logging.Write($"[WAQ-Equipment] Used {usedItems} bundles");
            if (usedItems > 0)
            {
                // 等待礼包打开的拾取窗口
                Lua.LuaDoString(@"
                    -- 等待并自动确认拾取窗口
                    for i = 1, 20 do
                        if LootFrame and LootFrame:IsVisible() then
                            for slot = 1, GetNumLootItems() do
                                LootSlot(slot)
                            end
                            CloseLoot()
                            break
                        end
                    end
                ");
                Thread.Sleep(3000);
            }
            
            // 步骤2: 装备物品 - 简化版,直接按ID装备到指定槽位
            Logging.Write("[WAQ-Equipment] Step 2: Equipping items from bag...");
            
            // 先扫描背包中有哪些物品
            var bagItems = ScanBagForConfiguredItems();
            Logging.Write($"[WAQ-Equipment] Found {bagItems.Count} configured items in bags");
            
            foreach (var kvp in bagItems)
            {
                Logging.Write($"[WAQ-Equipment]   - Item {kvp.Key} found in bag (target slot: {kvp.Value})");
            }
            
            // 构建槽位到物品ID的映射
            var slotItemMap = new Dictionary<int, int>();
            foreach (var slotEntry in _currentClassProfile.Slots)
            {
                int slotId = GetSlotId(slotEntry.Key);
                if (slotId > 0 && slotEntry.Value.ItemId > 0)
                {
                    slotItemMap[slotId] = slotEntry.Value.ItemId;
                }
            }
            
            string slotMapLua = "{";
            foreach (var kvp in slotItemMap)
            {
                slotMapLua += $"[{kvp.Key}]={kvp.Value},";
            }
            slotMapLua += "}";
            
            // 使用更简单直接的装备逻辑
            int totalEquipped = Lua.LuaDoString<int>($@"
                local equipped = 0;
                local slotItemMap = {slotMapLua};
                
                -- 处理灵魂绑定弹窗
                local function handlePopups()
                    for i = 1, 3 do
                        local frame = _G['StaticPopup' .. i]
                        if frame and frame:IsVisible() then
                            local text = _G['StaticPopup' .. i .. 'Text']:GetText() or ''
                            if text:find('绑定') or text:find('bind') or text:find('Soulbound') then
                                _G['StaticPopup' .. i .. 'Button1']:Click();
                            end
                        end
                    end
                end
                
                -- 直接遍历所有需要装备的槽位
                for targetSlot, expectedItemId in pairs(slotItemMap) do
                    -- 检查槽位当前装备
                    local currentLink = GetInventoryItemLink('player', targetSlot);
                    local currentId = nil;
                    if currentLink then
                        currentId = tonumber(currentLink:match('item:(%d+)'));
                    end
                    
                    -- 如果已经是正确的装备,跳过
                    if currentId == expectedItemId then
                        -- 已装备正确物品
                    else
                        -- 在背包中查找该物品
                        local found = false;
                        for bag = 0, 4 do
                            if found then break; end
                            for slot = 1, GetContainerNumSlots(bag) do
                                local itemLink = GetContainerItemLink(bag, slot);
                                if itemLink then
                                    local itemId = tonumber(itemLink:match('item:(%d+)'));
                                    if itemId == expectedItemId then
                                        local name = GetItemInfo(itemLink) or tostring(itemId);
                                        DEFAULT_CHAT_FRAME:AddMessage('[WAQ-Equipment] Equipping ' .. name .. ' (ID:' .. itemId .. ') to slot ' .. targetSlot);
                                        print('[WAQ-Equipment] Equipping ' .. name .. ' to slot ' .. targetSlot);
                                        
                                        PickupContainerItem(bag, slot);
                                        EquipCursorItem(targetSlot);
                                        equipped = equipped + 1;
                                        handlePopups();
                                        found = true;
                                        break;
                                    end
                                end
                            end
                        end
                        
                        if not found and currentId ~= expectedItemId then
                            print('[WAQ-Equipment] WARNING: Item ' .. expectedItemId .. ' not found in bags for slot ' .. targetSlot);
                        end
                    end
                end
                
                return equipped;
            ");
            
            Logging.Write($"[WAQ-Equipment] Equipped {totalEquipped} items");
            Thread.Sleep(2000);
            
            // 最终验证
            Logging.Write("[WAQ-Equipment] Verifying equipment...");
            foreach (var slotEntry in _currentClassProfile.Slots)
            {
                if (slotEntry.Value.Strategy == "Ignore") continue;
                if (slotEntry.Value.ItemId <= 0) continue;
                
                int slotId = GetSlotId(slotEntry.Key);
                int equippedId = GetEquippedItemId(slotId);
                
                if (equippedId == slotEntry.Value.ItemId)
                {
                    Logging.Write($"[WAQ-Equipment] ✓ Slot {slotEntry.Key}: Correct (ID: {equippedId})");
                }
                else if (equippedId == 0)
                {
                    Logging.Write($"[WAQ-Equipment] ✗ Slot {slotEntry.Key}: EMPTY (Expected: {slotEntry.Value.ItemId})");
                }
                else
                {
                    Logging.Write($"[WAQ-Equipment] ⚠ Slot {slotEntry.Key}: Wrong item (Current: {equippedId}, Expected: {slotEntry.Value.ItemId})");
                }
            }
            
            Logging.Write("[WAQ-Equipment] Equipment cycle complete");
        }
        
        /// <summary>
        /// 扫描背包中的配置物品
        /// </summary>
        private Dictionary<int, int> ScanBagForConfiguredItems()
        {
            var result = new Dictionary<int, int>();
            
            if (_currentClassProfile?.Slots == null) return result;
            
            // 获取所有配置的物品ID及其目标槽位
            var configuredItems = new Dictionary<int, int>();
            foreach (var slotEntry in _currentClassProfile.Slots)
            {
                if (slotEntry.Value.ItemId > 0)
                {
                    int slotId = GetSlotId(slotEntry.Key);
                    if (slotId > 0)
                    {
                        configuredItems[slotEntry.Value.ItemId] = slotId;
                    }
                }
            }
            
            // 扫描背包
            string itemIdsLua = "{" + string.Join(",", configuredItems.Keys) + "}";
            
            string foundItemsStr = Lua.LuaDoString<string>($@"
                local configuredIds = {itemIdsLua};
                local configSet = {{}};
                for _, id in ipairs(configuredIds) do
                    configSet[id] = true;
                end
                
                local found = '';
                for bag = 0, 4 do
                    for slot = 1, GetContainerNumSlots(bag) do
                        local itemLink = GetContainerItemLink(bag, slot);
                        if itemLink then
                            local itemId = tonumber(itemLink:match('item:(%d+)'));
                            if itemId and configSet[itemId] then
                                if found ~= '' then found = found .. ','; end
                                found = found .. itemId;
                            end
                        end
                    end
                end
                return found;
            ");
            
            if (!string.IsNullOrEmpty(foundItemsStr))
            {
                foreach (var idStr in foundItemsStr.Split(','))
                {
                    if (int.TryParse(idStr, out int itemId) && configuredItems.ContainsKey(itemId))
                    {
                        result[itemId] = configuredItems[itemId];
                    }
                }
            }
            
            return result;
        }

        private bool ShouldBuySupply(SupplyItem supply)
        {
            return Lua.LuaDoString<bool>($@"
                local itemId = {supply.ItemId};
                if not itemId or itemId == 0 then return false; end
                local name, link, quality, iLevel, reqLevel, class, subClass, maxStack, equipSlot, texture, vendorPrice = GetItemInfo(itemId);
                
                if not subClass then return true; end -- Item info not cached, process normally

                -- Detect Bag Config
                local hasQuiver = false;
                local hasAmmoPouch = false;
                
                for i=1,4 do
                     local free, bagType = GetContainerNumFreeSlots(i);
                     if bagType and bagType == 1 then hasQuiver = true; end
                     if bagType and bagType == 2 then hasAmmoPouch = true; end
                end
                
                -- Check constraints
                if subClass == 'Arrow' or subClass == '箭' then
                    if not hasQuiver then return false; end
                elseif subClass == 'Bullet' or subClass == '子弹' then
                    if not hasAmmoPouch then return false; end
                end
                
                return true;
            ");
        }
    }
}
