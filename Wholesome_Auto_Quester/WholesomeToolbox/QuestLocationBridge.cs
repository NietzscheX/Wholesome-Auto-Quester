using robotManager.Helpful;

namespace Wholesome_Auto_Quester.WholesomeToolbox
{
    /// <summary>
    /// 任务位置目标
    /// </summary>
    public class QuestTarget
    {
        public Vector3 Location { get; set; }
        public int Continent { get; set; }
        public string TargetName { get; set; }
        public int QuestId { get; set; }
        public bool IsValid => Location != null && Location != Vector3.Empty;
        
        public QuestTarget()
        {
            Location = Vector3.Empty;
            Continent = -1;
            TargetName = "";
            QuestId = 0;
        }
    }
    
    /// <summary>
    /// 任务位置提供者接口
    /// </summary>
    public interface IQuestLocationProvider
    {
        bool HasActiveQuestTarget();
        QuestTarget GetCurrentQuestTarget();
    }
    
    /// <summary>
    /// 任务位置桥接器 - 允许外部模块获取当前任务目标位置
    /// </summary>
    public static class QuestLocationBridge
    {
        private static IQuestLocationProvider _provider;
        
        /// <summary>
        /// 注册任务位置提供者
        /// </summary>
        public static void RegisterProvider(IQuestLocationProvider provider)
        {
            _provider = provider;
            Logging.Write("[WAQ-Bridge] Quest location provider registered");
        }
        
        /// <summary>
        /// 取消注册
        /// </summary>
        public static void UnregisterProvider()
        {
            _provider = null;
            Logging.Write("[WAQ-Bridge] Quest location provider unregistered");
        }
        
        /// <summary>
        /// 检查提供者是否可用
        /// </summary>
        public static bool IsProviderAvailable()
        {
            return _provider != null;
        }
        
        /// <summary>
        /// 获取当前提供者
        /// </summary>
        public static IQuestLocationProvider GetProvider()
        {
            return _provider;
        }
    }
}
