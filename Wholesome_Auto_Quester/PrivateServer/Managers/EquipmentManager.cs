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
            EquippingItems,
            TeleportingBack  // 返回原来位置
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
            
            foreach (var slot in _currentClassProfile.Slots)
            {
                if (slot.Value.Strategy == "Ignore") continue;
                if (NeedsSlotEquipment(slot.Key, slot.Value)) return true;
            }

            if (NeedsSupplies()) return true;
            
            return false;
        }

        public bool NeedsSupplies()
        {
            if (_config == null || _currentClassProfile == null) return false;
            if (_currentClassProfile.Supplies == null || _currentClassProfile.Supplies.Count == 0) return false;

            foreach (var supplyEntry in _currentClassProfile.Supplies)
            {
                var supply = supplyEntry.Value;
                if (supply.ItemId <= 0) continue;

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
            
            // 保存当前位置并查找最佳返回传送点
            SaveCurrentPosition(teleportManager);
            
            _currentPhase = EquipmentPhase.CleaningBags;
        }
        
        /// <summary>
        /// 保存当前位置并查找最佳返回传送点
        /// </summary>
        public void SaveCurrentPosition(TeleportManager teleportManager = null)
        {
            var pos = ObjectManager.Me.Position;
            _savedPosX = pos.X;
            _savedPosY = pos.Y;
            _savedPosZ = pos.Z;
            _savedMapId = Usefuls.ContinentId;
            
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
        
        public void SetPhase(EquipmentPhase phase)
        {
            _currentPhase = phase;
            Logging.Write($"[WAQ-Equipment] Equipment phase: {phase}");
        }
        
        public void ExecuteCleanBags()
        {
            DeleteDamagedEquipment();
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
            if (_config.Training == null || !_config.Training.UseCustomTeleport) return;

            float distance = ObjectManager.Me.Position.DistanceTo(targetPos);
            bool differentContinent = (int)wManager.Wow.Helpers.Usefuls.ContinentId != targetMapId;

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
                Thread.Sleep(5000);
            }
            
            // 步骤2: 装备物品
            Logging.Write("[WAQ-Equipment] Step 2: Equipping items...");
            
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
            
            int totalEquipped = Lua.LuaDoString<int>($@"
                local equipped = 0;
                local slotItemMap = {slotMapLua};
                
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

                local locToSlot = {{
                    INVTYPE_HEAD = 1, INVTYPE_NECK = 2, INVTYPE_SHOULDER = 3, INVTYPE_CLOAK = 15,
                    INVTYPE_CHEST = 5, INVTYPE_ROBE = 5, INVTYPE_WRIST = 9, INVTYPE_HAND = 10,
                    INVTYPE_WAIST = 6, INVTYPE_LEGS = 7, INVTYPE_FEET = 8, INVTYPE_FINGER = 11,
                    INVTYPE_TRINKET = 13, INVTYPE_RANGED = 18, INVTYPE_RELIC = 18,
                    INVTYPE_2HWEAPON = 16, INVTYPE_WEAPONMAINHAND = 16, INVTYPE_WEAPONOFFHAND = 17,
                    INVTYPE_HOLDABLE = 17, INVTYPE_SHIELD = 17, INVTYPE_THROWN = 18, INVTYPE_RANGEDRIGHT = 18
                }};

                local itemToSlotMap = {{}};
                for slotId, itemId in pairs(slotItemMap) do
                    itemToSlotMap[itemId] = slotId;
                end

                local nextFingerSlot = 11;
                local nextTrinketSlot = 13;
                local processedSlots = {{}};

                for bag = 0, 4 do
                    for slot = 1, GetContainerNumSlots(bag) do
                        local itemLink = GetContainerItemLink(bag, slot);
                        if itemLink then
                            local itemId = tonumber(itemLink:match('item:(%d+)'));
                            local name, _, _, _, _, _, _, _, iLoc = GetItemInfo(itemLink);
                            if iLoc and iLoc ~= '' and iLoc ~= 'INVTYPE_BAG' then
                                local targetSlot = nil;
                                
                                if itemId and itemToSlotMap[itemId] then
                                    targetSlot = itemToSlotMap[itemId];
                                elseif iLoc == 'INVTYPE_WEAPON' then
                                    -- Skip weapons without strict mapping
                                elseif iLoc == 'INVTYPE_FINGER' then
                                    targetSlot = nextFingerSlot;
                                    if nextFingerSlot < 12 then nextFingerSlot = 12 end
                                elseif iLoc == 'INVTYPE_TRINKET' then
                                    targetSlot = nextTrinketSlot;
                                    if nextTrinketSlot < 14 then nextTrinketSlot = 14 end
                                else
                                    targetSlot = locToSlot[iLoc];
                                end

                                if targetSlot and not processedSlots[targetSlot] then
                                    local currentLink = GetInventoryItemLink('player', targetSlot);
                                    local currentId = nil;
                                    if currentLink then
                                        currentId = tonumber(currentLink:match('item:(%d+)'));
                                    end
                                    
                                    if currentId == itemId then
                                        processedSlots[targetSlot] = true;
                                    else
                                        DEFAULT_CHAT_FRAME:AddMessage('[WAQ-Equipment] Equipping ' .. (name or 'item') .. ' to slot ' .. targetSlot);
                                        PickupContainerItem(bag, slot);
                                        EquipCursorItem(targetSlot);
                                        equipped = equipped + 1;
                                        handlePopups();
                                        processedSlots[targetSlot] = true;
                                    end
                                end
                            end
                        end
                    end
                end
                
                return equipped;
            ");
            
            Logging.Write($"[WAQ-Equipment] Equipped {totalEquipped} items");
            Thread.Sleep(2000);
            Logging.Write("[WAQ-Equipment] Equipment cycle complete");
        }
    }
}
