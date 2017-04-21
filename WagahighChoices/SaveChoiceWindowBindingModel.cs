using System.Windows.Media;

namespace WagahighChoices
{
    public class SaveChoiceWindowBindingModel
    {
        public SaveChoiceWindowBindingModel(ImageSource image)
        {
            this.Image = image;
        }

        public ImageSource Image { get; }
        public string Choice1 { get; set; }
        public string Choice2 { get; set; }
    }
}
