using Microsoft.Data.Sqlite;
using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace BliMonitorTest.util.MonitoringDb
{
    internal static class MonitoringDb
    {
        /// <summary>
        /// db생성 및 테이블 생성 보장
        /// </summary>
        public static void EnsureDb(ref SqliteConnection db, string dbPath, ref bool dbReady)
        {
            if (dbReady && db != null)
            {
                return;
            }

            db = new SqliteConnection(string.Format("Data Source={0}", dbPath));
            db.Open();

            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS receive_data (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        created_at TEXT,
                        mode INTEGER,
                        remain_time INTEGER,
                        heater_temp REAL,
                        heater_off_time REAL,
                        air_temp REAL,
                        fan_speed INTEGER,
                        avg_heater_off_time REAL,
                        hot_air_temp REAL,
                        hot_air_ontime REAL,
                        motor INTEGER,
                        motor_current REAL,
                        number INTEGER,
                        off_sum REAL,
                        off_avg REAL,
                        air_sum REAL,
                        air_avg REAL
                    );

                    CREATE INDEX IF NOT EXISTS idx_receive_data_created_at ON receive_data(created_at);
                    ";
                cmd.ExecuteNonQuery();
            }

            dbReady = true;
        }

        /// <summary>
        /// 테이블 데이터 삽입
        /// </summary>
        public static void InsertDb(
            ref SqliteConnection db,
            string dbPath,
            ref bool dbReady,
            bool isNewVersion,
            BliMonitorTest.data.ReadData data,
            int number,
            float off_sum,
            int air_sum
        )
        {
            EnsureDb(ref db, dbPath, ref dbReady);

            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT INTO receive_data (
                        created_at, mode, remain_time, heater_temp, heater_off_time,
                        air_temp, fan_speed, avg_heater_off_time, hot_air_temp, hot_air_ontime,
                        motor, motor_current, number, off_sum, off_avg, air_sum, air_avg
                    ) VALUES (
                        $created_at, $mode, $remain_time, $heater_temp, $heater_off_time,
                        $air_temp, $fan_speed, $avg_heater_off_time, $hot_air_temp, $hot_air_ontime,
                        $motor, $motor_current, $number, $off_sum, $off_avg, $air_sum, $air_avg
                    );
                    ";

                cmd.Parameters.AddWithValue("$created_at", data.date);
                cmd.Parameters.AddWithValue("$mode", data.mode);
                cmd.Parameters.AddWithValue("$remain_time", data.remain_time);
                cmd.Parameters.AddWithValue("$heater_temp", data.heater_temp);
                cmd.Parameters.AddWithValue("$heater_off_time", data.heater_off_time);
                cmd.Parameters.AddWithValue("$air_temp", data.air_temp);
                cmd.Parameters.AddWithValue("$fan_speed", data.fan_speed);

                // 신버전만 값, 아니면 NULL (기존 로직 유지 / ?: 제거해서 if로만)
                object avgOff;
                if (isNewVersion)
                {
                    avgOff = (off_sum / (double)Math.Max(1, number));
                }
                else
                {
                    avgOff = DBNull.Value;
                }
                cmd.Parameters.AddWithValue("$avg_heater_off_time", avgOff);

                cmd.Parameters.AddWithValue("$hot_air_temp", data.hot_air_temp);
                cmd.Parameters.AddWithValue("$hot_air_ontime", data.hot_air_ontime);

                cmd.Parameters.AddWithValue("$motor", data.motor);
                cmd.Parameters.AddWithValue("$motor_current", data.motor_current);

                cmd.Parameters.AddWithValue("$number", number);
                cmd.Parameters.AddWithValue("$off_sum", off_sum);
                cmd.Parameters.AddWithValue("$off_avg", (double)(off_sum / (double)Math.Max(1, number)));
                cmd.Parameters.AddWithValue("$air_sum", air_sum);
                cmd.Parameters.AddWithValue("$air_avg", (double)(air_sum / (double)Math.Max(1, number)));

                cmd.ExecuteNonQuery();
            }
        }

        public static string ToSqlLiteral(object value)
        {
            if (value == null || value == DBNull.Value) return "NULL";

            // 문자열
            var s = value as string;
            if (s != null)
                return "'" + s.Replace("'", "''") + "'";

            // char
            if (value is char)
            {
                var c = (char)value;
                return "'" + (c == '\'' ? "''" : c.ToString()) + "'";
            }

            // bool (SQLite는 보통 0/1로 표현)
            if (value is bool)
                return ((bool)value) ? "1" : "0";

            // 정수 계열
            if (value is byte || value is sbyte ||
                value is short || value is ushort ||
                value is int || value is uint ||
                value is long || value is ulong)
                return Convert.ToString(value, CultureInfo.InvariantCulture);

            // 실수/decimal
            if (value is float || value is double || value is decimal)
                return Convert.ToString(value, CultureInfo.InvariantCulture);

            // DateTime
            if (value is DateTime)
            {
                var dt = (DateTime)value;
                // 현재 코드가 created_at TEXT 비교(ISO 문자열)이므로 이 포맷이 가장 안전
                return "'" + dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "'";
            }

            // DateTimeOffset
            if (value is DateTimeOffset)
            {
                var dto = (DateTimeOffset)value;
                return "'" + dto.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture) + "'";
            }

            // byte[] (BLOB)
            var bytes = value as byte[];
            if (bytes != null)
                return "X'" + BitConverter.ToString(bytes).Replace("-", "") + "'";

            // 기타: 문자열로 변환 후 따옴표 처리
            return "'" + Convert.ToString(value, CultureInfo.InvariantCulture).Replace("'", "''") + "'";
        }

        public static string RenderFinalSqlForLog(Microsoft.Data.Sqlite.SqliteCommand cmd)
        {
            // 이름 긴 것부터 치환 (@id, @id2 충돌 방지)
            var parameters = cmd.Parameters
                .Cast<Microsoft.Data.Sqlite.SqliteParameter>()
                .OrderByDescending(p => p.ParameterName == null ? 0 : p.ParameterName.Length)
                .ToList();

            string sql = cmd.CommandText ?? "";

            for (int i = 0; i < parameters.Count; i++)
            {
                var p = parameters[i];
                var name = p.ParameterName;

                if (string.IsNullOrWhiteSpace(name))
                    continue;

                sql = sql.Replace(name, ToSqlLiteral(p.Value));
            }

            return sql;
        }

        public static string FormatSqlLog(Microsoft.Data.Sqlite.SqliteCommand cmd, string title)
        {
            var sb = new StringBuilder();

            sb.AppendLine("================================================================================");
            sb.AppendLine(string.Format("[{0}]  at {1:yyyy-MM-dd HH:mm:ss.fff}", title ?? "SQL", DateTime.Now));
            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine("[CommandText]");
            sb.AppendLine((cmd.CommandText ?? "").TrimEnd());
            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine("[Params]");

            if (cmd.Parameters.Count == 0)
            {
                sb.AppendLine("(none)");
            }
            else
            {
                foreach (Microsoft.Data.Sqlite.SqliteParameter p in cmd.Parameters)
                {
                    object v = p.Value;
                    string raw = (v == null || v == DBNull.Value) ? "NULL" : v.ToString();
                    string lit = ToSqlLiteral(v);

                    sb.AppendLine(string.Format(
                        "- {0} = {1}  | literal={2}  | DbType={3}",
                        p.ParameterName, raw, lit, p.DbType));
                }
            }

            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine("[FinalSqlForLog]  (rendered from CommandText + Params)");
            sb.AppendLine(RenderFinalSqlForLog(cmd));
            sb.AppendLine("================================================================================");

            return sb.ToString();
        }

        public static string DumpCommand(SqliteCommand cmd)
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("---- SQL ----");
            sb.AppendLine(cmd.CommandText);

            sb.AppendLine("---- PARAMS ----");
            foreach (SqliteParameter p in cmd.Parameters)
            {
                // Value가 null/DBNull이면 깔끔하게 표시
                object v = p.Value;
                string vs = (v == null || v == DBNull.Value) ? "NULL" : v.ToString();

                sb.AppendLine($"{p.ParameterName} = {vs} (DbType={p.DbType})");
            }

            return sb.ToString();
        }

        public static string DumpParamsOneLine(SqliteCommand cmd)
        {
            var parts = new System.Collections.Generic.List<string>();
            foreach (SqliteParameter p in cmd.Parameters)
            {
                object v = p.Value;
                string vs = (v == null || v == DBNull.Value) ? "NULL" : v.ToString();
                parts.Add($"{p.ParameterName}={vs}");
            }
            return string.Join(", ", parts);
        }

    }
}
