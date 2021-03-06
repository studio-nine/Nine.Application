﻿namespace Nine.Application
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices.WindowsRuntime;
    using System.Threading;
    using System.Threading.Tasks;
    using NotificationsExtensions.ToastContent;
    using Windows.ApplicationModel;
    using Windows.ApplicationModel.DataTransfer;
    using Windows.Foundation.Metadata;
    using Windows.Graphics.Display;
    using Windows.Graphics.Imaging;
    using Windows.Storage.Streams;
    using Windows.System;
    using Windows.UI;
    using Windows.UI.Core;
    using Windows.UI.Notifications;
    using Windows.UI.Popups;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Media;
    using Windows.UI.Xaml.Media.Animation;
    using Windows.UI.Xaml.Media.Imaging;

    public partial class AppUI : IAppUI
    {
        private readonly SemaphoreSlim _toastQueue = new SemaphoreSlim(1);
        private bool _showingInAppNotification;
        private bool _isWindowActive;

        public AppUI()
        {
            Window.Current.Activated += (a, b) => _isWindowActive = b.WindowActivationState != CoreWindowActivationState.Deactivated;
        }

        public async Task<bool> Confirm(string title, string message, string yes, string no, CancellationToken cancellation)
        {
            var dialog = new MessageDialog(message, title);
            var yesCommand = new UICommand(yes);
            dialog.Commands.Clear();
            dialog.Commands.Add(yesCommand);
            if (!string.IsNullOrEmpty(no)) dialog.Commands.Add(new UICommand(no, x => { }));

            var run = dialog.ShowAsync();
            cancellation.Register(() => run.Cancel());

            try
            {
                return yesCommand == await run;
            }
            catch (TaskCanceledException)
            {
                return false;
            }
        }

        private async Task<bool> InAppNotify(string title, string message, CancellationToken cancellation)
        {
            if (_showingInAppNotification || cancellation.IsCancellationRequested) return false;

            _showingInAppNotification = true;

            var grid = FindChild<Grid>(Window.Current.Content);
            if (grid == null)
            {
                _showingInAppNotification = false;
                return false;
            }

            try
            {
                var text = string.Join(": ", new[] { title, message }.Where(str => !string.IsNullOrEmpty(str)));

                var content = new Border
                {
                    BorderThickness = new Thickness(0),
                    Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)),
                    Margin = new Thickness(0),
                    Padding = new Thickness(12, 18, 12, 18),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Top,
                };

                content.Child = new TextBlock
                {
                    Text = text,
                    MaxLines = 2,
                    MaxWidth = Math.Min(grid.ActualWidth * 0.8, 320),
                    TextWrapping = TextWrapping.Wrap,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Foreground = new SolidColorBrush(Colors.White)
                };

                if (grid.RowDefinitions.Count > 0) Grid.SetRowSpan(content, grid.RowDefinitions.Count);
                if (grid.ColumnDefinitions.Count > 0) Grid.SetColumnSpan(content, grid.ColumnDefinitions.Count);

                grid.Children.Add(content);
                content.Opacity = 0;

                var fadeIn = new Storyboard();
                var fadeInAnim = new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromSeconds(0.2) };
                fadeIn.Children.Add(fadeInAnim);
                Storyboard.SetTarget(fadeInAnim, content);
                Storyboard.SetTargetProperty(fadeInAnim, "Opacity");
                fadeIn.Begin();

                var result = false;

                if (!cancellation.IsCancellationRequested)
                {
                    var cts = new TaskCompletionSource<bool>();
                    cancellation.Register(() => cts.TrySetResult(false));
                    content.PointerPressed += (sender, e) => { result = true; cts.TrySetResult(true); };
                    await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(4 - 0.2)), cts.Task);
                }

                var fade = new Func<Task>(async () =>
                {
                    var fadeOut = new Storyboard();
                    var fadeOutAnim = new DoubleAnimation { To = 0, Duration = TimeSpan.FromSeconds(0.2) };
                    fadeOut.Children.Add(fadeOutAnim);
                    Storyboard.SetTarget(fadeOutAnim, content);
                    Storyboard.SetTargetProperty(fadeOutAnim, "Opacity");
                    fadeOut.Begin();

                    await Task.Delay(TimeSpan.FromSeconds(0.2));

                    grid.Children.Remove(content);
                });

                fade();
                return result;
            }
            finally
            {
                _showingInAppNotification = false;
            }
        }

        public async void Toast(string title, string message)
        {
            await _toastQueue.WaitAsync();

            var grid = FindChild<Grid>(Window.Current.Content);
            if (grid == null)
            {
                _toastQueue.Release();
                return;
            }

            try
            {
                var text = string.Join(": ", new[] { title, message }.Where(str => !string.IsNullOrEmpty(str)));

                var content = new Border
                {
                    BorderThickness = new Thickness(0),
                    Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)),
                    Margin = new Thickness(0, 0, 0, 24),
                    Padding = new Thickness(12, 8, 12, 8),
                    CornerRadius = new CornerRadius(12),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                };

                content.Child = new TextBlock
                {
                    Text = text,
                    MaxLines = 2,
                    MaxWidth = Math.Min(grid.ActualWidth * 0.8, 320),
                    TextWrapping = TextWrapping.Wrap,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Foreground = new SolidColorBrush(Colors.White)
                };

                if (grid.RowDefinitions.Count > 0) Grid.SetRowSpan(content, grid.RowDefinitions.Count);
                if (grid.ColumnDefinitions.Count > 0) Grid.SetColumnSpan(content, grid.ColumnDefinitions.Count);

                grid.Children.Add(content);
                content.Opacity = 0;

                var fadeIn = new Storyboard();
                var fadeInAnim = new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromSeconds(0.2) };
                fadeIn.Children.Add(fadeInAnim);
                Storyboard.SetTarget(fadeInAnim, content);
                Storyboard.SetTargetProperty(fadeInAnim, "Opacity");
                fadeIn.Begin();

                await Task.Delay(TimeSpan.FromSeconds(4 - 0.2));

                var fadeOut = new Storyboard();
                var fadeOutAnim = new DoubleAnimation { To = 0, Duration = TimeSpan.FromSeconds(0.2) };
                fadeOut.Children.Add(fadeOutAnim);
                Storyboard.SetTarget(fadeOutAnim, content);
                Storyboard.SetTargetProperty(fadeOutAnim, "Opacity");
                fadeOut.Begin();

                await Task.Delay(TimeSpan.FromSeconds(0.2));

                grid.Children.Remove(content);
            }
            finally
            {
                _toastQueue.Release();
            }
        }

        private static T FindChild<T>(DependencyObject obj) where T : DependencyObject
        {
            if (obj == null) return null;

            var result = obj as T;

            if (result != null) return result;

            var count = VisualTreeHelper.GetChildrenCount(obj);

            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);

                var t = FindChild<T>(child);

                if (t != null) return t;
            }

            return null;
        }

        public Task<bool> Notify(string title, string message, CancellationToken cancellation)
        {
            if (!ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                if (_isWindowActive)
                {
                    return InAppNotify(title, message, cancellation);
                }
            }

            var templateContent = new ToastText02();
            templateContent.TextHeading.Text = title;
            templateContent.TextBodyWrap.Text = message;

            var tcs = new TaskCompletionSource<bool>();
            var notification = templateContent.CreateNotification();
            notification.Activated += (sender, e) => tcs.TrySetResult(true);
            notification.Failed += (sender, e) => tcs.TrySetResult(false);
            notification.Dismissed += (sender, e) => tcs.TrySetResult(false);

            var notifier = ToastNotificationManager.CreateToastNotifier();

            cancellation.Register(() =>
            {
                notifier.Hide(notification);
                tcs.TrySetResult(false);
            });

            notifier.Show(notification);
            return tcs.Task;
        }

        public Task<int?> Select(string title, int? selectedIndex, IEnumerable<string> items, CancellationToken cancellation)
        {
            throw new NotImplementedException();
        }

        public async Task<string> Input(string title, string defaultText, string yes, bool password, CancellationToken cancellation)
        {
            defaultText = defaultText ?? "";

            var tcs = new TaskCompletionSource<string>();

            var dialog = new ContentDialog
            {
                Title = title ?? "",
                PrimaryButtonText = yes,
                IsPrimaryButtonEnabled = true,
                IsSecondaryButtonEnabled = false,
            };

            if (password)
            {
                var passwordBox = new PasswordBox { Password = defaultText, MaxLength = 20, Margin = new Thickness(0, 6, 0, 0) };
                passwordBox.SelectAll();
                passwordBox.KeyDown += (s, e) =>
                {
                    if (e.Key == VirtualKey.Enter) { tcs.TrySetResult(passwordBox.Password); }
                };

                dialog.Content = passwordBox;
                dialog.PrimaryButtonClick += (a, b) => tcs.TrySetResult(passwordBox.Password);
            }
            else
            {
                var input = new TextBox { Text = defaultText, AcceptsReturn = false, MaxLength = 140, Margin = new Thickness(0, 6, 0, 0) };

                input.SelectAll();
                input.KeyDown += (s, e) =>
                {
                    if (e.Key == VirtualKey.Enter) { tcs.TrySetResult(input.Text); }
                };

                dialog.Content = input;
                dialog.PrimaryButtonClick += (a, b) => tcs.TrySetResult(input.Text);
            }

            dialog.Closed += (a, b) => tcs.TrySetResult(null);

            var run = dialog.ShowAsync();
            cancellation.Register(() => tcs.TrySetResult(null));

            var result = await tcs.Task;
            run.Cancel();
            return result;
        }

        public void CopyToClipboard(string text)
        {
            var content = new DataPackage();
            content.SetText(text);
            Clipboard.SetContent(content);
        }

        public Task<Stream> CaptureScreenshot()
        {
            return CaptureScreenshot(Window.Current.Content);
        }

        public async Task<Stream> CaptureScreenshot(UIElement element)
        {
            var renderTargetBitmap = new RenderTargetBitmap();
            await renderTargetBitmap.RenderAsync(element);
            var pixelBuffer = await renderTargetBitmap.GetPixelsAsync();
            var ms = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, ms);
            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Ignore,
                (uint)renderTargetBitmap.PixelWidth,
                (uint)renderTargetBitmap.PixelHeight,
                DisplayInformation.GetForCurrentView().LogicalDpi,
                DisplayInformation.GetForCurrentView().LogicalDpi,
                pixelBuffer.ToArray());
            await encoder.FlushAsync();
            return ms.AsStreamForRead();
        }

        public async void RateMe()
        {
            var pfn = Package.Current.Id.FamilyName;
            await Launcher.LaunchUriAsync(new Uri("ms-windows-store:REVIEW?PFN=" + pfn));
        }

        public async void Browse(string url)
        {
            await Launcher.LaunchUriAsync(new Uri(url));
        }
    }
}
