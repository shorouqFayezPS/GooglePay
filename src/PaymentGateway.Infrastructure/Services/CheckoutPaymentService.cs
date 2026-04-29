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
                address = new { country = request.BillingCountry }
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
            // Google Pay support — merchantId is required by Checkout.com
            payment_method_configuration = new
            {
                google_pay = new
                {
                    enabled = true,
                    merchant_id = request.GatewayMerchantId
                }
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
    //  Google Pay — Step 1: Tokenize
    //  POST /tokens
    //  Bearer = PublicKey  ← NOT the secret key
    // ─────────────────────────────────────────────

    public async Task<GooglePayTokenizeResult> TokenizeGooglePayAsync(
        GooglePayTokenizeRequest request, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("CheckoutApi");

        // /tokens uses the PUBLIC key as Bearer — this is intentional
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", request.PublicKey);

        var body = new
        {
            type = "googlepay",
            token_data = request.TokenData   // raw encrypted PaymentData from Google SDK
        };

        var rawRequest = JsonSerializer.Serialize(body, _jsonOpts);
        var content = new StringContent(rawRequest, Encoding.UTF8, "application/json");

        _logger.LogInformation("Tokenizing Google Pay token via Checkout /tokens");

        HttpResponseMessage response;
        try
        {
            var endpoint = request.ApiBaseUrl.TrimEnd('/') + "/tokens";
            response = await client.PostAsync(endpoint, content, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP call to Checkout /tokens failed");
            return new GooglePayTokenizeResult
            {
                Success = false,
                RawRequest = rawRequest,
                ErrorMessage = ex.Message
            };
        }

        var rawResponse = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Checkout /tokens error {StatusCode}: {Body}",
                (int)response.StatusCode, rawResponse);

            return new GooglePayTokenizeResult
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

        return new GooglePayTokenizeResult
        {
            Success = true,
            Token = root.TryGetProperty("token", out var tok) ? tok.GetString() : null,
            TokenFormat = root.TryGetProperty("token_format", out var fmt) ? fmt.GetString() : null,
            RawRequest = rawRequest,
            RawResponse = rawResponse
        };
    }

    // ─────────────────────────────────────────────
    //  Google Pay — Step 2: Charge
    //  POST /payments
    //  Bearer = SecretKey
    //  3ds.enabled = true  when token_format=pan_only
    // ─────────────────────────────────────────────

    public async Task<GooglePayChargeResult> ChargeGooglePayTokenAsync(
        GooglePayChargeRequest request, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("CheckoutApi");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", request.SecretKey);

        // Build payment body — conditionally include 3DS block
        var amountInMinorUnits = ConvertToMinorUnits(request.Amount, request.Currency);

        object body;

        if (request.ApplyThreeDs)
        {
            // pan_only: card is stored in Google account, not device-bound.
            // Must run 3DS. If enrolled, Checkout returns 202 Pending + redirect URL.
            body = new
            {
                source = new { type = "token", token = request.CheckoutToken },
                amount = amountInMinorUnits,
                currency = request.Currency,
                reference = request.OrderNumber,
                processing_channel_id = request.ProcessingChannelId,
                success_url = request.SuccessRedirectUrl,
                failure_url = request.FailureRedirectUrl,
                metadata = new Dictionary<string, string>
                {
                    ["transaction_id"] = request.TransactionId.ToString(),
                    ["order_number"]   = request.OrderNumber
                },
                three_ds = new { enabled = true }
            };
        }
        else
        {
            // cryptogram_3ds: Android device-bound, Google already authenticated.
            // No additional 3DS — just charge directly.
            body = new
            {
                source = new { type = "token", token = request.CheckoutToken },
                amount = amountInMinorUnits,
                currency = request.Currency,
                reference = request.OrderNumber,
                processing_channel_id = request.ProcessingChannelId,
                metadata = new Dictionary<string, string>
                {
                    ["transaction_id"] = request.TransactionId.ToString(),
                    ["order_number"]   = request.OrderNumber
                }
            };
        }

        var rawRequest = JsonSerializer.Serialize(body, _jsonOpts);
        var content = new StringContent(rawRequest, Encoding.UTF8, "application/json");

        _logger.LogInformation(
            "Charging Google Pay token. OrderNumber={OrderNumber}, ApplyThreeDs={Apply3DS}",
            request.OrderNumber, request.ApplyThreeDs);

        HttpResponseMessage response;
        try
        {
            var endpoint = request.ApiBaseUrl.TrimEnd('/') + "/payments";
            response = await client.PostAsync(endpoint, content, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP call to Checkout /payments failed");
            return new GooglePayChargeResult
            {
                Success = false,
                RawRequest = rawRequest,
                ErrorMessage = ex.Message
            };
        }

        var rawResponse = await response.Content.ReadAsStringAsync(ct);

        _logger.LogInformation("Checkout /payments responded with {StatusCode}",
            response.StatusCode);

        // 202 is valid — means 3DS redirect is required (pan_only + enrolled card)
        if (!response.IsSuccessStatusCode && (int)response.StatusCode != 202)
        {
            _logger.LogWarning("Checkout /payments error {StatusCode}: {Body}",
                (int)response.StatusCode, rawResponse);

            return new GooglePayChargeResult
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

        var status = root.TryGetProperty("status", out var s) ? s.GetString() : null;

        // Extract 3DS redirect URL when status=Pending
        string? redirectUrl = null;
        if (root.TryGetProperty("_links", out var links) &&
            links.TryGetProperty("redirect", out var redirect) &&
            redirect.TryGetProperty("href", out var href))
        {
            redirectUrl = href.GetString();
        }

        return new GooglePayChargeResult
        {
            Success = true,
            PaymentId = root.TryGetProperty("id", out var id) ? id.GetString() : null,
            Status = status,
            RedirectUrl = redirectUrl,
            RawRequest = rawRequest,
            RawResponse = rawResponse
        };
    }


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
