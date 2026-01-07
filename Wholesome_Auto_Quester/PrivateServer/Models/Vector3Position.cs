using System;
using System.Text.RegularExpressions;

namespace Wholesome_Auto_Quester.PrivateServer.Models
{
    public class Vector3Position
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public string Type { get; set; }
        
        public Vector3Position()
        {
            Type = "None";
        }
        
        /// <summary>
        /// 从 WRobot Vector3 构造函数字符串解析坐标
        /// 支持格式: new Vector3(-4923.17, -956.568, 501.513, "None")
        /// 或者: new Vector3(-4923.17, -956.568, 501.513)
        /// </summary>
        public static Vector3Position ParseFromString(string vectorString)
        {
            if (string.IsNullOrWhiteSpace(vectorString))
                return null;
                
            // 匹配 new Vector3(x, y, z, "type") 或 new Vector3(x, y, z)
            var match = Regex.Match(vectorString, 
                @"new\s+Vector3\s*\(\s*(-?\d+\.?\d*)\s*,\s*(-?\d+\.?\d*)\s*,\s*(-?\d+\.?\d*)(?:\s*,\s*""([^""]*)"")?",
                RegexOptions.IgnoreCase);
                
            if (match.Success)
            {
                return new Vector3Position
                {
                    X = float.Parse(match.Groups[1].Value),
                    Y = float.Parse(match.Groups[2].Value),
                    Z = float.Parse(match.Groups[3].Value),
                    Type = match.Groups[4].Success ? match.Groups[4].Value : "None"
                };
            }
            
            throw new FormatException($"无法解析 Vector3 字符串: {vectorString}");
        }
    }
}
