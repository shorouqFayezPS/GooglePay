-- ============================================================
--  Seed: Checkout.com LIVE Configuration
--
--  ApiBaseUrl: unique per merchant — first 8 chars of client_id
--  Find client_id: Dashboard → Settings icon → Account structure tab
--  Example: client_id = cli_vkuhvk4vjn2edkps7dfsq6emqm
--           → prefix  = vkuhvk4v
--           → ApiBaseUrl = https://vkuhvk4v.api.checkout.com
--
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
    1,
    8,
    N'CheckoutLive',
    N'Checkout.com (Live)',

    -- !! REPLACE with YOUR prefix from Dashboard → Settings → Account structure
    N'https://YOURPREFIX.api.checkout.com',

    N'Production',
    N'https://checkout.com',
    N'{
        "SecretKey":           "sk_XXXXXXXXXXXXXXXXXXXX",
        "PublicKey":           "pk_XXXXXXXXXXXXXXXXXXXX",
        "WebhookSecret":       "signature_key_from_dashboard_webhooks_tab",
        "ProcessingChannelId": "pc_XXXXXXXXXXXXXXXXXXXX",
        "GatewayName":         "Checkout.com",
        "GatewayMerchantId":   "your_google_pay_merchant_id",
        "BillingCountry":      "BH"
    }',
    0,
    GETUTCDATE(),
    N'System'
);
GO

