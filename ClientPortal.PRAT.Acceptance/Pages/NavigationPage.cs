using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using ClientPortal.PRAT.Acceptance.Support;
using System.Text.RegularExpressions;

namespace ClientPortal.PRAT.Acceptance.Pages;

public class NavigationPage
{
    private IPage _page;
    private readonly TestWorld _world;

    public NavigationPage(TestWorld world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _page = world.Page ?? throw new ArgumentNullException(nameof(_world.Page));
    }

    // Methods for navigation
    public async Task NavigateToPage(string page)
    {
        switch (page.ToLower())
        {
            case "login":
                await Expect(_page).ToHaveURLAsync(new Regex(".*/authentication/login"));
                await Expect(_world.Pages.loginPage._btnLogin).ToBeVisibleAsync();
                break;
            case "my notifications":
                await Expect(_page).ToHaveURLAsync(_world.BaseUrl);
                await Expect(_world.Pages.notifPage._labelTitle).ToBeVisibleAsync();
                break;
            case "my profile":
                await Expect(_page).ToHaveURLAsync(new Regex(".*/profile"));
                await Expect(_world.Pages.profilePage._labelTitle).ToBeVisibleAsync();
                break;
            case "my documents":
                await Expect(_page).ToHaveURLAsync(new Regex(".*/documents"));
                await Expect(_world.Pages.docPage._labelTitle).ToBeVisibleAsync();
                break;
            case "privacy policy":
                await Expect(_page).ToHaveURLAsync(new Regex(".*/policies/privacy"));
                await Expect(_world.Pages.privacyPage._labelTitle).ToBeVisibleAsync();
                break;
            case "terms and conditions":
                await Expect(_page).ToHaveURLAsync(new Regex(".*/policies/terms"));
                await Expect(_world.Pages.termsPage._labelTitle).ToBeVisibleAsync();
                break;
            case "cookie policy":
                await Expect(_page).ToHaveURLAsync(new Regex(".*/policies/cookies"));
                await Expect(_world.Pages.cookiePage._labelTitle).ToBeVisibleAsync();
                break;
            case "logout":
                await Expect(_page).ToHaveURLAsync(new Regex(".*/authentication/logout"));
                await Expect(_world.Pages.loginPage._btnLogin).ToBeVisibleAsync();
                break;
            case "password reset":
                await _world.Pages.loginPage._linkReset.ClickAsync();
                await Expect(_page).ToHaveURLAsync(new Regex(".*/authentication/forgotten-password"));
                await Expect(_world.Pages.resetPage._labelTitle).ToBeVisibleAsync();
                break;
            default:
                throw new ArgumentException($"Page '{page}' not recognised.");
        }
        var currentUrl = _page.Url;
        Console.WriteLine($"Current URL: {currentUrl}");
    }
}