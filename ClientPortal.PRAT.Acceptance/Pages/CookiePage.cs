using Microsoft.Playwright;
using ClientPortal.PRAT.Acceptance.Support;

namespace ClientPortal.PRAT.Acceptance.Pages;

public class CookiePage
{
    private IPage _page;

    public CookiePage(IPage page) => _page = page;

    // Locators for cookie policy pop-up elements
    public ILocator _labelPUTitle => _page.Locator("h1:has-text('Welcome to MyAdviceHub (REL)')");
    public ILocator _labelPUEssential => _page.Locator("h3:has-text('Essential cookies')");
    public ILocator _labelPUNonEssential => _page.Locator("h3:has-text('Non-essential analytics cookies')");
    public ILocator _btnPUReject => _page.Locator("button:has-text('Reject non-essential')");
    public ILocator _btnPUAccept => _page.Locator("button:has-text('Accept all')");

    // Locators for cookie policy details elements
    public ILocator _labelTitle => _page.Locator("h1:has-text('Our use of cookies')");

    // Methods to interact with cookie policy pop-up elements
    // public async Task ClickAccept() => await _btnPUAccept.ClickAsync();
    // public async Task ClickReject() => await _btnPUReject.ClickAsync();
    public async Task ClickAccept()
    {
        try
        {
            await _btnPUAccept.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 3000
            });
            await _btnPUAccept.ClickAsync();
        }
        catch (TimeoutException)
        {
            // Cookie banner was not present or already dismissed
        }
    }
    
    public async Task ClickReject()
    {
        try
        {
            await _btnPUReject.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 3000
            });
            await _btnPUReject.ClickAsync();
        }
        catch (TimeoutException)
        {
            // Cookie banner was not present or already dismissed
        }
    }

}