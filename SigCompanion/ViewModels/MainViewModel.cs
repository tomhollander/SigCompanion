using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SigCompanion.Lib.EnergyPrices;
using SigCompanion.Lib.Sigen;
using SigCompanion.Lib.Sigen.Modbus;
using SigCompanion.Lib.Sigen.Models;
using SigCompanion.Models;
using SigCompanion.Services;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

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
        private bool _isDemoMode;
        private bool _isDeactivated;
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
            backToMainCommand = new AsyncRelayCommand(BackToMainAsync);
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

        private bool enableAnimations = true;
        public bool EnableAnimations
        {
            get => enableAnimations;
            set
            {
                if (SetProperty(ref enableAnimations, value))
                {
                    _settings.EnableAnimations = value;
                    _settingsStore.Save(_settings);
                }
            }
        }

        private bool showEnergyPrices;
        public bool ShowEnergyPrices
        {
            get => showEnergyPrices;
            set
            {
                if (SetProperty(ref showEnergyPrices, value))
                {
                    _settings.ShowEnergyPrices = value;
                    _settingsStore.Save(_settings);
                }
            }
        }

        public int SigenergyBuyLowBelow
        {
            get => _settings.SigenergyBuyThresholds.LowBelow;
            set => SetSigenergyThresholds(isBuy: true, _settings.SigenergyBuyThresholds with { LowBelow = value });
        }

        public int SigenergyBuyVeryHighAt
        {
            get => _settings.SigenergyBuyThresholds.VeryHighAt;
            set => SetSigenergyThresholds(isBuy: true, _settings.SigenergyBuyThresholds with { VeryHighAt = value });
        }

        public int SigenergySellLowBelow
        {
            get => _settings.SigenergySellThresholds.LowBelow;
            set => SetSigenergyThresholds(isBuy: false, _settings.SigenergySellThresholds with { LowBelow = value });
        }

        public int SigenergySellVeryHighAt
        {
            get => _settings.SigenergySellThresholds.VeryHighAt;
            set => SetSigenergyThresholds(isBuy: false, _settings.SigenergySellThresholds with { VeryHighAt = value });
        }

        private void SetSigenergyThresholds(bool isBuy, SigenergyPriceThresholds thresholds)
        {
            if (thresholds.LowBelow < 0 || thresholds.VeryHighAt < thresholds.LowBelow)
            {
                return;
            }

            var previous = isBuy ? _settings.SigenergyBuyThresholds : _settings.SigenergySellThresholds;
            if (thresholds == previous)
            {
                return;
            }

            if (isBuy)
            {
                _settings.SigenergyBuyThresholds = thresholds;
                OnPropertyChanged(nameof(SigenergyBuyLowBelow));
                OnPropertyChanged(nameof(SigenergyBuyVeryHighAt));
            }
            else
            {
                _settings.SigenergySellThresholds = thresholds;
                OnPropertyChanged(nameof(SigenergySellLowBelow));
                OnPropertyChanged(nameof(SigenergySellVeryHighAt));
            }

            _settingsStore.Save(_settings);
            if (EnergyPrices is { } prices)
            {
                EnergyPrices = LabelSigenergyPrices(prices);
            }
        }

        private EnergyPriceSnapshot LabelSigenergyPrices(EnergyPriceSnapshot prices) => prices with
        {
            Buy = _settings.SigenergyBuyThresholds.Label(prices.Buy),
            Sell = _settings.SigenergySellThresholds.Label(prices.Sell),
        };

        private IEnergyPriceProvider? _configuredEnergyPriceProvider;
        private IEnergyPriceProvider? _sigenergyPriceProvider;
        private SigenHistoricalComparisonSnapshot? _latestDashboardHistory;

        private EnergyPriceSnapshot? energyPrices;
        public EnergyPriceSnapshot? EnergyPrices
        {
            get => energyPrices;
            private set
            {
                if (SetProperty(ref energyPrices, value))
                {
                    OnPropertyChanged(nameof(BuyPriceLevelBrush));
                    OnPropertyChanged(nameof(SellPriceLevelBrush));
                    OnPropertyChanged(nameof(BuyPriceLevelTextBrush));
                    OnPropertyChanged(nameof(SellPriceLevelTextBrush));
                    OnPropertyChanged(nameof(BuyEnergyPriceText));
                    OnPropertyChanged(nameof(SellEnergyPriceText));
                    OnPropertyChanged(nameof(TodayEnergyTotalText));
                    OnPropertyChanged(nameof(TodayEnergyTotalBrush));
                    OnPropertyChanged(nameof(YesterdayEnergyTotalText));
                    OnPropertyChanged(nameof(YesterdayEnergyTotalBrush));
                }
            }
        }

        public IBrush BuyPriceLevelBrush => GetPriceLevelBrush(EnergyPrices?.Buy.PriceLevel);
        public IBrush SellPriceLevelBrush => GetPriceLevelBrush(EnergyPrices?.Sell.PriceLevel);
        public IBrush BuyPriceLevelTextBrush => EnergyPrices?.Buy.PriceLevel == 7 ? Brushes.White : Brushes.Black;
        public IBrush SellPriceLevelTextBrush => EnergyPrices?.Sell.PriceLevel == 7 ? Brushes.White : Brushes.Black;
        public string BuyEnergyPriceText => FormatEnergyRate(EnergyPrices?.Buy.CentsPerKwh);
        public string SellEnergyPriceText => FormatEnergyRate(EnergyPrices?.Sell.CentsPerKwh);
        public string TodayEnergyTotalText => FormatEnergyTotal(EnergyPrices?.TodayTotalDollars);
        public string YesterdayEnergyTotalText => FormatEnergyTotal(EnergyPrices?.YesterdayTotalDollars);

        private static readonly IBrush DefaultEnergyTotalBrush = new SolidColorBrush(Color.Parse("#BEBEBE"));
        public IBrush TodayEnergyTotalBrush => GetEnergyTotalBrush(EnergyPrices?.TodayTotalDollars);
        public IBrush YesterdayEnergyTotalBrush => GetEnergyTotalBrush(EnergyPrices?.YesterdayTotalDollars);

        private string FormatEnergyTotal(decimal? amount) =>
            amount is decimal total && _configuredEnergyPriceProvider is { } provider
                ? total.ToString("C", provider.CurrencyCulture)
                : "—";

        private static IBrush GetEnergyTotalBrush(decimal? amount) =>
            amount is < 0m ? PowerColors.BatteryBrush : DefaultEnergyTotalBrush;

        private string FormatEnergyRate(decimal? rate)
        {
            if (rate is not decimal amount || _configuredEnergyPriceProvider is not { } provider)
            {
                return string.Empty;
            }

            var suffix = provider.MinorUnitSuffix;
            if (suffix.Length == 0)
            {
                return (amount / 100m).ToString("C", provider.CurrencyCulture);
            }

            var rounded = decimal.Round(amount, 0, MidpointRounding.AwayFromZero);
            return rounded.ToString("0", provider.CurrencyCulture) + suffix;
        }

        private static IBrush GetPriceLevelBrush(int? level) => level switch
        {
            1 => Brushes.Magenta,
            2 => Brushes.DodgerBlue,
            3 => Brushes.LimeGreen,
            4 => Brushes.Khaki,
            5 => Brushes.DarkOrange,
            6 => Brushes.IndianRed,
            7 => Brushes.DarkRed,
            0 => Brushes.Gray,
            _ => Brushes.Transparent,
        };

        private void ResetEnergyPriceProvider()
        {
            _configuredEnergyPriceProvider = null;
            EnergyPrices = null;
        }

        private async Task RefreshEnergyPricesAsync(CancellationToken cancellationToken = default)
        {
            if (!ShowEnergyPrices)
            {
                EnergyPrices = null;
                return;
            }

            try
            {
                var provider = _configuredEnergyPriceProvider ??= _isDemoMode
                    ? DemoDataProvider.GetEnergyPriceProvider()
                    : (_sigenergyPriceProvider ??= EnergyPriceProviderFactory.Create(EnergyPriceProviderFactory.Sigenergy, _sigenUiApi));
                var prices = provider is SigenergyEnergyPriceProvider sigenergy
                    ? await sigenergy.GetPricesAsync(GetSigenergyLocalTime(), cancellationToken)
                    : await provider.GetPricesAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (ShowEnergyPrices && ReferenceEquals(_configuredEnergyPriceProvider, provider))
                {
                    if (_isDemoMode && _latestDashboardHistory is { } history)
                    {
                        var now = DateTime.Now;
                        prices = prices with
                        {
                            TodayTotalDollars = DemoDataProvider.GetSigenergyDailyCost(history, now),
                            YesterdayTotalDollars = DemoDataProvider.GetSigenergyYesterdayCost(history, now),
                        };
                    }

                    EnergyPrices = LabelSigenergyPrices(prices);
                }
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                Debug.WriteLine($"Energy price refresh failed ({ex.GetType().Name}).");
            }
        }

        private DateTime GetSigenergyLocalTime()
        {
            var now = DateTime.UtcNow;
            return SelectedSystem?.TimeZone is { Length: > 0 } zoneId &&
                TimeZoneInfo.TryFindSystemTimeZoneById(zoneId, out var zone)
                ? TimeZoneInfo.ConvertTimeFromUtc(now, zone)
                : now;
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
            Telemetry.StartSession(() => _settings);
            Username = _settings.Username ?? string.Empty;
            Password = _settings.Password ?? string.Empty;
            SavePassword = _settings.SavePassword;
            ShowClock = _settings.ShowClock;
            CyclePagesWhenIdle = _settings.CyclePagesWhenIdle;
            EnableAnimations = _settings.EnableAnimations;
            ShowEnergyPrices = _settings.ShowEnergyPrices;
            ChartData = _settings.ChartData;
            UseModbus = _settings.UseModbus;
            ModbusHost = _settings.ModbusHost;
            ModbusPort = _settings.ModbusPort;
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
            var isDemoMode = string.Equals(Username, DemoDataProvider.Username, StringComparison.OrdinalIgnoreCase);
            if (_isDemoMode != isDemoMode)
            {
                _isDemoMode = isDemoMode;
                ResetEnergyPriceProvider();
            }

            try
            {
                IReadOnlyList<SigenSystemReference> loadedSystems;
                if (_isDemoMode)
                {
                    loadedSystems = DemoDataProvider.Systems;
                }
                else
                {
                    _sigenUiApi.SetRegion(SelectedRegion);
                    _sigenUiApi.SetLoginCredentials(Username, Password);
                    loadedSystems = await _sigenUiApi.GetSystemsAsync();
                }
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
                var credentials = _isDemoMode ? null : _sigenUiApi.GetCredentials();
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

        private async Task BackToMainAsync()
        {
            if (!_isAuthenticated)
            {
                return;
            }

            await RefreshEnergyPricesAsync();
            if (_isAuthenticated)
            {
                CurrentPage = AppPage.Main;
            }
        }

        private Task SignOutAsync()
        {
            StopPolling();
            _isAuthenticated = false;
            _isDemoMode = false;

            _settings = new AppSettings
            {
                ShowClock = _settings.ShowClock,
                CyclePagesWhenIdle = _settings.CyclePagesWhenIdle,
                EnableAnimations = _settings.EnableAnimations,
                ShowEnergyPrices = _settings.ShowEnergyPrices,
                SigenergyBuyThresholds = _settings.SigenergyBuyThresholds,
                SigenergySellThresholds = _settings.SigenergySellThresholds,
                ChartData = _settings.ChartData,
                UseModbus = _settings.UseModbus,
                ModbusHost = _settings.ModbusHost,
                ModbusPort = _settings.ModbusPort,
            };
            _settingsStore.Save(_settings);

            Username = string.Empty;
            Password = string.Empty;
            SavePassword = false;
            _sigenergyPriceProvider = null;
            ResetEnergyPriceProvider();
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
            _latestDashboardHistory = null;
            EnergyPrices = null;

            if (!_isAuthenticated || value is null)
            {
                StopPolling();
                return;
            }

            _settings.SystemId = value.SystemId;
            _settingsStore.Save(_settings);
            StartPolling(value.SystemId);
        }

        public void OnApplicationDeactivated()
        {
            _isDeactivated = true;
            StopPolling();
        }

        public void OnApplicationActivated()
        {
            if (!_isDeactivated)
            {
                return;
            }

            _isDeactivated = false;

            if (!_isAuthenticated || SelectedSystem is null)
            {
                return;
            }

            StartPolling(SelectedSystem.SystemId);
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
            _ = PollModbusLoopAsync(ModbusHost, ModbusPort, _modbusCts.Token);
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
            SolarYesterdayKwh = Yesterday("FROM_SOLAR") + Yesterday("FROM_THIRD_PARTY_INV");
            SolarTodayKwh = Today("FROM_SOLAR") + Today("FROM_THIRD_PARTY_INV");
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
                catch (SigenAuthenticationException ex)
                {
                    Debug.WriteLine($"Dashboard poll authentication failed: {ex}");
                    _isAuthenticated = false;
                    StopPolling();
                    CurrentPage = AppPage.Login;
                    StatusMessage = ex.Message;
                    return;
                }
                catch (Exception ex)
                {
                    IsCloudApiUnavailable = true;
                    Debug.WriteLine($"Dashboard poll failed: {ex}");
                    Telemetry.TrackException(ex);
                    StatusMessage = ex.Message;
                    CloudUnavailableError = "Unable to reach the Sigenergy API. Last error: " + ex.Message;
                }

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(2), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private async Task PollModbusLoopAsync(string host, int port, CancellationToken cancellationToken)
        {
            using var modbusClient = new SigenModbusClient(host, port, 247);

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
                    if (!(ex is SocketException || ex is TimeoutException))
                    {
                        Telemetry.TrackException(ex);
                    }
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
            var gridPower = plant.GridSensorActivePower;
            var pvPower = plant.PhotovoltaicPower + plant.ThirdPartyInverterActivePower;
            var loadPower = plant.TotalLoadPower;
            var evPower = (plant.AcEvChargingPower ?? 0m) + (plant.DcEvChargingPower ?? 0m);
            var essPower =  // Don't use plant.EssPower as the numbers don't add up. The Sig app seems to infer this from the other flows.
                pvPower + gridPower - loadPower;

            IsGridConnected = plant.OnOffGridStatus == 0;
            BatterySocValue = (double)plant.EnergyStorageSystemSoc;
            BatterySoc = $"{plant.EnergyStorageSystemSoc:0}%";
            GridStatus = IsGridConnected ? "Connected" : "Disconnected";

            PvPower = FormatPower(pvPower);
            GridPower = FormatPower(-gridPower);
            BatteryPower = FormatPower(-essPower);
            LoadPower = FormatPower(loadPower);
            EvPower = plant.DcEvChargingPower is { } dcPower ? FormatPower(dcPower) : "-";
            AcPower = plant.AcEvChargingPower is { } acPower ? FormatPower(acPower) : "-";

            FlowSolarText = FormatFlowValue(pvPower);
            FlowGridText = FormatFlowValue(gridPower);
            FlowHomeText = FormatFlowValue(loadPower);
            FlowBatteryText = FormatFlowValue(Math.Abs(essPower));
            FlowEvText = FormatFlowValue(evPower);
            EvIconVisible = evPower > 0;

            PowerFlows = new PowerFlowAllocation
            {
                SolarPower = (double)pvPower,
                HomePower = (double)loadPower,
                GridPower = (double)gridPower,
                BatteryPower = -(double)essPower
            };

            IsExportingToGrid = gridPower < -0.08M;
            IsImportingFromGrid = gridPower > 0.08M;
            IsBatteryCharging = essPower > 0.08M;
            IsBatteryDischarging = essPower < -0.08M;
        }

        private async Task RefreshDashboardAsync(string systemId, CancellationToken cancellationToken)
        {
            var historicalTask = _isDemoMode
                ? Task.FromResult(DemoDataProvider.GetHistoricalComparison(DateTime.Now))
                : _sigenUiApi.GetHistoricalDayComparisonAsync(systemId, cancellationToken);

            if (!_modbusHealthy)
            {
                var batteryStatusTask = _isDemoMode
                    ? Task.FromResult(DemoDataProvider.GetBatteryStatus(DateTime.Now))
                    : _sigenUiApi.GetBatteryStatusAsync(systemId, cancellationToken);
                var powerFlowTask = _isDemoMode
                    ? Task.FromResult(DemoDataProvider.GetPowerFlowSnapshot(DateTime.Now))
                    : _sigenUiApi.GetPowerFlowSnapshotAsync(systemId, cancellationToken);

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
                IsExportingToGrid = powerFlow.GridPower > 0.08M;
                IsImportingFromGrid = powerFlow.GridPower < -0.08M;
                IsBatteryCharging = powerFlow.BatteryPower > 0.08M;
                IsBatteryDischarging = powerFlow.BatteryPower < -0.08M;
            }

            var historical = await historicalTask;
            cancellationToken.ThrowIfCancellationRequested();
            _latestDashboardHistory = historical;

            HistoricalStationCards.Clear();
            foreach (var row in historical.StationCards)
            {
                HistoricalStationCards.Add(row);
            }

            UpdateEnergyTotals(historical.StationCards);

            TodayChartDataJson = JsonSerializer.Serialize(historical.TodayChartData, new JsonSerializerOptions { WriteIndented = false });
            YesterdayChartDataJson = JsonSerializer.Serialize(historical.YesterdayChartData, new JsonSerializerOptions { WriteIndented = false });
            UpdateHistoricalPowerChart(historical.YesterdayChartData, historical.TodayChartData, historical.YesterdayStateOfCharge, historical.TodayStateOfCharge);
            await RefreshEnergyPricesAsync(cancellationToken);
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

            var solar = GetChartSeriesValues(charts, "FROM_SOLAR", "FROM_THIRD_PARTY_INV");
            var battery = GetChartSeriesValues(charts, "BATTERY");
            var grid = GetChartSeriesValues(charts, "GRID");
            var load = GetChartSeriesValues(charts, "TO_LOAD");
            var evac = GetChartSeriesValues(charts, "TO_EVAC");
            var gridExport = grid;

            var times = solar.Keys.Concat(battery.Keys).Concat(grid.Keys).Concat(load.Keys).Concat(evac.Keys)
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
                        return $"{underlyingSelector(time):0.##} kW";
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
                CreateSeries("Grid", PowerColors.GridHex, times, t => Sun(t) + BatteryDischarge(t) + GridImport(t), t => GridImport(t) + GridExport(t)),
                CreateSeries("Battery", PowerColors.BatteryHex, times, t => Sun(t) + BatteryDischarge(t), t => BatteryDischarge(t) + BatteryCharge(t)),
                CreateSeries("Solar", PowerColors.SolarHex, times, t => Sun(t), t => Sun(t)),
                CreateSeries("Home", "#ED00FF", times, t => BatteryCharge(t) + Home(t) + Evac(t), t => Home(t)),
                CreateSeries("Grid", PowerColors.GridHex, times, t => BatteryCharge(t) + Home(t) + Evac(t) + GridExport(t), t => GridExport(t), showInLegend: false),
                CreateSeries("Battery", PowerColors.BatteryHex, times, t => BatteryCharge(t), t => BatteryCharge(t), showInLegend: false),
                .. times.Any(t => Evac(t) != 0m)
                    ? [CreateSeries("EV", PowerColors.EvHex, times, t => BatteryCharge(t) + Evac(t), t => Evac(t))]
                    : Array.Empty<ISeries>(),
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

        private static Dictionary<DateTime, decimal> GetChartSeriesValues(IEnumerable<SigenHistoricalChartData?> charts, params string[] seriesIds)
        {
            var values = new Dictionary<DateTime, decimal>();
            foreach (var chart in charts)
            {
                foreach (var series in (chart?.DataSeries ?? []).Where(s => seriesIds.Contains(s.Id)))
                {
                    if (series.Points is null)
                    {
                        continue;
                    }

                    foreach (var point in series.Points)
                    {
                        if (point.Value is decimal value && DateTime.TryParseExact(point.Time, "yyyyMMdd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
                        {
                            values[time] = values.GetValueOrDefault(time) + value;
                        }
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

        public string VersionText
        {
            get => "Version " + Telemetry.AppVersion;
        }
    }
}
