using System.Collections.Generic;

namespace Wholesome_Auto_Quester.PrivateServer.Models
{
    public class Bundle
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string SourceKey { get; set; }
        public List<string> MenuPath { get; set; }
        public List<int> ContainsItems { get; set; }
        
        public Bundle()
        {
            MenuPath = new List<string>();
            ContainsItems = new List<int>();
        }
    }
}
