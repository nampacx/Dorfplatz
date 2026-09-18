---
description: "Use when adding or changing backend logic in api/ (Azure Functions, Services, Data). Requires xUnit test coverage for new/changed behavior."
applyTo: "api/**/*.cs"
---
# Backend Testing Guidelines

- All backend functions (`api/Functions/**`) and services (`api/Services/**`) must be covered by xUnit tests when added or changed.
- Place tests in a sibling `api/BulletinBoard.Api.Tests` project (create it if it doesn't exist yet), mirroring the folder structure of the code under test (e.g. `Functions/GoodsFunctionsTests.cs` for `Functions/GoodsFunctions.cs`).
- Use `[Fact]`/`[Theory]` with clear Arrange/Act/Assert sections.
- Prefer testing against an in-memory/SQLite `AppDbContext` over mocking EF Core directly, so query logic (`.Where`, `.OrderBy`, paging) is actually exercised.
- Keep tests simple: one behavior per test, descriptive test names (`MethodName_Scenario_ExpectedResult`).
