using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Security.Cryptography;
using System.Threading;
using Newtonsoft.Json;
using Realms;
using NativePoint = NetCoreEx.Geometry.Point;
using WpfMedia = System.Windows.Media;

namespace WagahighChoices
{
    public class MainLogic
    {
        private const int ExpectedWidth = 1280;
        private const int ExpectedHeight = 720;

        private static readonly NativePoint s_center = new NativePoint(ExpectedWidth / 2, ExpectedHeight / 2);
        private static readonly NativePoint s_quickSavePos = new NativePoint(735, 705);
        private static readonly NativePoint s_quickLoadPos = new NativePoint(810, 705);
        private static readonly NativePoint s_nextChoicePos = new NativePoint(1120, 705);
        private static readonly NativePoint s_yesPos = new NativePoint(535, 360);
        private static readonly NativePoint s_choice1Pos = new NativePoint(640, 220);
        private static readonly NativePoint s_choice2Pos = new NativePoint(640, 305);

        private static readonly JsonSerializer s_serializer = JsonSerializer.Create();

        private static readonly RealmConfiguration s_realmConfig =
            new RealmConfiguration(Path.Combine(Path.GetDirectoryName(typeof(MainLogic).Assembly.Location), "default.realm"))
            {
                ShouldDeleteIfMigrationNeeded = true
            };

        public static void DeleteDatabase() => Realm.DeleteRealm(s_realmConfig);

        private readonly WagahighWindowService _windowService;

        public MainLogic(WagahighWindowService windowService)
        {
            this._windowService = windowService;
        }

        private Bitmap Capture()
        {
            var clientSize = this._windowService.GetClientSize();
            if (clientSize.Width != ExpectedWidth || clientSize.Height != ExpectedHeight)
                throw new BadWindowSizeException();

            this._windowService.ActivateWindow();
            this._windowService.CursorMoveOut();
            Thread.Sleep(100);

            return this._windowService.Capture();
        }

        private static unsafe string ComputeHash(Bitmap bmp)
        {
            char HexChar(uint i) => (char)(
                i <= 9
                    ? '0' + i
                    : 'a' + (i - 10)
            );

            var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            byte[] bs;

            try
            {
                using (var stream = new UnmanagedMemoryStream((byte*)data.Scan0, 3L * data.Width * data.Height))
                using (var md5 = MD5.Create())
                    bs = md5.ComputeHash(stream);
            }
            finally
            {
                bmp.UnlockBits(data);
            }

            // 16進法に変換
            var s = new string('\0', bs.Length * 2);
            fixed (char* p = s)
            {
                for (var i = 0; i < bs.Length; i++)
                {
                    var b = (uint)bs[i];
                    p[i * 2] = HexChar(b >> 4);
                    p[i * 2 + 1] = HexChar(b & 0b1111);
                }
            }

            return s;
        }

        public (WpfMedia.ImageSource Image, string Hash) GetScreenshotAndHash()
        {
            using (var bmp = this.Capture())
            {
                var img = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    bmp.GetHbitmap(),
                    IntPtr.Zero,
                    new System.Windows.Int32Rect(0, 0, bmp.Width, bmp.Height),
                    WpfMedia.Imaging.BitmapSizeOptions.FromEmptyOptions()
                );
                img.Freeze();

                return (img, ComputeHash(bmp));
            }
        }

        public void SaveChoice(string screenshotHash, string choice1, string choice2)
        {
            using (var realm = Realm.GetInstance(s_realmConfig))
            {
                realm.Write(() => realm.Add(new ChoiceWindowInfo()
                {
                    ScreenshotHash = screenshotHash,
                    Choice1 = choice1 ?? "選択肢1",
                    Choice2 = choice2 ?? "選択肢2"
                }));
            }
        }

        public void SaveRoute(string screenshotHash, string routeName)
        {
            using (var realm = Realm.GetInstance(s_realmConfig))
            {
                realm.Write(() => realm.Add(new ChoiceWindowInfo()
                {
                    ScreenshotHash = screenshotHash,
                    RouteName = routeName ?? screenshotHash
                }));
            }
        }

        public static ChoiceWindowInfo GetChoiceWindowInfo(string screenshotHash)
        {
            using (var realm = Realm.GetInstance(s_realmConfig))
                return realm.Find<ChoiceWindowInfo>(screenshotHash);
        }

        public static ChoiceWindowInfo[] GetAllChoiceWindowInfo()
        {
            using (var realm = Realm.GetInstance(s_realmConfig))
                return realm.All<ChoiceWindowInfo>().ToArray();
        }

        public IObservable<string /* ScreenshotHash */> TraceChoices(CancellationToken cancellationToken)
        {
            var subject = new Subject<string>();

            var thread = new Thread(this.TraceChoicesCore)
            {
                IsBackground = true
            };

            thread.Start(Tuple.Create(subject, cancellationToken));

            return subject;
        }

        private void TraceChoicesCore(object arg)
        {
            var (subject, cancellationToken) = (Tuple<Subject<string>, CancellationToken>)arg;

            try
            {
                this.QuickSave();

                using (var writer = new StreamWriter(DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json"))
                {
                    writer.Write('[');

                    var isFirst = true;
                    var choiceTracer = new ChoiceTracer();

                    while (true)
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        var hash = this.GetScreenshotHashSkippingMovie();
                        var (actions, foundRoute) = choiceTracer.Next(hash);

                        if (foundRoute.HasValue)
                        {
                            subject.OnNext(foundRoute.Value.RouteScreenshotHash);

                            if (!isFirst) writer.Write(',');
                            s_serializer.Serialize(writer, foundRoute.Value);
                        }

                        if (actions == null || actions.Count == 0)
                        {
                            subject.OnCompleted();
                            break;
                        }

                        foreach (var action in actions)
                        {
                            if (cancellationToken.IsCancellationRequested) break;

                            switch (action)
                            {
                                case ChoiceAction.Select1:
                                case ChoiceAction.Select2:
                                    this._windowService.MouseClick(
                                        action == ChoiceAction.Select1 ? s_choice1Pos : s_choice2Pos);
                                    Thread.Sleep(500);
                                    this.Skip();
                                    break;
                                case ChoiceAction.GoToStart:
                                    this.QuickLoad();
                                    break;
                                default:
                                    throw new InvalidOperationException("不正な ChoiceAction です。");
                            }
                        }
                    }

                    writer.Write(']');
                }
            }
            catch (Exception ex)
            {
                subject.OnError(ex);
            }
        }

        private string GetScreenshotHashSkippingMovie()
        {
            using (var bmp = this.Capture())
            {
                var px = bmp.GetPixel(ExpectedWidth - 1, ExpectedHeight - 1);
                if (px.R >= 220 && px.G < 220 && px.B < 220)
                    return ComputeHash(bmp);
            }

            // 一番右下の色が赤っぽくなかったらムービーだと判断してスキップ処理を入れる
            this._windowService.MouseClick(s_center);
            Thread.Sleep(6000);

            using (var bmp = this.Capture())
                return ComputeHash(bmp);
        }

        private void ClickYes()
        {
            this._windowService.MouseClick(s_yesPos);
            Thread.Sleep(3000);
        }

        private void QuickSave()
        {
            this._windowService.MouseClick(s_quickSavePos);
            Thread.Sleep(3000);
        }

        private void QuickLoad()
        {
            this._windowService.MouseClick(s_quickLoadPos);
            Thread.Sleep(500);
            this.ClickYes();
        }

        private void Skip()
        {
            this._windowService.MouseClick(s_nextChoicePos);
            Thread.Sleep(500);
            this.ClickYes();
        }
    }
}
