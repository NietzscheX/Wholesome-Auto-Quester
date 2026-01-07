using System;
using System.IO;
using robotManager.Helpful;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Wholesome_Auto_Quester.PrivateServer.Models;

namespace Wholesome_Auto_Quester.PrivateServer.Config
{
    public static class YamlConfigLoader
    {
        public static EquipmentConfig Load(string filePath)
        {
            return Load<EquipmentConfig>(filePath);
        }
        
        public static T Load<T>(string filePath) where T : class
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Logging.WriteError($"[WAQ-Private] YAML config file not found: {filePath}");
                    return null;
                }
                
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(PascalCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();
                    
                string yaml = File.ReadAllText(filePath);
                var config = deserializer.Deserialize<T>(yaml);
                
                // 特殊处理: TeleportConfig 需要解析 PositionString
                if (config is TeleportConfig teleportConfig)
                {
                    if (teleportConfig.TeleportLocations != null)
                    {
                        foreach (var location in teleportConfig.TeleportLocations)
                        {
                            location.AfterDeserialization();
                        }
                    }
                }
                
                // 特殊处理: EquipmentConfig 需要解析 TrainerPosition 和 NPC Position
                if (config is EquipmentConfig equipmentConfig)
                {
                    // 解析训练师位置
                    if (equipmentConfig.Training != null)
                    {
                        equipmentConfig.Training.AfterDeserialization();
                    }
                    
                    // 解析所有 NPC 位置
                    if (equipmentConfig.Sources != null)
                    {
                        foreach (var npcSource in equipmentConfig.Sources.Values)
                        {
                            npcSource.AfterDeserialization();
                        }
                    }
                }
                
                Logging.Write($"[WAQ-Private] YAML config ({typeof(T).Name}) loaded successfully from: {Path.GetFileName(filePath)}");
                
                return config;
            }
            catch (Exception e)
            {
                Logging.WriteError($"[WAQ-Private] Failed to load YAML config: {e.Message}");
                Logging.WriteError($"[WAQ-Private] Stack trace: {e.StackTrace}");
                return null;
            }
        }
    }
}
