using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FGate.Areas.Admin.Models;
using FGate.Services;

namespace FGate.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = "AdminCookie", Roles = "Administrator")]
    public class RouteTesterController : Controller
    {
        private readonly IEndpointCategorizer _endpointCategorizer;

        public RouteTesterController(IEndpointCategorizer endpointCategorizer)
        {
            _endpointCategorizer = endpointCategorizer;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var model = new RouteTesterViewModel();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Index(RouteTesterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = _endpointCategorizer.GetEndpointGroupForPath(model.RequestPath);

            model.HasResult = true;
            if (result != null)
            {
                model.MatchedGroupName = result.GroupName;
                model.MatchedPathPattern = result.MatchedPathPattern;
                model.RequiresToken = result.RequiresToken;

                if (result.GroupName == "Public_Group_NoMatch")
                {
                    model.ErrorMessage = "La ruta no coincidió con ningún grupo de endpoints configurado.";
                }
            }
            else
            {
                model.ErrorMessage = "Ocurrió un error al procesar la ruta.";
            }

            return View(model);
        }
    }
}