using System;
using System.ComponentModel;
using System.Diagnostics;
using NetCoreEx.Geometry;
using WinApi.Gdi32;
using WinApi.Kernel32;
using WinApi.User32;
using Gdip = System.Drawing;

namespace WagahighChoices
{
    public class WagahighWindowService
    {
        public IntPtr WindowHandle { get; }

        private WagahighWindowService(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero) throw new ArgumentException();

            this.WindowHandle = windowHandle;
        }

        public static WagahighWindowService FindWagahighWindow()
        {
            const string processName = "ワガママハイスペック";
            var processes = Process.GetProcessesByName(processName);
            if (processes.Length == 0) return null;

            var mainWindowHandle = processes[0].MainWindowHandle;

            foreach (var p in processes) p.Dispose();

            return mainWindowHandle == IntPtr.Zero
                ? null
                : new WagahighWindowService(mainWindowHandle);
        }

        private static void ThrowWin32Exception()
        {
            throw new Win32Exception((int)Kernel32Methods.GetLastError());
        }

        public Size GetClientSize()
        {
            if (!User32Methods.GetClientRect(this.WindowHandle, out var rect))
                ThrowWin32Exception();

            return rect.Size;
        }

        public Rectangle GetWindowRect()
        {
            if (!User32Methods.GetWindowRect(this.WindowHandle, out var rect))
                ThrowWin32Exception();

            return rect;
        }

        public Point GetClientPosition()
        {
            var point = new Point(); // 0,0
            if (!User32Methods.ClientToScreen(this.WindowHandle, ref point))
                ThrowWin32Exception();

            return point;
        }

        public Gdip.Bitmap Capture()
        {
            var clientSize = this.GetClientSize();
            var bmp = new Gdip.Bitmap(clientSize.Width, clientSize.Height, Gdip.Imaging.PixelFormat.Format24bppRgb);

            using (var g = Gdip.Graphics.FromImage(bmp))
            {
                var destHdc = g.GetHdc();
                var srcHdc = User32Methods.GetDC(this.WindowHandle);

                if (srcHdc == IntPtr.Zero) ThrowWin32Exception();

                try
                {
                    if (!Gdi32Methods.BitBlt(destHdc, 0, 0, clientSize.Width, clientSize.Height, srcHdc, 0, 0, BitBltFlags.SRCCOPY))
                        ThrowWin32Exception();
                }
                finally
                {
                    User32Methods.ReleaseDC(this.WindowHandle, srcHdc);
                }
            }

            return bmp;
        }

        public Point GetCursorPosition()
        {
            if (!User32Methods.GetCursorPos(out var point))
                ThrowWin32Exception();

            if (!User32Methods.ScreenToClient(this.WindowHandle, ref point))
                ThrowWin32Exception();

            return point;
        }

        public void SetCursorPosition(Point point)
        {
            if (!User32Methods.ClientToScreen(this.WindowHandle, ref point))
                ThrowWin32Exception();

            if (!User32Methods.SetCursorPos(point.X, point.Y))
                ThrowWin32Exception();
        }

        public bool CursorMoveOut()
        {
            var cursorPos = this.GetCursorPosition();
            var clientSize = this.GetClientSize();

            if (cursorPos.X >= 0 && cursorPos.X < clientSize.Width
                && cursorPos.Y >= 0 && cursorPos.Y < clientSize.Height)
            {
                // とりあえず 0,0 に移動（雑）
                if (!User32Methods.SetCursorPos(0, 0))
                    ThrowWin32Exception();
                return true;
            }

            return false;
        }

        public void ActivateWindow()
        {
            if (!User32Helpers.SetWindowPos(this.WindowHandle, HwndZOrder.HWND_TOP, 0, 0, 0, 0, WindowPositionFlags.SWP_NOMOVE | WindowPositionFlags.SWP_NOSIZE))
                ThrowWin32Exception();
        }

        private static readonly Input[] s_clickInputs =
        {
            new Input
            {
                Type = InputType.INPUT_MOUSE,
                Packet =new InputPacket
                {
                    MouseInput = new MouseInput
                    {
                        Flags = MouseInputFlags.MOUSEEVENTF_LEFTDOWN
                    }
                }
            },
            new Input
            {
                Type = InputType.INPUT_MOUSE,
                Packet = new InputPacket
                {
                    MouseInput = new MouseInput
                    {
                        Flags = MouseInputFlags.MOUSEEVENTF_LEFTUP
                    }
                }
            }
        };

        public void MouseClick(Point point)
        {
            this.ActivateWindow();
            this.SetCursorPosition(point);

            if (User32Helpers.SendInput(s_clickInputs) == 0)
                ThrowWin32Exception();
        }
    }
}
