using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SigCompanion.Lib.Sigen;
using SigCompanion.Lib.Sigen.Models;
using SigCompanion.Lib.Sigen.Modbus;
using SigCompanion.Models;
using SigCompanion.Services;
using SkiaSharp;

namespace SigCompanion.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        private readonly SigenUiApi _sigenUiApi;
        private readonly AppSettingsStore _settingsStore;
        private AppSettings _settings;
        private CancellationTokenSource? _pollingCts;
        private CancellationTokenSource? _modbusCts;
        private bool _modbusHealthy;
        private bool _isAuthenticated;
        private readonly DispatcherTimer _clockTimer;

        public MainViewModel()
        {
            var httpClient = new HttpClient();
            var apiClient = new SigenApiClient(httpClient);
            var cachedApiClient = new SigenCachedApiClient(apiClient);
            _sigenUiApi = new SigenUiApi(cachedApiClient);

            _settingsStore = new AppSettingsStore();
            _settings = new AppSettings();

            systems = [];
            statusMessage = "Loading...";
            batterySoc = "-";
            batterySocValue = 0d;
            batteryStatus = "-";
            gridStatus = "-";
            isGridConnected = true;
            pvPower = "-";
            gridPower = "-";
            evPower = "-";
            acPower = "-";
            loadPower = "-";
            heatPumpPower = "-";
            batteryPower = "-";
            historicalStationCards = [];
            todayChartDataJson = "-";
            yesterdayChartDataJson = "-";
            username = string.Empty;
            password = string.Empty;
            selectedRegion = SigenRegions.Default;
            currentPage = AppPage.Login; 

            signInCommand = new AsyncRelayCommand(SignInAsync);
            openSettingsCommand = new RelayCommand(OpenSettings);
            backToMainCommand = new RelayCommand(BackToMain);
            signOutCommand = new AsyncRelayCommand(SignOutAsync);

            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (_, _) => UpdateClock();
            _clockTimer.Start();
            UpdateClock();

            _ = InitializeAsync();
        }

        [ObservableProperty]
        private string username;

        [ObservableProperty]
        private string password;

        [ObservableProperty]
        private bool savePassword;

        [ObservableProperty]
        private SigenRegionInfo selectedRegion;

        public IReadOnlyList<SigenRegionInfo> Regions => SigenRegions.All;

        [ObservableProperty]
        private string statusMessage;

        [ObservableProperty]
        private ObservableCollection<SigenSystemReference> systems;

        [ObservableProperty]
        private SigenSystemReference? selectedSystem;

        [ObservableProperty]
        private string batterySoc;

        [ObservableProperty]
        private double batterySocValue;

        [ObservableProperty]
        private string batteryStatus;

        [ObservableProperty]
        private string gridStatus;

        [ObservableProperty]
        private bool isGridConnected;

        [ObservableProperty]
        private string pvPower;

        [ObservableProperty]
        private string gridPower;

        [ObservableProperty]
        private string evPower;

        [ObservableProperty]
        private string acPower;

        [ObservableProperty]
        private string loadPower;

        [ObservableProperty]
        private string heatPumpPower;

        [ObservableProperty]
        private string batteryPower;

        [ObservableProperty]
        private string flowSolarText;

        [ObservableProperty]
        private string flowGridText;

        [ObservableProperty]
        private string flowHomeText;

        [ObservableProperty]
        private string flowBatteryText;
        
        [ObservableProperty]
        private string flowEvText;

        [ObservableProperty]
        private bool evIconVisible;

        [ObservableProperty]
        private PowerFlowAllocation powerFlows;

        [ObservableProperty]
        private bool isExportingToGrid;

        [ObservableProperty]
        private bool isImportingFromGrid;

        [ObservableProperty]
        private bool isBatteryCharging;

        [ObservableProperty]
        private bool isBatteryDischarging;

        [ObservableProperty]
        private ObservableCollection<SigenStationCardComparisonRow> historicalStationCards;

        [ObservableProperty]
        private string todayChartDataJson;

        [ObservableProperty]
        private string yesterdayChartDataJson;

        [ObservableProperty]
        private ISeries[] historicalPowerSeries = [];

        private ISeries[] historicalPowerLineSeries = [];
        private DateTimePoint[] historicalSocPoints = [];

        [ObservableProperty]
        private Axis[] historicalXAxes = [];

        [ObservableProperty]
        private Axis[] historicalYAxes = [];

        [ObservableProperty]
        private decimal homeYesterdayKwh;

        [ObservableProperty]
        private decimal homeTodayKwh;

        [ObservableProperty]
        private decimal solarYesterdayKwh;

        [ObservableProperty]
        private decimal solarTodayKwh;

        [ObservableProperty]
        private decimal gridImportYesterdayKwh;

        [ObservableProperty]
        private decimal gridImportTodayKwh;

        [ObservableProperty]
        private decimal gridExportYesterdayKwh;

        [ObservableProperty]
        private decimal gridExportTodayKwh;

        [ObservableProperty]
        private bool isYesterdayGridExportVisible;

        [ObservableProperty]
        private bool isTodayGridExportVisible;

        [ObservableProperty]
        private decimal batteryImportYesterdayKwh;

        [ObservableProperty]
        private decimal batteryImportTodayKwh;

        [ObservableProperty]
        private decimal batteryExportYesterdayKwh;

        [ObservableProperty]
        private decimal batteryExportTodayKwh;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private AppPage currentPage;

        [ObservableProperty]
        private bool isCloudApiUnavailable;

        [ObservableProperty]
        private string cloudUnavailableError;

        [ObservableProperty]
        private bool isModbusUnconfigured;

        [ObservableProperty]
        private bool isModbusUnavailable;

        [ObservableProperty]
        private string modbusUnavailableError = string.Empty;

        private bool showClock;
        public bool ShowClock
        {
            get => showClock;
            set
            {
                if (SetProperty(ref showClock, value))
                {
                    _settings.ShowClock = value;
                    _settingsStore.Save(_settings);
                }
            }
        }

        private bool cyclePagesWhenIdle;
        public bool CyclePagesWhenIdle
        {
            get => cyclePagesWhenIdle;
            set
            {
                if (SetProperty(ref cyclePagesWhenIdle, value))
                {
                    _settings.CyclePagesWhenIdle = value;
                    _settingsStore.Save(_settings);
                }
            }
        }

        public string[] ChartDataOptions { get; } = ["Power", "Battery SoC", "Both"];

        private string chartData = "Power";
        public string ChartData
        {
            get => chartData;
            set
            {
                if (SetProperty(ref chartData, value))
                {
                    _settings.ChartData = value;
                    _settingsStore.Save(_settings);
                    UpdateHistoricalChartSeries();
                }
            }
        }

        private bool useModbus;
        public bool UseModbus
        {
            get => useModbus;
            set
            {
                if (SetProperty(ref useModbus, value))
                {
                    _settings.UseModbus = value;
                    _settingsStore.Save(_settings);
                    RestartModbusPolling();
                    IsModbusUnavailable = false;
                    IsModbusUnconfigured = !value;
                }
            }
        }

        private string? modbusHost;
        public string? ModbusHost
        {
            get => modbusHost;
            set
            {
                if (SetProperty(ref modbusHost, value))
                {
                    _settings.ModbusHost = value;
                    _settingsStore.Save(_settings);
                    RestartModbusPolling();
                }
            }
        }

        private int modbusPort = 502;
        public int ModbusPort
        {
            get => modbusPort;
            set
            {
                if (SetProperty(ref modbusPort, value))
                {
                    _settings.ModbusPort = value;
                    _settingsStore.Save(_settings);
                    RestartModbusPolling();
                }
            }
        }

        private byte modbusSlaveAddress = 247;
        public byte ModbusSlaveAddress
        {
            get => modbusSlaveAddress;
            set
            {
                if (SetProperty(ref modbusSlaveAddress, value))
                {
                    _settings.ModbusSlaveAddress = value;
                    _settingsStore.Save(_settings);
                    RestartModbusPolling();
                }
            }
        }

        private string currentTimeText = string.Empty;
        public string CurrentTimeText
        {
            get => currentTimeText;
            private set => SetProperty(ref currentTimeText, value);
        }

        private void UpdateClock()
        {
            CurrentTimeText = DateTime.Now.ToString("h:mm", CultureInfo.InvariantCulture);
        }

        public bool IsLoginPageVisible => CurrentPage == AppPage.Login;
        public bool IsMainPageVisible => CurrentPage != AppPage.Settings;
        public bool IsSettingsPageVisible => CurrentPage == AppPage.Settings;
        public bool IsGridDisconnected => !IsGridConnected;

        private readonly IAsyncRelayCommand signInCommand;
        public IAsyncRelayCommand SignInCommand => signInCommand;

        private readonly IRelayCommand openSettingsCommand;
        public IRelayCommand OpenSettingsCommand => openSettingsCommand;

        private readonly IRelayCommand backToMainCommand;
        public IRelayCommand BackToMainCommand => backToMainCommand;

        private readonly IAsyncRelayCommand signOutCommand;
        public IAsyncRelayCommand SignOutCommand => signOutCommand;

        private async Task InitializeAsync()
        {
            _settings = _settingsStore.Load();
            Username = _settings.Username ?? string.Empty;
            Password = _settings.Password ?? string.Empty;
            SavePassword = _settings.SavePassword;
            ShowClock = _settings.ShowClock;
            CyclePagesWhenIdle = _settings.CyclePagesWhenIdle;
            ChartData = _settings.ChartData;
            UseModbus = _settings.UseModbus;
            ModbusHost = _settings.ModbusHost;
            ModbusPort = _settings.ModbusPort;
            ModbusSlaveAddress = _settings.ModbusSlaveAddress;
            SelectedRegion = SigenRegions.GetByRegion(_settings.Region);
            _sigenUiApi.SetRegion(SelectedRegion);

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                StatusMessage = "Please sign in.";
                CurrentPage = AppPage.Login;
                return;
            }

            if (!string.IsNullOrWhiteSpace(_settings.AccessToken))
            {
                _sigenUiApi.SetCredentials(new SigenCredentials
                {
                    AccessToken = _settings.AccessToken,
                    ExpiresAt = _settings.AccessTokenExpiresAt,
                    Username = Username,
                });
            }

            StatusMessage = "Signing in with saved credentials...";
            await SignInCoreAsync();
        }

        private Task SignInAsync() => SignInCoreAsync();

        private async Task SignInCoreAsync()
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                StatusMessage = "Username and password are required.";
                CurrentPage = AppPage.Login;
                return;
            }

            IsBusy = true;

            try
            {
                _sigenUiApi.SetRegion(SelectedRegion);
                _sigenUiApi.SetLoginCredentials(Username, Password);

                var loadedSystems = await _sigenUiApi.GetSystemsAsync();
                Systems.Clear();
                foreach (var system in loadedSystems)
                {
                    Systems.Add(system);
                }

                if (Systems.Count == 0)
                {
                    StatusMessage = "No systems found for this account.";
                    CurrentPage = AppPage.Login;
                    _isAuthenticated = false;
                    StopPolling();
                    return;
                }

                _isAuthenticated = true;

                var preferredSystem = !string.IsNullOrWhiteSpace(_settings.SystemId)
                    ? Systems.FirstOrDefault(item => string.Equals(item.SystemId, _settings.SystemId, StringComparison.Ordinal))
                    : null;

                SelectedSystem = preferredSystem ?? Systems[0];

                _settings.Username = Username;
                _settings.SavePassword = SavePassword;
                _settings.Password = SavePassword ? Password : null;
                _settings.Region = SelectedRegion.Region;
                var credentials = _sigenUiApi.GetCredentials();
                _settings.AccessToken = credentials?.AccessToken;
                _settings.AccessTokenExpiresAt = credentials?.ExpiresAt;
                _settings.SystemId = SelectedSystem.SystemId;
                _settingsStore.Save(_settings);

                CurrentPage = AppPage.Main;
                StatusMessage = "Signed in.";
            }
            catch (SigenRateLimitException)
            {
                _isAuthenticated = false;
                StopPolling();
                CurrentPage = AppPage.Login;
                StatusMessage = "The Sigenergy API is rate-limiting requests. Please try again in a few minutes.";
            }
            catch (Exception ex)
            {
                _isAuthenticated = false;
                StopPolling();
                CurrentPage = AppPage.Login;
                StatusMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void OpenSettings()
        {
            if (!_isAuthenticated)
            {
                return;
            }

            CurrentPage = AppPage.Settings;
        }

        private void BackToMain()
        {
            if (!_isAuthenticated)
            {
                return;
            }

            CurrentPage = AppPage.Main;
        }

        private Task SignOutAsync()
        {
            StopPolling();
            _isAuthenticated = false;

            _settings = new AppSettings
            {
                ShowClock = _settings.ShowClock,
                CyclePagesWhenIdle = _settings.CyclePagesWhenIdle,
                ChartData = _settings.ChartData,
                UseModbus = _settings.UseModbus,
                ModbusHost = _settings.ModbusHost,
                ModbusPort = _settings.ModbusPort,
                ModbusSlaveAddress = _settings.ModbusSlaveAddress
            };
            _settingsStore.Save(_settings);

            Username = string.Empty;
            Password = string.Empty;
            SavePassword = false;
            SelectedRegion = SigenRegions.Default;
            SelectedSystem = null;
            Systems.Clear();
            BatterySoc = "-";
            BatterySocValue = 0d;
            BatteryStatus = "-";
            GridStatus = "-";
            IsGridConnected = true;
            PvPower = "-";
            GridPower = "-";
            EvPower = "-";
            AcPower = "-";
            LoadPower = "-";
            HeatPumpPower = "-";
            BatteryPower = "-";
            IsExportingToGrid = false;
            IsImportingFromGrid = false;
            IsBatteryCharging = false;
            IsBatteryDischarging = false;
            HistoricalStationCards.Clear();
            TodayChartDataJson = "-";
            YesterdayChartDataJson = "-";

            CurrentPage = AppPage.Login;
            StatusMessage = "Signed out.";

            return Task.CompletedTask;
        }

        partial void OnCurrentPageChanged(AppPage value)
        {
            OnPropertyChanged(nameof(IsLoginPageVisible));
            OnPropertyChanged(nameof(IsMainPageVisible));
            OnPropertyChanged(nameof(IsSettingsPageVisible));
        }

        partial void OnIsGridConnectedChanged(bool value)
        {
            OnPropertyChanged(nameof(IsGridDisconnected));
        }

        partial void OnSelectedSystemChanged(SigenSystemReference? value)
        {
            if (!_isAuthenticated || value is null)
            {
                StopPolling();
                return;
            }

            _settings.SystemId = value.SystemId;
            _settingsStore.Save(_settings);
            StartPolling(value.SystemId);
        }

        private void StartPolling(string systemId)
        {
            StopPolling();

            _pollingCts = new CancellationTokenSource();
            _ = PollBatteryStatusLoopAsync(systemId, _pollingCts.Token);
            RestartModbusPolling();
        }

        private void RestartModbusPolling()
        {
            StopModbusPolling();

            if (_pollingCts is null || !UseModbus || string.IsNullOrWhiteSpace(ModbusHost))
            {
                return;
            }

            _modbusCts = new CancellationTokenSource();
            _ = PollModbusLoopAsync(ModbusHost, ModbusPort, ModbusSlaveAddress, _modbusCts.Token);
        }

        private void StopModbusPolling()
        {
            _modbusCts?.Cancel();
            _modbusCts?.Dispose();
            _modbusCts = null;
            _modbusHealthy = false;
            IsModbusUnavailable = false;
            ModbusUnavailableError = string.Empty;
        }

        private void StopPolling()
        {
            StopModbusPolling();
            _pollingCts?.Cancel();
            _pollingCts?.Dispose();
            _pollingCts = null;
        }

        private void UpdateEnergyTotals(IEnumerable<SigenStationCardComparisonRow> rows)
        {
            var byKey = rows.ToDictionary(row => row.CardKey, StringComparer.Ordinal);
            decimal Yesterday(string key) => byKey.TryGetValue(key, out var row) ? row.YesterdayKwh : 0m;
            decimal Today(string key) => byKey.TryGetValue(key, out var row) ? row.TodayKwh : 0m;

            HomeYesterdayKwh = Yesterday("TO_LOAD");
            HomeTodayKwh = Today("TO_LOAD");
            SolarYesterdayKwh = Yesterday("FROM_SOLAR");
            SolarTodayKwh = Today("FROM_SOLAR");
            GridImportYesterdayKwh = Yesterday("FROM_GRID");
            GridImportTodayKwh = Today("FROM_GRID");
            GridExportYesterdayKwh = Yesterday("TO_GRID");
            GridExportTodayKwh = Today("TO_GRID");
            IsYesterdayGridExportVisible = GridExportYesterdayKwh > 0;
            IsTodayGridExportVisible = GridExportTodayKwh > 0;
            BatteryImportYesterdayKwh = Yesterday("TO_BATTERY");
            BatteryImportTodayKwh = Today("TO_BATTERY");
            BatteryExportYesterdayKwh = Yesterday("FROM_BATTERY");
            BatteryExportTodayKwh = Today("FROM_BATTERY");
        }

        private async Task PollBatteryStatusLoopAsync(string systemId, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await RefreshDashboardAsync(systemId, cancellationToken);
                    IsCloudApiUnavailable = false;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    IsCloudApiUnavailable = true;
                    Debug.WriteLine($"Dashboard poll failed: {ex}");
                    StatusMessage = ex.Message;
                    CloudUnavailableError = "Unable to reach the Sigenergy API. Last error: " + ex.Message;

                    // For testing
                    //PowerFlows = new PowerFlowAllocation
                    //{
                    //    SolarPower = (double)10,
                    //    HomePower = (double)10,
                    //    GridPower = -(double)-10,
                    //    BatteryPower = -(double)-10
                    //};
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private async Task PollModbusLoopAsync(string host, int port, byte slaveAddress, CancellationToken cancellationToken)
        {
            using var modbusClient = new SigenModbusClient(host, port, slaveAddress);

            while (!cancellationToken.IsCancellationRequested)
            {
                var delay = TimeSpan.FromSeconds(5);
                try
                {
                    var plant = await modbusClient.GetPlantInformationAsync(cancellationToken);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    ApplyModbusPlantInformation(plant);
                    _modbusHealthy = true;
                    IsModbusUnavailable = false;
                    ModbusUnavailableError = string.Empty;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _modbusHealthy = false;
                    IsModbusUnavailable = true;
                    ModbusUnavailableError = "Unable to reach the Modbus interface. Last error: " + ex.Message;
                    Debug.WriteLine($"Modbus poll failed: {ex}");
                    delay = TimeSpan.FromMinutes(1);
                }

                try
                {
                    await Task.Delay(delay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private void ApplyModbusPlantInformation(SigenPlantInformation plant)
        {
            var gridPower = plant.GridSensorActivePower / 1000;
            var pvPower = plant.PhotovoltaicPower / 1000;
            var essPower = plant.EssPower / 1000;
            var loadPower = pvPower + gridPower - essPower;

            IsGridConnected = plant.OnOffGridStatus == 1;
            BatterySocValue = (double)plant.EnergyStorageSystemSoc;
            BatterySoc = $"{plant.EnergyStorageSystemSoc:0}%";
            GridStatus = IsGridConnected ? "Connected" : "Disconnected";

            PvPower = FormatPower(pvPower);
            GridPower = FormatPower(-gridPower);
            BatteryPower = FormatPower(-essPower);
            LoadPower = FormatPower(loadPower);

            FlowSolarText = FormatFlowValue(pvPower);
            FlowGridText = FormatFlowValue(gridPower);
            FlowHomeText = FormatFlowValue(loadPower);
            FlowBatteryText = FormatFlowValue(Math.Abs(essPower));

            PowerFlows = new PowerFlowAllocation
            {
                SolarPower = (double)pvPower,
                HomePower = (double)loadPower,
                GridPower = (double)gridPower,
                BatteryPower = (double)essPower
            };

            IsExportingToGrid = gridPower < 0;
            IsImportingFromGrid = gridPower > 0;
            IsBatteryCharging = essPower < 0;
            IsBatteryDischarging = essPower > 0;
        }

        private async Task RefreshDashboardAsync(string systemId, CancellationToken cancellationToken)
        {
            var historicalTask = _sigenUiApi.GetHistoricalDayComparisonAsync(systemId, cancellationToken);

            if (!_modbusHealthy)
            {
                var batteryStatusTask = _sigenUiApi.GetBatteryStatusAsync(systemId, cancellationToken);
                var powerFlowTask = _sigenUiApi.GetPowerFlowSnapshotAsync(systemId, cancellationToken);

                await Task.WhenAll(batteryStatusTask, powerFlowTask);

                var snapshot = await batteryStatusTask;
                var powerFlow = await powerFlowTask;

                BatterySocValue = (double)snapshot.BatteryStateOfCharge;
                IsGridConnected = snapshot.IsGridConnected;
                BatterySoc = $"{snapshot.BatteryStateOfCharge:0}%";
                BatteryStatus = snapshot.BatteryStatus.ToString();
                GridStatus = snapshot.IsGridConnected ? "Connected" : "Disconnected";

                PvPower = FormatPower(powerFlow.PvPower);
                GridPower = FormatPower(powerFlow.GridPower);
                EvPower = FormatPower(powerFlow.EvPower);
                AcPower = FormatPower(powerFlow.AcPower);
                LoadPower = FormatPower(powerFlow.LoadPower);
                HeatPumpPower = FormatPower(powerFlow.HeatPumpPower);
                BatteryPower = FormatPower(powerFlow.BatteryPower);

                FlowSolarText = FormatFlowValue(powerFlow.PvPower);
                FlowGridText = FormatFlowValue(-powerFlow.GridPower);
                FlowHomeText = FormatFlowValue(powerFlow.LoadPower + powerFlow.EvPower + powerFlow.AcPower + powerFlow.HeatPumpPower);
                FlowBatteryText = FormatFlowValue(Math.Abs(powerFlow.BatteryPower));
                FlowEvText = FormatFlowValue(powerFlow.EvPower + powerFlow.AcPower);
                EvIconVisible = (powerFlow.EvPower + powerFlow.AcPower) > 0;

                PowerFlows = new PowerFlowAllocation
                {
                    SolarPower = (double)powerFlow.PvPower,
                    HomePower = (double)(powerFlow.LoadPower + powerFlow.EvPower + powerFlow.AcPower + powerFlow.HeatPumpPower),
                    GridPower = -(double)powerFlow.GridPower,
                    BatteryPower = -(double)powerFlow.BatteryPower
                };

                // Raw API signs: gridPower > 0 = export (Grid tile shows -GridPower); batteryPower > 0 = charging, < 0 = discharging.
                IsExportingToGrid = powerFlow.GridPower > 0;
                IsImportingFromGrid = powerFlow.GridPower < 0;
                IsBatteryCharging = powerFlow.BatteryPower > 0;
                IsBatteryDischarging = powerFlow.BatteryPower < 0;
            }

            var historical = await historicalTask;

            HistoricalStationCards.Clear();
            foreach (var row in historical.StationCards)
            {
                HistoricalStationCards.Add(row);
            }

            UpdateEnergyTotals(historical.StationCards);

            TodayChartDataJson = JsonSerializer.Serialize(historical.TodayChartData, new JsonSerializerOptions { WriteIndented = false });
            YesterdayChartDataJson = JsonSerializer.Serialize(historical.YesterdayChartData, new JsonSerializerOptions { WriteIndented = false });
            UpdateHistoricalPowerChart(historical.YesterdayChartData, historical.TodayChartData, historical.YesterdayStateOfCharge, historical.TodayStateOfCharge);
        }

        private void UpdateHistoricalPowerChart(
            JsonElement yesterdayChartData,
            JsonElement todayChartData,
            IReadOnlyList<SigenStateOfChargePoint> yesterdaySoc,
            IReadOnlyList<SigenStateOfChargePoint> todaySoc)
        {
            var charts = new[]
            {
                yesterdayChartData.Deserialize<SigenHistoricalChartData>(),
                todayChartData.Deserialize<SigenHistoricalChartData>()
            };

            var solar = GetChartSeriesValues(charts, "FROM_SOLAR");
            var battery = GetChartSeriesValues(charts, "BATTERY");
            var grid = GetChartSeriesValues(charts, "GRID");
            var load = GetChartSeriesValues(charts, "TO_LOAD");
            var evac = GetChartSeriesValues(charts, "TO_EVAC");
            var gridExport = GetChartSeriesValues(charts, "TO_GRID");

            var times = solar.Keys.Concat(battery.Keys).Concat(grid.Keys).Concat(load.Keys).Concat(evac.Keys).Concat(gridExport.Keys)
                .Distinct()
                .OrderBy(t => t)
                .ToArray();

            var socValues = yesterdaySoc.Concat(todaySoc)
                .GroupBy(p => p.Time)
                .Select(g => new KeyValuePair<DateTime, decimal>(g.Key, g.Last().StateOfCharge))
                .OrderBy(p => p.Key)
                .ToArray();

            // Each side of zero is drawn as non-stacked filled areas holding cumulative totals.
            // Series are ordered back to front, so the largest total is drawn first.
            // Tooltips show each series' underlying value (not the cumulative total) at that time.
            ISeries CreateSeries(string name, string color, DateTime[] times, Func<DateTime, decimal> valueSelector, Func<DateTime, decimal> underlyingSelector, bool showInLegend = true)
            {
                var baseColor = SKColor.Parse(color);
                return new LineSeries<DateTimePoint>
                {
                    Name = showInLegend ? name : null,
                    Values = times.Select(t => new DateTimePoint(t, (double)valueSelector(t))).ToArray(),
                    Fill = new SolidColorPaint(baseColor.WithAlpha(80)),
                    Stroke = new SolidColorPaint(baseColor) { StrokeThickness = 1 },
                    GeometrySize = 0,
                    LineSmoothness = 0,
                    IsVisibleAtLegend = showInLegend,
                    IsHoverable = showInLegend,
                    XToolTipLabelFormatter = point => new DateTime((long)point.Coordinate.SecondaryValue).ToString("h:mm tt", CultureInfo.InvariantCulture),
                    YToolTipLabelFormatter = point =>
                    {
                        var time = new DateTime((long)point.Coordinate.SecondaryValue);
                        return $"{Math.Abs(underlyingSelector(time)):0.##} kW";
                    },
                };
            }

            decimal Sun(DateTime t) => Math.Max(GetValue(solar, t), 0m);
            decimal BatteryDischarge(DateTime t) => Math.Max(GetValue(battery, t), 0m);
            decimal GridImport(DateTime t) => Math.Max(GetValue(grid, t), 0m);
            decimal BatteryCharge(DateTime t) => Math.Min(GetValue(battery, t), 0m);
            decimal Home(DateTime t) => Math.Min(GetValue(load, t), 0m);
            decimal Evac(DateTime t) => Math.Min(GetValue(evac, t), 0m);
            decimal GridExport(DateTime t) => Math.Min(GetValue(gridExport, t), 0m);

            historicalPowerLineSeries =
            [
                CreateSeries("Grid", "#CCCCCC", times, t => Sun(t) + BatteryDischarge(t) + GridImport(t), t => GridImport(t)),
                CreateSeries("Battery", "#3CC83C", times, t => Sun(t) + BatteryDischarge(t), t => BatteryDischarge(t)),
                CreateSeries("Solar", "#E0B800", times, t => Sun(t), t => Sun(t)),
                CreateSeries("Grid", "#CCCCCC", times, t => BatteryCharge(t) + Home(t) + Evac(t) + GridExport(t), t => GridExport(t), showInLegend: false),
                CreateSeries("Home", "#2AA7E0", times, t => BatteryCharge(t) + Home(t) + Evac(t), t => Home(t)),
                CreateSeries("EV", "#E03C3C", times, t => BatteryCharge(t) + Evac(t), t => Evac(t)),
                CreateSeries("Battery", "#3CC83C", times, t => BatteryCharge(t), t => BatteryCharge(t), showInLegend: false),
            ];
            historicalSocPoints = socValues.Select(p => new DateTimePoint(p.Key, (double)p.Value)).ToArray();
            UpdateHistoricalChartSeries();


            HistoricalXAxes =
            [
                new Axis
                {
                    Labeler = value => value < DateTime.MinValue.Ticks || value > DateTime.MaxValue.Ticks
                        ? string.Empty
                        : new DateTime((long)value).ToString("h tt", CultureInfo.InvariantCulture),
                    MinLimit = DateTime.Today.AddDays(-1).Ticks,
                    MaxLimit = DateTime.Today.AddDays(1).Ticks,
                    UnitWidth = TimeSpan.FromHours(2).Ticks,
                    MinStep = TimeSpan.FromHours(2).Ticks,
                    LabelsPaint = new SolidColorPaint(SKColor.Parse("#AAAAAA"))
                }
            ];

            }

        private void UpdateHistoricalChartSeries()
        {
            bool showPower = ChartData is "Power" or "Both";
            bool showSoc = ChartData is "Battery SoC" or "Both";

            var series = new List<ISeries>();
            var axes = new List<Axis>();

            if (showPower)
            {
                series.AddRange(historicalPowerLineSeries);
                axes.Add(new Axis
                {
                    Labeler = value => $"{Math.Abs(value):0} kW",
                    LabelsPaint = new SolidColorPaint(SKColor.Parse("#AAAAAA"))
                });
            }

            if (showSoc)
            {
                var socColor = SKColor.Parse("#00D3D3");
                series.Add(new LineSeries<DateTimePoint>
                {
                    Name = "SoC",
                    Values = historicalSocPoints,
                    ScalesYAt = showPower ? 1 : 0,
                    Fill = ChartData == "Battery SoC" ? new SolidColorPaint(socColor.WithAlpha(80)) : null,
                    Stroke = new SolidColorPaint(socColor) { StrokeThickness = 2 },
                    GeometrySize = 0,
                    LineSmoothness = 0,
                    XToolTipLabelFormatter = point => new DateTime((long)point.Coordinate.SecondaryValue).ToString("h:mm tt", CultureInfo.InvariantCulture),
                    YToolTipLabelFormatter = point => $"{point.Coordinate.PrimaryValue:0.#}%",
                });
                axes.Add(new Axis
                {
                    Position = LiveChartsCore.Measure.AxisPosition.End,
                    MinLimit = 0,
                    MaxLimit = 100,
                    Labeler = value => $"{value:0}%",
                    ShowSeparatorLines = false,
                    LabelsPaint = new SolidColorPaint(SKColor.Parse("#AAAAAA"))
                });
            }

            HistoricalPowerSeries = series.ToArray();
            HistoricalYAxes = axes.ToArray();
        }

        private static Dictionary<DateTime, decimal> GetChartSeriesValues(IEnumerable<SigenHistoricalChartData?> charts, string seriesId)
        {
            var values = new Dictionary<DateTime, decimal>();
            foreach (var chart in charts)
            {
                var series = chart?.DataSeries?.FirstOrDefault(s => s.Id == seriesId);
                if (series?.Points is null)
                {
                    continue;
                }

                foreach (var point in series.Points)
                {
                    if (point.Value is decimal value && DateTime.TryParseExact(point.Time, "yyyyMMdd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
                    {
                        values[time] = value;
                    }
                }
            }

            return values;
        }

        private static decimal GetValue(Dictionary<DateTime, decimal> values, DateTime time) => values.TryGetValue(time, out var value) ? value : 0m;

        private static string FormatPower(decimal power) => $"{power:0.###} kW";

        private static string FormatFlowValue(decimal power) => Math.Abs(power) < 0.1m ? string.Empty : power.ToString("0.0", CultureInfo.InvariantCulture);

        public enum AppPage
        {
            Login,
            Main,
            Settings,
        }
    }
}
