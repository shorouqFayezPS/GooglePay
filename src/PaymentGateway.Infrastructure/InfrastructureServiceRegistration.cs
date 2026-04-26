using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PaymentGateway.Domain.Interfaces;
using PaymentGateway.Infrastructure.Persistence;
using PaymentGateway.Infrastructure.Persistence.Repositories;
using PaymentGateway.Infrastructure.Services;
using PaymentGateway.Application.Abstractions;
using Polly;
using Polly.Extensions.Http;

namespace PaymentGateway.Infrastructure;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── EF Core ──────────────────────────────
        services.AddDbContext<PaymentDbContext>(opts =>
            opts.UseSqlServer(
                configuration.GetConnectionString("PaymentDb"),
                sql => sql
                    .EnableRetryOnFailure(3)
                    .CommandTimeout(30)));

        // ── Repositories ─────────────────────────
        services.AddScoped<IPaymentTransactionRepository, PaymentTransactionRepository>();
        services.AddScoped<ICallbackLogRepository, CallbackLogRepository>();
        services.AddScoped<IPaymentStatusHistoryRepository, PaymentStatusHistoryRepository>();
        services.AddScoped<IGatewayConfigurationRepository, GatewayConfigurationRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // ── Checkout HTTP Client with Polly retry ─
        services.AddHttpClient("CheckoutApi", c =>
            {
                c.Timeout = TimeSpan.FromSeconds(30);
                c.DefaultRequestHeaders.Add("Accept", "application/json");
            })
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());

        // ── Payment Service ───────────────────────
        services.AddScoped<ICheckoutPaymentService, CheckoutPaymentService>();

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, delay, attempt, _) =>
                {
                    Console.WriteLine(
                        $"[Polly] Retry {attempt} after {delay.TotalSeconds:N1}s — " +
                        $"{outcome.Exception?.Message ?? outcome.Result.StatusCode.ToString()}");
                });

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
}
