# Introduction 
Project for automating the Client Portal for testing purposes.

# Getting Started
What is required to run locally:
1. .NET 10.0 SDK
2. IDE such as VS Code, Visual Studio or JetBrains Rider
3. Powershell (Not legacy Windows Powershell)
4. Create `appsettings.local.json` in the `ClientPortal.PRAT.Acceptance` folder with the following structure:

```json
{
  "Environments": {
    "REL": { "BaseUrl": "https://your-rel-url" },
    "QA2": { "BaseUrl": "https://your-qa2-url" },
    "DEV": { "BaseUrl": "https://your-dev-url" }
  }
}
```

5. Create `credentials.local.json` in the `ClientPortal.PRAT.Acceptance` folder with the following structure:

```json
{
  "Accounts": {
    "goodLogin": { "Email": "your@email.com", "Password": "yourpassword" },
    "badLogin": { "Email": "invalid@email.com", "Password": "wrongpassword" }
  }
}
```

6. Create `appsettings.local.json` in the `TestRunner.Web` folder with the following structure:

```json
{
  "TestRunner": {
    "WorkingDirectory": "/path/to/ClientPortal.PRAT.Acceptance",
    "ReportsDirectory": "/path/to/ClientPortal.PRAT.Acceptance/TestResults"
  }
}
```

# VS Code Setup
Open the project using the workspace file at the solution root:
`ClientPortal.PRAT.code-workspace`

This ensures the Cucumber extension resolves step definitions correctly.

# VS Code Extensions
1. C# Extension by Microsoft (C# and C# Dev Kit)
2. Cucumber (Gherkin) Full Support
3. Cucumber for Visual Studio Code
4. Playwright Test for VS Code
5. .NET Extension Pack
6. .NET Install Tool

# Build
- In a terminal, run the following commands from the solution root:
```
dotnet clean
dotnet restore
dotnet build
```

# Setup Playwright
- In a terminal, navigate to `ClientPortal.PRAT.Acceptance`
- Use the command: `pwsh bin/Debug/net10.0/playwright.ps1 install`

# Test via Terminal
- In a terminal, run the following command from the solution root: `dotnet test ClientPortal.PRAT.Acceptance`

# Test via Frontend
- In a terminal, navigate to `TestRunner.Web`
- Use the command: `dotnet run`
- Open a browser and navigate to `https://localhost:5001`

# Configurable Command Parameters
- `HEADED=1` - Run tests in headed mode (default is headless)
- `BROWSER=chromium` - Browser to use (chromium, firefox, webkit)
- `DEVICE_TYPE=Desktop` - Device type to emulate (Desktop, TabletVer, TabletHor, Mobile - default is Desktop)
- `ENVIRON=REL` - Environment to run tests against (REL, QA2, DEV - default is REL)
- `RECORD_VIDEO=1` - Record video of test execution (default is off)
- `RECORD_TRACES=1` - Record Playwright traces (default is off)

# References
1. https://dotnet.microsoft.com/en-us/download/dotnet/10.0
2. https://learn.microsoft.com/en-us/dotnet/core/install/
3. https://code.visualstudio.com/docs/languages/csharp
4. https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp
5. https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp-developer-kit
6. https://marketplace.visualstudio.com/items?itemName=alexkrechik.cucumberautocomplete
7. https://marketplace.visualstudio.com/items?itemName=alexkrechik.cucumber
8. https://marketplace.visualstudio.com/items?itemName=ms-playwright.playwright-test
9. https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.vscode-dotnet-pack
10. https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.vscode-dotnet-install-tool
11. https://playwright.dev/dotnet/docs/introduction
12. https://cucumber.io/docs/guides/10-minute-tutorial/
13. https://docs.reqnroll.net/latest/index.html