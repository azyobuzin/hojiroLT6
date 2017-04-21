using Realms;

namespace WagahighChoices
{
    public class ChoiceWindowInfoRealmObject : RealmObject
    {
        // 選択肢の場合は Choice1, Choice2 に選択肢名を代入
        // 個別ルートに入った最初の選択肢の場合は RouteName にキャラ名を代入

        [PrimaryKey]
        public string ScreenshotHash { get; set; }
        public string Choice1 { get; set; }
        public string Choice2 { get; set; }
        public string RouteName { get; set; }
    }
}
