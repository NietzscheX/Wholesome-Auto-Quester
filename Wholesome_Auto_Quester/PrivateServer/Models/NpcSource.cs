namespace Wholesome_Auto_Quester.PrivateServer.Models
{
    public class NpcSource
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int MapId { get; set; }
        public Vector3Position Position { get; set; }
        
        /// <summary>
        /// NPC 位置 (Vector3 字符串格式)
        /// 例如: "new Vector3(-8930.02, -141.5619, 82.47603, \"None\")"
        /// </summary>
        public string PositionString { get; set; }
        
        /// <summary>
        /// YAML 加载后的处理
        /// </summary>
        public void AfterDeserialization()
        {
            if (!string.IsNullOrWhiteSpace(PositionString))
            {
                Position = Vector3Position.ParseFromString(PositionString);
            }
        }
    }
}
