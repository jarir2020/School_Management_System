using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SchoolManagement.Application.Finance;

namespace SchoolManagement.Infrastructure.Payments;

/// <summary>Communicates only with SSLCOMMERZ's sandbox endpoints; no live payment endpoint is configured.</summary>
public sealed class SslCommerzGateway(HttpClient httpClient, IOptions<SslCommerzOptions> options) : ISslCommerzGateway
{
    private const string SandboxRoot = "https://sandbox.sslcommerz.com";
    private readonly SslCommerzOptions settings = options.Value;

    public bool IsConfigured => settings.IsConfigured;

    public async Task<GatewaySessionResponse> CreateSessionAsync(
        GatewaySessionRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var parameters = new Dictionary<string, string>
        {
            ["store_id"] = settings.StoreId,
            ["store_passwd"] = settings.StorePassword,
            ["total_amount"] = request.Amount.ToString("0.00", CultureInfo.InvariantCulture),
            ["currency"] = request.Currency,
            ["tran_id"] = request.TransactionId,
            ["success_url"] = request.SuccessUrl,
            ["fail_url"] = request.FailUrl,
            ["cancel_url"] = request.CancelUrl,
            ["ipn_url"] = request.IpNUrl,
            ["cus_name"] = request.CustomerName,
            ["cus_email"] = request.CustomerEmail,
            ["cus_phone"] = request.CustomerPhone,
            ["cus_add1"] = "Dhaka",
            ["cus_city"] = "Dhaka",
            ["cus_state"] = "Dhaka",
            ["cus_postcode"] = "1000",
            ["cus_country"] = "Bangladesh",
            ["shipping_method"] = "NO",
            ["product_name"] = request.Description,
            ["product_category"] = "education",
            ["product_profile"] = "non-physical-goods"
        };
        using var response = await httpClient.PostAsync(
            $"{SandboxRoot}/gwprocess/v4/api.php",
            new FormUrlEncodedContent(parameters),
            cancellationToken);
        using var document = await ReadJsonAsync(response, cancellationToken);
        var root = document.RootElement;
        if (!string.Equals(ReadString(root, "status"), "SUCCESS", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("SSLCOMMERZ did not create a payment session. Check the sandbox credentials and required merchant settings.");
        }

        var gatewayPageUrl = ReadString(root, "GatewayPageURL");
        if (!Uri.TryCreate(gatewayPageUrl, UriKind.Absolute, out var gatewayUri)
            || gatewayUri.Scheme != Uri.UriSchemeHttps
            || !string.Equals(gatewayUri.Host, "sandbox.sslcommerz.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("SSLCOMMERZ returned an invalid sandbox checkout URL.");
        }

        var sessionKey = ReadString(root, "sessionkey");
        if (string.IsNullOrWhiteSpace(sessionKey)) throw new InvalidOperationException("SSLCOMMERZ returned an incomplete payment session.");
        return new GatewaySessionResponse(gatewayUri.ToString(), sessionKey);
    }

    public async Task<ValidatedGatewayPayment> ValidatePaymentAsync(
        string validationId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var root = await GetJsonAsync("/validator/api/validationserverAPI.php", new Dictionary<string, string>
        {
            ["val_id"] = validationId,
            ["store_id"] = settings.StoreId,
            ["store_passwd"] = settings.StorePassword,
            ["format"] = "json"
        }, cancellationToken);
        return new ValidatedGatewayPayment(
            ReadString(root, "status"),
            ReadString(root, "APIConnect"),
            ReadString(root, "tran_id"),
            ReadString(root, "val_id"),
            ReadDecimal(root, "amount"),
            ReadString(root, "currency_type"),
            ReadDecimal(root, "currency_amount"),
            ReadString(root, "bank_tran_id"),
            ReadString(root, "risk_level"));
    }

    public async Task<GatewayRefundStartResponse> StartRefundAsync(
        GatewayRefundRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var root = await GetJsonAsync("/validator/api/merchantTransIDvalidationAPI.php", new Dictionary<string, string>
        {
            ["bank_tran_id"] = request.BankTransactionId,
            ["refund_trans_id"] = request.RefundTransactionId,
            ["refund_amount"] = request.Amount.ToString("0.00", CultureInfo.InvariantCulture),
            ["refund_remarks"] = request.Reason,
            ["refe_id"] = request.MerchantReference,
            ["store_id"] = settings.StoreId,
            ["store_passwd"] = settings.StorePassword,
            ["v"] = "1",
            ["format"] = "json"
        }, cancellationToken);
        return new GatewayRefundStartResponse(
            ReadString(root, "APIConnect"),
            ReadString(root, "status"),
            ReadString(root, "refund_ref_id"),
            ReadString(root, "errorReason"));
    }

    public async Task<GatewayRefundStatusResponse> GetRefundStatusAsync(
        string refundReference,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var root = await GetJsonAsync("/validator/api/merchantTransIDvalidationAPI.php", new Dictionary<string, string>
        {
            ["refund_ref_id"] = refundReference,
            ["store_id"] = settings.StoreId,
            ["store_passwd"] = settings.StorePassword,
            ["format"] = "json"
        }, cancellationToken);
        return new GatewayRefundStatusResponse(
            ReadString(root, "APIConnect"),
            ReadString(root, "status"),
            ReadString(root, "refund_ref_id"),
            ReadString(root, "errorReason"));
    }

    private async Task<JsonElement> GetJsonAsync(
        string path,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        var query = await new FormUrlEncodedContent(parameters).ReadAsStringAsync(cancellationToken);
        using var response = await httpClient.GetAsync($"{SandboxRoot}{path}?{query}", cancellationToken);
        using var document = await ReadJsonAsync(response, cancellationToken);
        return document.RootElement.Clone();
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException("SSLCOMMERZ sandbox returned an unsuccessful response.");
        }

        try
        {
            return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("SSLCOMMERZ sandbox returned an unreadable response.", exception);
        }
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)) return "";
        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString() ?? "",
            JsonValueKind.Number => property.GetRawText(),
            _ => ""
        };
    }

    private static decimal ReadDecimal(JsonElement element, string propertyName) =>
        decimal.TryParse(ReadString(element, propertyName), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0m;

    private void EnsureConfigured()
    {
        if (!settings.IsConfigured) throw new InvalidOperationException("SSLCOMMERZ sandbox credentials are not configured.");
    }
}
