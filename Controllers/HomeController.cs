using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OpportunityHub.Models;
using OpportunityHub.Services;

namespace OpportunityHub.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IOpportunityRepository _repository;

    public HomeController(ILogger<HomeController> logger, IOpportunityRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    public IActionResult Index()
    {
        var all = _repository.GetAll();
        return View(new HomeViewModel
        {
            Featured = all.Where(x => x.IsFeatured).Take(4).ToList(),
            CategoryCounts = all.GroupBy(x => x.Category).ToDictionary(x => x.Key, x => x.Count()),
            OpportunityCount = all.Count
        });
    }

    public IActionResult About()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
