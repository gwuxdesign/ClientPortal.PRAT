using System.Threading.Tasks;
using Reqnroll;
using static Microsoft.Playwright.Assertions;
using ClientPortal.PRAT.Acceptance.Support;

namespace ClientPortal.PRAT.Acceptance.StepDefinitions
{
    [Binding]
    public class MenuSteps
    {
        private readonly IReqnrollOutputHelper _outputHelper;
        private readonly TestWorld _world;

        public MenuSteps(TestWorld world, IReqnrollOutputHelper outputHelper)
        {
            _world = world;
            _outputHelper = outputHelper;
        }

        [When("the user clicks the menu button")]
        public async Task WhenTheUserClicksTheMenuButton()
        {
            await _world.Pages.menuPage.ClickMenu();
        }

        [Given("the user clicks the {string} menu link")]
        [When("the user clicks the {string} menu link")]
        public async Task TheUserClicksThe(string menuItem)
        {
            if (_world.FaultProfile != null)
            {
                await _world.FaultProfile.ApplyAsync(_world.Page, _world.FaultMagnitude);
            }

            await _world.Pages.menuPage.ClickMenuItem(menuItem);
        }
    }
}