using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace WagahighChoices
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        private DispatcherTimer _pointerTimer;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void UpdateCursorLabel()
        {
            string status;

            try
            {
                var service = WagahighWindowService.FindWagahighWindow();

                if (service == null)
                {
                    status = "ウィンドウが見つかりません";
                }
                else
                {
                    var clientSize = service.GetClientSize();
                    var cursorPos = service.GetCursorPosition();
                    status = $"クライアント領域のサイズ: {clientSize.Width}, {clientSize.Height}\nカーソル位置: {cursorPos.X}, {cursorPos.Y}";
                }
            }
            catch (Exception ex)
            {
                status = ex.Message;
            }

            this.lblStatus.Text = status;
        }

        private void lblStatus_Loaded(object sender, RoutedEventArgs e)
        {
            this._pointerTimer = new DispatcherTimer(
                new TimeSpan(TimeSpan.TicksPerMillisecond * 100),
                DispatcherPriority.Background,
                (_, __) => this.UpdateCursorLabel(),
                this.Dispatcher
            );
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            this._pointerTimer?.Stop();
        }

        private void DoLogic(Action<MainLogic> action)
        {
            void ShowError(string message) =>
                MessageBox.Show(this, message, "WagahighChoices エラー", MessageBoxButton.OK, MessageBoxImage.Error);

            var service = WagahighWindowService.FindWagahighWindow();

            if (service == null)
            {
                ShowError("ウィンドウを見つかりませんでした。");
                return;
            }

            try
            {
                action(new MainLogic(service));
            }
            catch (Exception ex)
            {
                if (Debugger.IsAttached)
                    Debugger.Break();

                ShowError(ex.Message);
            }
        }

        private bool CheckAlreadySaved(string screenshotHash)
        {
            var info = MainLogic.GetChoiceWindowInfo(screenshotHash);
            if (info == null) return true;

            MessageBox.Show(this, $@"この画面は既に記録されています。
選択肢1: {info.Choice1 ?? "(null)"}
選択肢2: {info.Choice2 ?? "(null)"}
ルート名: { info.RouteName ?? "(null)" }");

            return false;
        }

        private void btnSaveAsChoice_Click(object sender, RoutedEventArgs e)
        {
            DoLogic(logic =>
            {
                var (img, hash) = logic.GetScreenshotAndHash();

                if (!this.CheckAlreadySaved(hash)) return;

                var bm = new SaveChoiceWindowBindingModel(img);
                var dialogResult = new SaveChoiceWindow { DataContext = bm }
                    .ShowDialog();

                if (!dialogResult.GetValueOrDefault()) return;

                logic.SaveChoice(hash, bm.Choice1, bm.Choice2);
            });
        }

        private void btnSaveAsRoute_Click(object sender, RoutedEventArgs e)
        {
            DoLogic(logic =>
            {
                var (img, hash) = logic.GetScreenshotAndHash();

                if (!this.CheckAlreadySaved(hash)) return;

                var bm = new SaveRouteWindowBindingModel(img);
                var dialogResult = new SaveRouteWindow { DataContext = bm }
                    .ShowDialog();

                if (!dialogResult.GetValueOrDefault()) return;

                logic.SaveRoute(hash, bm.RouteName);
            });
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            DoLogic(logic =>
            {
                var routeStatuses = MainLogic.GetAllChoiceWindowInfo()
                    .Where(x => x.RouteName != null)
                    .ToDictionary(x => x.ScreenshotHash, x => new RouteStatusBindingModel(x.RouteName));

                var cts = new CancellationTokenSource();

                var bm = new TracingChoicesWindowBindingModel(routeStatuses.Values, cts.Cancel);
                var window = new TracingChoicesWindow
                {
                    DataContext = bm,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this
                };
                window.Show();

                logic.TraceChoices(cts.Token)
                    .Subscribe(
                        hash => routeStatuses[hash].IncrementCount(),
                        ex => bm.SetError(ex.Message),
                        () => bm.SetCompleted()
                    );
            });
        }

        private void btnDeleteDatabase_Click(object sender, RoutedEventArgs e)
        {
            MainLogic.DeleteDatabase();
            MessageBox.Show("削除しました。");
        }
    }
}
