using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WagahighChoices
{
    public class MainLogic
    {
        private const int ExpectedWidth = 1280;
        private const int ExpectedHeight = 720;

        private readonly WagahighWindowService _windowService;

        public MainLogic(WagahighWindowService windowService)
        {
            this._windowService = windowService;
        }

        private void EnsureClientSize()
        {
            var clientSize = this._windowService.GetClientSize();
            if (clientSize.Width != ExpectedWidth || clientSize.Height != ExpectedHeight)
                throw new BadWindowSizeException();
        }

        public UnmanagedMemoryStream Capture()
        {
            this.EnsureClientSize();

            var bmp = this._windowService.Capture();
            return BitmapStream.Create(bmp);
        }


    }
}
