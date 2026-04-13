# velo-center

Desktop application for analyzing cycling progress with a local-first architecture.

## Stack

- Avalonia UI on .NET 10
- CommunityToolkit.Mvvm
- SQLite planned for persistence
- xUnit for tests

## Project layout

- `src/VeloCenter.App` - Avalonia desktop UI
- `src/VeloCenter.Core` - domain models and application logic
- `src/VeloCenter.Infrastructure` - data access and external integrations
- `tests/VeloCenter.Tests` - automated tests

## Getting started

1. Install the .NET 10 SDK.
2. Restore packages:

```powershell
dotnet restore .\VeloCenter.sln
```

3. Run the desktop app:

```powershell
dotnet run --project .\src\VeloCenter.App\VeloCenter.App.csproj
```

4. Run tests:

```powershell
dotnet test .\VeloCenter.sln
```

## Next milestones

- Add SQLite and EF Core
- Import FIT or GPX files
- Build the activities list and details view
- Add weekly and monthly training summaries
