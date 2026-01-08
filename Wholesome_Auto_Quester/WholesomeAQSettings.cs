using robotManager.Helpful;
using System;
using System.Collections.Generic;
using System.IO;
using Wholesome_Auto_Quester.Helpers;
using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;

namespace Wholesome_Auto_Quester
{
    [Serializable]
    public class WholesomeAQSettings : Settings
    {
        public static WholesomeAQSettings CurrentSetting { get; set; }

        public int LevelDeltaPlus { get; set; }
        public int LevelDeltaMinus { get; set; }
        public bool LogDebug { get; set; }
        public bool DevMode { get; set; }
        public bool ActivateQuestsGUI { get; set; }
        public List<int> ListCompletedQuests { get; set; }
        public bool RecordUnreachables { get; set; }
        public List<uint> RecordedUnreachables { get; set; }
        public bool SmoothMove { get; set; }
        public double LastUpdateDate { get; set; }
        public bool GrindOnly { get; set; }
        public double QuestTrackerPositionLeft { get; set; }
        public double QuestTrackerPositionTop { get; set; }
        public bool ContinentTravel { get; set; }
        public List<BlackListedQuest> BlackListedQuests { get; set; }
        public bool AbandonUnfitQuests { get; set; }
        public int StopAtLevel { get; set; }
        public bool BlacklistDangerousZones { get; set; }
        public bool AllowStopWatch { get; set; }
        public bool TurboLoot { get; set; }
        
        // ========== Private Server Features ==========
        /// <summary>
        /// 启用智能传送功能（使用炉石/传送物品进行长距离传送）
        /// </summary>
        public bool EnableSmartTeleport { get; set; }
        
        /// <summary>
        /// 启用自动装备功能（自动购买和装备初始装备）
        /// </summary>
        public bool EnableStarterEquipment { get; set; }
        
        /// <summary>
        /// 启用自动训练功能（自动前往训练师学习技能）
        /// </summary>
        public bool EnableAutoTraining { get; set; }
        
        /// <summary>
        /// 传送物品 Entry ID（默认为炉石 6948）
        /// </summary>
        public int TeleportItemEntry { get; set; }
        
        /// <summary>
        /// 触发传送的最小距离（码）
        /// </summary>
        public float MinTeleportDistance { get; set; }
        
        /// <summary>
        /// 装备配置文件路径（相对于 WRobot 根目录）
        /// </summary>
        public string EquipmentConfigPath { get; set; }
        
        /// <summary>
        /// 传送位置配置文件路径（相对于 WRobot 根目录）
        /// </summary>
        public string TeleportConfigPath { get; set; }
        
        /// <summary>
        /// 是否启用瞬移功能（同大陆直接瞬移，跨大陆先传送再瞬移）
        /// </summary>
        public bool Fly { get; set; }
        
        /// <summary>
        /// 瞬移最小距离阈值（码），距离超过此值才会触发瞬移
        /// </summary>
        public float FlyMinDistance { get; set; }

        public WholesomeAQSettings()
        {
            LogDebug = false;
            ActivateQuestsGUI = true;
            DevMode = false;
            ListCompletedQuests = new List<int>();
            RecordedUnreachables = new List<uint>();
            LevelDeltaPlus = 0;
            LevelDeltaMinus = 5;
            SmoothMove = false;
            LastUpdateDate = 0;
            GrindOnly = false;
            ContinentTravel = true;
            BlackListedQuests = new List<BlackListedQuest>();
            AbandonUnfitQuests = true;
            RecordUnreachables = false;
            StopAtLevel = 80;
            BlacklistDangerousZones = true;
            TurboLoot = true;

            AllowStopWatch = false;
            
            // Private Server Feature Defaults
            EnableSmartTeleport = false;
            EnableStarterEquipment = false;
            EnableAutoTraining = false;
            TeleportItemEntry = 6948; // Default to Hearthstone
            MinTeleportDistance = 500f;
            EquipmentConfigPath = @"Data\equipment.yml";
            TeleportConfigPath = @"Data\teleport_locations.yml";
            Fly = false; // 默认关闭瞬移
            FlyMinDistance = 200f; // 默认瞬移阈值 200 码
        }

        public static void RecordGuidAsUnreachable(uint guid)
        {
            if (CurrentSetting.RecordUnreachables && !CurrentSetting.RecordedUnreachables.Contains(guid))
            {
                CurrentSetting.RecordedUnreachables.Add(guid);
                CurrentSetting.Save();
                Logger.Log($"Recorded {guid} as unreachable");
            }
        }

        public bool Save()
        {
            try
            {
                return Save(AdviserFilePathAndName("WholesomeAQSettings",
                    ObjectManager.Me.Name + "." + Usefuls.RealmName));
            }
            catch (Exception e)
            {
                Logging.WriteError("WholesomeAQSettings > Save(): " + e);
                return false;
            }
        }

        public static bool Load()
        {
            try
            {
                if (File.Exists(AdviserFilePathAndName("WholesomeAQSettings",
                    ObjectManager.Me.Name + "." + Usefuls.RealmName)))
                {
                    CurrentSetting = Load<WholesomeAQSettings>(
                        AdviserFilePathAndName("WholesomeAQSettings",
                        ObjectManager.Me.Name + "." + Usefuls.RealmName));
                    return true;
                }
                CurrentSetting = new WholesomeAQSettings();
            }
            catch (Exception e)
            {
                Logging.WriteError("WholesomeAQSettings > Load(): " + e);
            }
            return false;
        }
    }
}

public struct BlackListedQuest
{
    public int Id;
    public string Reason;

    public BlackListedQuest(int id, string reason)
    {
        Id = id;
        Reason = reason;
    }
}