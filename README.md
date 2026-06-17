# Introduction 
Project for automating the Client Portal for testing purposes.

## Table of Contents
- [Getting Started](#getting-started)
- [VS Code Extensions](#vs-code-extensions)
- [Project Setup](#project-setup)
- [Build Project](#build-project)
- [Configure Playwright](#configure-playwright)
- [Testing](#testing)
- [References](#references)

# Getting Started
What you'll need:
1. .NET 10.0 SDK
2. IDE such as VS Code, Visual Studio or JetBrains Rider
3. Powershell (Not legacy Windows Powershell)

# VS Code Extensions
1. C# Extension by Microsoft (C# and C# Dev Kit)
2. Cucumber for Visual Studio Code
3. Playwright Test for VS Code
4. .NET Extension Pack
5. .NET Install Tool

# Project Setup
Before you can run the project, you will need to configure a few files. All files will be stored on LastPass eventually for security reasons, but till they're approved, manual creation is required.

## Environments
Create `appsettings.local.json` in the `ClientPortal.PRAT.Acceptance` folder with the following structure:

```json
{
  "Environments": {
    "REL": { "BaseUrl": "https://your-rel-url" },
    "QA2": { "BaseUrl": "https://your-qa2-url" },
    "DEV": { "BaseUrl": "https://your-dev-url" }
  }
}
```

## Credentials
Create `credentials.local.json` in the `ClientPortal.PRAT.Acceptance` folder with the following structure:

```json
{
  "Accounts": {
    "goodLogin": { "Email": "your@email.com", "Password": "yourpassword" },
    "badLogin": { "Email": "invalid@email.com", "Password": "wrongpassword" }
  }
}
```

## Test Suite + Test Result Logging
Create `appsettings.local.json` in the `TestRunner.Web` folder with the following structure:

```json
{
  "TestRunner": {
    "WorkingDirectory": "/path/to/ClientPortal.PRAT.Acceptance",
    "ReportsDirectory": "/path/to/ClientPortal.PRAT.Acceptance/TestResults"
  }
}
```

## Enabling Gherkin
Open the project using the workspace file at the solution root:
`ClientPortal.PRAT.code-workspace`

This ensures the Cucumber extension resolves step definitions correctly. This can be done two ways:
### Option 1
1. In terminal, ensure you are in the project root. I.e. `ClientPortal.PRAT` (Use command `pwd` to confirm)
2. Run command `code ClientPortal.PRAT.code-workspace`

### Option 2
1. In Windows File Explorer, go to the project. E.g. `C:\USER\Path\To\ClientPortal.PRAT`
2. Double click file called `ClientPortal.PRAT.code-workspace`

### Option 3
1. In VS Code, go to `File` -> `Open Workspace from File...`
2. Navigate to where the project is stored.
3. Select the file called `ClientPortal.PRAT.code-workspace` and open.


# Build Project
- In a terminal, run the following commands from the solution root:
```
dotnet clean
dotnet restore
dotnet build
```

# Configure Playwright
- In a terminal, navigate to `ClientPortal.PRAT.Acceptance` (Use command `cd ClientPortal.PRAT.Acceptance`)
- Use the command: `pwsh bin/Debug/net10.0/playwright.ps1 install`

# Testing
## Test via Frontend
- In a terminal, navigate to `TestRunner.Web` (Use command `cd TestRunner.Web`)
- Use the command: `dotnet run`
- Open a browser and navigate to `https://localhost:5001` (Port number may vary but terminal output should confirm)

## Test via Terminal
- In a terminal, run the following command from the solution root: `dotnet test ClientPortal.PRAT.Acceptance`

## Configurable Command Parameters (for Terminal)
- `HEADED=1` - Run tests in headed mode (default is headless)
- `BROWSER=chromium` - Browser to use (chromium, firefox, webkit)
- `DEVICE_TYPE=Desktop` - Device type to emulate (Desktop, TabletVer, TabletHor, Mobile - default is Desktop)
- `ENVIRON=REL` - Environment to run tests against (REL, QA2, DEV - default is REL)
- `RECORD_VIDEO=1` - Record video of test execution (default is off)
- `RECORD_TRACES=1` - Record Playwright traces (default is off)

Example command - `HEADED=1 BROWSER=firefox RECORD_VIDEO=1 dotnet test ClientPortal.PRAT.Acceptance`

# References
1. https://dotnet.microsoft.com/en-us/download/dotnet/10.0
2. https://learn.microsoft.com/en-us/dotnet/core/install/
3. https://code.visualstudio.com/docs/languages/csharp
4. https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp
5. https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp-developer-kit
6. https://marketplace.visualstudio.com/items?itemName=ms-playwright.playwright-test
7. https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.vscode-dotnet-pack
8. https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.vscode-dotnet-install-tool
9. https://playwright.dev/dotnet/docs/introduction
10. https://cucumber.io/docs/guides/10-minute-tutorial/
11. https://docs.reqnroll.net/latest/index.html