using System.Net;
using System.Text.Json;
using PaymentGateway.Application.Common.Exceptions;
using ValidationException = PaymentGateway.Application.Common.Exceptions.ValidationException;

namespace PaymentGateway.API.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);

        var (statusCode, title, detail, errors) = exception switch
        {
            ValidationException ve =>
                (HttpStatusCode.BadRequest, "Validation Failed", exception.Message, ve.Errors),

            GatewayConfigNotFoundException =>
                (HttpStatusCode.ServiceUnavailable, "Gateway Unavailable", exception.Message, (IDictionary<string, string[]>?)null),

            TransactionNotFoundException =>
                (HttpStatusCode.NotFound, "Not Found", exception.Message, (IDictionary<string, string[]>?)null),

            WebhookSignatureException =>
                (HttpStatusCode.Unauthorized, "Signature Invalid", exception.Message, (IDictionary<string, string[]>?)null),

            WebhookParsingException =>
                (HttpStatusCode.BadRequest, "Bad Request", exception.Message, (IDictionary<string, string[]>?)null),

            PaymentSessionCreationException =>
                (HttpStatusCode.BadGateway, "Payment Gateway Error", exception.Message, (IDictionary<string, string[]>?)null),

            _ =>
                (HttpStatusCode.InternalServerError, "Internal Server Error",
                 "An unexpected error occurred.", (IDictionary<string, string[]>?)null)
        };

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new
        {
            type = $"https://httpstatuses.com/{(int)statusCode}",
            title,
            status = (int)statusCode,
            detail,
            errors,
            traceId = context.TraceIdentifier
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(problem, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            }));
    }
}
