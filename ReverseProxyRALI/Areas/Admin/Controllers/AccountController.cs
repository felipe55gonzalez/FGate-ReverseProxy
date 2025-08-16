using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using FGate.Areas.Admin.Models;

namespace FGate.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AccountController : Controller
    {
        private readonly ILogger<AccountController> _logger;

        public AccountController(ILogger<AccountController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        [IgnoreAntiforgeryToken]
        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login([FromForm] LoginViewModel model, string returnUrl = null)
        {
            _logger.LogInformation("--- Inicio de Intento de Login ---");
            _logger.LogInformation("Usuario recibido: {Username}", model.UserName);
            _logger.LogInformation("Contraseña recibida: {Password}", model.Password);

            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                _logger.LogInformation("ModelState es VÁLIDO.");

                if (model.UserName.Equals("admin", StringComparison.OrdinalIgnoreCase) && model.Password == "ProxyAdmin123!")
                {
                    _logger.LogInformation("¡Credenciales CORRECTAS! Creando cookie de autenticación.");
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, model.UserName),
                        new Claim("FullName", "Administrador del Proxy"),
                        new Claim(ClaimTypes.Role, "Administrator"),
                    };

                    var claimsIdentity = new ClaimsIdentity(claims, "AdminCookie");
                    var authProperties = new AuthenticationProperties { IsPersistent = model.RememberMe };
                    await HttpContext.SignInAsync("AdminCookie", new ClaimsPrincipal(claimsIdentity), authProperties);

                    _logger.LogInformation("Redirigiendo a: {ReturnUrl}", returnUrl ?? "/Admin/Home/Index");
                    return RedirectToLocal(returnUrl);
                }

                _logger.LogWarning("Credenciales INCORRECTAS. Añadiendo error al modelo.");
                ModelState.AddModelError(string.Empty, "Intento de login inválido.");
            }
            else
            {
                _logger.LogWarning("ModelState es INVÁLIDO.");
                foreach (var state in ModelState)
                {
                    foreach (var error in state.Value.Errors)
                    {
                        _logger.LogWarning("- Error en '{Key}': {ErrorMessage}", state.Key, error.ErrorMessage);
                    }
                }
            }

            _logger.LogInformation("--- Fin de Intento de Login (Fallido) ---");
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("AdminCookie");
            return RedirectToAction("Login", "Account", new { Area = "Admin" });
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToAction("Index", "Home", new { Area = "Admin" });
        }
    }
}