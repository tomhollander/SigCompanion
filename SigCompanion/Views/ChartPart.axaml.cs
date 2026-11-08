using System.ComponentModel;
using Avalonia.Controls;
using SigCompanion.ViewModels;

namespace SigCompanion.Views
{
    public partial class ChartPart : UserControl
    {
        private MainViewModel? _subscribedViewModel;

        public ChartPart()
        {
            InitializeComponent();
            DataContextChanged += OnChartDataContextChanged;
        }

        private void OnChartDataContextChanged(object? sender, System.EventArgs e)
        {
            if (_subscribedViewModel != null)
            {
                _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            _subscribedViewModel = DataContext as MainViewModel;

            if (_subscribedViewModel != null)
            {
                _subscribedViewModel.PropertyChanged += OnViewModelPropertyChanged;
                SyncHistoricalChart(_subscribedViewModel);
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is MainViewModel vm &&
                (e.PropertyName == nameof(MainViewModel.HistoricalPowerSeries) ||
                 e.PropertyName == nameof(MainViewModel.HistoricalXAxes) ||
                 e.PropertyName == nameof(MainViewModel.HistoricalYAxes)))
            {
                SyncHistoricalChart(vm);
            }
        }

        private void SyncHistoricalChart(MainViewModel vm)
        {
            HistoricalPowerChart.Series = vm.HistoricalPowerSeries;
            HistoricalPowerChart.XAxes = vm.HistoricalXAxes;
            HistoricalPowerChart.YAxes = vm.HistoricalYAxes;
        }
    }
}
