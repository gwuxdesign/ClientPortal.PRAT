using System.Threading.Tasks;
using Reqnroll;
using static Microsoft.Playwright.Assertions;
using ClientPortal.PRAT.Acceptance.Support;

namespace ClientPortal.PRAT.Tests.StepDefinitions
{
    [Binding]
    public class LoginSteps
    {
        private readonly IReqnrollOutputHelper _outputHelper;
        private readonly TestWorld _world;

        public LoginSteps(TestWorld world, IReqnrollOutputHelper outputHelper)
        {
            _world = world;
            _outputHelper = outputHelper;
        }

        [Given("the user enters {string} credentials")]
        public async Task GivenTheUserEntersCredentials(string LoginType)
        {
            switch (LoginType.ToLower())
            {
                case "valid":
                    var goodCreds = CredentialReader.Get("goodLogin");
                    await _world.Pages.loginPage.Login(goodCreds.email, goodCreds.password);
                    break;
                case "invalid":
                    var badCreds = CredentialReader.Get("badLogin");
                    await _world.Pages.loginPage.Login(badCreds.email, badCreds.password);
                    break;
                default:
                    throw new ArgumentException($"Unknown login type: {LoginType}");
            }
        }

        [When("the user submits an {string} in the login form")]
        public async Task WhenTheUserSubmitsAnInTheLoginForm(string error)
        {
            switch(error)
            {
                case "empty":
                    await _world.Pages.loginPage._boxEmail.FillAsync("");
                    await _world.Pages.loginPage._boxPassword.FillAsync("");
                    await _world.Pages.loginPage._btnLogin.ClickAsync();
                    break;
                case "invalid":
                    await _world.Pages.loginPage._boxEmail.FillAsync("test");
                    await _world.Pages.loginPage._boxPassword.FillAsync("");
                    await _world.Pages.loginPage._btnLogin.ClickAsync();
                    break;
                default:
                    throw new ArgumentException($"Unknown error type: {error}");
            }
        }

        [When("the user submits the login form")]
        public async Task WhenTheUserSubmitsTheLoginForm()
        {
            await _world.Pages.loginPage._btnLogin.ClickAsync();
        }

        [Then("the login attempt was {string}")]
        public async Task ThenTheLoginAttemptWas(string status)
        {
            switch (status.ToLower())
            {
                case "successful":
                    await Expect(_world.Pages.notifPage._labelTitle).ToBeVisibleAsync();
                    await Expect(_world.Pages.menuPage._btnMenu).ToBeVisibleAsync();
                    await Expect(_world.Pages.notifPage._labelTasks).ToBeVisibleAsync();
                    await Expect(_world.Pages.notifPage._labelAdvisor).ToBeVisibleAsync();
                    await Expect(_world.Pages.notifPage._labelEmail).ToBeVisibleAsync();
                    await Expect(_world.Pages.notifPage._labelPhone).ToBeVisibleAsync();
                    break;
                case "unsuccessful":
                    await Expect(_world.Pages.loginPage._errorInvalid).ToBeVisibleAsync();
                    break;
                default:
                    throw new ArgumentException($"Unknown status: {status}");
            }
        }

        [Given("the user is logged in")]
        public async Task GivenTheUserIsLoggedIn()
        {
            var creds = CredentialReader.Get("goodLogin");
            await _world.Pages.loginPage.Login(creds.email, creds.password, clickLogin: true);
        }

        [When("the user clicks on the logout button")]
        public async Task WhenTheUserClicksOnTheLogoutButton()
        {
            await _world.Pages.menuPage.ClickMenu();
            await _world.Pages.loginPage.ClickLogout();
            await _world.Pages.loginPage._btnLogout.ClickAsync();
        }
    }
}
