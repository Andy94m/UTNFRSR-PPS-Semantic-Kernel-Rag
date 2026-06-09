/// <summary>
/// Code-behind de la página de error global (/Error).
/// Captura información de la solicitud que falló para mostrarla en la UI.
/// </summary>
/// <remarks>
/// ── Configuración ───────────────────────────────────────────────────────────
/// - [ResponseCache]: No cachea la página de error (siempre fresca)
/// - [IgnoreAntiforgeryToken]: No requiere token antifalsificación (solo GET)
///
/// ── Dependencias ────────────────────────────────────────────────────────────
/// ILogger&lt;ErrorModel&gt; _logger (inyectado por constructor):
///   - Serilog con sink a consola y archivo logs/chatrag-*.log
///   - Loggea el acceso a la página de error automáticamente via ASP.NET Core
///
/// ── OnGet() ─────────────────────────────────────────────────────────────────
/// Método ejecutado cuando se hace GET a /Error.
/// Obtiene el RequestId de:
///   1. Activity.Current?.Id (distributed tracing, si hay Activity activo)
///   2. Fallback: HttpContext.TraceIdentifier (ID único de la solicitud HTTP)
///
/// ── Propiedades ─────────────────────────────────────────────────────────────
/// RequestId (string?): ID de la solicitud que causó el error
/// ShowRequestId (bool): true si RequestId no es null ni vacío
///   → Controla si se muestra el Request ID en la UI (Error.cshtml)
///
/// ⚠️ Esta página NO se muestra en Development (DetailedErrors=true muestra
///    la excepción directamente en el navegador vía middleware de desarrollo).
///    Se usa en Staging/Production y cuando el middleware de error global atrapa
///    una excepción no manejada.
/// </remarks>
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChatRAG.Pages;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public class ErrorModel : PageModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    private readonly ILogger<ErrorModel> _logger;

    public ErrorModel(ILogger<ErrorModel> logger)
    {
        _logger = logger;
    }

    public void OnGet()
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
    }
}
