using System;
using System.Collections.Generic;
using System.Linq;

namespace WagahighChoices
{
    public sealed class ChoiceTracer
    {
        private static readonly ChoiceAction[] s_select1Actions = { ChoiceAction.Select1 };

        private readonly List<ChoiceStackItem> _stack = new List<ChoiceStackItem>();

        public (IReadOnlyCollection<ChoiceAction> Actions, FoundRoute? FoundRoute) Next(string screenshotHash)
        {
            if (screenshotHash == this._stack[this._stack.Count - 1].ScreenshotHash)
                throw new InvalidOperationException("前回と同じスクリーンショットです。");

            var info = MainLogic.GetChoiceWindowInfo(screenshotHash);

            if (info == null)
                throw new UnknownScreenException();

            if (info.RouteName != null)
            {
                var foundRoute = new FoundRoute(
                    info.ScreenshotHash,
                    this._stack.ToReversedImmutableArray()
                );

                var nextItem = this.PopStack();

                if (nextItem.HasValue)
                {
                    // 最初から辿りなおす
                    var actions = new List<ChoiceAction>(this._stack.Count + 2);
                    actions.Add(ChoiceAction.GoToStart);
                    actions.AddRange(this._stack
                        .Select(x => x.ChoiceNumber == 1 ? ChoiceAction.Select1 : ChoiceAction.Select2));
                    actions.Add(ChoiceAction.Select2);
                    return (actions, foundRoute);
                }

                return (Array.Empty<ChoiceAction>(), foundRoute);
            }

            return (s_select1Actions, null);
        }

        private ChoiceStackItem? PopStack()
        {
            while (this._stack.Count > 0)
            {
                var index = this._stack.Count - 1;
                var item = this._stack[index];
                this._stack.RemoveAt(index);

                // まだ2番目の選択肢を試していないので、次はこれ
                if (item.ChoiceNumber == 0) return item;
            }

            return null;
        }
    }
}
