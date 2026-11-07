using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SigCompanion.Lib.Sigen;
using SigCompanion.Lib.Sigen.Models;
using SigCompanion.Models;
using SigCompanion.Services;

namespace SigCompanion.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        private readonly SigenUiApi _sigenUiApi;
        private readonly AppSettingsStore _settingsStore;
        private AppSettings _settings;
        private CancellationTokenSource? _pollingCts;
        private bool _isAuthenticated;

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
            batteryStatus = "-";
            gridStatus = "-";
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
            currentPage = AppPage.Login;

            signInCommand = new AsyncRelayCommand(SignInAsync);
            openSettingsCommand = new RelayCommand(OpenSettings);
            backToMainCommand = new RelayCommand(BackToMain);
            signOutCommand = new AsyncRelayCommand(SignOutAsync);

            _ = InitializeAsync();
        }

        [ObservableProperty]
        private string username;

        [ObservableProperty]
        private string password;

        [ObservableProperty]
        private bool savePassword;

        [ObservableProperty]
        private string statusMessage;

        [ObservableProperty]
        private ObservableCollection<SigenSystemReference> systems;

        [ObservableProperty]
        private SigenSystemReference? selectedSystem;

        [ObservableProperty]
        private string batterySoc;

        [ObservableProperty]
        private string batteryStatus;

        [ObservableProperty]
        private string gridStatus;

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
        private ObservableCollection<SigenStationCardComparisonRow> historicalStationCards;

        [ObservableProperty]
        private string todayChartDataJson;

        [ObservableProperty]
        private string yesterdayChartDataJson;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private AppPage currentPage;

        public bool IsLoginPageVisible => CurrentPage == AppPage.Login;
        public bool IsMainPageVisible => CurrentPage == AppPage.Main;
        public bool IsSettingsPageVisible => CurrentPage == AppPage.Settings;

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

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                StatusMessage = "Please sign in.";
                CurrentPage = AppPage.Login;
                return;
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
                _settings.SystemId = SelectedSystem.SystemId;
                _settingsStore.Save(_settings);

                CurrentPage = AppPage.Main;
                StatusMessage = "Signed in.";
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

            _settings = new AppSettings();
            _settingsStore.Save(_settings);

            Username = string.Empty;
            Password = string.Empty;
            SavePassword = false;
            SelectedSystem = null;
            Systems.Clear();
            BatterySoc = "-";
            BatteryStatus = "-";
            GridStatus = "-";
            PvPower = "-";
            GridPower = "-";
            EvPower = "-";
            AcPower = "-";
            LoadPower = "-";
            HeatPumpPower = "-";
            BatteryPower = "-";
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
        }

        private void StopPolling()
        {
            _pollingCts?.Cancel();
            _pollingCts?.Dispose();
            _pollingCts = null;
        }

        private async Task PollBatteryStatusLoopAsync(string systemId, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var batteryStatusTask = _sigenUiApi.GetBatteryStatusAsync(systemId, cancellationToken);
                    var powerFlowTask = _sigenUiApi.GetPowerFlowSnapshotAsync(systemId, cancellationToken);
                    var historicalTask = _sigenUiApi.GetHistoricalDayComparisonAsync(systemId, cancellationToken);

                    await Task.WhenAll(batteryStatusTask, powerFlowTask, historicalTask);

                    var snapshot = await batteryStatusTask;
                    var powerFlow = await powerFlowTask;
                    var historical = await historicalTask;

                    BatterySoc = $"{snapshot.BatteryStateOfCharge:0.##}%";
                    BatteryStatus = snapshot.BatteryStatus.ToString();
                    GridStatus = snapshot.IsGridConnected ? "Connected" : "Disconnected";

                    PvPower = FormatPower(powerFlow.PvPower);
                    GridPower = FormatPower(powerFlow.GridPower);
                    EvPower = FormatPower(powerFlow.EvPower);
                    AcPower = FormatPower(powerFlow.AcPower);
                    LoadPower = FormatPower(powerFlow.LoadPower);
                    HeatPumpPower = FormatPower(powerFlow.HeatPumpPower);
                    BatteryPower = FormatPower(powerFlow.BatteryPower);

                    HistoricalStationCards.Clear();
                    foreach (var row in historical.StationCards)
                    {
                        HistoricalStationCards.Add(row);
                    }

                    TodayChartDataJson = JsonSerializer.Serialize(historical.TodayChartData, new JsonSerializerOptions { WriteIndented = false });
                    YesterdayChartDataJson = JsonSerializer.Serialize(historical.YesterdayChartData, new JsonSerializerOptions { WriteIndented = false });

                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;
            }
        }

        private static string FormatPower(decimal power) => $"{power:0.###} kW";

        public enum AppPage
        {
            Login,
            Main,
            Settings,
        }
    }
}
