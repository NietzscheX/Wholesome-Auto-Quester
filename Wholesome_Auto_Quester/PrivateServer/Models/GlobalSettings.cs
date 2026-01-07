using System.Collections.Generic;

namespace Wholesome_Auto_Quester.PrivateServer.Models
{
    public class GlobalSettings
    {
        public int DurabilityThreshold { get; set; }
        public bool AutoDestroyOld { get; set; }
        public bool StopIfNotFound { get; set; }
        public int CheckIntervalMs { get; set; }
        
        // 背包管理
        public List<int> ProtectedItems { get; set; }
        
        public GlobalSettings()
        {
            DurabilityThreshold = 10;
            AutoDestroyOld = true;
            StopIfNotFound = false;
            CheckIntervalMs = 30000;
            ProtectedItems = new List<int>();
        }
    }
}
