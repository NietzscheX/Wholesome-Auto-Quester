using robotManager.FiniteStateMachine;
using robotManager.Helpful;
using System;
using System.IO;
using Wholesome_Auto_Quester.PrivateServer.Config;
using Wholesome_Auto_Quester.PrivateServer.Managers;
using Wholesome_Auto_Quester.PrivateServer.Models;
using Wholesome_Auto_Quester.PrivateServer.States.Equipment;
using Wholesome_Auto_Quester.PrivateServer.States.Training;
using Wholesome_Auto_Quester.PrivateServer.States.Travel;
using wManager.Wow.Helpers;

namespace Wholesome_Auto_Quester.PrivateServer
{
    /// <summary>
    /// 私服功能集成管理器
    /// 负责初始化和管理所有私服相关功能（传送、装备、训练）
    /// </summary>
    public class PrivateServerManager
    {
        private static PrivateServerManager _instance;
        public static PrivateServerManager Instance => _instance ?? (_instance = new PrivateServerManager());
        
        // 管理器
        private TeleportManager _teleportManager;
        private EquipmentManager _equipmentManager;
        private TrainingManager _trainingManager;
        
        // 配置
        private TeleportConfig _teleportConfig;
        private EquipmentConfig _equipmentConfig;
        
        // 状态
        public bool IsInitialized { get; private set; }
        public TeleportManager TeleportManager => _teleportManager;
        public EquipmentManager EquipmentManager => _equipmentManager;
        public TrainingManager TrainingManager => _trainingManager;
        
        private PrivateServerManager()
        {
            IsInitialized = false;
        }
        
        /// <summary>
        /// 初始化私服功能
        /// </summary>
        public void Initialize()
        {
            try
            {
                var settings = WholesomeAQSettings.CurrentSetting;
                
                // 检查是否启用了任何私服功能
                if (!settings.EnableSmartTeleport && !settings.EnableStarterEquipment && 
                    !settings.EnableAutoTraining && !settings.Fly)
                {
                    Logging.Write("[WAQ-Private] No private server features enabled");
                    return;
                }
                
                Logging.Write("[WAQ-Private] ========================================");
                Logging.Write("[WAQ-Private] Initializing Private Server Features");
                Logging.Write("[WAQ-Private] ========================================");
                Logging.Write($"[WAQ-Private] Fly (瞬移) Enabled: {settings.Fly}");
                
                string wrobotRoot = Others.GetCurrentDirectory;
                
                // 加载传送配置（当启用 Smart Teleport 或 Fly 时）
                if (settings.EnableSmartTeleport || settings.Fly)
                {
                    string teleportPath = Path.Combine(wrobotRoot, settings.TeleportConfigPath);
                    if (File.Exists(teleportPath))
                    {
                        _teleportConfig = YamlConfigLoader.Load<TeleportConfig>(teleportPath);
                        if (_teleportConfig != null)
                        {
                            // 更新传送设置
                            _teleportConfig.TeleportSettings.HearthstoneItemEntry = settings.TeleportItemEntry;
                            _teleportConfig.TeleportSettings.MinDistanceForTeleport = settings.MinTeleportDistance;
                            _teleportConfig.TeleportSettings.EnableSmartTeleport = settings.EnableSmartTeleport;
                            
                            _teleportManager = new TeleportManager(_teleportConfig);
                            Logging.Write($"[WAQ-Private] ✓ Smart Teleport initialized ({_teleportConfig.TeleportLocations?.Count ?? 0} locations)");
                        }
                    }
                    else
                    {
                        Logging.WriteError($"[WAQ-Private] Teleport config not found: {teleportPath}");
                        Logging.Write("[WAQ-Private] Please copy teleport_locations.yml to the Data folder");
                    }
                }
                
                // 加载装备配置
                if (settings.EnableStarterEquipment || settings.EnableAutoTraining)
                {
                    string equipmentPath = Path.Combine(wrobotRoot, settings.EquipmentConfigPath);
                    if (File.Exists(equipmentPath))
                    {
                        _equipmentConfig = YamlConfigLoader.Load(equipmentPath);
                        if (_equipmentConfig != null)
                        {
                            // 初始化装备管理器（传入 TeleportManager 以支持返回传送）
                            if (settings.EnableStarterEquipment)
                            {
                                _equipmentManager = new EquipmentManager();
                                _equipmentManager.Initialize(equipmentPath, _teleportManager);
                                Logging.Write("[WAQ-Private] ✓ Starter Equipment initialized");
                            }
                            
                            // 初始化训练管理器
                            if (settings.EnableAutoTraining && _equipmentConfig.Training != null)
                            {
                                _trainingManager = new TrainingManager(_equipmentConfig.Training);
                                Logging.Write("[WAQ-Private] ✓ Auto Training initialized");
                            }
                        }
                    }
                    else
                    {
                        Logging.WriteError($"[WAQ-Private] Equipment config not found: {equipmentPath}");
                        Logging.Write("[WAQ-Private] Please copy equipment.yml to the Data folder");
                    }
                }
                
                IsInitialized = true;
                Logging.Write("[WAQ-Private] ========================================");
                Logging.Write("[WAQ-Private] Private Server Features Ready");
                Logging.Write("[WAQ-Private] ========================================");
            }
            catch (Exception ex)
            {
                Logging.WriteError($"[WAQ-Private] Initialization failed: {ex.Message}");
                Logging.WriteError($"[WAQ-Private] Stack: {ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// 向 FSM 注册私服状态
        /// </summary>
        /// <param name="engine">FSM 引擎</param>
        /// <param name="basePriority">基础优先级（状态将使用此优先级及以上）</param>
        public void RegisterStates(Engine engine, int basePriority = 12)
        {
            if (!IsInitialized) return;
            
            var settings = WholesomeAQSettings.CurrentSetting;
            int priority = basePriority;
            
            try
            {
                // 注册智能传送状态（当启用 Smart Teleport 或 Fly 时）
                if ((settings.EnableSmartTeleport || settings.Fly) && _teleportManager != null)
                {
                    var smartTravelState = new SmartTravelState(_teleportManager);
                    smartTravelState.Priority = priority++;
                    engine.AddState(smartTravelState);
                    string mode = settings.Fly ? "Fly Mode" : "Teleport Mode";
                    Logging.Write($"[WAQ-Private] Registered: SmartTravelState ({mode}, Priority: {smartTravelState.Priority})");
                }
                else if (settings.Fly)
                {
                    // Fly 启用但没有 TeleportManager，创建一个不带传送功能的状态
                    var smartTravelState = new SmartTravelState(null);
                    smartTravelState.Priority = priority++;
                    engine.AddState(smartTravelState);
                    Logging.Write($"[WAQ-Private] Registered: SmartTravelState (Fly Only, Priority: {smartTravelState.Priority})");
                }
                
                // 注册装备状态
                if (settings.EnableStarterEquipment && _equipmentManager != null)
                {
                    var cleanBagsState = new CleanBagsState(_equipmentManager, _teleportManager);
                    cleanBagsState.Priority = priority++;
                    engine.AddState(cleanBagsState);
                    
                    var purchaseState = new PurchaseEquipmentState(_equipmentManager, _equipmentConfig);
                    purchaseState.Priority = priority++;
                    engine.AddState(purchaseState);
                    
                    var equipState = new EquipItemsState(_equipmentManager);
                    equipState.Priority = priority++;
                    engine.AddState(equipState);
                    
                    // 注册传送返回状态
                    var teleportBackEquipState = new TeleportBackFromEquipmentState(
                        _equipmentManager, _equipmentConfig, _teleportManager);
                    teleportBackEquipState.Priority = priority++;
                    engine.AddState(teleportBackEquipState);
                    
                    Logging.Write($"[WAQ-Private] Registered: Equipment States (Priority: {cleanBagsState.Priority}-{teleportBackEquipState.Priority})");
                }
                
                // 注册训练状态
                if (settings.EnableAutoTraining && _trainingManager != null && _equipmentConfig?.Training != null)
                {
                    var idleTrainingState = new IdleTrainingState(_trainingManager, _equipmentConfig.Training);
                    idleTrainingState.Priority = priority++;
                    engine.AddState(idleTrainingState);
                    
                    var hearthstoneState = new HearthstoneState(_trainingManager);
                    hearthstoneState.Priority = priority++;
                    engine.AddState(hearthstoneState);
                    
                    var travelToTrainerState = new TravelToTrainerState(_trainingManager, _equipmentConfig.Training, _teleportManager);
                    travelToTrainerState.Priority = priority++;
                    engine.AddState(travelToTrainerState);
                    
                    var interactState = new InteractWithTrainerState(_trainingManager, _equipmentConfig.Training);
                    interactState.Priority = priority++;
                    engine.AddState(interactState);
                    
                    var teleportBackState = new TeleportBackState(_trainingManager, _equipmentConfig.Training);
                    teleportBackState.Priority = priority++;
                    engine.AddState(teleportBackState);
                    
                    var resumeState = new ResumeProductState(_trainingManager);
                    resumeState.Priority = priority++;
                    engine.AddState(resumeState);
                    
                    Logging.Write($"[WAQ-Private] Registered: Training States (Priority: {idleTrainingState.Priority}-{resumeState.Priority})");
                }
                
                Logging.Write($"[WAQ-Private] All private server states registered");
            }
            catch (Exception ex)
            {
                Logging.WriteError($"[WAQ-Private] State registration failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 清理资源
        /// </summary>
        public void Dispose()
        {
            try
            {
                _equipmentManager?.Dispose();
                _teleportManager = null;
                _equipmentManager = null;
                _trainingManager = null;
                _teleportConfig = null;
                _equipmentConfig = null;
                IsInitialized = false;
                
                Logging.Write("[WAQ-Private] Private server features disposed");
            }
            catch (Exception ex)
            {
                Logging.WriteError($"[WAQ-Private] Dispose error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 重置单例（用于重新初始化）
        /// </summary>
        public static void Reset()
        {
            _instance?.Dispose();
            _instance = null;
        }
    }
}
