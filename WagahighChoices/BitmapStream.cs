using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace WagahighChoices
{
    internal sealed class BitmapStream : UnmanagedMemoryStream
    {
        private readonly Bitmap _bmp;
        private readonly BitmapData _bmpData;

        private unsafe BitmapStream(Bitmap bmp, BitmapData bmpData)
            : base((byte*)bmpData.Scan0, bmpData.Width * bmpData.Height * 3)
        {
            this._bmp = bmp;
            this._bmpData = bmpData;
        }

        public static BitmapStream Create(Bitmap bmp)
        {
            var bmpData = bmp.LockBits(
                new Rectangle(0, 0, bmp.Width, bmp.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format24bppRgb
            );

            return new BitmapStream(bmp, bmpData);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            this._bmp.UnlockBits(this._bmpData);

            if (disposing)
                this._bmp.Dispose();
        }
    }
}
