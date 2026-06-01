using Microsoft.Playwright;
using ClientPortal.PRAT.Acceptance.Support;

namespace ClientPortal.PRAT.Acceptance.Pages;

public class DocumentsPage
{
    public IPage _page;

    // Locators for notifications page elements
    public DocumentsPage(IPage page) => _page = page;
    public ILocator _labelTitle => _page.Locator("h1:has-text('My documents')");
    public ILocator _labelAction => _page.Locator("h2:has-text('Action required')");
    public ILocator _labelUpload => _page.Locator("h3:has-text('Got something to upload?')");
    public ILocator _btnUpload => _page.Locator("button:has-text('Upload')");

    // Elements was in a generic div so had to use a combination of locators
    public ILocator _labelAdvisor => _page.Locator("aside.lg\\:w-80").GetByText("My adviser");
    public ILocator _labelEmail => _page.Locator("li").Locator("div:has-text('Email')");
    public ILocator _labelPhone => _page.Locator("li").Locator("div:has-text('Phone')");

    // Filter options
    public ILocator _filterAll => _page.GetByLabel("All", new() { Exact = true });
    public ILocator _filterRead => _page.GetByLabel("Read", new() { Exact = true });
    public ILocator _filterUnread => _page.GetByLabel("Unread", new() { Exact = true });
    public ILocator _filterMyUploads => _page.GetByLabel("My uploads", new() { Exact = true });
    public ILocator _filterSort => _page.Locator("select[aria-labelledby='sortlabel']");

    // Docuemnt results
    public ILocator _documentResults => _page.Locator("div[id='document_container']").Locator("a[href*='document']");
    public ILocator _documentList => _page.Locator("#document_container h3");

    // Filtering method
    public async Task FilterDocuments(string filter)
    {
        switch (filter.ToLower())
        {
            case "all":
                await _filterAll.ClickAsync();
                break;
            case "read":
                await _filterRead.ClickAsync();
                break;
            case "unread":
                await _filterUnread.ClickAsync();
                break;
            case "my uploads":
                await _filterMyUploads.ClickAsync();
                break;
            case "newest first":
                await _filterSort.SelectOptionAsync(new[] { "created_desc" });
                break;
            case "oldest first":
                await _filterSort.SelectOptionAsync(new[] { "created_asc" });
                break;
            case "title (a-z)":
                await _filterSort.SelectOptionAsync(new[] { "title_asc" });
                break;
            case "title (z-a)":
                await _filterSort.SelectOptionAsync(new[] { "title_desc" });
                break;
            default:
                throw new ArgumentException($"Filter '{filter}' not recognised.");
        }
    }
}