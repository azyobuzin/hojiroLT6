using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using WinApi.User32;

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

        private WagahighWindowService TryGetService()
        {
            var service = WagahighWindowService.FindWagahighWindow();

            if (service == null)
            {
                MessageBox.Show(this, "ウィンドウを見つけられませんでした。");
            }

            return service;
        }

        private void DoWithService(Action<WagahighWindowService> action)
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
                action(service);
            }
            catch (Exception ex)
            {
                if (Debugger.IsAttached)
                    Debugger.Break();

                ShowError(ex.Message);
            }
        }

        private void btnSaveAsChoice_Click(object sender, RoutedEventArgs e)
        {
            DoWithService(service =>
            {
                service.ActivateWindow();
                service.MouseClick(new NetCoreEx.Geometry.Point(600, 300));

                //using (var bmp = service.Capture())
                //    bmp.Save("capture.png");
            });
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
    }
}
