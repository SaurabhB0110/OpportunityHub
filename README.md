# OpportunityHub

A polished ASP.NET Core MVC hiring portal for internships and jobs.

## Run locally

```powershell
dotnet run
```

Open the local address shown in the terminal (normally `http://localhost:5000`).

## Included

- Responsive candidate-focused homepage
- Search and filtering by keyword, location, type, category, and sort order
- Opportunity detail pages with validated application form
- Saved opportunities stored in the browser
- Employer form for publishing a new opportunity
- Seed data and an in-memory repository, so no database setup is required

## Production roadmap

The repository layer is intentionally behind an interface. Replace `InMemoryOpportunityRepository` with Entity Framework Core and SQL Server, then add ASP.NET Core Identity for candidate and employer accounts before production use.
