using System.Collections.Generic;

namespace Wholesome_Auto_Quester.PrivateServer.Models
{
    public class ClassProfile
    {
        public string Name { get; set; }
        public int ClassId { get; set; }
        public InitialSetup InitialSetup { get; set; }
        public Dictionary<string, EquipmentSlot> Slots { get; set; }
        public Dictionary<string, SupplyItem> Supplies { get; set; }
        
        public ClassProfile()
        {
            Slots = new Dictionary<string, EquipmentSlot>();
            Supplies = new Dictionary<string, SupplyItem>();
        }
    }
    
    public class InitialSetup
    {
        public int PriorityBundleId { get; set; }
        public bool AutoEquipOnEmpty { get; set; }
    }
    
    public class EquipmentSlot
    {
        public int ItemId { get; set; }
        public string Strategy { get; set; } // "Replace" 或 "Ignore"
        public int? BundleId { get; set; }
        public string SourceKey { get; set; }
        public List<string> MenuPath { get; set; }
        public bool AllowEmpty { get; set; }
        
        public EquipmentSlot()
        {
            MenuPath = new List<string>();
        }
    }
    
    public class SuppliesConfig
    {
        public SupplyItem Food { get; set; }
        public SupplyItem Bags { get; set; }
    }
    
    public class SupplyItem
    {
        public int ItemId { get; set; }
        public int MinCount { get; set; }
        public int MaxCount { get; set; }
        public string SourceKey { get; set; }
        public List<string> MenuPath { get; set; }
        
        public SupplyItem()
        {
            MenuPath = new List<string>();
        }
    }
}
