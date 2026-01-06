using Db_To_Json.AutoQuester;
using System;
using System.Data.SQLite;
using System.IO;

namespace Db_To_Json
{
    internal class JSONGenerator
    {
        public static readonly char PathSep = Path.DirectorySeparatorChar;
        public static readonly string WorkingDirectory = Directory.GetParent(Environment.CurrentDirectory).Parent.FullName;
        public static readonly string OutputPath = WorkingDirectory + Path.DirectorySeparatorChar + "Output";
        private static readonly string DBName = "WoWDb335_ACore";

        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("===== WAQ Database to JSON Generator =====");
                Console.WriteLine("Chooseファイルatabase type:");
                Console.WriteLine("1. SQLite (English - AQ.json)");
                Console.WriteLine("2. MySQL (Chinese - AQ-cn.json)");
                Console.Write("Enter your choice (1 or 2): ");
                
                string choice = Console.ReadLine();
                DatabaseConfig config = null;

                if (choice == "1")
                {
                    // SQLite 英文版本
                    Console.WriteLine("\n[SQLite Mode] Generating English version (AQ.json)...");
                    string dbPath = $"{WorkingDirectory}{PathSep}WoWDB{PathSep}{DBName}";
                    
                    if (!File.Exists(dbPath))
                    {
                        Console.WriteLine($"ERROR: Database file not found: {dbPath}");
                        Console.WriteLine("Please place your SQLite database in the WoWDB folder.");
                    }
                    else
                    {
                        config = DatabaseConfig.DefaultSQLite(WorkingDirectory, PathSep.ToString());
                    }
                }
                else if (choice == "2")
                {
                    // MySQL 中文版本
                    Console.WriteLine("\n[MySQL Mode] Generating Chinese version (AQ-cn.json)...");
                    Console.Write("MySQL Host (default: 192.168.1.2): ");
                    string host = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(host)) host = "192.168.1.2";

                    Console.Write("MySQL Port (default: 3306): ");
                    string portStr = Console.ReadLine();
                    int port = string.IsNullOrWhiteSpace(portStr) ? 3306 : int.Parse(portStr);

                    Console.Write("MySQL User (default: root): ");
                    string user = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(user)) user = "root";

                    Console.Write("MySQL Password: ");
                    string password = Console.ReadLine();

                    Console.Write("MySQL Database (default: acore_world): ");
                    string database = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(database)) database = "acore_world";

                    config = DatabaseConfig.MySQLChinese(host, port, user, password, database);
                }
                else
                {
                    Console.WriteLine("Invalid choice. Exiting...");
                    Console.Read();
                    return;
                }

                if (config != null)
                {
                    using (var con = config.CreateConnection())
                    {
                        con.Open();
                        Console.WriteLine($"Connected to {config.Type} database successfully!");
                        
                        // Auto quester JSON
                        AutoQuesterGeneration.Generate(con, config);
                        
                        Console.WriteLine("\n===== Generation Complete =====");
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"\nERROR: {e.Message}");
                Console.WriteLine($"Stack Trace: {e.StackTrace}");
            }
            
            Console.WriteLine("\nPress any key to exit...");
            Console.Read();
        }
    }
}

public enum DBType
{
    TRINITY,
    AZEROTH_CORE
}
