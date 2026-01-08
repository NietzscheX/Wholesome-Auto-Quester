using robotManager.Helpful;
using Wholesome_Auto_Quester.Bot.TaskManagement;
using Wholesome_Auto_Quester.Bot.TaskManagement.Tasks;

namespace Wholesome_Auto_Quester.WholesomeToolbox
{
    /// <summary>
    /// WAQ 任务位置提供者实现
    /// </summary>
    public class WAQLocationProvider : IQuestLocationProvider
    {
        private readonly ITaskManager _taskManager;
        
        public WAQLocationProvider(ITaskManager taskManager)
        {
            _taskManager = taskManager;
        }
        
        public bool HasActiveQuestTarget()
        {
            return _taskManager?.ActiveTask != null && _taskManager.ActiveTask.IsValid;
        }
        
        public QuestTarget GetCurrentQuestTarget()
        {
            IWAQTask activeTask = _taskManager?.ActiveTask;
            
            if (activeTask == null || !activeTask.IsValid)
            {
                return null;
            }
            
            // 将 WAQContinent 枚举转换为 WoW ContinentId
            int wowContinentId = ConvertWAQContinentToWoWId(activeTask.WorldMapArea?.Continent);
            
            return new QuestTarget
            {
                Location = activeTask.Location,
                Continent = wowContinentId,
                TargetName = activeTask.TaskName,
                QuestId = 0 // WAQ 任务可能没有 QuestId
            };
        }
        
        /// <summary>
        /// 将 WAQContinent 枚举转换为 WoW 的 ContinentId
        /// </summary>
        private int ConvertWAQContinentToWoWId(WAQContinent? waqContinent)
        {
            if (waqContinent == null) return -1;
            
            switch (waqContinent.Value)
            {
                case WAQContinent.Kalimdor:
                    return 1;           // Kalimdor
                case WAQContinent.EasternKingdoms:
                    return 0;           // Eastern Kingdoms
                case WAQContinent.Outlands:
                    return 530;         // Outland (TBC)
                case WAQContinent.Northrend:
                    return 571;         // Northrend (WotLK)
                case WAQContinent.Teldrassil:
                    return 1;           // Teldrassil is part of Kalimdor
                case WAQContinent.BloodElfStartingZone:
                    return 530;         // Eversong Woods is technically in Outland map
                case WAQContinent.DraeneiStartingZone:
                    return 530;         // Azuremyst Isle is technically in Outland map
                case WAQContinent.DeeprunTram:
                    return 369;         // Deeprun Tram instance
                default:
                    return -1;
            }
        }
    }
}
