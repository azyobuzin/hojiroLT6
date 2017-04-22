using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using Newtonsoft.Json;
using Realms;
using Shipwreck.Phash;
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
            Thread.Sleep(200);

            return this._windowService.Capture();
        }

        private static string ComputeHash(Bitmap bmp)
        {
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                ms.Position = 0;

                // どうせ中で WPF の Bitmap に変換されるのですごい無駄感ある
                return ImagePhash.ComputeDigest(ms).ToString();
            }
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
                realm.Write(() => realm.Add(new ChoiceWindowInfoRealmObject()
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
                realm.Write(() => realm.Add(new ChoiceWindowInfoRealmObject()
                {
                    ScreenshotHash = screenshotHash,
                    RouteName = routeName ?? screenshotHash
                }));
            }
        }

        public static ChoiceWindowInfo GetChoiceWindowInfo(string screenshotHash)
        {
            using (var realm = Realm.GetInstance(s_realmConfig))
            {
                var ro = realm.Find<ChoiceWindowInfoRealmObject>(screenshotHash);
                return ro == null ? null
                    : new ChoiceWindowInfo
                    {
                        ScreenshotHash = ro.ScreenshotHash,
                        Choice1 = ro.Choice1,
                        Choice2 = ro.Choice2,
                        RouteName = ro.RouteName
                    };
            }
        }

        public static ChoiceWindowInfo[] GetAllChoiceWindowInfo()
        {
            using (var realm = Realm.GetInstance(s_realmConfig))
                return realm.All<ChoiceWindowInfoRealmObject>()
                    .AsEnumerable()
                    .Select(ro => new ChoiceWindowInfo
                    {
                        ScreenshotHash = ro.ScreenshotHash,
                        Choice1 = ro.Choice1,
                        Choice2 = ro.Choice2,
                        RouteName = ro.RouteName
                    })
                    .ToArray();
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
                using (var writer = new JsonTextWriter(new StreamWriter(DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json")))
                {
                    writer.Formatting = Formatting.Indented;

                    // JSON ヘッダー書き込み
                    writer.WriteStartObject();
                    writer.WritePropertyName("ChoiceWindowInfo");
                    s_serializer.Serialize(writer, GetAllChoiceWindowInfo());
                    writer.WritePropertyName("FoundRoutes");
                    writer.WriteStartArray();
                    writer.Flush();

                    var stopwatch = Stopwatch.StartNew();

                    this.QuickSave();

                    var choiceTracer = new ChoiceTracer();

                    while (true)
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        var hash = this.GetScreenshotHashSkippingMovie();
                        var (actions, foundRoute) = choiceTracer.Next(hash);

                        if (foundRoute.HasValue)
                        {
                            subject.OnNext(foundRoute.Value.RouteScreenshotHash);

                            s_serializer.Serialize(writer, foundRoute.Value);
                            writer.Flush();
                        }

                        if (actions == null || actions.Count == 0)
                        {
                            subject.OnCompleted();
                            break;
                        }

                        foreach (var action in actions)
                        {
                            if (cancellationToken.IsCancellationRequested) break;

                            // ムービーチェック
                            var currentHash = this.GetScreenshotHashSkippingMovie();

                            switch (action)
                            {
                                case ChoiceAction.Select1:
                                case ChoiceAction.Select2:
                                    this._windowService.MouseClick(
                                        action == ChoiceAction.Select1 ? s_choice1Pos : s_choice2Pos);
                                    Thread.Sleep(
                                        HashEquals(currentHash, "0x9CE16F0082FED687619F967AA3BFA99EBBB3938EB7BB9CA290919C7B92AF918B93A07E9FABB99B9C")
                                            ? 6000 // 「調理場を手伝う」は待機時間が長い
                                            : 1700
                                    );
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

                    stopwatch.Stop();

                    writer.WriteEndArray();
                    writer.WritePropertyName("ElapsedTime");
                    s_serializer.Serialize(writer, stopwatch.Elapsed);
                    writer.WriteEndObject();
                }
            }
            catch (Exception ex)
            {
                if (Debugger.IsAttached)
                    Debugger.Break();

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
            this.Skip();

            using (var bmp = this.Capture())
                return ComputeHash(bmp);
        }

        private void ClickYes()
        {
            this._windowService.MouseClick(s_yesPos);
            Thread.Sleep(3500);
        }

        private void QuickSave()
        {
            this._windowService.MouseClick(s_quickSavePos);
            Thread.Sleep(100);
            this._windowService.CursorMoveOut();
            Thread.Sleep(4000);
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

        private static bool HashEquals(string x, string y)
        {
            if (x.Length != y.Length) return false;

            var c = 0;
            for (var i = 0; i < x.Length; i++)
            {
                if (x[i] != y[i]) c++;
                if (c > 5) return false; // 基準値は2で
            }

            return true;
        }
    }
}
