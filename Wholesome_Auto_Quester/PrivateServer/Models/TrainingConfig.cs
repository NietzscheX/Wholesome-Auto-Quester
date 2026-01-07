using System.Collections.Generic;

namespace Wholesome_Auto_Quester.PrivateServer.Models
{
    public class PluginConfig
    {
        public bool Enabled { get; set; }
        public bool EnableAutoTraining { get; set; }
        public bool EnableAutoEquipment { get; set; }
        
        public PluginConfig()
        {
            Enabled = true;
            EnableAutoTraining = false;
            EnableAutoEquipment = true;
        }
    }
    
    public class TrainingConfig
    {
        public bool TrainOnEvenLevels { get; set; }
        public List<int> TrainAtLevels { get; set; }
        public int TrainerNpcEntry { get; set; }
        public Vector3Position TrainerPosition { get; set; }
        
        /// <summary>
        /// 训练师位置 (Vector3 字符串格式)
        /// 例如: "new Vector3(-8934.85, -146.407, 82.85, \"None\")"
        /// </summary>
        public string TrainerPositionString { get; set; }
        
        public int TrainerMapId { get; set; }
        public string TrainerGossipOption { get; set; }
        
        // 传送配置
        public int HearthstoneEntry { get; set; }
        public int TeleportItemEntry { get; set; }
        public bool UseCustomTeleport { get; set; }
        public List<string> TeleportMenuPath { get; set; }
        
        public TrainingConfig()
        {
            TrainOnEvenLevels = true;
            TrainAtLevels = new List<int>();
            TrainerGossipOption = "1";
            HearthstoneEntry = 6948;
            TeleportItemEntry = 6948;
            UseCustomTeleport = false;
            TeleportMenuPath = new List<string>();
        }
        
        /// <summary>
        /// YAML 加载后的处理
        /// </summary>
        public void AfterDeserialization()
        {
            if (!string.IsNullOrWhiteSpace(TrainerPositionString))
            {
                TrainerPosition = Vector3Position.ParseFromString(TrainerPositionString);
            }
        }
    }
}
