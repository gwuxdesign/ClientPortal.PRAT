using System.Threading.Tasks;
using Reqnroll;
using static Microsoft.Playwright.Assertions;
using ClientPortal.PRAT.Acceptance.Support;

namespace ClientPortal.PRAT.Acceptance.StepDefinitions
{
    [Binding]
    public class TermSteps
    {
        private readonly IReqnrollOutputHelper _outputHelper;
        private readonly TestWorld _world;

        public TermSteps(TestWorld world, IReqnrollOutputHelper outputHelper)
        {
            _world = world;
            _outputHelper = outputHelper;
        }

        [When("the user is able to navigate to the terms and conditions page")]
        public async Task WhenTheUserIsAbleToNavigateToTheTermsAndConditionsPage()
        {
            await Expect(_world.Pages.menuPage._btnMenu).ToBeVisibleAsync();
            await _world.Pages.menuPage.ClickMenuItem("Terms and conditions");
        }

        [Then("the correct terms and conditions elements are visible")]
        public async Task ThenTheCorrectTermsAndConditionsElementsAreVisible()
        {
            await Expect(_world.Pages.termsPage._labelTitle).ToBeVisibleAsync();

        }

    }
}