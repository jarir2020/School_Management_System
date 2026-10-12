using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using SchoolManagement.Application.Administration;
using SchoolManagement.Infrastructure.Identity;

namespace SchoolManagement.Web.Components.Account;

internal sealed class QueuedIdentityEmailSender(IPhaseSixService phaseSix) : IEmailSender<ApplicationUser>
{
    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        phaseSix.QueueEmailAsync(
            email,
            "Confirm your school account",
            $"<p>Please confirm your account by <a href=\"{System.Net.WebUtility.HtmlEncode(confirmationLink)}\">opening this link</a>.</p>",
            $"identity-confirmation:{user.Id}:{Guid.NewGuid():N}");

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        phaseSix.QueueEmailAsync(
            email,
            "Reset your school account password",
            $"<p>Please reset your password by <a href=\"{System.Net.WebUtility.HtmlEncode(resetLink)}\">opening this link</a>.</p>",
            $"identity-reset-link:{user.Id}:{Guid.NewGuid():N}");

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        phaseSix.QueueEmailAsync(
            email,
            "Reset your school account password",
            $"<p>Use this code to reset your password:</p><p><strong>{System.Net.WebUtility.HtmlEncode(resetCode)}</strong></p>",
            $"identity-reset-code:{user.Id}:{Guid.NewGuid():N}");
}
