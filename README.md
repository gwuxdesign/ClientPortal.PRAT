# Introduction 
Project for automating the Client Portal for testing purposes.

# Getting Started
What is required to run locally:
1.	.NET 10.0 SDK
2.	IDE such as VS Code, Visual Studio 2022 or JetBrains Rider
3. Powershell (Not legacy Windows Powershell)

# VS Code Extensions
1. C# Extension by Microsoft (C# and C# Dev Kit)
2. Cucumber (Gherkin) Full Support
3. Cucumber for Visual Studio Code
4. Playwright Test for VS Code
5. .NET Extension Pack
6. .NET Install Tool

# Build and Test
dotnet clean
dotnet restore
dotnet build
dotnet test tests/ClientPortal.PRAT.Acceptance

# Configurable Command Parameters
HEADED=1 # Run tests in headed mode (default is headless)
BROWSER=chromium # Browser to use (chromium, firefox, webkit)
DEVICE_TYPE=Desktop # Device type to emulate (Desktop, Tablet, Mobile - default is Desktop)
ENVIRON=REL # Environment to run tests against (REL, DEV, QA2 - default is REL)

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