// Copyright (c) 2026 Robert Garner
// SPDX-License-Identifier: MIT

namespace JBIDS.Server.Components.Account
{
    using JBIDS.Server.Data;
    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Identity;

    /// <summary>
    /// Navigates between account pages and carries short-lived status messages.
    /// </summary>
    /// <param name="navigationManager">The navigation service for the current request or circuit.</param>
    internal sealed class IdentityRedirectManager(NavigationManager navigationManager)
    {
        /// <summary>
        /// Identifies the cookie used to display an account status message.
        /// </summary>
        public const string StatusCookieName = "Identity.StatusMessage";

        /// <summary>
        /// Defines the security settings and lifetime of the status cookie.
        /// </summary>
        private static readonly CookieBuilder StatusCookieBuilder = new CookieBuilder()
        {
            SameSite = SameSiteMode.Strict,
            HttpOnly = true,
            IsEssential = true,
            MaxAge = TimeSpan.FromSeconds(5),
        };

        /// <summary>
        /// Gets the absolute path of the current page without its query string.
        /// </summary>
        private string CurrentPath => navigationManager.ToAbsoluteUri(navigationManager.Uri).GetLeftPart(UriPartial.Path);

        /// <summary>
        /// Navigates to a URI within the application.
        /// </summary>
        /// <param name="uri">The destination URI, or null for the application root.</param>
        public void RedirectTo(string? uri)
        {
            uri ??= string.Empty;

            // Prevent open redirects.
            if (!Uri.IsWellFormedUriString(uri, UriKind.Relative)
                || uri.StartsWith("//", StringComparison.Ordinal))
            {
                uri = navigationManager.ToBaseRelativePath(uri);
            }

            navigationManager.NavigateTo(uri);
        }

        /// <summary>
        /// Navigates to a page with the supplied query parameters.
        /// </summary>
        /// <param name="uri">The destination URI.</param>
        /// <param name="queryParameters">The query parameter names and values to apply.</param>
        public void RedirectTo(string uri, Dictionary<string, object?> queryParameters)
        {
            var uriWithoutQuery = navigationManager.ToAbsoluteUri(uri).GetLeftPart(UriPartial.Path);
            var newUri = navigationManager.GetUriWithQueryParameters(uriWithoutQuery, queryParameters);
            this.RedirectTo(newUri);
        }

        /// <summary>
        /// Stores a status message and navigates to the requested page.
        /// </summary>
        /// <param name="uri">The destination URI.</param>
        /// <param name="message">The message to display after navigation.</param>
        /// <param name="context">The HTTP context used to write the status cookie.</param>
        public void RedirectToWithStatus(string uri, string message, HttpContext context)
        {
            context.Response.Cookies.Append(StatusCookieName, message, StatusCookieBuilder.Build(context));
            this.RedirectTo(uri);
        }

        /// <summary>
        /// Reloads the current page without its query string.
        /// </summary>
        public void RedirectToCurrentPage() => this.RedirectTo(this.CurrentPath);

        /// <summary>
        /// Reloads the current page with a status message.
        /// </summary>
        /// <param name="message">The message to display after navigation.</param>
        /// <param name="context">The HTTP context used to write the status cookie.</param>
        public void RedirectToCurrentPageWithStatus(string message, HttpContext context)
            => this.RedirectToWithStatus(this.CurrentPath, message, context);

        /// <summary>
        /// Navigates to the invalid-user page with the missing user identifier.
        /// </summary>
        /// <param name="userManager">The manager used to read the current user identifier.</param>
        /// <param name="context">The HTTP context containing the current principal.</param>
        public void RedirectToInvalidUser(UserManager<ApplicationUser> userManager, HttpContext context)
            => this.RedirectToWithStatus("Account/InvalidUser", $"Error: Unable to load user with ID '{userManager.GetUserId(context.User)}'.", context);
    }
}
