using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ServicePortal.Web.Models;

namespace ServicePortal.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult TriggerError()
    {
        _logger.LogInformation("Demonstration error triggered manually via Home/TriggerError.");
        throw new ApplicationException("Simulated error to demonstrate centralized exception handling and correlation ID tracking.");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(string? correlationId = null)
    {
        var id = correlationId ?? HttpContext.Items["CorrelationId"]?.ToString() ?? Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        return View(new ErrorViewModel { RequestId = id });
    }
}
