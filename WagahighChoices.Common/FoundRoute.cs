using System.Collections.Immutable;

namespace WagahighChoices
{
    public struct FoundRoute
    {
        public string RouteScreenshotHash { get; set; }
        public ImmutableArray<ChoiceStackItem> ChoiceStack { get; set; }

        public FoundRoute(string routeScreenshotHash, ImmutableArray<ChoiceStackItem> choiceStack)
        {
            this.RouteScreenshotHash = routeScreenshotHash;
            this.ChoiceStack = choiceStack;
        }
    }
}
