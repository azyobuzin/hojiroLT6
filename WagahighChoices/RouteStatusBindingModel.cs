using System.ComponentModel;

namespace WagahighChoices
{
    public class RouteStatusBindingModel : INotifyPropertyChanged
    {
        public RouteStatusBindingModel(string routeName)
        {
            this.RouteName = routeName;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string RouteName { get; }

        public int Count { get; private set; }

        public void IncrementCount()
        {
            this.Count++;
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Count)));
        }
    }
}
