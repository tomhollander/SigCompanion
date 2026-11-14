using SigCompanion.Lib.Sigen.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace SigCompanion.Services
{
    public static class DemoDataProvider
    {
        public const string Username = "demo";

        public static IReadOnlyList<SigenSystemReference> Systems { get; } =
        [
            new SigenSystemReference
            {
                SystemId = "demo-system",
                SystemName = "Demo Home",
            }
        ];

        public static SigenBatteryStatusSnapshot GetBatteryStatus(DateTime now)
        {
            var powerFlow = GetPowerFlowSnapshot(now);

            var batteryStatus = powerFlow.BatteryPower switch
            {
                > 0.05m => BatteryStatus.Charging,
                < -0.05m => BatteryStatus.Discharging,
                _ => BatteryStatus.Idle,
            };

            return new SigenBatteryStatusSnapshot
            {
                BatteryStateOfCharge = GetStateOfCharge(now),
                BatteryStatus = batteryStatus,
                IsGridConnected = true,
            };
        }

        public static SigenPowerFlowSnapshot GetPowerFlowSnapshot(DateTime now)
        {
            var hour = now.TimeOfDay.TotalHours;

            var sunFactor = Math.Max(0d, Math.Sin((hour - 6d) / 12d * Math.PI));
            var eveningFactor = Math.Max(0d, Math.Sin((hour - 16d) / 6d * Math.PI));

            var pvPower = (decimal)(sunFactor * 5.8d);
            var loadPower = (decimal)(0.7d + eveningFactor * 1.4d);
            var acPower = sunFactor > 0.7d ? 0.9m : 0m;

            var netPower = pvPower - loadPower - acPower;
            var batteryPower = Math.Clamp(netPower, -3m, 3m);
            var gridPower = netPower - batteryPower;

            return new SigenPowerFlowSnapshot
            {
                PvPower = Math.Round(pvPower, 2),
                GridPower = Math.Round(gridPower, 2),
                EvPower = 0m,
                AcPower = acPower,
                LoadPower = Math.Round(loadPower, 2),
                HeatPumpPower = 0m,
                BatteryPower = Math.Round(batteryPower, 2),
            };
        }

        public static SigenHistoricalComparisonSnapshot GetHistoricalComparison(DateTime now)
        {
            var todayDate = DateOnly.FromDateTime(now);
            var yesterdayDate = todayDate.AddDays(-1);

            var todayChart = BuildChartData(todayDate, now);
            var yesterdayChart = BuildChartData(yesterdayDate, null);

            var todayStateOfCharge = BuildStateOfCharge(todayDate, now);
            var yesterdayStateOfCharge = BuildStateOfCharge(yesterdayDate, null);

            var todayKwh = ToStationTotals(todayChart);
            var yesterdayKwh = ToStationTotals(yesterdayChart);

            return new SigenHistoricalComparisonSnapshot
            {
                TodayDate = todayDate,
                YesterdayDate = yesterdayDate,
                StationCards = StationCardKeys.Select(key => new SigenStationCardComparisonRow
                {
                    CardKey = key,
                    TodayKwh = todayKwh.GetValueOrDefault(key),
                    YesterdayKwh = yesterdayKwh.GetValueOrDefault(key),
                }).ToArray(),
                TodayChartData = JsonSerializer.SerializeToElement(todayChart),
                YesterdayChartData = JsonSerializer.SerializeToElement(yesterdayChart),
                TodayStateOfCharge = todayStateOfCharge,
                YesterdayStateOfCharge = yesterdayStateOfCharge,
            };
        }

        private static readonly string[] StationCardKeys =
        [
            "TO_LOAD", "FROM_SOLAR", "FROM_GRID", "TO_GRID", "TO_BATTERY", "FROM_BATTERY",
        ];

        private static Dictionary<string, decimal> ToStationTotals(SigenHistoricalChartData chart)
        {
            decimal KwhOf(string seriesId, Func<decimal, bool> filter)
            {
                var kw = (chart.DataSeries ?? [])
                    .Where(s => s.Id == seriesId)
                    .SelectMany(s => s.Points ?? [])
                    .Select(p => p.Value ?? 0m)
                    .Where(filter)
                    .Sum(Math.Abs);
                return Math.Round(kw * 5m / 60m, 2);
            }

            return new Dictionary<string, decimal>
            {
                ["FROM_SOLAR"] = KwhOf("FROM_SOLAR", v => v > 0),
                ["TO_LOAD"] = KwhOf("TO_LOAD", v => v < 0),
                ["FROM_GRID"] = KwhOf("GRID", v => v > 0),
                ["TO_GRID"] = KwhOf("TO_GRID", v => v < 0),
                ["TO_BATTERY"] = KwhOf("BATTERY", v => v < 0),
                ["FROM_BATTERY"] = KwhOf("BATTERY", v => v > 0),
            };
        }

        private static SigenHistoricalChartData BuildChartData(DateOnly date, DateTime? upTo)
        {
            var solar = new List<SigenHistoricalChartPoint>();
            var battery = new List<SigenHistoricalChartPoint>();
            var grid = new List<SigenHistoricalChartPoint>();
            var load = new List<SigenHistoricalChartPoint>();
            var gridExport = new List<SigenHistoricalChartPoint>();

            var start = date.ToDateTime(TimeOnly.MinValue);
            var end = upTo ?? start.AddDays(1).AddMinutes(-5);

            for (var t = start; t <= end && t.Date == start.Date; t = t.AddMinutes(5))
            {
                var flow = GetPowerFlowSnapshot(t);
                var time = t.ToString("yyyyMMdd HH:mm", CultureInfo.InvariantCulture);

                solar.Add(new SigenHistoricalChartPoint { Time = time, Value = flow.PvPower  });
                battery.Add(new SigenHistoricalChartPoint { Time = time, Value = -flow.BatteryPower });
                grid.Add(new SigenHistoricalChartPoint { Time = time, Value = Math.Max(-flow.GridPower, 0m) });
                gridExport.Add(new SigenHistoricalChartPoint { Time = time, Value = Math.Min(-flow.GridPower, 0m) });
                load.Add(new SigenHistoricalChartPoint { Time = time, Value = -(flow.LoadPower + flow.AcPower) });
            }

            return new SigenHistoricalChartData
            {
                ChartName = "Power",
                Unit = "W",
                ChartType = "line",
                DataSeries =
                [
                    new SigenHistoricalSeriesData { Id = "FROM_SOLAR", Points = solar },
                    new SigenHistoricalSeriesData { Id = "BATTERY", Points = battery },
                    new SigenHistoricalSeriesData { Id = "GRID", Points = grid },
                    new SigenHistoricalSeriesData { Id = "TO_LOAD", Points = load },
                    new SigenHistoricalSeriesData { Id = "TO_GRID", Points = gridExport },
                ],
            };
        }

        private static IReadOnlyList<SigenStateOfChargePoint> BuildStateOfCharge(DateOnly date, DateTime? upTo)
        {
            var start = date.ToDateTime(TimeOnly.MinValue);
            var end = upTo ?? start.AddDays(1).AddMinutes(-5);
            var points = new List<SigenStateOfChargePoint>();

            for (var t = start; t <= end && t.Date == start.Date; t = t.AddMinutes(5))
            {
                points.Add(new SigenStateOfChargePoint { Time = t, StateOfCharge = GetStateOfCharge(t) });
            }

            return points;
        }

        private static decimal GetStateOfCharge(DateTime now)
        {
            var hour = now.TimeOfDay.TotalHours;
            var soc = 60d + 30d * Math.Sin((hour - 10d) / 24d * 2d * Math.PI);
            return Math.Round((decimal)soc, 0);
        }
    }
}
