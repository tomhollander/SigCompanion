
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace SigCompanion.Views
{
    public partial class MainView : UserControl
    {
        // Width below which the phone (two-page) layouts are used.
        private const double PhoneWidthThreshold = 500;

        // Horizontal distance (in DIPs) a pointer must travel to count as a swipe.
        private const double SwipeThreshold = 50;

        private int _phonePage;
        private double? _pointerStartX;

        public MainView()
        {
            InitializeComponent();
            SizeChanged += OnViewSizeChanged;
        }

        private void OnViewSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            UpdateLayout(e.NewSize.Width, e.NewSize.Height);
        }

        private void UpdateLayout(double width, double height)
        {
            if (width <= 0 || height <= 0)
            {
                return;
            }

            bool isLandscape = width >= height;
            bool isPhone = isLandscape ? (height < PhoneWidthThreshold) : (width < PhoneWidthThreshold);

            LargeLandscapeLayout.IsVisible = !isPhone && isLandscape;
            LargePortraitLayout.IsVisible = !isPhone && !isLandscape;
            PhoneLandscapeLayout.IsVisible = isPhone && isLandscape;
            PhonePortraitLayout.IsVisible = isPhone && !isLandscape;

            ShowPhonePage(_phonePage);
        }

        private void ShowPhonePage(int page)
        {
            _phonePage = page;

            LandscapePage1.IsVisible = page == 0;
            LandscapePage2.IsVisible = page == 1;
            PortraitPage1.IsVisible = page == 0;
            PortraitPage2.IsVisible = page == 1;
        }

        private void OnPagerPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            _pointerStartX = e.GetPosition(this).X;
        }

        private void OnPagerPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_pointerStartX is not double startX)
            {
                return;
            }

            _pointerStartX = null;
            double delta = e.GetPosition(this).X - startX;

            if (delta <= -SwipeThreshold && _phonePage == 0)
            {
                ShowPhonePage(1);
            }
            else if (delta >= SwipeThreshold && _phonePage == 1)
            {
                ShowPhonePage(0);
            }
        }
    }
}