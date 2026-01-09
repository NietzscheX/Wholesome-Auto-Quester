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
        // === 职业技能训练配置 ===
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
        
        // === 武器训练配置 ===
        public bool EnableWeaponTraining { get; set; }
        public string WeaponTrainerGossipOption { get; set; }
        /// <summary>
        /// 武器训练师 NPC ID（如果为0或未设置，则使用 TrainerNpcEntry）
        /// </summary>
        public int WeaponTrainerNpcEntry { get; set; }
        public Vector3Position WeaponTrainerPosition { get; set; }
        public string WeaponTrainerPositionString { get; set; }
        public int WeaponTrainerMapId { get; set; }
        
        // === 骑术训练配置 ===
        public bool EnableRidingTraining { get; set; }
        public string RidingTrainerGossipOption { get; set; }
        public List<int> RidingTrainAtLevels { get; set; }
        /// <summary>
        /// 骑术训练师 NPC ID（如果为0或未设置，则使用 TrainerNpcEntry）
        /// </summary>
        public int RidingTrainerNpcEntry { get; set; }
        public Vector3Position RidingTrainerPosition { get; set; }
        public string RidingTrainerPositionString { get; set; }
        public int RidingTrainerMapId { get; set; }
        
        // === 双天赋配置 ===
        public bool EnableDualTalent { get; set; }
        public string DualTalentGossipOption { get; set; }
        public int DualTalentMinLevel { get; set; }
        /// <summary>
        /// 双天赋 NPC ID（如果为0或未设置，则使用 TrainerNpcEntry）
        /// </summary>
        public int DualTalentNpcEntry { get; set; }
        public Vector3Position DualTalentPosition { get; set; }
        public string DualTalentPositionString { get; set; }
        public int DualTalentMapId { get; set; }
        
        // 传送配置
        public int HearthstoneEntry { get; set; }
        public int TeleportItemEntry { get; set; }
        public bool UseCustomTeleport { get; set; }
        public List<string> TeleportMenuPath { get; set; }
        
        public TrainingConfig()
        {
            TrainOnEvenLevels = true;
            TrainAtLevels = new List<int>();
            TrainerGossipOption = "职业技能";
            HearthstoneEntry = 6948;
            TeleportItemEntry = 6948;
            UseCustomTeleport = false;
            TeleportMenuPath = new List<string>();
            
            // 武器训练默认值
            EnableWeaponTraining = false;
            WeaponTrainerGossipOption = "武器训练";
            WeaponTrainerNpcEntry = 0;
            WeaponTrainerMapId = 0;
            
            // 骑术训练默认值
            EnableRidingTraining = false;
            RidingTrainerGossipOption = "骑术技能";
            RidingTrainAtLevels = new List<int> { 40, 60 };
            RidingTrainerNpcEntry = 0;
            RidingTrainerMapId = 0;
            
            // 双天赋默认值
            EnableDualTalent = false;
            DualTalentGossipOption = "学双天赋";
            DualTalentMinLevel = 40;
            DualTalentNpcEntry = 0;
            DualTalentMapId = 0;
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
            if (!string.IsNullOrWhiteSpace(WeaponTrainerPositionString))
            {
                WeaponTrainerPosition = Vector3Position.ParseFromString(WeaponTrainerPositionString);
            }
            if (!string.IsNullOrWhiteSpace(RidingTrainerPositionString))
            {
                RidingTrainerPosition = Vector3Position.ParseFromString(RidingTrainerPositionString);
            }
            if (!string.IsNullOrWhiteSpace(DualTalentPositionString))
            {
                DualTalentPosition = Vector3Position.ParseFromString(DualTalentPositionString);
            }
        }
        
        // === Helper 方法：获取指定训练类型的 NPC 信息 ===
        
        public int GetNpcEntry(TrainingType type)
        {
            switch (type)
            {
                case TrainingType.WeaponSkills:
                    return WeaponTrainerNpcEntry > 0 ? WeaponTrainerNpcEntry : TrainerNpcEntry;
                case TrainingType.RidingSkills:
                    return RidingTrainerNpcEntry > 0 ? RidingTrainerNpcEntry : TrainerNpcEntry;
                case TrainingType.DualTalent:
                    return DualTalentNpcEntry > 0 ? DualTalentNpcEntry : TrainerNpcEntry;
                case TrainingType.ClassSkills:
                default:
                    return TrainerNpcEntry;
            }
        }
        
        public Vector3Position GetNpcPosition(TrainingType type)
        {
            switch (type)
            {
                case TrainingType.WeaponSkills:
                    return WeaponTrainerPosition ?? TrainerPosition;
                case TrainingType.RidingSkills:
                    return RidingTrainerPosition ?? TrainerPosition;
                case TrainingType.DualTalent:
                    return DualTalentPosition ?? TrainerPosition;
                case TrainingType.ClassSkills:
                default:
                    return TrainerPosition;
            }
        }
        
        public int GetNpcMapId(TrainingType type)
        {
            switch (type)
            {
                case TrainingType.WeaponSkills:
                    return WeaponTrainerMapId > 0 ? WeaponTrainerMapId : TrainerMapId;
                case TrainingType.RidingSkills:
                    return RidingTrainerMapId > 0 ? RidingTrainerMapId : TrainerMapId;
                case TrainingType.DualTalent:
                    return DualTalentMapId > 0 ? DualTalentMapId : TrainerMapId;
                case TrainingType.ClassSkills:
                default:
                    return TrainerMapId;
            }
        }
        
        public string GetGossipOption(TrainingType type)
        {
            switch (type)
            {
                case TrainingType.WeaponSkills:
                    return WeaponTrainerGossipOption;
                case TrainingType.RidingSkills:
                    return RidingTrainerGossipOption;
                case TrainingType.DualTalent:
                    return DualTalentGossipOption;
                case TrainingType.ClassSkills:
                default:
                    return TrainerGossipOption;
            }
        }
    }
}
