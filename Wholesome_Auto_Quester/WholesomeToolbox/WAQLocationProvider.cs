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
            
            return new QuestTarget
            {
                Location = activeTask.Location,
                Continent = activeTask.WorldMapArea?.Continent ?? -1,
                TargetName = activeTask.TaskName,
                QuestId = 0 // WAQ 任务可能没有 QuestId
            };
        }
    }
}
