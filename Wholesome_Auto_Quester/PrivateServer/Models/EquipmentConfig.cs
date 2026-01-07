using System.Collections.Generic;

namespace Wholesome_Auto_Quester.PrivateServer.Models
{
    public class EquipmentConfig
    {
        public PluginConfig Plugin { get; set; }
        public TrainingConfig Training { get; set; }
        public GlobalSettings GlobalSettings { get; set; }
        public Dictionary<string, NpcSource> Sources { get; set; }
        public List<Bundle> Bundles { get; set; }
        public List<ClassProfile> Classes { get; set; }
        
        public EquipmentConfig()
        {
            Plugin = new PluginConfig();
            Training = new TrainingConfig();
            GlobalSettings = new GlobalSettings();
            Sources = new Dictionary<string, NpcSource>();
            Bundles = new List<Bundle>();
            Classes = new List<ClassProfile>();
        }
    }
}
