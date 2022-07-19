using System.Collections.Generic;
using Playnite.SDK;
using Playnite.SDK.Data;
using System.Windows.Input;
using System.Text.RegularExpressions;

namespace GameJoltLibrary
{
    public class GameJoltLibrarySettings : ObservableObject
    {
        private bool _importInstalledGames = true;
        private string _userName;

        public bool ImportInstalledGames { get => _importInstalledGames; set => SetValue(ref _importInstalledGames, value); }

        public string UserName { get => _userName; set => SetValue(ref _userName, value); }
    }

    public class GameJoltLibrarySettingsViewModel : ObservableObject, ISettings
    {
        private readonly GameJoltLibrary plugin;
        private GameJoltLibrarySettings editingClone { get; set; }

        private GameJoltLibrarySettings settings;
        public GameJoltLibrarySettings Settings { get => settings; set => SetValue(ref settings, value); }

        public GameJoltLibrarySettingsViewModel(GameJoltLibrary plugin)
        {
            // Injecting your plugin instance is required for Save/Load method because Playnite saves data to a location based on what plugin requested the operation.
            this.plugin = plugin;

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
            editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            // Code executed when user decides to cancel any changes made since BeginEdit was called.
            // This method should revert any changes made to Option1 and Option2.
            Settings = editingClone;
        }

        public void EndEdit()
        {
            // Code executed when user decides to confirm changes made since BeginEdit was called.
            // This method should save settings made to Option1 and Option2.
            plugin.SavePluginSettings(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            // Code execute when user decides to confirm changes made since BeginEdit was called.
            // Executed before EndEdit is called and EndEdit is not called if false is returned.
            // List of errors is presented to user if verification fails.
            errors = new List<string>();
            return true;
        }

        private RelayCommand authenticateCommand;
        public ICommand AuthenticateCommand => authenticateCommand ??= new RelayCommand(Authenticate);

        private void Authenticate()
        {
            using var webView = plugin.PlayniteApi.WebViews.CreateView(new WebViewSettings
            {
                JavaScriptEnabled = true,
                WindowWidth = 490,
                WindowHeight = 670
            });

            webView.DeleteDomainCookies(".gamejolt.com");
            webView.DeleteDomainCookies("gamejolt.com");
            webView.Navigate("https://gamejolt.com/login");

            webView.LoadingChanged += (sender, args) =>
            {
                string address = webView.GetCurrentAddress();
                _ = address.ToLower();
                if (!args.IsLoading && Regex.IsMatch(address, @"https:\/\/gamejolt\.com\/?$"))
                {
                    webView.Close();
                }
            };

            webView.OpenDialog();

        }

        private RelayCommand logoutCommand;
        public ICommand LogoutCommand => logoutCommand ??= new RelayCommand(Logout);

        private void Logout()
        {
            using var webView = plugin.PlayniteApi.WebViews.CreateOffscreenView(new WebViewSettings
            {
                JavaScriptEnabled = true
            });

            webView.DeleteDomainCookies(".gamejolt.com");
            webView.DeleteDomainCookies("gamejolt.com");
        }
    }
}