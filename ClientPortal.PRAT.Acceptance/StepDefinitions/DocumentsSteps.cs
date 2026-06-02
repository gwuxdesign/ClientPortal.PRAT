using System.Threading.Tasks;
// using System.Text.RegularExpressions;
using Reqnroll;
using static Microsoft.Playwright.Assertions;
using ClientPortal.PRAT.Acceptance.Support;

namespace ClientPortal.PRAT.Acceptance.StepDefinitions
{
    [Binding]
    public class DocumentsSteps
    {
        private readonly IReqnrollOutputHelper _outputHelper;
        private readonly TestWorld _world;

        public DocumentsSteps(TestWorld world, IReqnrollOutputHelper outputHelper)
        {
            _world = world;
            _outputHelper = outputHelper;
        }

        [When("the user is able to navigate to the documents page")]
        public async Task WhenTheUserIsAbleToNavigateToTheDocumentsPage()
        {
            await Expect(_world.Pages.menuPage._btnMenu).ToBeVisibleAsync();
            await _world.Pages.menuPage.ClickMenuItem("My documents");
        }

        [Then("the correct documents elements are visible")]
        public async Task ThenTheCorrectDocumentsElementsAreVisible()
        {
            await Expect(_world.Pages.docPage._labelTitle).ToBeVisibleAsync();
            await Expect(_world.Pages.menuPage._btnMenu).ToBeVisibleAsync();
            await Expect(_world.Pages.docPage._labelAdvisor).ToBeVisibleAsync();
            await Expect(_world.Pages.docPage._labelEmail).ToBeVisibleAsync();
            await Expect(_world.Pages.docPage._labelPhone).ToBeVisibleAsync();
            await Expect(_world.Pages.docPage._labelUpload).ToBeVisibleAsync();
            await Expect(_world.Pages.docPage._btnUpload).ToBeVisibleAsync();
        }

        [When("the user applies a {string} on the Documents")]
        public async Task WhenTheUserAppliesAOnTheDocuments(string filter)
        {
            await _world.Pages.docPage.FilterDocuments(filter);
        }

        [Then(@"the user should only see the right ""(.*)""")]
        public async Task ThenTheUserShouldOnlySeeTheRight(int count)
        {
            int documentCount = await _world.Pages.docPage._documentResults.CountAsync();

            if (documentCount != count)
            {
                throw new Exception($"Expected {count} documents, but found {documentCount}.");
            }
        }

        [Then("the {string} of documents")]
        public async Task ThenTheOfDocuments(string titlesCsv)
        {
            var expectedTitles = titlesCsv
                    .Split(',')
                    .Select(t => t.Trim())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .ToArray();

            await Expect(_world.Pages.docPage._documentList).ToHaveTextAsync(expectedTitles);
        }
    }
}