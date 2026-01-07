using System.Collections.Generic;
using robotManager.Helpful;
using System.Linq;
using System.Threading;
using wManager.Wow.Bot.Tasks;
using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;

namespace Wholesome_Auto_Quester.PrivateServer.Helpers
{
    public static class NpcInteractionHelper
    {
        public static void InteractWithTrainer(int npcEntry, string gossipOption)
        {
            var npc = FindNpcByEntry(npcEntry);
            if (npc == null)
            {
                Logging.WriteError($"[WAQ-Private] Trainer NPC {npcEntry} not found");
                return;
            }
            
            Logging.Write($"[WAQ-Private] Interacting with trainer NPC {npc.Name} (Entry: {npcEntry})");
            
            // 移动到NPC附近
            int attempts = 0;
            while (npc.GetDistance > 5f && attempts < 50)
            {
                GoToTask.ToPosition(npc.Position, 3f);
                Thread.Sleep(100);
                attempts++;
            }
            
            if (npc.GetDistance > 5f)
            {
                Logging.WriteError("[WAQ-Private] Failed to reach trainer NPC");
                return;
            }
            
            // 交互
            Interact.InteractGameObject(npc.GetBaseAddress);
            Thread.Sleep(1500);
            
            // 选择Gossip选项 (支持文本匹配)
            if (!string.IsNullOrEmpty(gossipOption))
            {
                SelectGossipByText(gossipOption);
                Thread.Sleep(1000);
            }
            
            // 学习所有可用技能
            Logging.Write("[WAQ-Private] Learning services...");
            Lua.LuaDoString("BuyTrainerService(0)");
            Thread.Sleep(1000);
            
            Logging.Write("[WAQ-Private] Training complete");
        }
        
        private static void SelectGossipByText(string targetText)
        {
            if (string.IsNullOrEmpty(targetText)) return;
            
            Logging.Write($"[WAQ-Private] Attempting to match menu option: '{targetText}'");
            
            // Clean target text from C# side as well to be safe
            string escapedTarget = targetText.Replace("'", "\\'");
            
            Lua.LuaDoString($@"
                local target = '{escapedTarget}';
                local found = false;
                
                local function cleanText(text)
                    if not text then return '' end
                    -- 1. Strip Colors (|cff... |r)
                    text = text:gsub('|c%x%x%x%x%x%x%x%x', ''):gsub('|r', '')
                    -- 2. Strip all non-alphanumeric and non-Chinese characters
                    local cleaned = ''
                    for i = 1, #text do
                        local b = text:byte(i)
                        -- 48-57: 0-9, 65-90: A-Z, 97-122: a-z, >127: non-ascii (UTF-8 Chinese)
                        if (b >= 48 and b <= 57) or (b >= 65 and b <= 90) or (b >= 97 and b <= 122) or b > 127 then
                            cleaned = cleaned .. text:sub(i, i)
                        end
                    end
                    return cleaned:lower()
                end

                local targetClean = cleanText(target)
                print('[WAQ-Private] Matching target: ' .. target .. ' (Clean: ' .. targetClean .. ')')

                -- Check Gossip Options
                local options = {{ GetGossipOptions() }};
                for i = 1, #options, 2 do
                    local currentLabel = options[i]
                    local currentClean = cleanText(currentLabel)
                    
                    if currentClean ~= '' and (currentClean == targetClean or currentClean:find(targetClean, 1, true) or targetClean:find(currentClean, 1, true)) then
                        local idx = (i + 1) / 2;
                        DEFAULT_CHAT_FRAME:AddMessage('|cff00ff00[WAQ-Private] Matched: |r ' .. currentLabel .. ' (Index: ' .. idx .. ')');
                        print('[WAQ-Private] MATCHED! Index: ' .. idx)
                        SelectGossipOption(idx);
                        found = true;
                        break;
                    end
                end
                
                -- Check Available Quests
                if not found then
                    local quests = {{ GetGossipAvailableQuests() }};
                    for i = 1, #quests, 7 do
                        local currentLabel = quests[i]
                        local currentClean = cleanText(currentLabel)
                        if currentClean ~= '' and (currentClean == targetClean or currentClean:find(targetClean, 1, true) or targetClean:find(currentClean, 1, true)) then
                            local idx = (i + 6) / 7;
                            DEFAULT_CHAT_FRAME:AddMessage('|cff00ff00[WAQ-Private] Matched Quest: |r ' .. currentLabel);
                            print('[WAQ-Private] MATCHED QUEST! Index: ' .. idx)
                            SelectGossipAvailableQuest(idx);
                            found = true;
                            break;
                        end
                    end
                end

                if not found then
                    print('[WAQ-Private] FAILED to match: ' .. target)
                end
            ");
        }
        
        public static void InteractWithNpc(int npcEntry, string gossipOption)
        {
            var npc = FindNpcByEntry(npcEntry);
            if (npc == null)
            {
                Logging.WriteError($"[WAQ-Private] NPC {npcEntry} not found");
                return;
            }
            
            Logging.Write($"[WAQ-Private] Interacting with NPC {npc.Name} (Entry: {npcEntry})");
            
            // 移动到NPC附近
            int attempts = 0;
            while (npc.GetDistance > 5f && attempts < 50)
            {
                GoToTask.ToPosition(npc.Position, 3f);
                Thread.Sleep(100);
                attempts++;
            }
            
            // 交互
            Interact.InteractGameObject(npc.GetBaseAddress);
            Thread.Sleep(1500);
            
            // 选择Gossip选项
            if (!string.IsNullOrEmpty(gossipOption))
            {
                SelectGossipByText(gossipOption);
                Thread.Sleep(1000);
            }
        }
        
        public static void NavigateMenuPath(List<string> menuPath)
        {
            if (menuPath == null || menuPath.Count == 0) return;
            
            foreach (var step in menuPath)
            {
                SelectGossipByText(step);
                Thread.Sleep(1500);
            }
        }
        
        private static WoWUnit FindNpcByEntry(int entry)
        {
            return ObjectManager.GetObjectWoWUnit()
                .FirstOrDefault(u => u.Entry == entry && u.IsValid);
        }
    }
}
