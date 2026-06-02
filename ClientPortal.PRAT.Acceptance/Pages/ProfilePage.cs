using Microsoft.Playwright;

namespace ClientPortal.PRAT.Acceptance.Pages;

public class ProfilePage : BasePage
{
    public ProfilePage(IPage page) : base(page) { }

    // Locators for profile page elements
    public ILocator _labelTitle => _page.Locator("h1:has-text('My profile')");
    public ILocator _labelPersonal => _page.Locator("h1:has-text('My personal details')");
    public ILocator _labelIdentity => _page.Locator("h2:has-text('Identity')");
    public ILocator _labelContact => _page.Locator("h2:has-text('Contact details')");
    public ILocator _labelHome => _page.Locator("h2:has-text('Home & residency')");
    
    public ILocator _btnEditPersonal => _page.Locator("button[aria-label='Edit your profile identity details']");


    // Elements was in a generic div so had to use a combination of locators
    public ILocator _labelAdvisor => _page.Locator("aside.lg\\:w-80").GetByText("My adviser");
    public ILocator _labelEmail => _page.Locator("li").Locator("div:has-text('Email')");
    public ILocator _labelPhone => _page.Locator("li").Locator("div:has-text('Phone')");

    // Method to expand personal details section
    public async Task ExpandDetails() => await _labelPersonal.ClickAsync();
}