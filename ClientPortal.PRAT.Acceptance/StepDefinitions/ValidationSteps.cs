using System.Threading.Tasks;
using Reqnroll;
using static Microsoft.Playwright.Assertions;
using ClientPortal.PRAT.Acceptance.Support;

namespace ClientPortal.PRAT.Acceptance.StepDefinitions
{
    [Binding]
    public class ValidationSteps
    {
        private readonly IReqnrollOutputHelper _outputHelper;
        private readonly TestWorld _world;

        public ValidationSteps(TestWorld world, IReqnrollOutputHelper outputHelper)
        {
            _world = world;
            _outputHelper = outputHelper;
        }

        [Then("the user should see validation messages for {string}")]
        public async Task ThenTheUserShouldSeeValidationMessages(string location)
        {
            switch (location)
            {
                case "empty login":
                    await Expect(_world.Pages.loginPage._errorEmail).ToBeVisibleAsync();
                    await Expect(_world.Pages.loginPage._errorPassword).ToBeVisibleAsync();
                    break;
                case "bad email":
                    await Expect(_world.Pages.loginPage._invalidEmail).ToBeVisibleAsync();
                    await Expect(_world.Pages.loginPage._errorPassword).ToBeVisibleAsync();
                    break;
                case "email required":
                    await Expect(_world.Pages.resetPage._errorEmail).ToBeVisibleAsync();
                    break;
                case "bad email password":
                    await Expect(_world.Pages.resetPage._errorInvalid).ToBeVisibleAsync();
                    break;
                default:
                    throw new ArgumentException($"Unknown location: {location}");
            }
        }
    }
}
