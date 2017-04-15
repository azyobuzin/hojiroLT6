using System;

namespace WagahighChoices
{
    public class BadWindowSizeException : Exception
    {
        public BadWindowSizeException() : base("クライアント領域のサイズが 1280x720 ではありません。") { }
    }
}
