using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using SigesEdu.Application.Services;
using SigesEdu.Domain.Enums; 
using System;

namespace SigesEdu.UI.Controllers;

public class AuthController : Controller
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    // Muestra la pantalla de Login
    [HttpGet]
    public IActionResult Login()
    {
        // Si ya hay sesión activa, lo sacamos del login
        if (HttpContext.Session.GetInt32("UsuarioId") != null)
        {
            return RedirectToAction("Index", "Home");
        }
        return View();
    }

    // Procesa los datos del formulario
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ViewBag.Error = "Debe ingresar usuario y contraseña.";
            return View();
        }

        // Captura la IP (Requerido por tu método IniciarSesionAsync)
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        try
        {
            var sessionDto = await _authService.IniciarSesionAsync(username, password, ipAddress);

            if (sessionDto == null)
            {
                ViewBag.Error = "Credenciales inválidas.";
                return View();
            }

            // Guardamos las variables convirtiendo el Enum a String con .ToString()
            HttpContext.Session.SetInt32("UsuarioId", sessionDto.UsuarioId);
            HttpContext.Session.SetString("UsuarioRol", sessionDto.Rol.ToString());
            HttpContext.Session.SetString("Username", sessionDto.Username);

            // Redirección dinámica usando directamente el enumerador RolUsuario
            return sessionDto.Rol switch
            {
                RolUsuario.Padre => RedirectToAction("Solicitud", "Matricula"),
                RolUsuario.Tesorero => RedirectToAction("ValidacionPago", "Tesoreria"),
                RolUsuario.Coordinador => RedirectToAction("EstructuraCurricular", "Coordinacion"),
                RolUsuario.Docente => RedirectToAction("IngresoNotas", "Docente"),
                RolUsuario.Estudiante => RedirectToAction("Dashboard", "Estudiante"),
                RolUsuario.Administrador => RedirectToAction("PanelControl", "Administracion"),
                _ => RedirectToAction("Index", "Home")
            };
        }
        catch (UnauthorizedAccessException ex)
        {
            // Este catch atrapa el error de tu AuthService si la cuenta superó intentos fallidos
            ViewBag.Error = ex.Message; 
            return View();
        }
        catch (Exception ex)
        {
            // Ahora nos mostrará el error técnico real de C# o SQLite
            ViewBag.Error = $"Error interno: {ex.Message}";
            return View();
        }
    }

    // Cierra la sesión y limpia la memoria
    [HttpGet]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Login");
    }
}