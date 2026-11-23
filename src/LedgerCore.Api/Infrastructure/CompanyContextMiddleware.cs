using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Application.Features.Companies;

namespace LedgerCore.Api.Infrastructure;

/// <summary>
/// Ustala firmę, w której pracuje użytkownik (nagłówek X-Company-Id; bez nagłówka – pierwsza dostępna firma)
/// i odmawia dostępu do firm, do których użytkownik nie ma uprawnień. Reszta aplikacji widzi wyłącznie dane tej firmy.
/// </summary>
public class CompanyContextMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Company-Id";

    public async Task Invoke(HttpContext http, ICompanyContext company, IAppDbContext db, IIdentityService identity, IUserDetails me)
    {
        if (http.User.Identity?.IsAuthenticated == true && me.User is not null)
        {
            var accessible = await CompanyAccess.Accessible(db, identity, me.User.Id, http.RequestAborted);
            var header = http.Request.Headers[HeaderName].FirstOrDefault();
            // Endpointy niezależne od firmy (profil, lista firm) działają nawet z nieaktualnym nagłówkiem – inaczej zapamiętana,
            // utracona firma blokowałaby logowanie.
            var companyAgnostic = http.Request.Path.StartsWithSegments("/api/v1/auth") || http.Request.Path.StartsWithSegments("/api/v1/companies/mine");
            if (Guid.TryParse(header, out var requested) && !(companyAgnostic && accessible.All(c => c.Id != requested)))
            {
                if (accessible.All(c => c.Id != requested))
                {
                    http.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await http.Response.WriteAsJsonAsync(new { title = "Brak dostępu do firmy", detail = "Nie masz dostępu do wybranej firmy.", status = 403 });
                    return;
                }
                company.CompanyId = requested;
            }
            else company.CompanyId = accessible.FirstOrDefault()?.Id ?? Guid.Empty;
        }
        await next(http);
    }
}
