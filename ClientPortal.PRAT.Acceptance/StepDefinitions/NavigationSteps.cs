using System.Threading.Tasks;
using Reqnroll;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using ClientPortal.PRAT.Acceptance.Support;

namespace ClientPortal.PRAT.Acceptance.StepsDefinitions
{
    [Binding]
    public sealed class NavigationSteps
    {
        private readonly TestWorld _world;
        private readonly IReqnrollOutputHelper _output;

        public NavigationSteps(TestWorld world, IReqnrollOutputHelper outputHelper)
        {
            _world = world;
            _output = outputHelper;
        }

        [Given(@"the user navigates to the Client Portal")]
        public async Task GivenTheUserNavigatesToTheClientPortal()
        {
            await _world.Page.GotoAsync(_world.BaseUrl);
        }

        [Then("the user navigates to the {string} page")]
        [When("the user navigates to the {string} page")]
        public async Task TheUserNavigatesToPage(string page)
        {
            await _world.Pages.navPage.NavigateToPage(page);
        }

        [When("the user uses the {string} button")]
        [Then("the user uses the {string} button")]
        public async Task ButtonUsed(string button)
        {
            await TheUserUsesTheButton(button);
        }

        [When("the user opts to reset their password")]
        public async Task TheUserOptsToResetTheirPassword()
        {
            await _world.Pages.loginPage._linkReset.ClickAsync();
        }

        public async Task TheUserUsesTheButton(string button)
        {
            switch (button.ToLower())
            {
                case "password back":
                    await _world.Pages.resetPage.ClickBack();
                    break;
                default:
                    throw new ArgumentException($"Button '{button}' not recognised.");
            }
        }
    }
}
