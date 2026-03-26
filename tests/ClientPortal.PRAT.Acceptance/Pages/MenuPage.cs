using Microsoft.Playwright;
using ClientPortal.PRAT.Acceptance.Support;
using System.Text.RegularExpressions;

namespace ClientPortal.PRAT.Acceptance.Pages;

public class MenuPage
{
    public IPage _page;

    private readonly TestWorld _world;

    public MenuPage(TestWorld world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _page = world.Page ?? throw new ArgumentNullException(nameof(_world.Page));
    }

    // Locators for menu items
    public ILocator _btnMenu => _page.Locator("button:has-text('Menu')");
    public ILocator _menuLabel => _page.Locator("div.text-sm:has-text('Logged in to ')");
    public ILocator _menuMyNotifs => _page.Locator("li").Locator("button:has-text('My notifications')");
    public ILocator _menuMyProfile => _page.Locator("li").Locator("button:has-text('My profile')");
    public ILocator _menuMyDocs => _page.Locator("li").Locator("button:has-text('My documents')");
    public ILocator _menuLogout => _page.Locator("li").Locator("button:has-text('Logout')");
    public ILocator _menuPrivacy => _page.Locator("li").Locator("button:has-text('Privacy policy')");
    public ILocator _menuTerms => _page.Locator("li").Locator("button:has-text('Terms and conditions')");
    public ILocator _menuCookie => _page.Locator("li").Locator("button:has-text('Cookie policy')");

    public async Task ClickMenu() => await _btnMenu.ClickAsync();

    public async Task ClickMenuItem(string menuItem)
    {
        await _btnMenu.ClickAsync();
        switch (menuItem.ToLower())
        {
            case "my notifications":
                var currentURL = _page.Url;
                if (!Regex.IsMatch(currentURL, $"^{Regex.Escape(_world.PortalUrl)}/?$"))
                {
                    await _menuMyNotifs.ClickAsync();
                }
                break;
            case "my profile":
                await _menuMyProfile.ClickAsync();
                break;
            case "my documents":
                await _menuMyDocs.ClickAsync();
                break;
            case "privacy policy":
                await _menuPrivacy.ClickAsync();
                break;
            case "terms and conditions":
                await _menuTerms.ClickAsync();
                break;
            case "cookie policy":
                await _menuCookie.ClickAsync();
                break;
            case "logout":
                await _menuLogout.ClickAsync();
                break;
            default:
                throw new ArgumentException($"Menu item '{menuItem}' not recognised.");
        }
    }

}