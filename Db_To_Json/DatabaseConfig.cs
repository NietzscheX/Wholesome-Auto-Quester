using MySql.Data.MySqlClient;
using System.Data;
using System.Data.SQLite;

namespace Db_To_Json
{
    public enum DatabaseType
    {
        SQLite,
        MySQL
    }

    internal class DatabaseConfig
    {
        public DatabaseType Type { get; set; }
        public string OutputFileName { get; set; }
        
        // SQLite配置
        public string SQLiteFilePath { get; set; }
        
        // MySQL配置
        public string MySQLHost { get; set; }
        public int MySQLPort { get; set; }
        public string MySQLUser { get; set; }
        public string MySQLPassword { get; set; }
        public string MySQLDatabase { get; set; }

        /// <summary>
        /// 根据数据库类型获取spell表名
        /// MySQL: spell_dbc
        /// SQLite: spell
        /// </summary>
        public string GetSpellTableName()
        {
            return Type == DatabaseType.MySQL ? "spell_dbc" : "spell";
        }

        /// <summary>
        /// 创建数据库连接
        /// </summary>
        public IDbConnection CreateConnection()
        {
            if (Type == DatabaseType.SQLite)
            {
                return new SQLiteConnection("Data Source=" + SQLiteFilePath + ";Cache=Shared;");
            }
            else
            {
                string connectionString = $"Server={MySQLHost};Port={MySQLPort};Database={MySQLDatabase};Uid={MySQLUser};Pwd={MySQLPassword};CharSet=utf8mb4;";
                return new MySqlConnection(connectionString);
            }
        }

        /// <summary>
        /// 默认SQLite英文配置
        /// </summary>
        public static DatabaseConfig DefaultSQLite(string workingDirectory, string pathSep)
        {
            return new DatabaseConfig
            {
                Type = DatabaseType.SQLite,
                OutputFileName = "AQ.json",
                SQLiteFilePath = $"{workingDirectory}{pathSep}WoWDB{pathSep}WoWDb335_ACore;Cache=Shared;"
            };
        }

        /// <summary>
        /// MySQL中文配置
        /// </summary>
        public static DatabaseConfig MySQLChinese(string host, int port, string user, string password, string database)
        {
            return new DatabaseConfig
            {
                Type = DatabaseType.MySQL,
                OutputFileName = "AQ-cn.json",
                MySQLHost = host,
                MySQLPort = port,
                MySQLUser = user,
                MySQLPassword = password,
                MySQLDatabase = database
            };
        }
    }
}
