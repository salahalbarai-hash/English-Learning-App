using CommunityToolkit.Maui.Views;

namespace English.Popups
{
    public partial class TenWordsGamesPopup : Popup
    {
        public string? SelectedGame { get; private set; }

        public TenWordsGamesPopup()
        {
            InitializeComponent();
        }

        private void OnCloseClicked(object sender, EventArgs e)
        {
            Close(null);
        }

        private void OnWordMatrixTapped(object sender, TappedEventArgs e)
        {
            SelectedGame = "WordMatrix";
            Close(SelectedGame);
        }

        private void OnSkywardBirdTapped(object sender, TappedEventArgs e)
        {
            SelectedGame = "SkywardBird";
            Close(SelectedGame);
        }

        private void OnSpellReactorTapped(object sender, TappedEventArgs e)
        {
            SelectedGame = "SpellReactor";
            Close(SelectedGame);
        }
    }
}

