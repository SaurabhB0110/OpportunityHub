using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using OpportunityHub.Data;
using OpportunityHub.Models;

namespace OpportunityHub.Services;

public class EfOpportunityRepository : IOpportunityRepository
{
    private readonly ApplicationDbContext _db;

    public EfOpportunityRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public IReadOnlyList<Opportunity> GetAll()
    {
        return _db.Opportunities
            .AsNoTracking()
            .OrderByDescending(o => o.PostedAt)
            .ToList();
    }

    public Opportunity? GetById(int id)
    {
        return _db.Opportunities.Find(id);
    }

    public Opportunity Add(CreateOpportunityInput input)
    {
        var item = new Opportunity
        {
            Title = input.Title?.Trim() ?? string.Empty,
            Company = input.Company?.Trim() ?? string.Empty,
            Location = input.Location?.Trim() ?? string.Empty,
            WorkMode = input.WorkMode,
            Type = input.Type,
            Category = input.Category,
            Compensation = input.Compensation?.Trim() ?? string.Empty,
            Experience = input.Experience?.Trim() ?? string.Empty,
            Description = input.Description?.Trim() ?? string.Empty,
            ApplyBy = input.ApplyBy,
            PostedAt = DateTime.UtcNow,
            // Lists in Opportunity are not persisted by default in current model mapping;
            // we still set them for runtime uses (front-end), EF will ignore unmapped collection properties.
            Skills = input.Skills?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList() ?? new List<string>(),
            Responsibilities = new List<string> { "Collaborate with the team on meaningful projects", "Take ownership of assigned tasks and share progress" },
            Requirements = new List<string> { "Strong communication and problem-solving skills", "A curious, proactive approach to learning" },
            LogoColor = "#635bff",
            IsFeatured = false
        };

        _db.Opportunities.Add(item);
        _db.SaveChanges();

        return item;
    }

    // Keep this for UI applicant counter parity; DB application creation is done in JobsController.Apply
    public void AddApplication(int opportunityId, ApplicationInput input)
    {
        var opp = _db.Opportunities.Find(opportunityId);
        if (opp != null)
        {
            opp.Applicants++;
            _db.SaveChanges();
        }
    }
}
