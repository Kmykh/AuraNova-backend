using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.OutputCaching;

namespace AuraNova.API.Configuration
{
    /// <summary>
    /// Caché de salida para el catálogo público (productos y categorías). Cada visita al catálogo ya no
    /// ejecuta consultas a la BD; se invalida en cuanto el panel modifica productos, categorías o campañas.
    /// </summary>
    public static class CatalogCache
    {
        public const string Policy = "public-catalog";
        public const string Tag = "catalog";
        public static readonly TimeSpan Duration = TimeSpan.FromSeconds(60);
    }

    /// <summary>
    /// Invalida la caché del catálogo después de cualquier operación de escritura exitosa (POST/PUT/PATCH/DELETE).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class EvictCatalogCacheAttribute : Attribute, IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var executed = await next();

            if (HttpMethods.IsGet(context.HttpContext.Request.Method) || executed.Exception is { } && !executed.ExceptionHandled)
                return;

            var statusCode = executed.Result switch
            {
                Microsoft.AspNetCore.Mvc.Infrastructure.IStatusCodeActionResult { StatusCode: { } code } => code,
                _ => context.HttpContext.Response.StatusCode
            };

            if (statusCode is < 200 or >= 300)
                return;

            var store = context.HttpContext.RequestServices.GetService<IOutputCacheStore>();
            if (store != null)
                await store.EvictByTagAsync(CatalogCache.Tag, context.HttpContext.RequestAborted);
        }
    }
}
