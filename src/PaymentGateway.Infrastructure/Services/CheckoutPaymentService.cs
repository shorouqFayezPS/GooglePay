using Microsoft.Extensions.Logging;
using PaymentGateway.Application.Abstractions;
using PaymentGateway.Application.Payments.DTOs;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PaymentGateway.Infrastructure.Services;

public sealed class CheckoutPaymentService : ICheckoutPaymentService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CheckoutPaymentService> _logger;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public CheckoutPaymentService(
        IHttpClientFactory httpClientFactory,
        ILogger<CheckoutPaymentService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    // ─────────────────────────────────────────────
    //  Create Payment Session (Hosted Payments)
    // ─────────────────────────────────────────────

    public async Task<CheckoutSessionResult> CreatePaymentSessionAsync(
        CreateCheckoutSessionRequest request, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("CheckoutApi");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", request.SecretKey);

        // Build Checkout.com hosted-payments request body
        // Amount is in minor units (e.g. BHD 1.000 → 1000, USD 1.00 → 100)
        var amountInMinorUnits = ConvertToMinorUnits(request.Amount, request.Currency);

        var body = new
        {
            amount = amountInMinorUnits,
            currency = request.Currency,
            reference = request.OrderNumber,
            billing = new
            {
                address = new { country = "BH" }  // Extend as needed per gateway config
            },
            customer = new
            {
                email = request.CustomerEmail,
                name = request.CustomerName,
                phone = string.IsNullOrEmpty(request.CustomerPhone)
                    ? null
                    : new { number = request.CustomerPhone }
            },
            success_url = request.SuccessRedirectUrl,
            failure_url = request.FailureRedirectUrl,
            cancel_url = request.CancelUrl,
            processing_channel_id = request.ProcessingChannelId,
            metadata = new Dictionary<string, string>
            {
                ["transaction_id"] = request.TransactionId.ToString(),
                ["order_number"] = request.OrderNumber
            },
            // Google Pay support
            payment_method_configuration = new
            {
                google_pay = new { enabled = true }
            }
        };

        var rawRequest = JsonSerializer.Serialize(body, _jsonOpts);
        var content = new StringContent(rawRequest, Encoding.UTF8, "application/json");

        _logger.LogInformation(
            "Calling Checkout API. OrderNumber={OrderNumber}, Amount={Amount} {Currency}",
            request.OrderNumber, request.Amount, request.Currency);

        HttpResponseMessage response;
        try
        {
            var endpoint = request.ApiBaseUrl.TrimEnd('/') + "/hosted-payments";
            response = await client.PostAsync(endpoint, content, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP call to Checkout API failed");
            return new CheckoutSessionResult
            {
                Success = false,
                RawRequest = rawRequest,
                ErrorMessage = ex.Message
            };
        }

        var rawResponse = await response.Content.ReadAsStringAsync(ct);

        _logger.LogInformation(
            "Checkout API responded with {StatusCode}", response.StatusCode);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Checkout API error {StatusCode}: {Body}",
                (int)response.StatusCode, rawResponse);

            return new CheckoutSessionResult
            {
                Success = false,
                RawRequest = rawRequest,
                RawResponse = rawResponse,
                ErrorMessage = $"HTTP {(int)response.StatusCode}",
                ErrorCode = response.StatusCode.ToString()
            };
        }

        using var doc = JsonDocument.Parse(rawResponse);
        var root = doc.RootElement;

        return new CheckoutSessionResult
        {
            Success = true,
            SessionId = root.TryGetProperty("id", out var idProp) ? idProp.GetString() : null,
            PaymentUrl = root.TryGetProperty("_links", out var links) &&
                         links.TryGetProperty("redirect", out var redirect) &&
                         redirect.TryGetProperty("href", out var href)
                ? href.GetString()
                : null,
            RawRequest = rawRequest,
            RawResponse = rawResponse
        };
    }

    // ─────────────────────────────────────────────
    //  Webhook Signature Verification
    //  Checkout uses HMAC-SHA256 over raw body
    //  Header: cko-signature
    // ─────────────────────────────────────────────

    public bool VerifyWebhookSignature(string payload, string signature, string webhookSecret)
    {
        try
        {
            var secretBytes = Encoding.UTF8.GetBytes(webhookSecret);
            var payloadBytes = Encoding.UTF8.GetBytes(payload);

            using var hmac = new HMACSHA256(secretBytes);
            var computed = hmac.ComputeHash(payloadBytes);
            var computedHex = Convert.ToHexString(computed).ToLowerInvariant();

            // Checkout may prefix signature with "sha256="
            var incomingSig = signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)
                ? signature[7..]
                : signature;

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedHex),
                Encoding.UTF8.GetBytes(incomingSig.ToLowerInvariant()));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Webhook signature verification threw an exception");
            return false;
        }
    }

    // ─────────────────────────────────────────────
    //  Parse Webhook
    // ─────────────────────────────────────────────

    public CheckoutWebhookEvent ParseWebhookEvent(string payload)
    {
        return JsonSerializer.Deserialize<CheckoutWebhookEvent>(
            payload,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Webhook payload deserialized to null.");
    }

    // ─────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────

    private static long ConvertToMinorUnits(decimal amount, string currency)
    {
        // 3-decimal currencies: BHD, KWD, OMR
        // 0-decimal currencies: JPY
        // Default: 2 decimal places
        var decimals = currency.ToUpperInvariant() switch
        {
            "BHD" or "KWD" or "OMR" => 3,
            "JPY" or "KRW" => 0,
            _ => 2
        };

        return (long)Math.Round(amount * (decimal)Math.Pow(10, decimals), MidpointRounding.AwayFromZero);
    }
}
