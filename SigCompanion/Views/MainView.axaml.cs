
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace SigCompanion.Views
{
    public partial class MainView : UserControl
    {
        // Width below which the phone (two-page) layouts are used.
        private const double PhoneWidthThreshold = 500;

        // Horizontal distance (in DIPs) a pointer must travel to count as a swipe.
        private const double SwipeThreshold = 50;

        private const double PageTransitionMilliseconds = 300;

        private int _phonePage;
        private double? _pointerStartX;
        private bool _isTransitioning;

        public MainView()
        {
            InitializeComponent();
            SizeChanged += OnViewSizeChanged;
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            // Retrieve the InsetsManager from the TopLevel control
            var insetsManager = TopLevel.GetTopLevel(this)?.InsetsManager;

            if (insetsManager != null)
            {
                // 1. Tell the window to draw edge-to-edge
                insetsManager.DisplayEdgeToEdgePreference = true;

                // 2. Hide the status bar / system bars
                insetsManager.IsSystemBarVisible = false;
            }
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
                _ = TransitionToPhonePageAsync(1);
            }
            else if (delta >= SwipeThreshold && _phonePage == 1)
            {
                _ = TransitionToPhonePageAsync(0);
            }
        }

        private async Task TransitionToPhonePageAsync(int page)
        {
            if (_isTransitioning || page == _phonePage)
            {
                return;
            }

            _isTransitioning = true;

            try
            {
                Control[] pages = PhoneLandscapeLayout.IsVisible
                    ? new Control[] { LandscapePage1, LandscapePage2 }
                    : new Control[] { PortraitPage1, PortraitPage2 };

                Control outgoing = pages[_phonePage];
                Control incoming = pages[page];

                double shift = (page > _phonePage ? -1 : 1) * outgoing.Bounds.Width;
                _phonePage = page;

                var outgoingTransform = (TranslateTransform)outgoing.RenderTransform!;
                var incomingTransform = (TranslateTransform)incoming.RenderTransform!;

                incoming.IsVisible = true;
                outgoingTransform.X = 0;
                incomingTransform.X = -shift;

                await AnimateSlideAsync(outgoingTransform, incomingTransform, shift);

                outgoing.IsVisible = false;
                outgoingTransform.X = 0;
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        private static async Task AnimateSlideAsync(TranslateTransform outgoing, TranslateTransform incoming, double shift)
        {
            var stopwatch = Stopwatch.StartNew();
            double duration = PageTransitionMilliseconds;
            double elapsed;

            do
            {
                elapsed = stopwatch.Elapsed.TotalMilliseconds;
                double t = Math.Min(elapsed / duration, 1);
                double eased = 1 - Math.Pow(1 - t, 3);

                outgoing.X = shift * eased;
                incoming.X = -shift + shift * eased;

                if (t < 1)
                {
                    await Task.Delay(16);
                }
            }
            while (elapsed < duration);
        }
    }
}