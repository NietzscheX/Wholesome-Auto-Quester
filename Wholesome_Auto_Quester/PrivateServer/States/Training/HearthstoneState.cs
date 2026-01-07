using robotManager.FiniteStateMachine;
using robotManager.Helpful;
using System;
using System.Linq;
using System.Threading;
using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;

namespace Wholesome_Auto_Quester.PrivateServer.States.Training
{
    public class HearthstoneState : State
    {
        private Managers.TrainingManager _trainingManager;
        
        public HearthstoneState(Managers.TrainingManager trainingManager)
        {
            _trainingManager = trainingManager;
            Priority = 15;
        }
        
        public override string DisplayName => "WAQ-Private - Hearthstone";
        
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
                
                return _trainingManager.CurrentTrainingPhase == Managers.TrainingManager.TrainingPhase.UsingHearthstone;
            }
        }
        
        public override void Run()
        {
            try
            {
                int teleportItemEntry;
                string teleportType;
                
                if (_trainingManager.UseCustomTeleport)
                {
                    teleportItemEntry = _trainingManager.TeleportItemEntry;
                    teleportType = "自定义传送物品";
                    Logging.Write($"[WAQ-Private] 使用自定义传送物品回城 (Entry: {teleportItemEntry})");
                }
                else
                {
                    teleportItemEntry = _trainingManager.HearthstoneEntry;
                    teleportType = "炉石";
                    Logging.Write($"[WAQ-Private] 使用炉石回城 (Entry: {teleportItemEntry})");
                }
                
                var teleportItem = Bag.GetBagItem().FirstOrDefault(item => item.Entry == teleportItemEntry);
                if (teleportItem == null)
                {
                    Logging.WriteError($"[WAQ-Private] {teleportType} (Entry: {teleportItemEntry}) 未在背包中找到!");
                    _trainingManager.SetPhase(Managers.TrainingManager.TrainingPhase.TravelingToTrainer);
                    return;
                }
                
                Logging.Write($"[WAQ-Private] 停止移动，准备使用 {teleportItem.Name}");
                wManager.Wow.Helpers.MovementManager.StopMove();
                Thread.Sleep(500);
                
                Logging.Write($"[WAQ-Private] 正在使用 {teleportItem.Name}...");
                ItemsManager.UseItem(teleportItem.Name);
                
                Logging.Write($"[WAQ-Private] 等待传送弹窗打开...");
                
                bool windowOpened = false;
                int waitCount = 0;
                
                while (waitCount < 10 && !windowOpened)
                {
                    string windowTitle = Lua.LuaDoString<string>(@"
                        if GossipFrame and GossipFrame:IsVisible() then
                            local title = GossipFrameTitleText:GetText() or ''
                            return title
                        end
                        return ''
                    ");
                    
                    if (!string.IsNullOrEmpty(windowTitle))
                    {
                        Logging.Write($"[WAQ-Private] 检测到弹窗: {windowTitle}");
                        if (windowTitle.Contains("炉石") || windowTitle.Contains("Hearthstone"))
                        {
                            windowOpened = true;
                            Logging.Write($"[WAQ-Private] ✓ 炉石弹窗已打开!");
                            break;
                        }
                    }
                    
                    Thread.Sleep(500);
                    waitCount++;
                }
                
                if (!windowOpened)
                {
                    Logging.WriteError($"[WAQ-Private] ✗ 炉石弹窗未打开，物品可能未使用或冷却中");
                }
                
                // 处理菜单
                if (_trainingManager.UseCustomTeleport && 
                    _trainingManager.TeleportMenuPath != null && 
                    _trainingManager.TeleportMenuPath.Count > 0)
                {
                    Logging.Write($"[WAQ-Private] 等待传送菜单出现...");
                    
                    bool menuAppeared = false;
                    for (int i = 0; i < 10; i++)
                    {
                        bool hasMenu = Lua.LuaDoString<bool>(@"
                            return (GossipFrame and GossipFrame:IsVisible()) or 
                                   (QuestFrame and QuestFrame:IsVisible())
                        ");
                        
                        if (hasMenu)
                        {
                            menuAppeared = true;
                            Logging.Write($"[WAQ-Private] 菜单出现！");
                            break;
                        }
                        Thread.Sleep(500);
                    }
                    
                    if (menuAppeared)
                    {
                        foreach (var menuOption in _trainingManager.TeleportMenuPath)
                        {
                            Logging.Write($"[WAQ-Private] 选择菜单选项: {menuOption}");
                            SelectMenuOption(menuOption);
                            Thread.Sleep(1500);
                        }
                    }
                }
                
                Logging.Write($"[WAQ-Private] 等待传送加载...");
                Thread.Sleep(3000);
                
                int loadWaitCount = 0;
                while (Usefuls.IsLoadingOrConnecting && loadWaitCount < 30)
                {
                    Logging.Write($"[WAQ-Private] 正在加载... ({loadWaitCount}s)");
                    Thread.Sleep(1000);
                    loadWaitCount++;
                }
                
                if (Usefuls.IsLoadingOrConnecting)
                {
                    Logging.WriteError("[WAQ-Private] 传送加载超时");
                }
                else
                {
                    Logging.Write("[WAQ-Private] 传送完成，角色已到达目的地");
                }
                
                Thread.Sleep(2000);
                
                _trainingManager.SetPhase(Managers.TrainingManager.TrainingPhase.TravelingToTrainer);
            }
            catch (Exception ex)
            {
                Logging.WriteError($"[WAQ-Private] 传送错误: {ex.Message}");
                _trainingManager.SetPhase(Managers.TrainingManager.TrainingPhase.TravelingToTrainer);
            }
        }
        
        private void SelectMenuOption(string menuOption)
        {
            string escapedText = menuOption.Replace("'", "\\'");
            
            Lua.LuaDoString($@"
                local targetText = '{escapedText}'
                local cleanTarget = string.gsub(targetText, '%s', '')
                
                for i = 1, 32 do
                    local button = _G['GossipTitleButton' .. i]
                    if button and button:IsVisible() then
                        local text = string.gsub(button:GetText() or '', '%s', '')
                        if string.find(text, cleanTarget) then
                            button:Click()
                            return
                        end
                    end
                end
                
                local numOptions = GetNumGossipOptions()
                if numOptions then
                    for i = 1, numOptions do
                        local option = select(i * 2 - 1, GetGossipOptions())
                        if option then
                            local cleanOption = string.gsub(option, '%s', '')
                            if string.find(cleanOption, cleanTarget) then
                                SelectGossipOption(i)
                                return
                            end
                        end
                    end
                end
            ");
        }
    }
}
