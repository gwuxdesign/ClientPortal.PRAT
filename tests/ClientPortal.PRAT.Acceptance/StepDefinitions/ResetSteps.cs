using System.Threading.Tasks;
using Reqnroll;
using static Microsoft.Playwright.Assertions;
using ClientPortal.PRAT.Acceptance.Support;

namespace ClientPortal.PRAT.Tests.StepDefinitions
{
    [Binding]
    public class ResetSteps
    {
        private readonly IReqnrollOutputHelper _outputHelper;
        private readonly TestWorld _world;

        public ResetSteps(TestWorld world, IReqnrollOutputHelper outputHelper)
        {
            _world = world;
            _outputHelper = outputHelper;
        }

        [When("the user submits an {string} in the reset form")]
        public async Task WhenTheUserSubmitsAnInTheResetForm(string error)
        {
            switch(error)
            {
                case "empty":
                    await _world.Pages.resetPage._boxEmail.FillAsync("");
                    await _world.Pages.resetPage._btnReset.ClickAsync();
                    break;
                case "invalid":
                    await _world.Pages.resetPage._boxEmail.FillAsync("test");
                    await _world.Pages.resetPage._btnReset.ClickAsync();
                    break;
                default:
                    throw new ArgumentException($"Unknown error type: {error}");
            }
            
        }

        [When("the user submits a valid email")]
        public async Task WhenTheUserSubmitsAValidEmail()
        {
            var resetCreds = CredentialReader.Get("resetLogin");
            await _world.Pages.resetPage.PasswordReset(resetCreds.email, true);
        }

        [Then("the user is presented with a confirmation message")]
        public async Task ThenTheUserIsPresentedWithAConfirmationMessage()
        {
            await Expect(_world.Pages.resetPage._labelConfirm).ToBeVisibleAsync();
        }
    }
}
