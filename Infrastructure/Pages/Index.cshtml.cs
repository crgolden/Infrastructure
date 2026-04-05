namespace Infrastructure.Pages;

using Microsoft.AspNetCore.Mvc.RazorPages;

public sealed class IndexModel : PageModel
{
    private readonly ILogger _logger;

    public IndexModel(ILogger<IndexModel> logger)
    {
        _logger = logger;
    }

    public void OnGet()
    {
        _logger.LogDebug("Dashboard requested.");
    }
}
