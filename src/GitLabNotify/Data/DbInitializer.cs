using Microsoft.Data.Sqlite;

namespace Larpx.PersonalTools.GitLabNotify.Data
{
    /// <summary>
    /// 数据库初始化器
    /// </summary>
    /// <remarks>
    /// 负责创建 SQLite 表结构和索引，在应用启动时调用一次。
    /// 使用 IF NOT EXISTS 保证幂等，可安全重复执行。
    /// </remarks>
    public static class DbInitializer
    {
        /// <summary>
        /// 初始化数据库（创建表和索引）
        /// </summary>
        /// <param name="connectionString">SQLite 连接字符串</param>
        public static void Initialize(string connectionString)
        {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            const string createTableSql = @"
            CREATE TABLE IF NOT EXISTS webhook_records (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                received_at TEXT NOT NULL,
                event_type TEXT NOT NULL,
                payload TEXT NOT NULL,
                target_name TEXT NOT NULL,
                target_type TEXT NOT NULL,
                status INTEGER NOT NULL DEFAULT 0,
                retry_count INTEGER NOT NULL DEFAULT 0,
                error TEXT,
                completed_at TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_webhook_records_status ON webhook_records(status);
            CREATE INDEX IF NOT EXISTS idx_webhook_records_event_type ON webhook_records(event_type);
            CREATE INDEX IF NOT EXISTS idx_webhook_records_received_at ON webhook_records(received_at);";

            using var command = new SqliteCommand(createTableSql, connection);
            command.ExecuteNonQuery();
        }
    }
}
