using Microsoft.Playwright;

namespace ClientPortal.PRAT.Acceptance.Pages;

public abstract class BasePage
{
    protected readonly IPage _page;

    protected BasePage(IPage page)
    {
        _page = page ?? throw new ArgumentNullException(nameof(page));
    }
}