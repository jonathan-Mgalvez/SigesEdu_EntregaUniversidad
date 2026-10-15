using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SigesEdu.Domain.Enums;

namespace SigesEdu.UI.Filters;

/// <summary>
/// Interceptor de autorización que restringe acciones según los roles RBAC definidos en el SAD/SRS.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class AuthorizeRolesAttribute : Attribute, IAuthorizationFilter
{
    private readonly RolUsuario[] _rolesPermitidos;

    public AuthorizeRolesAttribute(params RolUsuario[] rolesPermitidos)
    {
        _rolesPermitidos = rolesPermitidos;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        // 1. Obtener la sesión activa del usuario
        var rolSession = context.HttpContext.Session.GetString("UsuarioRol");
        var usuarioIdSession = context.HttpContext.Session.GetInt32("UsuarioId");

        // Si no hay sesión activa, redirige a la pantalla de Login
        if (usuarioIdSession == null || string.IsNullOrWhiteSpace(rolSession))
        {
            context.Result = new RedirectToActionResult("Login", "Auth", null);
            return;
        }

        // 2. Validar parseo del rol
        if (!Enum.TryParse<RolUsuario>(rolSession, out var rolUsuario))
        {
            context.Result = new ForbidResult();
            return;
        }

        // 3. Evaluar coincidencia contra la matriz RBAC requerida
        if (_rolesPermitidos.Length > 0 && !_rolesPermitidos.Contains(rolUsuario))
        {
            // Retorna HTTP 403 Forbidden si el perfil no posee el privilegio
            context.Result = new ViewResult
            {
                ViewName = "~/Views/Shared/AccessDenied.cshtml"
            };
        }
    }
}