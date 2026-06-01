using System.Threading.Tasks;
using Reqnroll;
using static Microsoft.Playwright.Assertions;
using ClientPortal.PRAT.Acceptance.Support;

namespace ClientPortal.PRAT.Acceptance.StepDefinitions
{
    [Binding]
    public class CookieSteps
    {
        private readonly IReqnrollOutputHelper _outputHelper;
        private readonly TestWorld _world;

        public CookieSteps(TestWorld world, IReqnrollOutputHelper outputHelper)
        {
            _world = world;
            _outputHelper = outputHelper;
        }

        [Given("the user clears the cookie pop up")]
        public async Task GivenTheUserClearsTheCookiePopUp()
        {
            await _world.Pages.cookiePage.ClickAccept();
        }

        [When("the cookie policy pop-up appears")]
        public async Task WhenTheCookiePolicyPopUpAppears()
        {
            await Expect(_world.Pages.cookiePage._labelPUTitle).ToBeVisibleAsync();
            await Expect(_world.Pages.cookiePage._labelPUEssential).ToBeVisibleAsync();
            await Expect(_world.Pages.cookiePage._labelPUNonEssential).ToBeVisibleAsync();
        }

        [Then("the user can accept or reject it")]
        public async Task ThenTheUserCanAcceptOrRejectIt()
        {
            await Expect(_world.Pages.cookiePage._btnPUAccept).ToBeVisibleAsync();
            await Expect(_world.Pages.cookiePage._btnPUReject).ToBeVisibleAsync();
        }
    }
}
