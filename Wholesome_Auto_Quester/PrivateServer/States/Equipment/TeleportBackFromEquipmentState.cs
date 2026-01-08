using robotManager.FiniteStateMachine;
using robotManager.Helpful;
using System;
using System.Threading;
using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;
using Wholesome_Auto_Quester.PrivateServer.Managers;
using Wholesome_Auto_Quester.PrivateServer.Models;

namespace Wholesome_Auto_Quester.PrivateServer.States.Equipment
{
    /// <summary>
    /// 装备更换完成后传送返回原来位置的状态
    /// 使用 TriggerRefresh 时保存的 TeleportLocation 进行传送
    /// </summary>
    public class TeleportBackFromEquipmentState : State
    {
        private EquipmentManager _equipmentManager;
        private TeleportManager _teleportManager;
        private EquipmentConfig _config;
        
        public TeleportBackFromEquipmentState(EquipmentManager equipmentManager, 
                                               EquipmentConfig config,
                                               TeleportManager teleportManager = null)
        {
            _equipmentManager = equipmentManager;
            _config = config;
            _teleportManager = teleportManager;
            Priority = 10;
        }
        
        public override string DisplayName => "WAQ-Private - Teleport Back (Equipment)";
        
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
                
                return _equipmentManager.CurrentEquipmentPhase == EquipmentManager.EquipmentPhase.TeleportingBack;
            }
        }
        
        public override void Run()
        {
            try
            {
                Logging.Write("[WAQ-Private] ========================================");
                Logging.Write("[WAQ-Private] Step 4: Teleporting back to original area");
                Logging.Write("[WAQ-Private] ========================================");
                
                // 获取保存的原始位置
                var savedPos = new Vector3(
                    _equipmentManager.SavedPosX,
                    _equipmentManager.SavedPosY,
                    _equipmentManager.SavedPosZ
                );
                int savedMapId = _equipmentManager.SavedMapId;

                // 检查保存的位置是否有效
                if (savedPos.X == 0 && savedPos.Y == 0)
                {
                    Logging.WriteError("[WAQ-Private] ✗ Invalid saved position (0,0,0)! Teleport back cancelled.");
                    CompleteEquipmentCycle();
                    return;
                }
                
                int currentMapId = Usefuls.ContinentId;
                float distance = ObjectManager.Me.Position.DistanceTo(savedPos);
                
                // 检查是否已经在目标位置附近
                if (currentMapId == savedMapId && distance < 300)
                {
                    Logging.Write($"[WAQ-Private] Already close to original position ({distance:F1}y), no teleport needed");
                    CompleteEquipmentCycle();
                    return;
                }
                
                // 优先使用瞬移功能
                if (Helpers.FlyHelper.IsEnabled)
                {
                    Logging.Write("[WAQ-Private] 瞬移功能已启用，使用智能旅行返回原位置");
                    
                    if (Helpers.FlyHelper.SmartTravelTo(savedPos, savedMapId, _teleportManager))
                    {
                        Logging.Write("[WAQ-Private] ✓ 瞬移返回成功");
                        CompleteEquipmentCycle();
                        return;
                    }
                    Logging.Write("[WAQ-Private] 瞬移失败，尝试使用传统传送方式");
                }
                
                // 传统传送逻辑
                if (!_equipmentManager.HasSavedReturnLocation)
                {
                    Logging.Write("[WAQ-Private] No saved return teleport location, will travel normally");
                    CompleteEquipmentCycle();
                    return;
                }
                
                var returnLocation = _equipmentManager.SavedReturnLocation;
                
                Logging.Write($"[WAQ-Private] Return teleport point: {returnLocation.Name}");
                Logging.Write($"[WAQ-Private] Target continent: {returnLocation.Continent}");
                Logging.Write($"[WAQ-Private] Menu path: [{string.Join(" > ", returnLocation.MenuPath ?? new System.Collections.Generic.List<string>())}]");
                
                // 执行传送
                bool success = false;
                
                if (_teleportManager != null)
                {
                    success = _teleportManager.ExecuteTeleport(returnLocation);
                }
                else
                {
                    // 如果没有 TeleportManager，尝试直接使用传送物品
                    success = ExecuteTeleportDirect(returnLocation);
                }
                
                if (success)
                {
                    Logging.Write($"[WAQ-Private] ✓ Teleport successful! Arrived at {returnLocation.Name}");
                }
                else
                {
                    Logging.WriteError("[WAQ-Private] ✗ Teleport failed, will travel normally");
                }
                
                CompleteEquipmentCycle();
            }
            catch (Exception ex)
            {
                Logging.WriteError($"[WAQ-Private] Error in teleport back: {ex.Message}");
                CompleteEquipmentCycle();
            }
        }
        
        /// <summary>
        /// 直接使用传送物品执行传送（不通过 TeleportManager）
        /// </summary>
        private bool ExecuteTeleportDirect(TeleportLocation location)
        {
            try
            {
                if (_config?.Training == null) return false;
                
                int teleportItem = _config.Training.TeleportItemEntry;
                if (teleportItem <= 0)
                {
                    teleportItem = 6948; // 默认炉石
                }
                
                // 检查背包中是否有传送物品
                int count = Lua.LuaDoString<int>($"return GetItemCount({teleportItem}) or 0");
                if (count <= 0)
                {
                    Logging.Write($"[WAQ-Private] Teleport item {teleportItem} not found in bags");
                    return false;
                }
                
                if (location.MenuPath == null || location.MenuPath.Count == 0)
                {
                    Logging.Write("[WAQ-Private] No menu path configured for this location");
                    return false;
                }
                
                Logging.Write($"[WAQ-Private] Using teleport item {teleportItem}...");
                
                MovementManager.StopMove();
                Thread.Sleep(500);
                
                // 使用传送物品
                Lua.RunMacroText($"/use item:{teleportItem}");
                
                // 等待菜单出现
                bool menuOpen = false;
                for (int i = 0; i < 30; i++)
                {
                    if (Lua.LuaDoString<bool>("return (GossipFrame and GossipFrame:IsVisible()) or (QuestFrame and QuestFrame:IsVisible())"))
                    {
                        menuOpen = true;
                        Logging.Write("[WAQ-Private] Teleport menu opened");
                        break;
                    }
                    Thread.Sleep(500);
                }
                
                if (!menuOpen)
                {
                    Logging.Write("[WAQ-Private] Teleport menu did not open");
                    return false;
                }
                
                // 导航菜单
                Logging.Write($"[WAQ-Private] Navigating menu: [{string.Join(" > ", location.MenuPath)}]");
                Helpers.NpcInteractionHelper.NavigateMenuPath(location.MenuPath);
                
                // 等待传送完成
                Logging.Write("[WAQ-Private] Waiting for teleport...");
                Thread.Sleep(8000);
                
                // 等待世界同步
                for (int i = 0; i < 30; i++)
                {
                    if (Usefuls.ContinentId == location.Continent)
                    {
                        Logging.Write("[WAQ-Private] ✓ Teleport complete!");
                        Thread.Sleep(2000);
                        return true;
                    }
                    Thread.Sleep(1000);
                }
                
                return Usefuls.ContinentId == location.Continent;
            }
            catch (Exception ex)
            {
                Logging.WriteError($"[WAQ-Private] Teleport direct failed: {ex.Message}");
                return false;
            }
        }
        
        private void CompleteEquipmentCycle()
        {
            _equipmentManager.ClearSavedPosition();
            _equipmentManager.MarkRefreshComplete(true);  // 验证并更新冷却时间
            _equipmentManager.SetPhase(EquipmentManager.EquipmentPhase.Idle);
            Logging.Write("[WAQ-Private] ========================================");
            Logging.Write("[WAQ-Private] Equipment refresh cycle complete!");
            Logging.Write("[WAQ-Private] ========================================");
        }
    }
}
