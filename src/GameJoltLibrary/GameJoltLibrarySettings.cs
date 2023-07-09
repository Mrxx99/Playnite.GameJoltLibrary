using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading.Tasks;
using GameJoltLibrary.Exceptions;
using GameJoltLibrary.Models;
using Playnite.SDK;
using Playnite.SDK.Data;

namespace GameJoltLibrary
{
    public class GameJoltLibrarySettings : ObservableObject
    {
        private bool _importInstalledGames = true;
        private bool _importLibraryGames = false;
        private bool _treatFollowedGamesAsLibraryGames = false;
        private bool _treatPlaylistGamesAsLibraryGames = false;
        private string _userName;

        public bool ImportInstalledGames { get => _importInstalledGames; set => SetValue(ref _importInstalledGames, value); }

        public bool ImportLibraryGames { get => _importLibraryGames; set => SetValue(ref _importLibraryGames, value); }
        
        public bool TreatFollowedGamesAsLibraryGames { get => _treatFollowedGamesAsLibraryGames; set => SetValue(ref _treatFollowedGamesAsLibraryGames, value); }
        public bool TreatPlaylistGamesAsLibraryGames { get => _treatPlaylistGamesAsLibraryGames; set => SetValue(ref _treatPlaylistGamesAsLibraryGames, value); }

        public string UserName { get => _userName; set => SetValue(ref _userName, value); }

        public Version LibraryVersion { get; set; }
    }

    public class GameJoltLibrarySettingsViewModel : ObservableObject, ISettings
    {
        private readonly GameJoltLibrary _plugin;
        private GameJoltLibrarySettings _editingClone;

        private GameJoltLibrarySettings _settings;
        public GameJoltLibrarySettings Settings { get => _settings; set => SetValue(ref _settings, value); }

        private ObservableCollection<GameJoltPlaylist> _playlists;
        public ObservableCollection<GameJoltPlaylist> Playlists { get => _playlists; set => SetValue(ref _playlists, value); }

        private string _playlistUser;

        public bool ImportPlaylistGames
        {
            get => Settings.TreatPlaylistGamesAsLibraryGames;
            set
            {
                Settings.TreatPlaylistGamesAsLibraryGames = value;
                OnPropertyChanged();
                if (Settings.TreatPlaylistGamesAsLibraryGames)
                {
                    _ = GetPlaylists();
                }
            }
        }


        public GameJoltLibrarySettingsViewModel(GameJoltLibrary plugin)
        {
            // Injecting your plugin instance is required for Save/Load method because Playnite saves data to a location based on what plugin requested the operation.
            _plugin = plugin;

            // Load saved settings.
            var savedSettings = plugin.LoadPluginSettings<GameJoltLibrarySettings>();

            // LoadPluginSettings returns null if not saved data is available.
            if (savedSettings != null)
            {
                Settings = savedSettings;
            }
            else
            {
                Settings = new GameJoltLibrarySettings { ImportInstalledGames = true };
            }
        }

        public async Task GetPlaylists()
        {
            if (Settings.UserName is null)
            {
                Playlists = null;
                _playlistUser = null;
                return;
            }
            else if (_playlistUser == Settings.UserName)
            {
                return;
            }

            using var http = new HttpClient();

            var result = await http.GetAsync($"https://gamejolt.com/site-api/web/library/@{Settings.UserName}");

            if (result.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new UserNotFoundException();
            }

            var stringContent = await result.Content.ReadAsStringAsync();

            var playlistsResult = Serialization.FromJsonStream<GameJoltWebResult<LibraryPlaylistsPayload>>(result.Content.ReadAsStreamAsync().GetAwaiter().GetResult());
            _playlistUser = Settings.UserName;
            Playlists = new ObservableCollection<GameJoltPlaylist>(playlistsResult.Payload.Playlists);
        }

        public void BeginEdit()
        {
            // Code executed when settings view is opened and user starts editing values.
            _editingClone = Serialization.GetClone(Settings);

            if (Settings.TreatPlaylistGamesAsLibraryGames)
            {
                _ = GetPlaylists();
            }
        }

        public void CancelEdit()
        {
            // Code executed when user decides to cancel any changes made since BeginEdit was called.
            // This method should revert any changes made to Option1 and Option2.
            Settings = _editingClone;
        }

        public void EndEdit()
        {
            // Code executed when user decides to confirm changes made since BeginEdit was called.
            // This method should save settings made to Option1 and Option2.
            _plugin.SavePluginSettings(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            // Code execute when user decides to confirm changes made since BeginEdit was called.
            // Executed before EndEdit is called and EndEdit is not called if false is returned.
            // List of errors is presented to user if verification fails.
            errors = new List<string>();
            return true;
        }
    }
}