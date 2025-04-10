using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Playnite.SDK;
using Playnite.SDK.Data;

namespace GameJoltLibrary;
internal class GameJoltAccountClient
{
    private readonly IWebView _webView;

    public GameJoltAccountClient(IWebView webView)
    {
        _webView = webView;
    }

    public bool GetIsUserLoggedIn() => GetIsUserLoggedIn(_webView);

    private static bool GetIsUserLoggedIn(IWebView webView)
    {
        var account = GetAccountInfo(webView);
        return account != null;
    }

    public AccountResultUser Authenticate(IWebView onScreenWebView, string userName)
    {
        onScreenWebView.DeleteDomainCookies(".gamejolt.com");
        onScreenWebView.DeleteDomainCookies("gamejolt.com");
        onScreenWebView.Navigate("https://gamejolt.com/login");

        bool firstStartPageAfterLogin = false;
        AccountResultUser account = null;

        onScreenWebView.LoadingChanged += async (sender, args) =>
        {
            string address = onScreenWebView.GetCurrentAddress();

            if (!args.IsLoading)
            {
                if (Regex.IsMatch(address, @"https:\/\/gamejolt\.com\/login\/?$") && !string.IsNullOrEmpty(userName))
                {
                    // set username input element value
                    var result = await onScreenWebView.EvaluateScriptAsync(@$"this.document.getElementsByName(""username"")[0].value = ""{userName}"";");
                }
                if (!firstStartPageAfterLogin && Regex.IsMatch(address, @"https:\/\/gamejolt\.com\/?$"))
                {
                    firstStartPageAfterLogin = true;
                    account = await Task.Run(() => GetAccountInfo(_webView));
                    if (account != null)
                    {
                        onScreenWebView.Close();
                    }
                }
            }
        };

        onScreenWebView.OpenDialog();
        return account;
    }

    public void Logout()
    {
        _webView.DeleteDomainCookies(".gamejolt.com");
        _webView.DeleteDomainCookies("gamejolt.com");
    }

    public AccountResultUser GetAccountInfo() => GetAccountInfo(_webView);

    private static AccountResultUser GetAccountInfo(IWebView webView)
    {
        webView.NavigateAndWait(@"https://gamejolt.com/site-api/web/profile");
        var stringInfo = webView.GetPageText();
        var accountInfo = Serialization.FromJson<AccountResult>(stringInfo);
        return accountInfo.User;
    }
}

public class AccountResult
{
    public AccountResultUser User { get; set; }
}

public class AccountResultUser
{
    [SerializationPropertyName("id")]
    public string Id { get; set; }

    [SerializationPropertyName("username")]
    public string UserName { get; set; }
}