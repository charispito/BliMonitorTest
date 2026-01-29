using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace BliMonitorTest.ToastMessage
{
    public sealed class ToastService
    {
        private static readonly Lazy<ToastService> _instance = new Lazy<ToastService>(() => new ToastService());
        public static ToastService Instance => _instance.Value;

        private ToastService() { }

        public static class AppToast
        {
            public static void Show(string message, int ms = 2000)
                => BliMonitorTest.ToastMessage.ToastService.Instance.Show(message, ms);
        }

        // 외부에서 호출하는 공용 API
        public void Show(string message, int milliseconds = 2000)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var win = GetActiveWindow();
                if (win == null) return;

                var visuals = EnsureToastLayer(win);
                visuals.Text.Text = message ?? "";
                visuals.Host.Visibility = Visibility.Visible;

                // 타이머 초기화 및 재시작
                visuals.Timer.Stop();
                FadeIn(visuals.Border);
                visuals.Timer.Interval = TimeSpan.FromMilliseconds(milliseconds);
                visuals.Timer.Start();
            });
        }

        // 현재 활성 창(포어그라운드) 탐색
        private Window GetActiveWindow()
        {
            var active = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
            if (active != null) return active;

            var topMost = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.Topmost);
            if (topMost != null) return topMost;

            return Application.Current.Windows.OfType<Window>().Cast<Window>().LastOrDefault();
        }

        // 창에 토스트 레이어가 없으면 동적으로 생성하고, 관련 요소를 묶은 상태 객체 반환
        private ToastVisuals EnsureToastLayer(Window window)
        {
            const string HostName = "ToastHost_Auto";
            const string BorderName = "ToastBorder_Auto";
            const string TextName = "ToastText_Auto";

            // 루트 컨테이너 확보
            var root = window.Content as Panel;
            if (root == null)
            {
                var oldContent = window.Content as UIElement;
                var grid = new Grid();
                window.Content = grid;
                if (oldContent != null) grid.Children.Add(oldContent);
                root = grid;
            }

            // 기존 호스트가 있으면 재사용
            var host = root.Children.OfType<Grid>().FirstOrDefault(g => g.Name == HostName);
            Border border;
            TextBlock text;
            DispatcherTimer timer;

            if (host == null)
            {
                host = new Grid
                {
                    Name = HostName,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0),
                    IsHitTestVisible = false,
                    Visibility = Visibility.Collapsed
                };
                Panel.SetZIndex(host, 9999);

                border = new Border
                {
                    Name = BorderName,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 8, 8, 0),
                    CornerRadius = new CornerRadius(6),
                    Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#CC111827"),
                    Padding = new Thickness(14, 8, 14, 8),
                    Opacity = 0.0
                };

                text = new TextBlock
                {
                    Name = TextName,
                    Foreground = System.Windows.Media.Brushes.White,
                    FontSize = 14,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                border.Child = text;

                host.Children.Add(border);
                root.Children.Add(host);

                // 창마다 타이머 1개
                timer = new DispatcherTimer();
                // 람다에서 ref/out을 쓰지 않음 → 컴파일 오류 없음
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    FadeOut(border, host);
                };

                // 요소들을 Tag에 묶어서 저장
                var visuals = new ToastVisuals(host, border, text, timer);
                host.Tag = visuals;
                return visuals;
            }
            else
            {
                // 이미 생성된 경우 Tag에서 꺼내 재사용
                var visuals = host.Tag as ToastVisuals;
                if (visuals != null) return visuals;

                // 만약 Tag가 없으면 구성 요소를 찾아 재구성
                border = host.Children.OfType<Border>().FirstOrDefault(b => b.Name == BorderName);
                if (border == null)
                {
                    border = new Border
                    {
                        Name = BorderName,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 8, 8, 0),
                        CornerRadius = new CornerRadius(6),
                        Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#CC111827"),
                        Padding = new Thickness(14, 8, 14, 8),
                        Opacity = 0.0
                    };
                    host.Children.Add(border);
                }

                text = border.Child as TextBlock;
                if (text == null)
                {
                    text = new TextBlock
                    {
                        Name = TextName,
                        Foreground = System.Windows.Media.Brushes.White,
                        FontSize = 14,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    };
                    border.Child = text;
                }

                timer = new DispatcherTimer();
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    FadeOut(border, host);
                };

                visuals = new ToastVisuals(host, border, text, timer);
                host.Tag = visuals;
                return visuals;
            }
        }

        private static void FadeIn(Border toastBorder)
        {
            var anim = new DoubleAnimation
            {
                From = toastBorder.Opacity,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(180),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            toastBorder.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        private static void FadeOut(Border toastBorder, Grid toastHost)
        {
            var anim = new DoubleAnimation
            {
                From = toastBorder.Opacity,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            anim.Completed += (s, e) =>
            {
                toastBorder.Opacity = 0.0;
                toastHost.Visibility = Visibility.Collapsed;
            };
            toastBorder.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        // 상태 객체: 창마다 토스트 관련 요소를 묶어서 보관
        private sealed class ToastVisuals
        {
            public Grid Host { get; }
            public Border Border { get; }
            public TextBlock Text { get; }
            public DispatcherTimer Timer { get; }

            public ToastVisuals(Grid host, Border border, TextBlock text, DispatcherTimer timer)
            {
                Host = host;
                Border = border;
                Text = text;
                Timer = timer;
            }
        }
    }
}
