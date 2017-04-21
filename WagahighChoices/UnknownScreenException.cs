using System;

namespace WagahighChoices
{
    public class UnknownScreenException : Exception
    {
        public UnknownScreenException() : base("記録されていない画面です。") { }
    }
}
