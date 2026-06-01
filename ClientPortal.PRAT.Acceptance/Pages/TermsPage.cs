using Microsoft.Playwright;
using ClientPortal.PRAT.Acceptance.Support;

namespace ClientPortal.PRAT.Acceptance.Pages;

public class TermsPage
{
    public IPage _page;

    // Locators for notifications page elements
    public TermsPage(IPage page) => _page = page;
    public ILocator _labelTitle => _page.Locator("h1:has-text('Terms and Conditions')");
}