using robotManager.Helpful;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using wManager.Wow.Helpers;

namespace Wholesome_Auto_Quester.PrivateServer.Helpers
{
    public static class ShopHelper
    {
        /// <summary>
        /// 导航多级Gossip菜单
        /// </summary>
        /// <param name="gossipPath">Gossip选项路径，例如 [2, 1] 表示先选第2项再选第1项</param>
        public static void NavigateGossipPath(List<int> gossipPath)
        {
            if (gossipPath == null || gossipPath.Count == 0)
            {
                Logging.Write("[WAQ-Private] No gossip path configured");
                return;
            }
            
            foreach (int option in gossipPath)
            {
                Logging.Write($"[WAQ-Private] Selecting gossip option {option}");
                Lua.LuaDoString($"SelectGossipOption({option})");
                Thread.Sleep(500); // 等待菜单打开
            }
            
            Thread.Sleep(1000); // 最后等待商店窗口打开
        }
        
        /// <summary>
        /// 购买指定名称的物品
        /// </summary>
        public static void PurchaseItemsByName(List<string> itemNames)
        {
            if (itemNames == null || itemNames.Count == 0)
            {
                Logging.Write("[WAQ-Private] No items to purchase");
                return;
            }
            
            foreach (string itemName in itemNames)
            {
                BuyItemByName(itemName);
                Thread.Sleep(500);
            }
        }
        
        /// <summary>
        /// 购买指定ID的物品
        /// </summary>
        public static void PurchaseItemsById(List<int> itemIds)
        {
            if (itemIds == null || itemIds.Count == 0)
            {
                Logging.Write("[WAQ-Private] No item IDs to purchase");
                return;
            }
            
            foreach (int itemId in itemIds)
            {
                BuyItemById(itemId);
                Thread.Sleep(500);
            }
        }
        
        /// <summary>
        /// 购买单个物品 (通过ID)
        /// </summary>
        public static void PurchaseItemById(int itemId, int quantity = 1)
        {
            BuyItemById(itemId, quantity);
        }
        
        /// <summary>
        /// 通过物品名称购买（搜索所有页面）
        /// </summary>
        private static void BuyItemByName(string itemName)
        {
            Logging.Write($"[WAQ-Private] Searching for item: {itemName}");
            
            // Lua脚本：遍历所有商店页面查找并购买物品
            string luaScript = $@"
                local itemName = '{EscapeLuaString(itemName)}';
                local found = false;
                
                -- 检查当前页的所有物品（不翻页，直接检查当前可见的所有商品）
                local numItems = GetMerchantNumItems() or 0;
                print('[WAQ-Private] Total merchant items: ' .. numItems);
                print('[WAQ-Private] Listing all merchant items:');
                
                -- 先列出所有商品名称（用于调试）
                for i = 1, numItems do
                    local name = GetMerchantItemInfo(i);
                    if name then
                        print('[WAQ-Private]   [' .. i .. '] ' .. name);
                    end
                end
                
                print('[WAQ-Private] Now searching for: ' .. itemName);
                
                -- 再次遍历查找并购买
                for i = 1, numItems do
                    local name, texture, price, quantity, numAvailable, isUsable = GetMerchantItemInfo(i);
                    if name then
                        if string.find(name, itemName) then
                            print('[WAQ-Private] ✓ MATCH! Purchasing: ' .. name .. ' (index: ' .. i .. ')');
                            BuyMerchantItem(i, 1);
                            found = true;
                            -- 不break，继续查找其他匹配的物品
                        end
                    end
                end
                
                if not found then
                    print('[WAQ-Private] ✗ NOT FOUND: ' .. itemName);
                end
            ";
            
            Lua.LuaDoString(luaScript);
            
            Logging.Write($"[WAQ-Private] Purchase attempt completed for: {itemName}");
        }
        
        /// <summary>
        /// 通过物品ID购买
        /// </summary>
        private static void BuyItemById(int itemId, int quantity = 1)
        {
            string luaScript = $@"
                local targetId = {itemId};
                local found = false;
                
                local numItems = GetMerchantNumItems() or 0;
                print('[WAQ-Private] Merchant items: ' .. numItems);
                
                for i = 1, numItems do
                    local link = GetMerchantItemLink(i);
                    if link then
                        local id = tonumber(link:match('item:(%d+)'));
                        if id == targetId then
                            DEFAULT_CHAT_FRAME:AddMessage('|cff00ff00[WAQ-Private] Purchasing:|r ' .. (name or targetId));
                            print('[WAQ-Private] Purchasing ID ' .. targetId .. ' at index ' .. i);
                            BuyMerchantItem(i, {quantity});
                            found = true;
                            break;
                        end
                    end
                end
                
                if not found then
                    DEFAULT_CHAT_FRAME:AddMessage('|cffff0000[WAQ-Private] Item ' .. targetId .. ' not found!|r');
                    print('[WAQ-Private] FAILED to find item ID: ' .. targetId);
                    
                    -- List first 5 items to help debug
                    for i = 1, math.min(10, numItems) do
                        local link = GetMerchantItemLink(i);
                        if link then print('[WAQ-Private]   Merchant item ' .. i .. ': ' .. link) end
                    end
                end
            ";
            
            Lua.LuaDoString(luaScript);
        }
        
        /// <summary>
        /// 关闭商店窗口
        /// </summary>
        public static void CloseMerchant()
        {
            Lua.LuaDoString("CloseMerchant()");
        }
        
        /// <summary>
        /// 转义Lua字符串中的特殊字符
        /// </summary>
        private static string EscapeLuaString(string str)
        {
            return str.Replace("'", "\\'").Replace("\"", "\\\"");
        }
    }
}
