using System;
using System.Collections.Generic;
using Playnite.SDK;
using Playnite.SDK.Data;

namespace GameJoltLibrary
{
    public class GameJoltLibrarySettings : ObservableObject
    {
        private bool _importInstalledGames = true;
        private bool _importLibraryGames = false;
        private string _userName;

        public bool ImportInstalledGames { get => _importInstalledGames; set => SetValue(ref _importInstalledGames, value); }

        public bool ImportLibraryGames { get => _importLibraryGames; set => SetValue(ref _importLibraryGames, value); }

        public string UserName { get => _userName; set => SetValue(ref _userName, value); }

        public Version LibraryVersion { get; set; }
    }

    public class GameJoltLibrarySettingsViewModel : ObservableObject, ISettings
    {
        private readonly GameJoltLibrary _plugin;
        private GameJoltLibrarySettings _editingClone;

        private GameJoltLibrarySettings _settings;
        public GameJoltLibrarySettings Settings { get => _settings; set => SetValue(ref _settings, value); }

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

        public void BeginEdit()
        {
            // Code executed when settings view is opened and user starts editing values.
            _editingClone = Serialization.GetClone(Settings);
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