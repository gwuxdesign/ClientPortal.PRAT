using Microsoft.Playwright;
using ClientPortal.PRAT.Acceptance.Support;

namespace ClientPortal.PRAT.Acceptance.Pages;

public class LoginPage
{
    private IPage _page;

    // Locators for the login page elements
    public LoginPage(IPage page) => _page = page;
    public ILocator _labelTitle => _page.Locator("h1:has-text('MyAdviceHub (REL) login')");
    public ILocator _boxEmail => _page.Locator("#email");
    public ILocator _boxPassword => _page.Locator("#password");
    public ILocator _linkReset => _page.Locator("a:has-text('Forgotten password?')");
    public ILocator _btnLogin => _page.Locator("button:has-text('Log in')");
    public ILocator _invalidEmail => _page.Locator("p:has-text('Invalid email address')");
    public ILocator _errorEmail => _page.Locator("p:has-text('Email address is required')");
    public ILocator _errorPassword => _page.Locator("p:has-text('Password is required')");
    public ILocator _errorInvalid =>
    _page.GetByRole(AriaRole.Region).Filter(new() { HasText = "The email address or password is not valid." });
    public ILocator _btnCancel => _page.Locator("button:has-text('Cancel')");
    public ILocator _btnLogout => _page.Locator("button:has-text('Logout')");
    public ILocator _labelLogout => _page.Locator("h1:has-text('Log out')");

    // Methods to interact with login page elements
    public async Task ClickLogin() => await _btnLogin.ClickAsync();
    public async Task ClickLogout() => await _btnLogout.ClickAsync();
    public async Task Login(string? email, string? password, bool clickLogin = false)
    {
        CookiePage cookiePage = new CookiePage(_page);
        await cookiePage.ClickAccept();
        await _boxEmail.FillAsync(email ?? string.Empty);
        await _boxPassword.FillAsync(password ?? string.Empty);
        if (clickLogin)
        {
            await _btnLogin.ClickAsync();
        }
    }

    public async Task PasswordReset()
    {
        CookiePage cookiePage = new CookiePage(_page);
        await cookiePage.ClickAccept();
        await _linkReset.ClickAsync();
    }

    // Method to simulate clearing the password field by pressing backspace
    public async Task ClearField()
    {
        var curentValue = await _boxPassword.InputValueAsync();
        for (int i = 0; i < curentValue.Length; i++)
        {
            await _boxPassword.PressAsync("Backspace");
        }
        await _boxPassword.BlurAsync();
    }
}