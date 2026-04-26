# PaymentGateway — ASP.NET Core 8 / Checkout.com / Google Pay

Production-ready payment gateway API built with **Clean Architecture**, **CQRS + MediatR**,
**EF Core**, **Polly**, and full **Checkout.com hosted-payments + Google Pay** support.

---

## Project Structure

```
PaymentGateway/
├── src/
│   ├── PaymentGateway.Domain/           # Entities, Enums, Value Objects, Repo Interfaces
│   │   ├── Entities/
│   │   │   ├── PaymentTransaction.cs
│   │   │   ├── PaymentStatusHistory.cs
│   │   │   ├── CallbackLog.cs
│   │   │   └── GatewayConfiguration.cs
│   │   ├── Enums/
│   │   │   ├── PaymentStatus.cs
│   │   │   ├── GatewayType.cs
│   │   │   ├── RequestSource.cs
│   │   │   └── CallbackType.cs
│   │   ├── ValueObjects/
│   │   │   └── CheckoutCredentials.cs
│   │   └── Interfaces/
│   │       └── IRepositories.cs         # IUnitOfWork + all repo interfaces
│   │
│   ├── PaymentGateway.Application/      # CQRS commands, validators, behaviours
│   │   ├── Abstractions/
│   │   │   └── ICheckoutPaymentService.cs
│   │   ├── Common/
│   │   │   ├── Behaviors/PipelineBehaviors.cs   # Logging + Validation
│   │   │   └── Exceptions/DomainExceptions.cs
│   │   ├── Payments/
│   │   │   ├── Commands/
│   │   │   │   ├── InitiatePaymentCommand.cs
│   │   │   │   └── ProcessWebhookCommand.cs
│   │   │   ├── DTOs/PaymentDtos.cs
│   │   │   └── Validators/InitiatePaymentValidator.cs
│   │   └── ApplicationServiceRegistration.cs
│   │
│   ├── PaymentGateway.Infrastructure/   # EF Core, Repositories, HTTP, Polly
│   │   ├── Persistence/
│   │   │   ├── PaymentDbContext.cs
│   │   │   ├── UnitOfWork.cs
│   │   │   ├── Configurations/
│   │   │   │   ├── PaymentTransactionConfiguration.cs
│   │   │   │   └── OtherEntityConfigurations.cs
│   │   │   └── Repositories/Repositories.cs
│   │   ├── Services/CheckoutPaymentService.cs
│   │   └── InfrastructureServiceRegistration.cs
│   │
│   └── PaymentGateway.API/              # Controllers, Middleware, Program.cs
│       ├── Controllers/
│       │   ├── PaymentsController.cs
│       │   └── WebhookController.cs
│       ├── Middleware/ExceptionHandlingMiddleware.cs
│       ├── Program.cs
│       └── appsettings.json
│
└── docs/
    ├── seed_gateway_config.sql
    └── README.md                        ← this file
```

---

## Quick Start

### 1. Prerequisites
- .NET 8 SDK
- SQL Server (LocalDB or full instance)
- Checkout.com Sandbox account

### 2. Database
The schema is **pre-existing** — do not run EF migrations.
Just seed the gateway configuration:

```sql
-- Edit docs/seed_gateway_config.sql with your Checkout sandbox keys, then run it.
```

### 3. Configuration

`appsettings.Development.json` (create locally, never commit):
```json
{
  "ConnectionStrings": {
    "PaymentDb": "Server=.;Database=PaymentGateways;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

Credentials are stored **in the database** (`GatewayConfigurations.CredentialsJson`), not in appsettings.

### 4. Run

```bash
cd src/PaymentGateway.API
dotnet run
# Swagger UI: https://localhost:7xxx/swagger
```

---

## API Endpoints

### POST /api/payments/initiate

Initiates a Checkout.com hosted-payment session (Google Pay enabled by default).

**Request:**
```json
{
  "orderNumber": "ORD-2024-001",
  "countryId": 1,
  "amount": 15.500,
  "currency": "BHD",
  "customerName": "Ahmed Al-Rashid",
  "customerEmail": "ahmed@example.com",
  "customerPhone": "+97312345678",
  "requestSource": 1,
  "successRedirectUrl": "https://myapp.com/payment/success",
  "failureRedirectUrl": "https://myapp.com/payment/failure",
  "cancelUrl": "https://myapp.com/payment/cancel",
  "appCallbackUrl": "https://myapp.com/api/payment-notification",
  "metadata": "{\"userId\": \"USR-999\"}"
}
```

**Optional header:** `X-Payment-Guid: <uuid>` — stored as `HeaderPaymentGuid` for idempotency tracking from the caller.

**Response 200:**
```json
{
  "transactionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "orderNumber": "ORD-2024-001",
  "paymentUrl": "https://pay.sandbox.checkout.com/pages/pl_xxx",
  "status": "Pending",
  "createdAt": "2024-04-25T19:00:00Z"
}
```

**Response 400 (validation):**
```json
{
  "type": "https://httpstatuses.com/400",
  "title": "Validation Failed",
  "status": 400,
  "detail": "One or more validation failures have occurred.",
  "errors": {
    "Currency": ["Currency must be a 3-letter ISO code (e.g. BHD)."],
    "Amount":   ["'Amount' must be greater than '0'."]
  },
  "traceId": "00-abc-01"
}
```

---

### POST /api/payments/webhook/checkout

Receives payment event notifications from Checkout.com.

**Required header:** `cko-signature: <hmac-sha256-hex>`

**Example webhook payload (payment_captured):**
```json
{
  "id": "evt_dj3n2ld4md7u3obykzxl5tzdle",
  "type": "payment_captured",
  "version": "1.0.0",
  "created_on": "2024-04-25T19:05:00Z",
  "data": {
    "id": "pay_mbabizu24mvu3mela5yap3ehu",
    "action_id": "act_mbabizu24mvu3mela5yap3ehu",
    "amount": 15500,
    "currency": "BHD",
    "reference": "ORD-2024-001",
    "response_code": "10000",
    "response_summary": "Approved",
    "metadata": {
      "transaction_id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "order_number": "ORD-2024-001"
    }
  }
}
```

**Supported event types:**

| Checkout Event       | PaymentStatus |
|----------------------|---------------|
| `payment_captured`   | Paid          |
| `payment_approved`   | Authorized    |
| `payment_declined`   | Failed        |
| `payment_expired`    | Failed        |
| `payment_cancelled`  | Cancelled     |
| `payment_refunded`   | Refunded      |

**Response 200:**
```json
{
  "processed": true,
  "result": "Success",
  "message": "Transaction updated to Paid."
}
```

**Duplicate webhook (idempotent):**
```json
{
  "processed": false,
  "result": "Duplicate",
  "message": "Event already processed."
}
```

---

## Security Notes

| Concern | Implementation |
|---|---|
| Webhook authenticity | HMAC-SHA256 over raw body; compared with `cko-signature` header using **fixed-time comparison** to prevent timing attacks |
| Duplicate processing | SHA-256 hash of raw payload stored in `CallbackLogs.IdempotencyHash`; checked before processing |
| Credential storage | Keys stored **encrypted in DB** (`GatewayConfigurations.CredentialsJson`); never in appsettings |
| Concurrency | EF optimistic concurrency; `CallbackLogs` duplicate hash check before insert |
| Input validation | FluentValidation in MediatR pipeline before any handler executes |

---

## Architecture Decisions

### Clean Architecture Layer Rules
```
API  →  Application  →  Domain
              ↑
       Infrastructure (implements Application interfaces)
```
- **Domain** has zero dependencies.
- **Application** depends only on Domain.
- **Infrastructure** depends on Application + Domain (implements interfaces).
- **API** depends on Application + Infrastructure (wires DI only).

### CQRS Flow

```
HTTP POST /api/payments/initiate
  → PaymentsController
    → MediatR.Send(InitiatePaymentCommand)
      → ValidationBehavior (FluentValidation)
        → LoggingBehavior
          → InitiatePaymentCommandHandler
            → IGatewayConfigurationRepository (load credentials)
            → PaymentTransaction.Create(...)
            → IPaymentTransactionRepository.AddAsync(...)
            → ICheckoutPaymentService.CreatePaymentSessionAsync(...)
            → transaction.SetGatewaySession(...)
            → IUnitOfWork.SaveChangesAsync()
            → return InitiatePaymentResponse
```

### Idempotency Implementation

```
Webhook arrives
  → Compute SHA-256(rawPayload) → hash
  → SELECT COUNT(*) FROM CallbackLogs WHERE IdempotencyHash = @hash
  → If exists → return { result: "Duplicate" }
  → If not   → insert CallbackLog with hash → process → mark IsProcessed=true
```

### Retry / Resilience (Polly)

The `CheckoutApi` named `HttpClient` is wired with:
- **Retry**: 3 attempts with exponential back-off (2s, 4s, 8s) on transient HTTP errors
- **Circuit Breaker**: opens after 5 consecutive failures, stays open for 30 seconds

### Google Pay

Enabled in the Checkout hosted-payments request body:
```json
"payment_method_configuration": {
  "google_pay": { "enabled": true }
}
```
Checkout.com handles the Google Pay tokenisation — no additional client-side integration needed when using hosted payments.

---

## Currency Minor-Unit Conversion

| Currency | Decimal Places | Example |
|---|---|---|
| BHD, KWD, OMR | 3 | 1.500 BHD → 1500 |
| USD, EUR, GBP | 2 | 1.00 USD → 100  |
| JPY, KRW | 0 | 100 JPY → 100   |

---

## Error Codes

| HTTP | Scenario |
|---|---|
| 400 | Validation failure / bad webhook payload |
| 401 | Invalid `cko-signature` |
| 404 | TransactionId not found |
| 502 | Checkout.com returned an error |
| 503 | No active gateway configuration found |
| 500 | Unexpected internal error |
