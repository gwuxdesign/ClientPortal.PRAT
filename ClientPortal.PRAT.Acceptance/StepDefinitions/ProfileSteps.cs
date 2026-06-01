using System.Threading.Tasks;
using Reqnroll;
using static Microsoft.Playwright.Assertions;
using ClientPortal.PRAT.Acceptance.Support;

namespace ClientPortal.PRAT.Acceptance.StepDefinitions
{
    [Binding]
    public class ProfileSteps
    {
        private readonly IReqnrollOutputHelper _outputHelper;
        private readonly TestWorld _world;

        public ProfileSteps(TestWorld world, IReqnrollOutputHelper outputHelper)
        {
            _world = world;
            _outputHelper = outputHelper;
        }

        [When("the user is able to navigate to the profile page")]
        public async Task WhenTheUserIsAbleToNavigateToTheProfilePage()
        {
            await Expect(_world.Pages.menuPage._btnMenu).ToBeVisibleAsync();
            await _world.Pages.menuPage.ClickMenuItem("My profile");
        }

        [Then("the correct profile elements are visible")]
        public async Task ThenTheCorrectProfileElementsAreVisible()
        {
            await Expect(_world.Pages.profilePage._labelTitle).ToBeVisibleAsync();
            await Expect(_world.Pages.menuPage._btnMenu).ToBeVisibleAsync();
            await Expect(_world.Pages.profilePage._labelPersonal).ToBeVisibleAsync();
            await Expect(_world.Pages.profilePage._labelAdvisor).ToBeVisibleAsync();
            await Expect(_world.Pages.profilePage._labelEmail).ToBeVisibleAsync();
            await Expect(_world.Pages.profilePage._labelPhone).ToBeVisibleAsync();

        }

    }
}