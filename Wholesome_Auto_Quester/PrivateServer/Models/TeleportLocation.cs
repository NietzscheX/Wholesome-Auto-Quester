using System.Collections.Generic;

namespace Wholesome_Auto_Quester.PrivateServer.Models
{
    /// <summary>
    /// 传送目的地配置
    /// </summary>
    public class TeleportLocation
    {
        /// <summary>
        /// 传送点名称 (如: "暴风城", "奥格瑞玛")
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// 菜单选择路径 (从顶层到目标的完整路径)
        /// 例如: ["主城", "联盟", "暴风城"]
        /// </summary>
        public List<string> MenuPath { get; set; }
        
        /// <summary>
        /// 大陆ID (0=东部王国, 1=卡利姆多, 530=外域, 571=诺森德)
        /// </summary>
        public int Continent { get; set; }
        
        /// <summary>
        /// 传送到达位置 (方式1: 使用结构化配置)
        /// </summary>
        public Vector3Position Position { get; set; }
        
        /// <summary>
        /// 传送到达位置 (方式2: 使用 Vector3 字符串)
        /// 例如: "new Vector3(-4923.17, -956.568, 501.513, \"None\")"
        /// 如果设置了此字段，会自动解析并覆盖 Position
        /// </summary>
        public string PositionString { get; set; }
        
        /// <summary>
        /// 传送点分类 (如: "主城", "副本", "战场")
        /// </summary>
        public string Category { get; set; }
        
        /// <summary>
        /// 阵营限制: "Alliance", "Horde", 或 "Neutral"
        /// </summary>
        public string Faction { get; set; }
        
        public TeleportLocation()
        {
            MenuPath = new List<string>();
            Faction = "Neutral";
            Category = "其他";
        }
        
        /// <summary>
        /// YAML 加载后的处理: 如果提供了 PositionString，则解析并设置 Position
        /// </summary>
        public void AfterDeserialization()
        {
            if (!string.IsNullOrWhiteSpace(PositionString))
            {
                Position = Vector3Position.ParseFromString(PositionString);
            }
        }
    }
    
    /// <summary>
    /// 传送系统设置
    /// </summary>
    public class TeleportSettings
    {
        /// <summary>
        /// 最小传送距离(码), 小于此距离直接跑路
        /// </summary>
        public float MinDistanceForTeleport { get; set; }
        
        /// <summary>
        /// 跨大陆时总是使用传送
        /// </summary>
        public bool CrossContinentAlwaysTeleport { get; set; }
        
        /// <summary>
        /// 传送后允许的最大步行距离(码)
        /// 如果最近的传送点仍需步行超过此距离,则不使用传送
        /// </summary>
        public float MaxWalkDistanceAfterTeleport { get; set; }
        
        /// <summary>
        /// 炉石物品ID (用于打开传送菜单)
        /// </summary>
        public int HearthstoneItemEntry { get; set; }
        
        /// <summary>
        /// 传送冷却时间(秒), 私服通常为0
        /// </summary>
        public int TeleportCooldown { get; set; }
        
        /// <summary>
        /// 是否启用智能传送系统
        /// </summary>
        public bool EnableSmartTeleport { get; set; }
        
        public TeleportSettings()
        {
            MinDistanceForTeleport = 500f;
            CrossContinentAlwaysTeleport = true;
            MaxWalkDistanceAfterTeleport = 300f;
            HearthstoneItemEntry = 6948;
            TeleportCooldown = 0;
            EnableSmartTeleport = true;
        }
    }
    
    /// <summary>
    /// 传送配置 (从 YAML 加载)
    /// </summary>
    public class TeleportConfig
    {
        /// <summary>
        /// 所有可用的传送点列表
        /// </summary>
        public List<TeleportLocation> TeleportLocations { get; set; }
        
        /// <summary>
        /// 传送系统设置
        /// </summary>
        public TeleportSettings TeleportSettings { get; set; }
        
        public TeleportConfig()
        {
            TeleportLocations = new List<TeleportLocation>();
            TeleportSettings = new TeleportSettings();
        }
    }
}
