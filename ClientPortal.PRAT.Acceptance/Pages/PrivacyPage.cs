using Microsoft.Playwright;

namespace ClientPortal.PRAT.Acceptance.Pages;

public class PrivacyPage : BasePage
{
    public PrivacyPage(IPage page) : base(page) { }

    // Locators for notifications page elements
    public ILocator _labelTitle => _page.Locator("h1:has-text('Privacy Notice')");
}