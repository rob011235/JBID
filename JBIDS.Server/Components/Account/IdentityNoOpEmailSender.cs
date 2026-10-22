// Copyright (c) 2026 Robert Garner
// SPDX-License-Identifier: MIT

namespace JBIDS.Server.Components.Account
{
    using JBIDS.Server.Data;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Identity.UI.Services;

    // Remove the "else if (EmailSender is IdentityNoOpEmailSender)" block from RegisterConfirmation.razor after updating with a real implementation.

    /// <summary>
    /// Provides placeholder email delivery for account development.
    /// </summary>
    internal sealed class IdentityNoOpEmailSender : IEmailSender<ApplicationUser>
    {
        /// <summary>
        /// Discards messages until a real email sender is configured.
        /// </summary>
        private readonly IEmailSender emailSender = new NoOpEmailSender();

        /// <inheritdoc/>
        public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
            this.emailSender.SendEmailAsync(email, "Confirm your email", $"Please confirm your account by <a href='{confirmationLink}'>clicking here</a>.");

        /// <inheritdoc/>
        public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
            this.emailSender.SendEmailAsync(email, "Reset your password", $"Please reset your password by <a href='{resetLink}'>clicking here</a>.");

        /// <inheritdoc/>
        public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
            this.emailSender.SendEmailAsync(email, "Reset your password", $"Please reset your password using the following code: {resetCode}");
    }
}
