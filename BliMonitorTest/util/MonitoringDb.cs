using System;
using System.Globalization;
using System.Linq;
using System.Text;
using BliMonitorTest.data;
using log4net;
using Microsoft.Data.Sqlite;

namespace BliMonitorTest.util.MonitoringDb
{
    internal static class MonitoringDb
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MonitoringDb));

        public const int SOURCE_SINGLE = 1;
        public const int SOURCE_MULTI = 2;

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
                        id                     INTEGER PRIMARY KEY AUTOINCREMENT,
                        source_type            INTEGER NOT NULL,
                        channel_no             INTEGER NOT NULL,
                        created_at             TEXT    NOT NULL,
                        created_at_ms          INTEGER NOT NULL,

                        start_packet           INTEGER,
                        version                INTEGER,
                        command                INTEGER,
                        payload_size           INTEGER,

                        model_code             INTEGER,
                        error_code             INTEGER,
                        water_init_done        INTEGER,
                        water_init_go          INTEGER,
                        empty_detect           INTEGER,
                        buffer_low             INTEGER,
                        pcb_hw_version         INTEGER,
                        pcb_sw_version         INTEGER,
                        heater_pwm             INTEGER,
                        night                  INTEGER,
                        test_mode              INTEGER,
                        mode_selected          INTEGER,
                        qty_selected           INTEGER,
                        dispense_phase         INTEGER,
                        dispense_sub_phase     INTEGER,
                        hot_temp_raw           INTEGER,
                        cold_temp_raw          INTEGER,
                        float_low_stable       INTEGER,
                        ball_top_full_stable   INTEGER,
                        water_buf_full_stable  INTEGER,
                        heater_output          INTEGER,
                        compressor_output      INTEGER,
                        hot_valve_output       INTEGER,
                        cold_select_output     INTEGER,
                        outlet_valve_output    INTEGER,
                        button_info            INTEGER,
                        status_a               INTEGER,
                        status_b               INTEGER,
                        checksum               INTEGER,
                        end_packet             INTEGER
                    );

                    CREATE INDEX IF NOT EXISTS idx_receive_data_time
                      ON receive_data(created_at_ms);

                    CREATE INDEX IF NOT EXISTS idx_receive_data_src_ch_time
                      ON receive_data(source_type, channel_no, created_at_ms);

                    CREATE INDEX IF NOT EXISTS idx_receive_data_model_code
                      ON receive_data(model_code);

                    CREATE TABLE IF NOT EXISTS error_history (
                        id                     INTEGER PRIMARY KEY AUTOINCREMENT,
                        source_type            INTEGER NOT NULL,
                        channel_no             INTEGER NOT NULL,
                        created_at             TEXT    NOT NULL,
                        created_at_ms          INTEGER NOT NULL,

                        request_command        INTEGER,
                        response_command       INTEGER,
                        slot_no                INTEGER NOT NULL,

                        valid_mark             INTEGER,
                        sequence_no            INTEGER,
                        error_code             INTEGER,
                        hot_temp_raw           INTEGER,
                        cold_temp_raw          INTEGER,
                        adc_hot_raw            INTEGER,
                        adc_cold_raw           INTEGER,
                        water_init_done        INTEGER,
                        status_a               INTEGER,
                        status_b               INTEGER,
                        buffer_low             INTEGER,
                        record_crc             INTEGER
                    );

                    CREATE INDEX IF NOT EXISTS idx_error_history_time
                      ON error_history(created_at_ms);

                    CREATE INDEX IF NOT EXISTS idx_error_history_src_ch_time
                      ON error_history(source_type, channel_no, created_at_ms);

                    CREATE INDEX IF NOT EXISTS idx_error_history_slot
                      ON error_history(slot_no);

                    CREATE INDEX IF NOT EXISTS idx_error_history_error_code
                      ON error_history(error_code);
                ";
                cmd.ExecuteNonQuery();
            }

            dbReady = true;
        }

        public static void InsertDb(
            ref SqliteConnection db,
            string dbPath,
            ref bool dbReady,
            int sourceType,
            int channelNo,
            Duo8StatusPacket pkt)
        {
            InsertStatusData(ref db, dbPath, ref dbReady, sourceType, channelNo, pkt);
        }

        public static void InsertStatusData(
            ref SqliteConnection db,
            string dbPath,
            ref bool dbReady,
            int sourceType,
            int channelNo,
            Duo8StatusPacket pkt)
        {
            EnsureDb(ref db, dbPath, ref dbReady);
            if (pkt == null) return;

            DateTime now = DateTime.Now;
            long createdAtMs = new DateTimeOffset(now).ToUnixTimeMilliseconds();
            string createdAt = now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);

            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT INTO receive_data (
                        source_type, channel_no, created_at, created_at_ms,
                        start_packet, version, command, payload_size,
                        model_code, error_code, water_init_done, water_init_go,
                        empty_detect, buffer_low, pcb_hw_version, pcb_sw_version,
                        heater_pwm, night, test_mode, mode_selected, qty_selected,
                        dispense_phase, dispense_sub_phase,
                        hot_temp_raw, cold_temp_raw,
                        float_low_stable, ball_top_full_stable, water_buf_full_stable,
                        heater_output, compressor_output, hot_valve_output,
                        cold_select_output, outlet_valve_output,
                        button_info, status_a, status_b,
                        checksum, end_packet
                    ) VALUES (
                        $source_type, $channel_no, $created_at, $created_at_ms,
                        $start_packet, $version, $command, $payload_size,
                        $model_code, $error_code, $water_init_done, $water_init_go,
                        $empty_detect, $buffer_low, $pcb_hw_version, $pcb_sw_version,
                        $heater_pwm, $night, $test_mode, $mode_selected, $qty_selected,
                        $dispense_phase, $dispense_sub_phase,
                        $hot_temp_raw, $cold_temp_raw,
                        $float_low_stable, $ball_top_full_stable, $water_buf_full_stable,
                        $heater_output, $compressor_output, $hot_valve_output,
                        $cold_select_output, $outlet_valve_output,
                        $button_info, $status_a, $status_b,
                        $checksum, $end_packet
                    );
                ";

                cmd.Parameters.AddWithValue("$source_type", sourceType);
                cmd.Parameters.AddWithValue("$channel_no", channelNo);
                cmd.Parameters.AddWithValue("$created_at", createdAt);
                cmd.Parameters.AddWithValue("$created_at_ms", createdAtMs);

                cmd.Parameters.AddWithValue("$start_packet", 0x12);
                cmd.Parameters.AddWithValue("$version", 0x01);
                cmd.Parameters.AddWithValue("$command", 0xA0);
                cmd.Parameters.AddWithValue("$payload_size", 37);

                cmd.Parameters.AddWithValue("$model_code", pkt.ModelCode);
                cmd.Parameters.AddWithValue("$error_code", pkt.ErrorCode);
                cmd.Parameters.AddWithValue("$water_init_done", pkt.WaterInitDone);
                cmd.Parameters.AddWithValue("$water_init_go", pkt.WaterInitGo);
                cmd.Parameters.AddWithValue("$empty_detect", pkt.EmptyDetect);
                cmd.Parameters.AddWithValue("$buffer_low", pkt.BufferLow);
                cmd.Parameters.AddWithValue("$pcb_hw_version", pkt.PcbHwVersion);
                cmd.Parameters.AddWithValue("$pcb_sw_version", pkt.PcbSwVersion);
                cmd.Parameters.AddWithValue("$heater_pwm", pkt.HeaterPwm);
                cmd.Parameters.AddWithValue("$night", pkt.Night);
                cmd.Parameters.AddWithValue("$test_mode", pkt.TestMode);
                cmd.Parameters.AddWithValue("$mode_selected", pkt.ModeSelected);
                cmd.Parameters.AddWithValue("$qty_selected", pkt.QtySelected);
                cmd.Parameters.AddWithValue("$dispense_phase", pkt.DispensePhase);
                cmd.Parameters.AddWithValue("$dispense_sub_phase", pkt.DispenseSubPhase);
                cmd.Parameters.AddWithValue("$hot_temp_raw", pkt.HotTempRaw);
                cmd.Parameters.AddWithValue("$cold_temp_raw", pkt.ColdTempRaw);
                cmd.Parameters.AddWithValue("$float_low_stable", pkt.FloatLowStable);
                cmd.Parameters.AddWithValue("$ball_top_full_stable", pkt.BallTopFullStable);
                cmd.Parameters.AddWithValue("$water_buf_full_stable", pkt.WaterBufFullStable);
                cmd.Parameters.AddWithValue("$heater_output", pkt.HeaterOutput);
                cmd.Parameters.AddWithValue("$compressor_output", pkt.CompressorOutput);
                cmd.Parameters.AddWithValue("$hot_valve_output", pkt.HotValveOutput);
                cmd.Parameters.AddWithValue("$cold_select_output", pkt.ColdSelectOutput);
                cmd.Parameters.AddWithValue("$outlet_valve_output", pkt.OutletValveOutput);
                cmd.Parameters.AddWithValue("$button_info", pkt.ButtonInfo);
                cmd.Parameters.AddWithValue("$status_a", pkt.StatusA);
                cmd.Parameters.AddWithValue("$status_b", pkt.StatusB);
                cmd.Parameters.AddWithValue("$checksum", pkt.Checksum);
                cmd.Parameters.AddWithValue("$end_packet", 0x34);

                try
                {
                    cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    log.Error("InsertStatusData 예외 : " + ex);
                    log.Error(FormatSqlLog(cmd, "INSERT receive_data"));
                }
            }
        }

        public static void InsertErrorHistory(
            ref SqliteConnection db,
            string dbPath,
            ref bool dbReady,
            int sourceType,
            int channelNo,
            Duo8ErrorResponse resp)
        {
            EnsureDb(ref db, dbPath, ref dbReady);
            if (resp == null || resp.Records == null || resp.Records.Count == 0) return;

            DateTime now = DateTime.Now;
            long createdAtMs = new DateTimeOffset(now).ToUnixTimeMilliseconds();
            string createdAt = now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);

            for (int i = 0; i < resp.Records.Count; i++)
            {
                var r = resp.Records[i];

                using (var cmd = db.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO error_history (
                            source_type, channel_no, created_at, created_at_ms,
                            request_command, response_command, slot_no,
                            valid_mark, sequence_no, error_code,
                            hot_temp_raw, cold_temp_raw,
                            adc_hot_raw, adc_cold_raw,
                            water_init_done, status_a, status_b,
                            buffer_low, record_crc
                        ) VALUES (
                            $source_type, $channel_no, $created_at, $created_at_ms,
                            $request_command, $response_command, $slot_no,
                            $valid_mark, $sequence_no, $error_code,
                            $hot_temp_raw, $cold_temp_raw,
                            $adc_hot_raw, $adc_cold_raw,
                            $water_init_done, $status_a, $status_b,
                            $buffer_low, $record_crc
                        );
                    ";

                    cmd.Parameters.AddWithValue("$source_type", sourceType);
                    cmd.Parameters.AddWithValue("$channel_no", channelNo);
                    cmd.Parameters.AddWithValue("$created_at", createdAt);
                    cmd.Parameters.AddWithValue("$created_at_ms", createdAtMs);

                    cmd.Parameters.AddWithValue("$request_command", 0xB9);
                    cmd.Parameters.AddWithValue("$response_command", 0xB9);
                    cmd.Parameters.AddWithValue("$slot_no", i + 1);

                    cmd.Parameters.AddWithValue("$valid_mark", r.IsValid ? 1 : 0);
                    cmd.Parameters.AddWithValue("$sequence_no", r.Sequence);
                    cmd.Parameters.AddWithValue("$error_code", r.ErrorCode);
                    cmd.Parameters.AddWithValue("$hot_temp_raw", r.HotTempRaw);
                    cmd.Parameters.AddWithValue("$cold_temp_raw", r.ColdTempRaw);
                    cmd.Parameters.AddWithValue("$adc_hot_raw", r.AdcHotRaw);
                    cmd.Parameters.AddWithValue("$adc_cold_raw", r.AdcColdRaw);
                    cmd.Parameters.AddWithValue("$water_init_done", r.WaterInitDone != 0 ? 1 : 0);
                    cmd.Parameters.AddWithValue("$status_a", r.StatusA);
                    cmd.Parameters.AddWithValue("$status_b", r.StatusB);
                    cmd.Parameters.AddWithValue("$buffer_low", r.BufferLow != 0 ? 1 : 0);
                    cmd.Parameters.AddWithValue("$record_crc", r.Crc);

                    try
                    {
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        log.Error("InsertErrorHistory 예외 : " + ex);
                        log.Error(FormatSqlLog(cmd, "INSERT error_history"));
                    }
                }
            }
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
                return (bool)value ? "1" : "0";

            if (value is byte || value is sbyte ||
                value is short || value is ushort ||
                value is int || value is uint ||
                value is long || value is ulong)
                return Convert.ToString(value, CultureInfo.InvariantCulture);

            if (value is float || value is double || value is decimal)
                return Convert.ToString(value, CultureInfo.InvariantCulture);

            if (value is DateTime dt)
                return "'" + dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "'";

            if (value is DateTimeOffset dto)
                return "'" + dto.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture) + "'";

            var bytes = value as byte[];
            if (bytes != null)
                return "X'" + BitConverter.ToString(bytes).Replace("-", "") + "'";

            return "'" + Convert.ToString(value, CultureInfo.InvariantCulture).Replace("'", "''") + "'";
        }

        public static string RenderFinalSqlForLog(SqliteCommand cmd)
        {
            var parameters = cmd.Parameters.Cast<SqliteParameter>()
                .OrderByDescending(p => p.ParameterName == null ? 0 : p.ParameterName.Length)
                .ToList();

            string sql = cmd.CommandText ?? "";

            foreach (var p in parameters)
            {
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
            var sb = new StringBuilder();
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
