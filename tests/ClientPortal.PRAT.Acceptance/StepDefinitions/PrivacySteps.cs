using System.Threading.Tasks;
using Reqnroll;
using static Microsoft.Playwright.Assertions;
using ClientPortal.PRAT.Acceptance.Support;

namespace ClientPortal.PRAT.Tests.StepDefinitions
{
    [Binding]
    public class PrivacySteps
    {
        private readonly IReqnrollOutputHelper _outputHelper;
        private readonly TestWorld _world;

        public PrivacySteps(TestWorld world, IReqnrollOutputHelper outputHelper)
        {
            _world = world;
            _outputHelper = outputHelper;
        }

        [When("the user is able to navigate to the privacy policy page")]
        public async Task WhenTheUserIsAbleToNavigateToThePrivacyPolicyPage()
        {
            await Expect(_world.Pages.menuPage._btnMenu).ToBeVisibleAsync();
            await _world.Pages.menuPage.ClickMenuItem("Privacy policy");
        }

        [Then("the correct privacy policy elements are visible")]
        public async Task ThenTheCorrectPrivacyPolicyElementsAreVisible()
        {
            await Expect(_world.Pages.privacyPage._labelTitle).ToBeVisibleAsync();

        }

    }
}