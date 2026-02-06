using BliMonitorTest.data;
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

        public const int SOURCE_SINGLE = 1;
        public const int SOURCE_MULTI = 2;

        // 테이블명 고정
        private const string TABLE = "receive_data";

        /// <summary>
        /// DB 생성 및 테이블 생성 보장 (DB 삭제/재생성 전제: 마이그레이션 없음)
        /// </summary>
        public static void EnsureDb(ref SqliteConnection db, string dbPath, ref bool dbReady)
        {
            if (dbReady && db != null && db.State == System.Data.ConnectionState.Open) return;

            if (db == null)
                db = new SqliteConnection($"Data Source={dbPath};");

            if (db.State != System.Data.ConnectionState.Open)
                db.Open();

            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = @"
                    PRAGMA journal_mode=WAL;
                    PRAGMA synchronous=NORMAL;
                    PRAGMA busy_timeout=5000;

                    CREATE TABLE IF NOT EXISTS receive_data (
                        id               INTEGER PRIMARY KEY AUTOINCREMENT,
                        source_type      INTEGER NOT NULL,
                        channel_no       INTEGER NOT NULL,
                        created_at       TEXT    NOT NULL,
                        created_at_ms    INTEGER NOT NULL,

                        start_packet     INTEGER,
                        command          INTEGER,         -- 변경: cmd_byte → command
                        payload_size     INTEGER,
                        model_no         INTEGER,
                        sw_ver           INTEGER,
                        heater_temp      INTEGER,         -- 변경 주석 유지
                        cold_temp        INTEGER,         -- 변경 주석 유지
                        low_water_sensor INTEGER,
                        floor_sensor     INTEGER,
                        uv_led           INTEGER,         -- 변경: uv_led_byte → uv_led (0/1)
                        sol_3way1        INTEGER,
                        sol_3way2        INTEGER,
                        sol_3way3        INTEGER,
                        air_vent_sol     INTEGER,
                        cv_sol           INTEGER,
                        button_flags     INTEGER,
                        pump             INTEGER,
                        cold_sol         INTEGER,
                        normal_sol       INTEGER,
                        hot_sol1         INTEGER,
                        needle_pos       INTEGER,
                        Compressor       INTEGER,         -- 변경: pel_voltage → Compressor (0/1)
                        checksum         INTEGER,         -- 변경: checksum_byte → checksum
                        end_packet       INTEGER
                    );

                    CREATE INDEX IF NOT EXISTS idx_receive_data_time
                      ON receive_data(created_at_ms);
                    CREATE INDEX IF NOT EXISTS idx_receive_data_src_ch_time
                      ON receive_data(source_type, channel_no, created_at_ms);
                    CREATE INDEX IF NOT EXISTS idx_receive_data_model
                      ON receive_data(model_no);

                    CREATE TABLE IF NOT EXISTS error_events (
                        id               INTEGER PRIMARY KEY AUTOINCREMENT,
                        source_type      INTEGER NOT NULL,
                        channel_no       INTEGER NOT NULL,
                        created_at       TEXT    NOT NULL,
                        created_at_ms    INTEGER NOT NULL,
                        snapshot_id      TEXT,
                        file_name        TEXT,
                        error_slot       INTEGER,
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
                    CREATE INDEX IF NOT EXISTS idx_error_events_src_ch_time
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
        public static void InsertDb(ref SqliteConnection db, string dbPath, ref bool dbReady, BliResponse57Packet resp, int channelNo, int sourceType)
        {
            EnsureDb(ref db, dbPath, ref dbReady);
            if (resp == null) return;

            DateTime now = DateTime.Now;
            long createdAtMs = new DateTimeOffset(now).ToUnixTimeMilliseconds();
            string createdAtIso = now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);

            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT INTO receive_data (
                        source_type, channel_no, created_at, created_at_ms,
                        start_packet, command, payload_size,
                        model_no, sw_ver, heater_temp, cold_temp,
                        low_water_sensor, floor_sensor, uv_led,
                        sol_3way1, sol_3way2, sol_3way3,
                        air_vent_sol, cv_sol, button_flags,
                        pump, cold_sol, normal_sol, hot_sol1,
                        needle_pos, Compressor,
                        checksum, end_packet
                    ) VALUES (
                        $source_type, $channel_no, $created_at, $created_at_ms,
                        $start_packet, $command, $payload_size,
                        $model_no, $sw_ver, $heater_temp, $cold_temp,
                        $low_water_sensor, $floor_sensor, $uv_led,
                        $sol_3way1, $sol_3way2, $sol_3way3,
                        $air_vent_sol, $cv_sol, $button_flags,
                        $pump, $cold_sol, $normal_sol, $hot_sol1,
                        $needle_pos, $Compressor,
                        $checksum, $end_packet
                    );
                ";

                cmd.Parameters.AddWithValue("$source_type", sourceType);
                cmd.Parameters.AddWithValue("$channel_no", channelNo);
                cmd.Parameters.AddWithValue("$created_at", createdAtIso);
                cmd.Parameters.AddWithValue("$created_at_ms", createdAtMs);

                cmd.Parameters.AddWithValue("$start_packet", resp.StartPacket);
                cmd.Parameters.AddWithValue("$command", resp.CmdByte);        // 입력은 기존 속성 사용
                cmd.Parameters.AddWithValue("$payload_size", resp.PayloadSize);
                cmd.Parameters.AddWithValue("$model_no", resp.ModelNo);
                cmd.Parameters.AddWithValue("$sw_ver", resp.SwVer);

                // 값 매핑 (바이트 그대로 저장)
                cmd.Parameters.AddWithValue("$heater_temp", resp.HeaterTempB);
                cmd.Parameters.AddWithValue("$cold_temp", resp.ColdTempB);
                cmd.Parameters.AddWithValue("$low_water_sensor", resp.LowWater);
                cmd.Parameters.AddWithValue("$floor_sensor", resp.FloorSensor);
                cmd.Parameters.AddWithValue("$uv_led", resp.UvLedByte);       // 이름만 변경
                cmd.Parameters.AddWithValue("$sol_3way1", resp.Sol3Way1);
                cmd.Parameters.AddWithValue("$sol_3way2", resp.Sol3Way2);
                cmd.Parameters.AddWithValue("$sol_3way3", resp.Sol3Way3);
                cmd.Parameters.AddWithValue("$air_vent_sol", resp.AirVentSol);
                cmd.Parameters.AddWithValue("$cv_sol", resp.CvSol);
                cmd.Parameters.AddWithValue("$button_flags", resp.ButtonFlags);
                cmd.Parameters.AddWithValue("$pump", resp.Pump);
                cmd.Parameters.AddWithValue("$cold_sol", resp.ColdSol);
                cmd.Parameters.AddWithValue("$normal_sol", resp.NormalSol);
                cmd.Parameters.AddWithValue("$hot_sol1", resp.HotSol1);
                cmd.Parameters.AddWithValue("$needle_pos", resp.NeedlePos);

                // Compressor: ON/OFF(0/1)로 저장
                cmd.Parameters.AddWithValue("$Compressor", resp.PelVoltageB); // 기존 바이트를 그대로 사용(0/1로 오는 전제)

                cmd.Parameters.AddWithValue("$checksum", resp.Checksum);
                cmd.Parameters.AddWithValue("$end_packet", resp.EndPacket);

                try { cmd.ExecuteNonQuery(); }
                catch (Exception ex)
                {
                    log.Error("InsertDb 예외 : " + ex);
                    log.Error(FormatSqlLog(cmd, "INSERT receive_data"));
                }
            }
        }

        public static void InsertErrorEvent(
            ref SqliteConnection db, string dbPath, ref bool dbReady,
            int sourceType, int channelNo, DateTime createdAt, string snapshotId, string fileName,
            int errorSlot, string errorText, int? runMode, double? heaterTemp, double? heaterOffTime, double? hotAirTemp, double? hotAirOnTime, int? runCount, double? exhaustTemp)
        {
            EnsureDb(ref db, dbPath, ref dbReady);

            long createdAtMs = new DateTimeOffset(createdAt).ToUnixTimeMilliseconds();
            string createdAtIso = createdAt.ToString("yyyy-MM-dd HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture);

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
                cmd.Parameters.AddWithValue("$error_text", (object)(errorText ?? ""));

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

        // ===== 내부 헬퍼 및 로그 유틸 =====

        private static long ToUnixMs(DateTime dt)
        {
            var dto = new DateTimeOffset(dt);
            return dto.ToUnixTimeMilliseconds();
        }

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

        public static string RenderFinalSqlForLog(SqliteCommand cmd)
        {
            var parameters = cmd.Parameters.Cast<SqliteParameter>().OrderByDescending(p => p.ParameterName == null ? 0 : p.ParameterName.Length).ToList();
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

        public static string FormatSqlLog(SqliteCommand cmd, string title)
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
                foreach (SqliteParameter p in cmd.Parameters)
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
