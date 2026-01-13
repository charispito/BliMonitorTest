using System;
using Microsoft.Data.Sqlite;

namespace BliMonitorTest.util
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
    }
}
