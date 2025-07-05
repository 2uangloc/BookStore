// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Security.Claims;
using System.Threading.Tasks;
using BookStore.Services.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using static BookStoreWeb.Areas.Identity.Pages.Account.LoginModel;

namespace BookStoreWeb.Areas.Identity.Pages.Account
{
    public class LogoutModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ILogger<LogoutModel> _logger;
        private readonly IAuditLogger _auditLogger;
        private readonly UserManager<IdentityUser> _userManager;

        public LogoutModel(SignInManager<IdentityUser> signInManager, ILogger<LogoutModel> logger,
            IAuditLogger auditLogger, UserManager<IdentityUser> userManager)
        {
            _signInManager = signInManager;
            _logger = logger;
            _auditLogger = auditLogger;
            _userManager = userManager;
        }
        [BindProperty]
        public InputModel Input { get; set; }
        public async Task<IActionResult> OnPost(string returnUrl = null)
        {

            // ✅ Lấy UserId và Email trước khi đăng xuất
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = User.FindFirstValue(ClaimTypes.Email);
            var userName = User.Identity?.Name ?? "Unknown";
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            // ✅ Đăng xuất
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out.");
            if (userId != null && email != null)
            {
                await _auditLogger.LogAsync(
                userId: userId,
                userName: userName,
                action: "Logout",
                entityName: "User",
                entityId: userId,
                ipAddress: ipAddress,
                description: $"User {email} has logged out."
                );
            }
            if (returnUrl != null)
            {
                return LocalRedirect(returnUrl);
            }
            else
            {
                // This needs to be a redirect so that the browser performs a new
                // request and the identity for the user gets updated.
                return RedirectToPage();
            }
        }
    }
}
