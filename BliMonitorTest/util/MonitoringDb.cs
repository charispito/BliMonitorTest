using log4net;
using Microsoft.Data.Sqlite;
using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace BliMonitorTest.util.MonitoringDb
{
    internal static class MonitoringDb
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MonitoringDb));

        // 단/다채널 구분값(조회조건에도 그대로 사용)
        public const int SOURCE_SINGLE = 1;
        public const int SOURCE_MULTI = 2;

        // 테이블명 고정
        private const string TABLE = "receive_data";

        /// <summary>
        /// DB 생성 및 테이블 생성 보장 (DB 삭제/재생성 전제: 마이그레이션 없음)
        /// </summary>
        public static void EnsureDb(ref SqliteConnection db, string dbPath, ref bool dbReady)
        {
            if (dbReady && db != null)
                return;

            db = new SqliteConnection(string.Format("Data Source={0}", dbPath));
            db.Open();

            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS receive_data (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,

                        -- 단/다채널 구분 + 채널
                        source_type    INTEGER NOT NULL,
                        channel_no     INTEGER NOT NULL,

                        -- 시간(문자열 + epoch)
                        created_at     TEXT    NOT NULL,
                        created_at_ms  INTEGER NOT NULL,

                        -- 원본/표시용 + 검색용
                        mode               INTEGER,
                        remain_time_text   TEXT,
                        remain_seconds     INTEGER,

                        heater_temp        REAL,
                        heater_off_time    REAL,
                        air_temp           REAL,
                        fan_speed          INTEGER,

                        avg_heater_off_time REAL,   -- 신버전 의미 유지(기존 로직)
                        hot_air_temp       REAL,
                        hot_air_ontime     REAL,

                        motor_state        TEXT,
                        motor_code         INTEGER,
                        motor_current      REAL,

                        number             INTEGER,
                        off_sum            REAL,
                        off_avg            REAL,
                        air_sum            REAL,
                        air_avg            REAL
                    );

                    CREATE INDEX IF NOT EXISTS idx_receive_data_created_at_ms
                    ON receive_data(created_at_ms);

                    CREATE INDEX IF NOT EXISTS idx_receive_data_source_channel_time
                    ON receive_data(source_type, channel_no, created_at_ms);

                    CREATE TABLE IF NOT EXISTS error_events (
                        id               INTEGER PRIMARY KEY AUTOINCREMENT,
                        source_type      INTEGER NOT NULL,
                        channel_no       INTEGER NOT NULL,
                        created_at       TEXT    NOT NULL,
                        created_at_ms    INTEGER NOT NULL,
                        snapshot_id      TEXT    NOT NULL,
                        file_name        TEXT,
                        error_slot       INTEGER NOT NULL,
                        error_text       TEXT,
                        run_mode         INTEGER,
                        heater_temp      REAL,
                        heater_off_time  REAL,
                        hot_air_temp     REAL,
                        hot_air_on_time  REAL,
                        run_count        INTEGER,
                        exhaust_temp     REAL
                    );

                    CREATE INDEX IF NOT EXISTS idx_error_events_time
                      ON error_events(created_at_ms);

                    CREATE INDEX IF NOT EXISTS idx_error_events_group
                      ON error_events(snapshot_id);

                    CREATE INDEX IF NOT EXISTS idx_error_events_source_channel_time
                      ON error_events(source_type, channel_no, created_at_ms);
                ";
                cmd.ExecuteNonQuery();
            }

            dbReady = true;
        }

        /// <summary>
        /// (권장) 단일/다채널 모두 공용 Insert
        /// - channelNo: 단일은 1 고정, 다채널은 실제 채널
        /// - sourceType: 1=Single, 2=Multi
        /// </summary>
        public static void InsertDb( ref SqliteConnection db, string dbPath, ref bool dbReady, bool isNewVersion, BliMonitorTest.data.ReadData data, int number, float off_sum, int air_sum, int channelNo, int sourceType )
        {
            EnsureDb(ref db, dbPath, ref dbReady);

            if (data == null) return;

            // created_at 표준화(문자열이 이상해도 최대한 맞춰줌)
            DateTime now = DateTime.Now;
            DateTime createdAt = TryParseCreatedAt(data.date, out var parsed) ? parsed : now;
            long createdAtMs = ToUnixMs(createdAt);
            string createdAtIso = createdAt.ToString("yyyy -MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);

            // remain_time 처리: "MM:SS" -> seconds
            string remainText = data.remain_time; // 원본 보관
            int? remainSeconds = TryParseRemainSeconds(remainText, out var sec) ? sec : (int?)null;

            // motor 처리: data.motor가 숫자/문자열 혼재 가능
            string motorState = null;
            int? motorCode = null;

            if (data.motor != null)
            {
                // 1) 숫자 문자열이면 motor_code로 저장
                if (int.TryParse(Convert.ToString(data.motor, CultureInfo.InvariantCulture), out int mcode))
                {
                    motorCode = mcode;
                    motorState = null;
                }
                else
                {
                    // 2) 아니면 motor_state로 저장
                    motorState = Convert.ToString(data.motor, CultureInfo.InvariantCulture);
                    motorCode = null;
                }
            }

            // 신버전 avg_heater_off_time: 기존 로직 유지(원하시면 나중에 의미 재정리)
            object avgOff;
            if (isNewVersion)
                avgOff = (off_sum / (double)Math.Max(1, number));
            else
                avgOff = DBNull.Value;

            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT INTO receive_data (
                        source_type, channel_no,
                        created_at, created_at_ms,
                        mode, remain_time_text, remain_seconds,
                        heater_temp, heater_off_time,
                        air_temp, fan_speed,
                        avg_heater_off_time, hot_air_temp, hot_air_ontime,
                        motor_state, motor_code, motor_current,
                        number, off_sum, off_avg, air_sum, air_avg
                    ) VALUES (
                        $source_type, $channel_no,
                        $created_at, $created_at_ms,
                        $mode, $remain_time_text, $remain_seconds,
                        $heater_temp, $heater_off_time,
                        $air_temp, $fan_speed,
                        $avg_heater_off_time, $hot_air_temp, $hot_air_ontime,
                        $motor_state, $motor_code, $motor_current,
                        $number, $off_sum, $off_avg, $air_sum, $air_avg
                    );
                    ";

                cmd.Parameters.AddWithValue("$source_type", sourceType);
                cmd.Parameters.AddWithValue("$channel_no", channelNo);

                cmd.Parameters.AddWithValue("$created_at", createdAtIso);
                cmd.Parameters.AddWithValue("$created_at_ms", createdAtMs);

                cmd.Parameters.AddWithValue("$mode", data.mode);

                cmd.Parameters.AddWithValue("$remain_time_text", (object)remainText ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$remain_seconds", (object)remainSeconds ?? DBNull.Value);

                cmd.Parameters.AddWithValue("$heater_temp", data.heater_temp);
                cmd.Parameters.AddWithValue("$heater_off_time", data.heater_off_time);
                cmd.Parameters.AddWithValue("$air_temp", data.air_temp);
                cmd.Parameters.AddWithValue("$fan_speed", data.fan_speed);

                cmd.Parameters.AddWithValue("$avg_heater_off_time", avgOff);
                cmd.Parameters.AddWithValue("$hot_air_temp", data.hot_air_temp);
                cmd.Parameters.AddWithValue("$hot_air_ontime", data.hot_air_ontime);

                cmd.Parameters.AddWithValue("$motor_state", (object)motorState ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$motor_code", (object)motorCode ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$motor_current", data.motor_current);

                cmd.Parameters.AddWithValue("$number", number);
                cmd.Parameters.AddWithValue("$off_sum", off_sum);
                cmd.Parameters.AddWithValue("$off_avg", (double)(off_sum / (double)Math.Max(1, number)));
                cmd.Parameters.AddWithValue("$air_sum", air_sum);
                cmd.Parameters.AddWithValue("$air_avg", (double)(air_sum / (double)Math.Max(1, number)));

                try {
                    cmd.ExecuteNonQuery();
                } catch(Exception ex)
                {
                    log.Error("InsertDb 예외 : " + ex.ToString());
                }
                
            }
        }

        // ---------------------------
        // 조회(Query) 유틸 (조회화면에서 사용)
        // ---------------------------

        public sealed class ReceiveDataRow
        {
            public long Id { get; set; }
            public int SourceType { get; set; }
            public int ChannelNo { get; set; }

            public string CreatedAt { get; set; }
            public long CreatedAtMs { get; set; }

            public int Mode { get; set; }
            public string RemainTimeText { get; set; }
            public int? RemainSeconds { get; set; }

            public double HeaterTemp { get; set; }
            public double HeaterOffTime { get; set; }
            public double AirTemp { get; set; }
            public int FanSpeed { get; set; }

            public double? AvgHeaterOffTime { get; set; }
            public double HotAirTemp { get; set; }
            public double HotAirOntime { get; set; }

            public string MotorState { get; set; }
            public int? MotorCode { get; set; }
            public double MotorCurrent { get; set; }

            public int Number { get; set; }
        }

        /// <summary>
        /// 조회 (기간 + 단/다채널 + 채널 필터)
        /// - sourceType: 0=전체, 1=단일, 2=다채널
        /// - channelNo: 0=전체
        /// </summary>
        public static System.Collections.Generic.List<ReceiveDataRow> QueryReceiveData( ref SqliteConnection db, string dbPath, ref bool dbReady, DateTime from, DateTime to, int sourceType, int channelNo, int limit
        )
        {
            EnsureDb(ref db, dbPath, ref dbReady);

            long fromMs = ToUnixMs(from);
            long toMs = ToUnixMs(to);

            if (limit <= 0) limit = 2000;

            var rows = new System.Collections.Generic.List<ReceiveDataRow>();

            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT
                        id,
                        source_type, channel_no,
                        created_at, created_at_ms,
                        mode, remain_time_text, remain_seconds,
                        heater_temp, heater_off_time,
                        air_temp, fan_speed,
                        avg_heater_off_time, hot_air_temp, hot_air_ontime,
                        motor_state, motor_code, motor_current,
                        number
                    FROM receive_data
                    WHERE created_at_ms BETWEEN $fromMs AND $toMs
                      AND ($sourceType = 0 OR source_type = $sourceType)
                      AND ($channelNo  = 0 OR channel_no  = $channelNo)
                    ORDER BY created_at_ms DESC
                    LIMIT $limit;
                    ";
                cmd.Parameters.AddWithValue("$fromMs", fromMs);
                cmd.Parameters.AddWithValue("$toMs", toMs);
                cmd.Parameters.AddWithValue("$sourceType", sourceType);
                cmd.Parameters.AddWithValue("$channelNo", channelNo);
                cmd.Parameters.AddWithValue("$limit", limit);

                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        var row = new ReceiveDataRow
                        {
                            Id = r.GetInt64(0),
                            SourceType = r.GetInt32(1),
                            ChannelNo = r.GetInt32(2),
                            CreatedAt = r.IsDBNull(3) ? null : r.GetString(3),
                            CreatedAtMs = r.GetInt64(4),

                            Mode = r.IsDBNull(5) ? 0 : r.GetInt32(5),
                            RemainTimeText = r.IsDBNull(6) ? null : r.GetString(6),
                            RemainSeconds = r.IsDBNull(7) ? (int?)null : r.GetInt32(7),

                            HeaterTemp = r.IsDBNull(8) ? 0 : r.GetDouble(8),
                            HeaterOffTime = r.IsDBNull(9) ? 0 : r.GetDouble(9),

                            AirTemp = r.IsDBNull(10) ? 0 : r.GetDouble(10),
                            FanSpeed = r.IsDBNull(11) ? 0 : r.GetInt32(11),

                            AvgHeaterOffTime = r.IsDBNull(12) ? (double?)null : r.GetDouble(12),
                            HotAirTemp = r.IsDBNull(13) ? 0 : r.GetDouble(13),
                            HotAirOntime = r.IsDBNull(14) ? 0 : r.GetDouble(14),

                            MotorState = r.IsDBNull(15) ? null : r.GetString(15),
                            MotorCode = r.IsDBNull(16) ? (int?)null : r.GetInt32(16),
                            MotorCurrent = r.IsDBNull(17) ? 0 : r.GetDouble(17),

                            Number = r.IsDBNull(18) ? 0 : r.GetInt32(18),
                        };

                        rows.Add(row);
                    }
                }
            }

            return rows;
        }

        public static void InsertErrorEvent( ref SqliteConnection db, string dbPath, ref bool dbReady, int sourceType, int channelNo, DateTime createdAt, string snapshotId, string fileName,
            int errorSlot, string errorText, int? runMode, double? heaterTemp, double? heaterOffTime, double? hotAirTemp, double? hotAirOnTime, int? runCount, double? exhaustTemp
        )
        {
            EnsureDb(ref db, dbPath, ref dbReady);

            long createdAtMs = new DateTimeOffset(createdAt).ToUnixTimeMilliseconds();
            string createdAtIso = createdAt.ToString("yyyy-MM-dd HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture);

            // NULL → 0 정규화 (숫자 필드)
            int runModeV = runMode ?? 0;
            double heaterTempV = heaterTemp ?? 0;
            double heaterOffV = heaterOffTime ?? 0;
            double hotAirTempV = hotAirTemp ?? 0;
            double hotAirOnV = hotAirOnTime ?? 0;
            int runCountV = runCount ?? 0;
            double exhaustTempV = exhaustTemp ?? 0;

            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT INTO error_events(
                        source_type, channel_no, created_at, created_at_ms,
                        snapshot_id, file_name, error_slot, error_text,
                        run_mode, heater_temp, heater_off_time,
                        hot_air_temp, hot_air_on_time,
                        run_count, exhaust_temp
                    ) VALUES(
                        $source_type, $channel_no, $created_at, $created_at_ms,
                        $snapshot_id, $file_name, $error_slot, $error_text,
                        $run_mode, $heater_temp, $heater_off_time,
                        $hot_air_temp, $hot_air_on_time,
                        $run_count, $exhaust_temp
                    );
                ";

                cmd.Parameters.AddWithValue("$source_type", sourceType);
                cmd.Parameters.AddWithValue("$channel_no", channelNo);
                cmd.Parameters.AddWithValue("$created_at", createdAtIso);
                cmd.Parameters.AddWithValue("$created_at_ms", createdAtMs);
                cmd.Parameters.AddWithValue("$snapshot_id", snapshotId ?? "");
                cmd.Parameters.AddWithValue("$file_name", (object)fileName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$error_slot", errorSlot);
                cmd.Parameters.AddWithValue("$error_text", (object)(errorText ?? "")); // 빈 문자열로 통일

                // 숫자들은 절대 NULL 안 보냄
                cmd.Parameters.AddWithValue("$run_mode", runModeV);
                cmd.Parameters.AddWithValue("$heater_temp", heaterTempV);
                cmd.Parameters.AddWithValue("$heater_off_time", heaterOffV);
                cmd.Parameters.AddWithValue("$hot_air_temp", hotAirTempV);
                cmd.Parameters.AddWithValue("$hot_air_on_time", hotAirOnV);
                cmd.Parameters.AddWithValue("$run_count", runCountV);
                cmd.Parameters.AddWithValue("$exhaust_temp", exhaustTempV);

                cmd.ExecuteNonQuery();
            }
        }

        /// 하부는 내부 헬퍼 및 로그 유틸
        // ---------------------------
        // 내부 헬퍼
        // ---------------------------
        private static long ToUnixMs(DateTime dt)
        {
            // 로컬 시간을 기준으로 epoch 변환(조회도 같은 기준을 쓸 것)
            var dto = new DateTimeOffset(dt);
            return dto.ToUnixTimeMilliseconds();
        }

        private static bool TryParseCreatedAt(string s, out DateTime dt)
        {
            dt = default;
            if (string.IsNullOrWhiteSpace(s)) return false;

            // 기존 코드들에서 섞여 들어올 수 있는 포맷들:
            // 1) "yyyy-MM-dd_HH_mm_ss"
            // 2) "yyyy-MM-dd HH:mm:ss"
            // 3) "yyyy-MM-dd HH:mm:ss.fff"
            // 4) 기타
            string[] formats = new[]
            {
                "yyyy-MM-dd_HH_mm_ss",
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-dd HH:mm:ss.fff",
                "yyyy-MM-dd_HH_mm_ss_fff"
            };

            return DateTime.TryParseExact( s, formats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out dt );
        }

        private static bool TryParseRemainSeconds(string remainText, out int seconds)
        {
            seconds = 0;
            if (string.IsNullOrWhiteSpace(remainText)) return false;

            // "MM:SS" 또는 "HH:MM:SS" 대응
            var parts = remainText.Trim().Split(':');
            if (parts.Length == 2)
            {
                if (!int.TryParse(parts[0], out int mm)) return false;
                if (!int.TryParse(parts[1], out int ss)) return false;
                seconds = mm * 60 + ss;
                return true;
            }
            if (parts.Length == 3)
            {
                if (!int.TryParse(parts[0], out int hh)) return false;
                if (!int.TryParse(parts[1], out int mm)) return false;
                if (!int.TryParse(parts[2], out int ss)) return false;
                seconds = hh * 3600 + mm * 60 + ss;
                return true;
            }

            return false;
        }

        // 아래 로그 유틸들은 기존 그대로 유지(복붙)
        public static string ToSqlLiteral(object value)
        {
            if (value == null || value == DBNull.Value) return "NULL";

            var s = value as string;
            if (s != null)
                return "'" + s.Replace("'", "''") + "'";

            if (value is char)
            {
                var c = (char)value;
                return "'" + (c == '\'' ? "''" : c.ToString()) + "'";
            }

            if (value is bool)
                return ((bool)value) ? "1" : "0";

            if (value is byte || value is sbyte ||
                value is short || value is ushort ||
                value is int || value is uint ||
                value is long || value is ulong)
                return Convert.ToString(value, CultureInfo.InvariantCulture);

            if (value is float || value is double || value is decimal)
                return Convert.ToString(value, CultureInfo.InvariantCulture);

            if (value is DateTime)
            {
                var dt = (DateTime)value;
                return "'" + dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "'";
            }

            if (value is DateTimeOffset)
            {
                var dto = (DateTimeOffset)value;
                return "'" + dto.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture) + "'";
            }

            var bytes = value as byte[];
            if (bytes != null)
                return "X'" + BitConverter.ToString(bytes).Replace("-", "") + "'";

            return "'" + Convert.ToString(value, CultureInfo.InvariantCulture).Replace("'", "''") + "'";
        }

        public static string RenderFinalSqlForLog(Microsoft.Data.Sqlite.SqliteCommand cmd)
        {
            var parameters = cmd.Parameters.Cast<Microsoft.Data.Sqlite.SqliteParameter>().OrderByDescending(p => p.ParameterName == null ? 0 : p.ParameterName.Length).ToList();

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

                    sb.AppendLine(string.Format("- {0} = {1}  | literal={2}  | DbType={3}", p.ParameterName, raw, lit, p.DbType));
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
