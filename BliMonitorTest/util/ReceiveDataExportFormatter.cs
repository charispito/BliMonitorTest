using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using BliMonitorTest.data;

namespace BliMonitorTest.util
{
    public static class ReceiveDataExportFormatter
    {
        public sealed class GridColumnSpec
        {
            public string ColumnName { get; set; }
            public string Header { get; set; }
            public bool Visible { get; set; } = true;
            public int DisplayIndex { get; set; } = int.MaxValue;
            public double Width { get; set; } = double.NaN;
        }

        public static readonly List<GridColumnSpec> ReceiveColumnSpecs = new List<GridColumnSpec>
        {
            new GridColumnSpec { ColumnName = "created_at", Header = "수신시각", DisplayIndex = 0, Width = 150 },
            new GridColumnSpec { ColumnName = "source_type_text", Header = "Source", DisplayIndex = 1, Width = 90 },
            new GridColumnSpec { ColumnName = "channel_no", Header = "채널번호", DisplayIndex = 2, Width = 80, Visible = false },
            new GridColumnSpec { ColumnName = "model_name", Header = "모델명", DisplayIndex = 3, Width = 100 },
            new GridColumnSpec { ColumnName = "error_text", Header = "에러해석", DisplayIndex = 4, Width = 140 },

            new GridColumnSpec { ColumnName = "mode_selected_text", Header = "선택모드", DisplayIndex = 5, Width = 100 },
            new GridColumnSpec { ColumnName = "qty_selected_text", Header = "선택용량", DisplayIndex = 6, Width = 100 },
            new GridColumnSpec { ColumnName = "dispense_phase_text", Header = "출수단계", DisplayIndex = 7, Width = 120 },
            new GridColumnSpec { ColumnName = "dispense_sub_phase_text", Header = "출수세부단계", DisplayIndex = 8, Width = 140 },

            new GridColumnSpec { ColumnName = "hot_temp_text", Header = "온수Temp", DisplayIndex = 9, Width = 90 },
            new GridColumnSpec { ColumnName = "cold_temp_text", Header = "냉수Temp", DisplayIndex = 10, Width = 90 },

            new GridColumnSpec { ColumnName = "water_init_done_text", Header = "초기급수완료", DisplayIndex = 11, Width = 110 },
            new GridColumnSpec { ColumnName = "water_init_go_text", Header = "초기급수중", DisplayIndex = 12, Width = 110 },
            new GridColumnSpec { ColumnName = "empty_detect_text", Header = "물부족감지", DisplayIndex = 13, Width = 110 },
            new GridColumnSpec { ColumnName = "buffer_low_text", Header = "버퍼부족", DisplayIndex = 14, Width = 100 },
            new GridColumnSpec { ColumnName = "reheat_running_text", Header = "재가열", DisplayIndex = 15, Width = 90 },
            new GridColumnSpec { ColumnName = "hot_ing_text", Header = "가열중", DisplayIndex = 16, Width = 90 },

            new GridColumnSpec { ColumnName = "heater_output_text", Header = "히터출력", DisplayIndex = 17, Width = 90 },
            new GridColumnSpec { ColumnName = "compressor_output_text", Header = "컴프출력", DisplayIndex = 18, Width = 90 },
            new GridColumnSpec { ColumnName = "hot_valve_output_text", Header = "온수밸브", DisplayIndex = 19, Width = 90 },
            new GridColumnSpec { ColumnName = "cold_select_output_text", Header = "냉수선택밸브", DisplayIndex = 20, Width = 110 },
            new GridColumnSpec { ColumnName = "outlet_valve_output_text", Header = "출수밸브", DisplayIndex = 21, Width = 90 },

            new GridColumnSpec { ColumnName = "status_a_text", Header = "상태A", DisplayIndex = 22, Width = 200 },
            new GridColumnSpec { ColumnName = "status_b_text", Header = "상태B", DisplayIndex = 23, Width = 200 },
            new GridColumnSpec { ColumnName = "button_info_text", Header = "버튼정보", DisplayIndex = 24, Width = 200 }
        };

        public static void AddReceiveInterpretColumns(DataTable dt)
        {
            if (dt == null) return;

            AddColumnIfMissing(dt, "source_type_text");
            AddColumnIfMissing(dt, "model_name");
            AddColumnIfMissing(dt, "error_text");
            AddColumnIfMissing(dt, "water_init_done_text");
            AddColumnIfMissing(dt, "water_init_go_text");
            AddColumnIfMissing(dt, "empty_detect_text");
            AddColumnIfMissing(dt, "buffer_low_text");
            AddColumnIfMissing(dt, "reheat_running_text");
            AddColumnIfMissing(dt, "hot_ing_text");
            AddColumnIfMissing(dt, "heater_pwm_text");
            AddColumnIfMissing(dt, "night_text");
            AddColumnIfMissing(dt, "test_mode_text");
            AddColumnIfMissing(dt, "mode_selected_text");
            AddColumnIfMissing(dt, "qty_selected_text");
            AddColumnIfMissing(dt, "dispense_phase_text");
            AddColumnIfMissing(dt, "dispense_sub_phase_text");
            AddColumnIfMissing(dt, "hot_temp_text");
            AddColumnIfMissing(dt, "cold_temp_text");
            AddColumnIfMissing(dt, "float_low_stable_text");
            AddColumnIfMissing(dt, "ball_top_full_stable_text");
            AddColumnIfMissing(dt, "water_buf_full_stable_text");
            AddColumnIfMissing(dt, "heater_output_text");
            AddColumnIfMissing(dt, "compressor_output_text");
            AddColumnIfMissing(dt, "hot_valve_output_text");
            AddColumnIfMissing(dt, "cold_select_output_text");
            AddColumnIfMissing(dt, "outlet_valve_output_text");
            AddColumnIfMissing(dt, "button_info_text");
            AddColumnIfMissing(dt, "status_a_text");
            AddColumnIfMissing(dt, "status_b_text");

            foreach (DataRow row in dt.Rows)
            {
                row["source_type_text"] = Duo8ValueText.GetSourceTypeText(ToInt(row["source_type"]));
                row["model_name"] = Duo8ValueText.GetModelName(ToInt(row["model_code"]));
                row["error_text"] = Duo8ValueText.GetErrorText((byte)ToInt(row["error_code"]));
                row["water_init_done_text"] = Duo8ValueText.ToDoneText((byte)ToInt(row["water_init_done"]));
                row["water_init_go_text"] = Duo8ValueText.ToRunText((byte)ToInt(row["water_init_go"]));
                row["empty_detect_text"] = Duo8ValueText.ToYesNo((byte)ToInt(row["empty_detect"]));
                row["buffer_low_text"] = Duo8ValueText.ToYesNo((byte)ToInt(row["buffer_low"]));
                row["reheat_running_text"] = Duo8ValueText.ToRunText((byte)ToInt(row["reheat_running"]));
                row["hot_ing_text"] = Duo8ValueText.ToRunText((byte)ToInt(row["hot_ing"]));
                row["heater_pwm_text"] = Duo8ValueText.ToOnOff((byte)ToInt(row["heater_pwm"]));
                row["night_text"] = Duo8ValueText.ToYesNo((byte)ToInt(row["night"]));
                row["test_mode_text"] = Duo8ValueText.ToYesNo((byte)ToInt(row["test_mode"]));
                row["mode_selected_text"] = Duo8ValueText.GetModeText((byte)ToInt(row["mode_selected"]));
                row["qty_selected_text"] = Duo8ValueText.GetQtyText((byte)ToInt(row["qty_selected"]));
                row["dispense_phase_text"] = Duo8ValueText.GetDispensePhaseText((byte)ToInt(row["dispense_phase"]));
                row["dispense_sub_phase_text"] = Duo8ValueText.GetDispenseSubPhaseText((byte)ToInt(row["dispense_sub_phase"]));
                row["hot_temp_text"] = Duo8ValueText.FormatTempX10((ushort)ToInt(row["hot_temp_raw"]));
                row["cold_temp_text"] = Duo8ValueText.FormatTempX10((ushort)ToInt(row["cold_temp_raw"]));
                row["float_low_stable_text"] = Duo8ValueText.ToActiveInactive((byte)ToInt(row["float_low_stable"]));
                row["ball_top_full_stable_text"] = Duo8ValueText.ToActiveInactive((byte)ToInt(row["ball_top_full_stable"]));
                row["water_buf_full_stable_text"] = Duo8ValueText.ToActiveInactive((byte)ToInt(row["water_buf_full_stable"]));
                row["heater_output_text"] = Duo8ValueText.ToOnOff((byte)ToInt(row["heater_output"]));
                row["compressor_output_text"] = Duo8ValueText.ToOnOff((byte)ToInt(row["compressor_output"]));
                row["hot_valve_output_text"] = Duo8ValueText.ToOnOff((byte)ToInt(row["hot_valve_output"]));
                row["cold_select_output_text"] = Duo8ValueText.ToOnOff((byte)ToInt(row["cold_select_output"]));
                row["outlet_valve_output_text"] = Duo8ValueText.ToOnOff((byte)ToInt(row["outlet_valve_output"]));
                row["button_info_text"] = Duo8ValueText.DecodeButtonInfo((ushort)ToInt(row["button_info"]));
                row["status_a_text"] = Duo8ValueText.DecodeStatusA((byte)ToInt(row["status_a"]));
                row["status_b_text"] = Duo8ValueText.DecodeStatusB((byte)ToInt(row["status_b"]));
            }
        }

        public static DataTable BuildReceiveExportTable(DataTable source)
        {
            var export = new DataTable("receive_export");

            var visibleSpecs = new List<GridColumnSpec>();
            foreach (var spec in ReceiveColumnSpecs)
            {
                if (!spec.Visible) continue;
                if (!source.Columns.Contains(spec.ColumnName)) continue;
                visibleSpecs.Add(spec);
            }

            visibleSpecs.Sort((a, b) => a.DisplayIndex.CompareTo(b.DisplayIndex));

            foreach (var spec in visibleSpecs)
                export.Columns.Add(spec.Header, typeof(string));

            foreach (DataRow srcRow in source.Rows)
            {
                var newRow = export.NewRow();
                for (int i = 0; i < visibleSpecs.Count; i++)
                {
                    object value = srcRow[visibleSpecs[i].ColumnName];
                    newRow[i] = value == null || value == DBNull.Value
                        ? ""
                        : Convert.ToString(value, CultureInfo.InvariantCulture);
                }
                export.Rows.Add(newRow);
            }

            return export;
        }

        private static void AddColumnIfMissing(DataTable dt, string columnName)
        {
            if (!dt.Columns.Contains(columnName))
                dt.Columns.Add(columnName, typeof(string));
        }

        private static int ToInt(object value)
        {
            if (value == null || value == DBNull.Value) return 0;
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
    }
}
