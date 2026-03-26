using Microsoft.Playwright;
using ClientPortal.PRAT.Acceptance.Support;

namespace ClientPortal.PRAT.Acceptance.Pages;

public class PWResetPage
{
    private IPage _page;

    // Locators for the password reset page elements
    public PWResetPage(IPage page) => _page = page;
    public ILocator _labelTitle => _page.Locator("h1:has-text('Forgotten password')");
    public ILocator _boxEmail => _page.Locator("#email");
    public ILocator _btnBack => _page.Locator("button:has-text('Back')");
    public ILocator _btnReset => _page.Locator("button:has-text('Reset password')");
    public ILocator _errorEmail => _page.Locator("p:has-text('Email address is required')");
    public ILocator _errorInvalid => _page.Locator("p:has-text('Invalid email address')");
    public ILocator _labelConfirm => _page.GetByText("Password reset request submitted", new() { Exact = true });

    public ILocator _labelSpam => _page.GetByText("Be sure to check your spam folder.", new() { Exact = true });

    // Methods to interact with the password reset page elements
    public async Task ClickBack() => await _btnBack.ClickAsync();
    public async Task PasswordReset(string? email, bool fullReset = false)
    {
        await _boxEmail.FillAsync(email ?? string.Empty);
        if (fullReset)
        {
            await _btnReset.ClickAsync();
        }
    }
    
    // Method to simulate clearing the email field by pressing backspace
    public async Task ClearField()
    {
        var curentValue = await _boxEmail.InputValueAsync();
        for (int i = 0; i < curentValue.Length; i++)
        {
            await _boxEmail.PressAsync("Backspace");
        }
        await _boxEmail.BlurAsync();
    }
}