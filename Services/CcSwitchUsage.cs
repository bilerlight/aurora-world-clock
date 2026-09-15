using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Data.Sqlite;

namespace AuroraClock.Services
{
    public sealed record UsageRow(string App, int Requests, long Input, long Output, long CacheRead, decimal Cost);

    /// <summary>
    /// Reads the usage statistics CC Switch collects into ~/.cc-switch/cc-switch.db.
    /// The live per-request table (proxy_request_logs) is used rather than the daily rollups,
    /// because the rollups lag behind (they are only rebuilt periodically).
    /// Everything degrades to "no data" when CC Switch is missing or stopped.
    /// </summary>
    public static class CcSwitchUsage
    {
        public static string DbPath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cc-switch", "cc-switch.db");

        public static bool IsRunning()
        {
            try { return Process.GetProcessesByName("cc-switch").Length > 0; }
            catch { return false; }
        }

        public static bool Available => File.Exists(DbPath);

        private static string ConnectionString => new SqliteConnectionStringBuilder
        {
            DataSource = DbPath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = 3
        }.ToString();

        /// <summary>Usage since a local time, grouped by app.</summary>
        public static List<UsageRow> Since(DateTime fromLocal)
        {
            var list = new List<UsageRow>();
            if (!Available) return list;

            try
            {
                long from = new DateTimeOffset(fromLocal).ToUnixTimeSeconds();

                using var con = new SqliteConnection(ConnectionString);
                con.Open();

                // Preferred: the live request log.
                using (var cmd = con.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT app_type,
                               COUNT(*),
                               COALESCE(SUM(input_tokens), 0),
                               COALESCE(SUM(output_tokens), 0),
                               COALESCE(SUM(cache_read_tokens), 0),
                               COALESCE(SUM(CAST(total_cost_usd AS REAL)), 0)
                        FROM proxy_request_logs
                        WHERE created_at IS NOT NULL AND created_at >= $from
                        GROUP BY app_type
                        ORDER BY 6 DESC";
                    cmd.Parameters.AddWithValue("$from", from);

                    using var rd = cmd.ExecuteReader();
                    while (rd.Read())
                    {
                        list.Add(new UsageRow(
                            rd.IsDBNull(0) ? "其他" : rd.GetString(0),
                            rd.IsDBNull(1) ? 0 : (int)rd.GetInt64(1),
                            rd.IsDBNull(2) ? 0 : rd.GetInt64(2),
                            rd.IsDBNull(3) ? 0 : rd.GetInt64(3),
                            rd.IsDBNull(4) ? 0 : rd.GetInt64(4),
                            rd.IsDBNull(5) ? 0m : (decimal)rd.GetDouble(5)));
                    }
                }

                // Fallback for older databases that only have the daily rollups.
                if (list.Count == 0)
                {
                    using var cmd2 = con.CreateCommand();
                    cmd2.CommandText = @"
                        SELECT app_type,
                               COALESCE(SUM(request_count), 0),
                               COALESCE(SUM(input_tokens), 0),
                               COALESCE(SUM(output_tokens), 0),
                               COALESCE(SUM(cache_read_tokens), 0),
                               COALESCE(SUM(CAST(total_cost_usd AS REAL)), 0)
                        FROM usage_daily_rollups
                        WHERE date = $d
                        GROUP BY app_type
                        ORDER BY 6 DESC";
                    cmd2.Parameters.AddWithValue("$d", fromLocal.ToString("yyyy-MM-dd"));

                    using var rd2 = cmd2.ExecuteReader();
                    while (rd2.Read())
                    {
                        list.Add(new UsageRow(
                            rd2.IsDBNull(0) ? "其他" : rd2.GetString(0),
                            rd2.IsDBNull(1) ? 0 : (int)rd2.GetInt64(1),
                            rd2.IsDBNull(2) ? 0 : rd2.GetInt64(2),
                            rd2.IsDBNull(3) ? 0 : rd2.GetInt64(3),
                            rd2.IsDBNull(4) ? 0 : rd2.GetInt64(4),
                            rd2.IsDBNull(5) ? 0m : (decimal)rd2.GetDouble(5)));
                    }
                }
            }
            catch
            {
                // locked / missing table / schema drift - report nothing rather than crash
            }
            return list;
        }

        public static List<UsageRow> Day(DateTime? day = null) => Since((day ?? DateTime.Now).Date);

        public static List<UsageRow> LastDays(int days) => Since(DateTime.Now.Date.AddDays(-(days - 1)));

        public static List<UsageRow> LastHours(int hours) => Since(DateTime.Now.AddHours(-hours));

        /// <summary>Requests seen in the last <paramref name="minutes"/> minutes (live feel).</summary>
        public static int RecentRequests(int minutes = 5)
        {
            if (!Available) return 0;
            try
            {
                using var con = new SqliteConnection(ConnectionString);
                con.Open();
                using var cmd = con.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM proxy_request_logs WHERE created_at IS NOT NULL AND created_at >= $t";
                cmd.Parameters.AddWithValue("$t", DateTimeOffset.UtcNow.AddMinutes(-minutes).ToUnixTimeSeconds());
                var v = cmd.ExecuteScalar();
                return v == null || v is DBNull ? 0 : Convert.ToInt32(v);
            }
            catch
            {
                return 0;
            }
        }

        public static string FormatTokens(long n)
        {
            if (n >= 1_000_000) return (n / 1_000_000.0).ToString("0.##") + "M";
            if (n >= 1_000) return (n / 1_000.0).ToString("0.#") + "K";
            return n.ToString();
        }
    }
}
