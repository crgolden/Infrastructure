namespace Infrastructure.Pages;

using Microsoft.AspNetCore.Mvc.RazorPages;

public sealed class IndexModel(ILogger<IndexModel> logger) : PageModel
{
    public void OnGet()
    {
        logger.LogDebug("Dashboard requested.");
    }
}
