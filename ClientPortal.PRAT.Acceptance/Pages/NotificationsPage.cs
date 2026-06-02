using Microsoft.Playwright;

namespace ClientPortal.PRAT.Acceptance.Pages;

public class NotificationsPage : BasePage
{
    public NotificationsPage(IPage page) : base(page) { }

    // Locators for notifications page elements
    public ILocator _labelTitle => _page.Locator("h1:has-text('My notifications')");
    public ILocator _labelTasks => _page.Locator("h2:has-text('My tasks')");

    // Elements was in a generic div so had to use a combination of locators
    public ILocator _labelAdvisor => _page.Locator("aside.lg\\:w-80").GetByText("My adviser");
    public ILocator _labelEmail => _page.Locator("li").Locator("div:has-text('Email')");
    public ILocator _labelPhone => _page.Locator("li").Locator("div:has-text('Phone')");
}