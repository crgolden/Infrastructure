namespace Infrastructure.Pages;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

[Authorize]
public sealed class IndexModel : PageModel
{
}
