using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace WagahighChoices
{
    public class TracingChoicesWindowBindingModel : INotifyPropertyChanged
    {
        private readonly Action _cancelCallback;

        public TracingChoicesWindowBindingModel(IEnumerable<RouteStatusBindingModel> routeStatuses, Action cancelCallback)
        {
            this.RouteStatuses = routeStatuses.ToArray();
            this._cancelCallback = cancelCallback;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public IReadOnlyList<RouteStatusBindingModel> RouteStatuses { get; }

        private string _statusText = "探索中";
        public string StatusText
        {
            get => this._statusText;
            protected set
            {
                if (!ReferenceEquals(this._statusText, value))
                {
                    this._statusText = value;
                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.StatusText)));
                }
            }
        }

        public void SetCompleted()
        {
            this.StatusText = "完了";
        }

        public void SetError(string errorMessage)
        {
            this.StatusText = errorMessage;
        }

        public void Cancel()
        {
            this._cancelCallback();
        }
    }
}
