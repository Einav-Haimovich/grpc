using Basics;
using Microsoft.AspNetCore.Mvc;
using MVCGRPCClient.Models;
using System.Diagnostics;

namespace MVCGRPCClient.Controllers
{
    public class HomeController : Controller
    {

        private readonly FirstServiceDefinition.FirstServiceDefinitionClient _grpcClient;
        private readonly ILogger<HomeController> _logger;

        public HomeController(FirstServiceDefinition.FirstServiceDefinitionClient grpcClient, ILogger<HomeController> logger)
        {
            _grpcClient = grpcClient;
            _logger = logger;
        }

        public IActionResult Index()
        {
            var firstCall = _grpcClient.Unary(new Request { Content = "MVC gRPC Client" });
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
