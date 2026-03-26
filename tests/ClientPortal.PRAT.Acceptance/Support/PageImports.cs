using System;
using Microsoft.Playwright;
using ClientPortal.PRAT.Acceptance.Pages;

namespace ClientPortal.PRAT.Acceptance.Support
{
    public class PageImports
    {
        private readonly TestWorld _world;
        private readonly IPage _page;

        public PageImports(TestWorld world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _page = _world.Page ?? throw new ArgumentNullException(nameof(_world.Page));
        }

        // Legal pages
        public CookiePage cookiePage => new CookiePage(_page);
        public PrivacyPage privacyPage => new PrivacyPage(_page);
        public TermsPage termsPage => new TermsPage(_page);

        // Main functional pages
        public LoginPage loginPage => new LoginPage(_page);
        public PWResetPage resetPage => new PWResetPage(_page);
        public MenuPage menuPage => new MenuPage(_world);
        public DocumentsPage docPage => new DocumentsPage(_page);
        public NotificationsPage notifPage => new NotificationsPage(_page);
        public ProfilePage profilePage => new ProfilePage(_page);

        // Navigation helper needs TestWorld, not just IPage
        public NavigationPage navPage => new NavigationPage(_world);
    }
}
