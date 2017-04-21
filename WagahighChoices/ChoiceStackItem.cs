namespace WagahighChoices
{
    public struct ChoiceStackItem
    {
        public string ScreenshotHash { get; set; }
        public int ChoiceNumber { get; set; }

        public ChoiceStackItem(string screenshotHash, int choiceNumber)
        {
            this.ScreenshotHash = screenshotHash;
            this.ChoiceNumber = choiceNumber;
        }
    }
}
