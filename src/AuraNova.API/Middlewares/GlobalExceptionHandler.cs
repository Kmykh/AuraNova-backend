using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AuraNova.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AuraNova.API.Middlewares
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            _logger.LogError(exception, "Unhandled exception occurred: {Message}", exception.Message);

            var problemDetails = new ProblemDetails
            {
                Instance = httpContext.Request.Path,
                Extensions =
                {
                    ["traceId"] = httpContext.TraceIdentifier
                }
            };

            switch (exception)
            {
                case DomainException domainEx:
                    problemDetails.Title = "Validation Error";
                    problemDetails.Status = domainEx.StatusCode;
                    problemDetails.Detail = domainEx.Message;
                    problemDetails.Type = $"https://api.auranova.pe/errors/{domainEx.ErrorCode.ToLower()}";
                    problemDetails.Extensions["code"] = domainEx.ErrorCode;
                    break;

                case UnauthorizedAccessException:
                    problemDetails.Title = "Unauthorized";
                    problemDetails.Status = StatusCodes.Status401Unauthorized;
                    problemDetails.Detail = "No está autenticado o su sesión ha expirado.";
                    problemDetails.Type = "https://api.auranova.pe/errors/unauthorized";
                    problemDetails.Extensions["code"] = "UNAUTHORIZED";
                    break;

                case ArgumentException argEx:
                    problemDetails.Title = "Invalid Argument";
                    problemDetails.Status = StatusCodes.Status400BadRequest;
                    problemDetails.Detail = argEx.Message;
                    problemDetails.Type = "https://api.auranova.pe/errors/invalid_argument";
                    problemDetails.Extensions["code"] = "INVALID_ARGUMENT";
                    break;
                    
                case HttpRequestException:
                    problemDetails.Title = "External Service Error";
                    problemDetails.Status = StatusCodes.Status502BadGateway;
                    problemDetails.Detail = "Hubo un problema al comunicarse con un servicio externo.";
                    problemDetails.Type = "https://api.auranova.pe/errors/external_service";
                    problemDetails.Extensions["code"] = "EXTERNAL_SERVICE_ERROR";
                    break;

                default:
                    problemDetails.Title = "Internal Server Error";
                    problemDetails.Status = StatusCodes.Status500InternalServerError;
                    problemDetails.Detail = "Ocurrió un error inesperado en el servidor.";
                    problemDetails.Type = "https://api.auranova.pe/errors/internal_error";
                    problemDetails.Extensions["code"] = "INTERNAL_ERROR";
                    break;
            }

            httpContext.Response.StatusCode = problemDetails.Status.Value;
            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

            return true;
        }
    }
}
