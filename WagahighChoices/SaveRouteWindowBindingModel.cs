using System.Windows.Media;

namespace WagahighChoices
{
    public class SaveRouteWindowBindingModel
    {
        public SaveRouteWindowBindingModel(ImageSource image)
        {
            this.Image = image;
        }

        public ImageSource Image { get; }
        public string RouteName { get; set; }
    }
}
