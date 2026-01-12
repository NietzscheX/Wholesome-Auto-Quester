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
        /// 通过物品ID购买(支持翻页搜索)
        /// </summary>
        private static void BuyItemById(int itemId, int quantity = 1)
        {
            string luaScript = $@"
                local targetId = {itemId};
                local found = false;
                local currentPage = 1;
                local maxPages = 10; -- 最多翻10页
                
                -- 获取总页数
                local numPages = GetMerchantNumPages and GetMerchantNumPages() or 1;
                if numPages > maxPages then numPages = maxPages; end
                
                print('[WAQ-Private] Searching for item ' .. targetId .. ' across ' .. numPages .. ' pages');
                
                -- 遍历所有页面
                for page = 1, numPages do
                    if found then break; end
                    
                    -- 切换到指定页面(如果有多页)
                    if GetMerchantNumPages and page > 1 then
                        -- 某些服务器可能没有翻页API,跳过
                        if MerchantNextPageButton then
                            for p = 2, page do
                                MerchantNextPageButton:Click();
                                -- 等待页面加载
                                local waited = 0;
                                while waited < 20 do
                                    if GetMerchantNumItems() > 0 then break; end
                                    waited = waited + 1;
                                end
                            end
                        end
                    end
                    
                    local numItems = GetMerchantNumItems() or 0;
                    print('[WAQ-Private] Page ' .. page .. ': ' .. numItems .. ' items');
                    
                    -- 搜索当前页
                    for i = 1, numItems do
                        local link = GetMerchantItemLink(i);
                        if link then
                            local id = tonumber(link:match('item:(%d+)'));
                            if id == targetId then
                                local name = GetItemInfo(link) or tostring(targetId);
                                DEFAULT_CHAT_FRAME:AddMessage('|cff00ff00[WAQ-Private] Found on page ' .. page .. '! Purchasing: ' .. name .. '|r');
                                print('[WAQ-Private] Purchasing ID ' .. targetId .. ' at page ' .. page .. ', index ' .. i);
                                BuyMerchantItem(i, {quantity});
                                found = true;
                                break;
                            end
                        end
                    end
                end
                
                if not found then
                    DEFAULT_CHAT_FRAME:AddMessage('|cffff0000[WAQ-Private] Item ' .. targetId .. ' not found in any page!|r');
                    print('[WAQ-Private] FAILED to find item ID: ' .. targetId .. ' after searching ' .. numPages .. ' pages');
                end
            ";
            
            Lua.LuaDoString(luaScript);
            Thread.Sleep(500); // 等待购买完成
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
