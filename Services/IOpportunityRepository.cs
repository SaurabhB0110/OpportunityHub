using OpportunityHub.Models;

namespace OpportunityHub.Services;

public interface IOpportunityRepository
{
    IReadOnlyList<Opportunity> GetAll();
    Opportunity? GetById(int id);
    Opportunity Add(CreateOpportunityInput input);
    void AddApplication(int opportunityId, ApplicationInput input);
}
