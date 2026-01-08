using robotManager.Helpful;
using System.Linq;
using wManager.Wow.Bot.Tasks;
using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;
using Wholesome_Auto_Quester.PrivateServer.Models;

namespace Wholesome_Auto_Quester.PrivateServer.Helpers
{
    public static class TeleportHelper
    {
        public static WoWItem GetHearthstone(TrainingConfig config)
        {
            return Bag.GetBagItem().FirstOrDefault(item => item.Entry == config.HearthstoneEntry);
        }
        
        public static bool TeleportTo(float x, float y, float z, int mapId, TrainingConfig config)
        {
            if (x == 0 && y == 0)
            {
                Logging.WriteError("[WAQ-Private] TeleportTo received (0,0,0) coordinates, aborting teleport.");
                return false;
            }
            if (config.UseCustomTeleport && config.TeleportItemEntry > 0)
            {
                // 使用自定义传送宝石
                var teleportItem = Bag.GetBagItem().FirstOrDefault(i => i.Entry == config.TeleportItemEntry);
                if (teleportItem != null)
                {
                    Logging.Write($"[WAQ-Private] Using custom teleport item to {x},{y},{z} on map {mapId}");
                    // 自定义传送逻辑 - 需要根据私服调整
                    Lua.LuaDoString($@"
                        -- 使用传送宝石的自定义Lua命令
                        -- 示例: TeleportToCoords({mapId}, {x}, {y}, {z})
                    ");
                    System.Threading.Thread.Sleep(5000);
                    return true;
                }
                else
                {
                    Logging.WriteError($"[WAQ-Private] Teleport item {config.TeleportItemEntry} not found in bags");
                }
            }
            
            // 回退：使用寻路
            Logging.Write($"[WAQ-Private] Using pathfinding to return to position");
            GoToTask.ToPosition(new robotManager.Helpful.Vector3(x, y, z), 10f);
            return true;
        }
    }
}
