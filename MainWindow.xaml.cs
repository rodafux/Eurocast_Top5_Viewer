using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Eurocast_Top5_Viewer
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += (s, e) =>
            {
                if (this.DataContext is ViewModels.MainViewModel vm)
                {
                    vm.FlashAppeared -= Vm_FlashAppeared;
                    vm.FlashAppeared += Vm_FlashAppeared;
                }
            };
            this.DataContextChanged += (s, e) =>
            {
                if (e.OldValue is ViewModels.MainViewModel oldVm) oldVm.FlashAppeared -= Vm_FlashAppeared;
                if (e.NewValue is ViewModels.MainViewModel newVm) newVm.FlashAppeared += Vm_FlashAppeared;
            };
        }

        private async void Vm_FlashAppeared(object? sender, EventArgs e)
        {
            // DETECTION MODE FENETRÉ : Si actif, on ne déclenche pas l'animation
            if (this.DataContext is ViewModels.MainViewModel vm && vm.IsWindowed)
            {
                return;
            }

            int retries = 0;
            while ((FlashImageContainer == null || FlashImageContainer.ActualWidth == 0 || this.ActualWidth == 0) && retries < 20)
            {
                await Task.Delay(100);
                retries++;
            }

            if (FlashImageContainer == null || FlashImageContainer.ActualWidth == 0) return;

            Dispatcher.Invoke(() =>
            {
                try
                {
                    this.UpdateLayout();
                    System.Windows.Controls.Panel.SetZIndex(FlashImageContainer, 1000);

                    Point windowCenter = new Point(this.ActualWidth / 2, this.ActualHeight / 2);
                    Point containerCenter = FlashImageContainer.TransformToAncestor(this).Transform(
                        new Point(FlashImageContainer.ActualWidth / 2, FlashImageContainer.ActualHeight / 2));

                    double deltaX = windowCenter.X - containerCenter.X;
                    double deltaY = windowCenter.Y - containerCenter.Y;

                    double scaleX = (this.ActualWidth * 0.98) / FlashImageContainer.ActualWidth;
                    double scaleY = (this.ActualHeight * 0.98) / FlashImageContainer.ActualHeight;
                    double targetScale = Math.Min(scaleX, scaleY);

                    if (targetScale < 1) targetScale = 1.0;

                    var storyboard = new Storyboard();
                    TimeSpan holdTime = TimeSpan.FromSeconds(9);
                    TimeSpan endTime = TimeSpan.FromSeconds(12);

                    var animX = new DoubleAnimationUsingKeyFrames();
                    animX.KeyFrames.Add(new DiscreteDoubleKeyFrame(deltaX, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                    animX.KeyFrames.Add(new DiscreteDoubleKeyFrame(deltaX, KeyTime.FromTimeSpan(holdTime)));
                    animX.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(endTime), new KeySpline(0.25, 1, 0.05, 1)));

                    var animY = new DoubleAnimationUsingKeyFrames();
                    animY.KeyFrames.Add(new DiscreteDoubleKeyFrame(deltaY, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                    animY.KeyFrames.Add(new DiscreteDoubleKeyFrame(deltaY, KeyTime.FromTimeSpan(holdTime)));
                    animY.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(endTime), new KeySpline(0.25, 1, 0.05, 1)));

                    var animScaleX = new DoubleAnimationUsingKeyFrames();
                    animScaleX.KeyFrames.Add(new DiscreteDoubleKeyFrame(targetScale, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                    animScaleX.KeyFrames.Add(new DiscreteDoubleKeyFrame(targetScale, KeyTime.FromTimeSpan(holdTime)));
                    animScaleX.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(endTime), new KeySpline(0.25, 1, 0.05, 1)));

                    var animScaleY = new DoubleAnimationUsingKeyFrames();
                    animScaleY.KeyFrames.Add(new DiscreteDoubleKeyFrame(targetScale, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                    animScaleY.KeyFrames.Add(new DiscreteDoubleKeyFrame(targetScale, KeyTime.FromTimeSpan(holdTime)));
                    animScaleY.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(endTime), new KeySpline(0.25, 1, 0.05, 1)));

                    Storyboard.SetTarget(animX, FlashImageContainer);
                    Storyboard.SetTargetProperty(animX, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[1].(TranslateTransform.X)"));
                    Storyboard.SetTarget(animY, FlashImageContainer);
                    Storyboard.SetTargetProperty(animY, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[1].(TranslateTransform.Y)"));
                    Storyboard.SetTarget(animScaleX, FlashImageContainer);
                    Storyboard.SetTargetProperty(animScaleX, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleX)"));
                    Storyboard.SetTarget(animScaleY, FlashImageContainer);
                    Storyboard.SetTargetProperty(animScaleY, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleY)"));

                    storyboard.Children.Add(animX); storyboard.Children.Add(animY); storyboard.Children.Add(animScaleX); storyboard.Children.Add(animScaleY);
                    storyboard.Completed += (s, ev) => System.Windows.Controls.Panel.SetZIndex(FlashImageContainer, 0);
                    storyboard.Begin();
                }
                catch { }
            });
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F11) ToggleFullScreen();
        }

        private void ToggleFullScreen()
        {
            var vm = DataContext as ViewModels.MainViewModel;
            if (WindowStyle == WindowStyle.None)
            {
                WindowStyle = WindowStyle.SingleBorderWindow; WindowState = WindowState.Normal; Topmost = false;
                if (vm != null) vm.IsWindowed = true;
            }
            else
            {
                WindowStyle = WindowStyle.None; WindowState = WindowState.Maximized; Topmost = true;
                if (vm != null) vm.IsWindowed = false;
            }
        }
    }
}