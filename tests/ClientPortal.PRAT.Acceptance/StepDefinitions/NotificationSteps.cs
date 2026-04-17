using System.Threading.Tasks;
using Reqnroll;
using static Microsoft.Playwright.Assertions;
using ClientPortal.PRAT.Acceptance.Support;

namespace ClientPortal.PRAT.Tests.StepDefinitions
{
    [Binding]
    public class NotificationSteps
    {
        private readonly IReqnrollOutputHelper _outputHelper;
        private readonly TestWorld _world;

        public NotificationSteps(TestWorld world, IReqnrollOutputHelper outputHelper)
        {
            _world = world;
            _outputHelper = outputHelper;
        }

        [When("the user has landed on the notifications page")]
        public async Task WhenTheUserHasLandedOnTheNotificationsPage()
        {
            await Expect(_world.Pages.notifPage._labelTitle).ToBeVisibleAsync();
        }

        [Then("the correct notification elements are visible")]
        public async Task ThenTheCorrectNotificationElementsAreVisible()
        {
            await Expect(_world.Pages.notifPage._labelTitle).ToBeVisibleAsync();
            await Expect(_world.Pages.menuPage._btnMenu).ToBeVisibleAsync();
            await Expect(_world.Pages.notifPage._labelTasks).ToBeVisibleAsync();
            await Expect(_world.Pages.notifPage._labelAdvisor).ToBeVisibleAsync();
            await Expect(_world.Pages.notifPage._labelEmail).ToBeVisibleAsync();
            await Expect(_world.Pages.notifPage._labelPhone).ToBeVisibleAsync();
        }
    }
}
