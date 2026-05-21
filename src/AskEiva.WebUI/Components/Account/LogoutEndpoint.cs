using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using AskEiva.Domain.Entities;

namespace AskEiva.WebUI.Components.Account;

public static class LogoutEndpoint
{
    public static IEndpointConventionBuilder MapCustomLogoutEndpoint(this IEndpointRouteBuilder endpoints)
    {
        // 💡 We change the routing map to a clean GET route, bypassing form tokens completely
        return endpoints.MapGet("Account/ForceLogout", async (
            SignInManager<ApplicationUser> signInManager, 
            string? returnUrl) =>
        {
            await signInManager.SignOutAsync();
            
            var cleanUrl = string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl;
            return Results.Redirect($"~{cleanUrl}");
        });
    }
}