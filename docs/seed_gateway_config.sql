-- ============================================================
--  Seed: Checkout.com Sandbox Configuration
--  GatewayType 8 = Checkout
--  CountryId 1  = Bahrain (example)
-- ============================================================
USE [PaymentGateways]
GO

INSERT INTO [Payment].[GatewayConfigurations]
    ([CountryId], [GatewayType], [Name], [DisplayName],
     [ApiBaseUrl], [Environment], [WebsiteUrl],
     [CredentialsJson], [IsDeleted], [CreatedDate], [CreatedBy])
VALUES
(
    1,          -- CountryId = Bahrain
    8,          -- GatewayType = Checkout
    N'CheckoutSandbox',
    N'Checkout.com (Sandbox)',
    N'https://api.sandbox.checkout.com',
    N'Sandbox',
    N'https://sandbox.checkout.com',
    -- CredentialsJson: replace values with your real Sandbox keys
    N'{
        "SecretKey": "sk_sbox_XXXXXXXXXXXXXXXXXXXXXXXX",
        "PublicKey": "pk_sbox_XXXXXXXXXXXXXXXXXXXXXXXX",
        "WebhookSecret": "your_webhook_secret_here",
        "ProcessingChannelId": "pc_XXXXXXXXXXXXXXXXXXXXXXXX",
        "GatewayName": "Checkout.com",
        "GatewayMerchantId": "XX"
    }',
    0,
    GETUTCDATE(),
    N'System'
);
GO
