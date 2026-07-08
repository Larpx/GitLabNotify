using Dapper;
using Larpx.PersonalTools.GitLabNotify.Models;
using Microsoft.Data.Sqlite;

namespace Larpx.PersonalTools.GitLabNotify.Data
{
    /// <summary>
    /// 基于 SQLite + Dapper 的 Webhook 记录仓储实现
    /// </summary>
    /// <remarks>
    /// 每次操作独立打开连接，依赖 Microsoft.Data.Sqlite 的连接池保证性能。
    /// SQLite 自增主键通过 last_insert_rowid() 回读。
    /// </remarks>
    public class SqliteRecordRepository : IWebhookRecordRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<SqliteRecordRepository> _logger;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="connectionString">SQLite 连接字符串</param>
        /// <param name="logger">日志记录器</param>
        public SqliteRecordRepository(string connectionString, ILogger<SqliteRecordRepository> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<long> InsertAsync(WebhookRecord record)
        {
            using var connection = new SqliteConnection(_connectionString);
            const string sql = @"
            INSERT INTO webhook_records
                (received_at, event_type, payload, target_name, target_type, status, retry_count, error, completed_at)
            VALUES
                (@ReceivedAt, @EventType, @Payload, @TargetName, @TargetType, @Status, @RetryCount, @Error, @CompletedAt);
            SELECT last_insert_rowid();";

            var id = await connection.ExecuteScalarAsync<long>(sql, new
            {
                record.ReceivedAt,
                record.EventType,
                record.Payload,
                record.TargetName,
                record.TargetType,
                Status = (int)record.Status,
                record.RetryCount,
                record.Error,
                record.CompletedAt
            });

            _logger.LogDebug("插入 Webhook 记录，ID={RecordId}，事件={EventType}，目标={TargetName}",
                id, record.EventType, record.TargetName);

            return id;
        }

        /// <inheritdoc/>
        public async Task UpdateStatusAsync(long recordId, WebhookStatus status, int retryCount, string? error, DateTime? completedAt)
        {
            using var connection = new SqliteConnection(_connectionString);
            const string sql = @"
            UPDATE webhook_records
            SET status = @Status,
                retry_count = @RetryCount,
                error = @Error,
                completed_at = @CompletedAt
            WHERE id = @Id";

            await connection.ExecuteAsync(sql, new
            {
                Id = recordId,
                Status = (int)status,
                RetryCount = retryCount,
                Error = error,
                CompletedAt = completedAt
            });

            _logger.LogDebug("更新 Webhook 记录状态，ID={RecordId}，状态={Status}，重试={RetryCount}",
                recordId, status, retryCount);
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<WebhookRecord>> GetRecentAsync(int? count = null)
        {
            using var connection = new SqliteConnection(_connectionString);

            const string baseSql = @"
            SELECT id AS Id,
                   received_at AS ReceivedAt,
                   event_type AS EventType,
                   payload AS Payload,
                   target_name AS TargetName,
                   target_type AS TargetType,
                   status AS Status,
                   retry_count AS RetryCount,
                   error AS Error,
                   completed_at AS CompletedAt
            FROM webhook_records
            ORDER BY received_at DESC, id DESC";

            var sql = count.HasValue ? $"{baseSql} LIMIT @Limit" : baseSql;
            var args = count.HasValue ? (object)new { Limit = count.Value } : new { };

            var rows = await connection.QueryAsync<WebhookRecord>(sql, args);
            return rows;
        }
    }
}
