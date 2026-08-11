using System.Globalization;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Logging;
using PrivateExpenses.Application.Abstractions.Parsing;
using PrivateExpenses.Application.Dtos.Receipts;

namespace PrivateExpenses.Infrastructure.Parsing;

/// <summary>
/// Real receipt-recognition provider using Claude's vision + structured-output support. Handles both
/// photos (JPEG/PNG/WEBP) and PDFs — Claude reads a PDF's embedded text directly when present and
/// falls back to visual reading for scanned/image-based pages, including multi-page documents, without
/// any special-casing here (section 12).
///
/// The model is instructed to extract only what's visibly printed, never invent missing products or
/// prices, and report uncertainty — see the system prompt below (section 83). Money is requested as
/// plain decimal strings ("12,34" is never used — always "12.34") and converted to integer cents in
/// code rather than trusted from model arithmetic, so a parsing bug can never silently lose or invent
/// a cent (section 5/6).
/// </summary>
public class AnthropicVisionReceiptParser(AnthropicClient client, string modelId, ILogger<AnthropicVisionReceiptParser> logger)
    : IReceiptParser
{
    public string ProviderName => "anthropic-vision";

    private const string SystemPrompt = """
        You are extracting structured data from a photo or PDF of a shop receipt or invoice, for a
        private expense-splitting app used by three housemates in the Netherlands.

        Follow these rules strictly:
        - Extract ONLY information that is visibly printed on the document. Never invent, guess, or
          infer a product, price, or other value that is not actually shown.
        - Preserve negative values exactly as printed (discounts, refunds) — do not convert them to
          positive or drop them.
        - Preserve quantities exactly as printed, including weight-based quantities (e.g. "0.42 kg").
        - Distinguish real purchased product/service lines from non-product lines. Do NOT include as
          items: subtotal, total, VAT/BTW lines, payment method/card lines, change given, loyalty
          card balance, customer card info, store address, opening hours, barcodes, cashier name,
          transaction/receipt number, or similar metadata.
        - DO include as items: discounts (mark isDiscount=true, keep the negative sign), deposits /
          statiegeld (mark isDeposit=true), and any other real financial line tied to a product.
        - If a field is not visible or you are not confident about it, set it to null. Do not guess a
          plausible-looking value to fill a gap.
        - Never infer a price for an item whose price is not printed.
        - All monetary amounts must be plain decimal strings using a period as the decimal separator
          and no thousands separator or currency symbol, e.g. "12.34" or "-1.00" — regardless of how
          the amount is formatted on the receipt itself.
        - Dates must be ISO 8601 (YYYY-MM-DD). Times must be 24-hour HH:MM. If the year is not printed,
          infer it only from an explicitly printed date; never guess a year.
        - If you are meaningfully uncertain about the overall reading (blurry image, cut-off receipt,
          handwriting, etc.), add a short plain-language note to confidenceWarnings explaining what is
          uncertain. Leave confidenceWarnings empty if you are confident.
        - Respond with ONLY the structured JSON output. Do not include any other commentary.
        """;

    public async Task<ReceiptParseResult> ParseAsync(ReceiptParseRequest request, CancellationToken cancellationToken = default)
    {
        // Without a bound here, a stalled outbound connection to Anthropic (network-level, not an
        // API error) leaves the caller waiting on the SDK's own ~10 minute default forever from the
        // user's perspective — bon-analyseren just sits there with no error. A hard deadline turns
        // that into a fast, clear, recoverable failure instead.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(90));

        try
        {
            await using var buffered = new MemoryStream();
            await request.FileContent.CopyToAsync(buffered, timeoutCts.Token);
            var base64Data = Convert.ToBase64String(buffered.ToArray());

            ContentBlockParam documentBlock = request.MimeType == "application/pdf"
                ? new DocumentBlockParam { Source = new Base64PdfSource { Data = base64Data } }
                : new ImageBlockParam { Source = new Base64ImageSource { Data = base64Data, MediaType = request.MimeType } };

            var parameters = new MessageCreateParams
            {
                Model = modelId,
                // Room for the full structured JSON output on a receipt with many line items — this
                // budget is no longer shared with thinking (disabled below), so it only needs to cover
                // the actual response text.
                MaxTokens = 8192,
                System = SystemPrompt,
                Messages =
                [
                    new MessageParam
                    {
                        Role = Role.User,
                        Content = new List<ContentBlockParam>
                        {
                            documentBlock,
                            new TextBlockParam { Text = "Extract this receipt according to the schema." },
                        },
                    },
                ],
                // Claude Opus 5 thinks by default (adaptive, effort "high"), and MaxTokens caps
                // thinking + output combined. For a real receipt photo that reliably burned the whole
                // token budget on invisible reasoning before any JSON output was written, leaving
                // stop_reason=max_tokens and no usable text — confirmed via a raw request against the
                // API directly. Receipt field extraction is mechanical, not a reasoning task, so
                // thinking is disabled outright rather than just capped.
                Thinking = new ThinkingConfigDisabled(),
                OutputConfig = new OutputConfig { Format = new JsonOutputFormat { Schema = BuildSchema() } },
            };

            // Stream rather than wait for one large buffered response: a non-streaming call sits
            // idle from the HTTP client's perspective until the entire (structured-JSON, vision)
            // response is ready, which is exactly the shape that trips a transport-level read
            // timeout on a flaky outbound connection — streaming starts receiving bytes immediately
            // and keeps the connection demonstrably alive the whole way through.
            var jsonBuilder = new System.Text.StringBuilder();
            string? stopReason = null;

            await foreach (var streamEvent in client.Messages.CreateStreaming(parameters, cancellationToken: timeoutCts.Token))
            {
                if (streamEvent.TryPickContentBlockDelta(out var contentDelta) && contentDelta.Delta.TryPickText(out var textDelta))
                {
                    jsonBuilder.Append(textDelta.Text);
                }
                else if (streamEvent.TryPickDelta(out var messageDelta))
                {
                    stopReason = messageDelta.Delta.StopReason;
                }
            }

            if (stopReason == "refusal")
            {
                logger.LogWarning("Receipt parsing was refused by the model's safety classifiers.");
                return ReceiptParseResult.Failed("De bon kon niet worden geanalyseerd (geweigerd door de AI-provider).");
            }

            var jsonText = jsonBuilder.ToString();

            if (string.IsNullOrWhiteSpace(jsonText))
            {
                logger.LogWarning("Receipt parsing returned no text content. StopReason={StopReason}", stopReason);
                return ReceiptParseResult.Failed("De bon kon niet worden uitgelezen: geen resultaat ontvangen.");
            }

            return MapToResult(jsonText);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            logger.LogError("Receipt parsing via Anthropic timed out after 90 seconds (no response received).");
            return ReceiptParseResult.Failed("Bon kon niet automatisch worden uitgelezen: de AI-provider reageerde niet op tijd.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never leak provider internals (stack traces, request/response bodies) to the end user —
            // log the technical detail server-side and return a friendly, generic failure instead.
            logger.LogError(ex, "Receipt parsing via Anthropic failed.");
            return ReceiptParseResult.Failed("Bon kon niet automatisch worden uitgelezen door een fout bij de AI-provider.");
        }
    }

    private ReceiptParseResult MapToResult(string jsonText)
    {
        RawReceipt? raw;
        try
        {
            raw = JsonSerializer.Deserialize<RawReceipt>(jsonText, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Receipt parsing returned invalid JSON.");
            return ReceiptParseResult.Failed("De AI-provider gaf een onverwerkbaar antwoord terug.");
        }

        if (raw is null)
        {
            return ReceiptParseResult.Failed("De AI-provider gaf een leeg antwoord terug.");
        }

        var items = (raw.Items ?? [])
            .Where(i => !string.IsNullOrWhiteSpace(i.Description))
            .Select(i => new ReceiptParsedItem
            {
                Description = i.Description!.Trim(),
                Quantity = ParseDecimalOrNull(i.Quantity),
                UnitPriceCents = ParseAmountToCentsOrNull(i.UnitPriceAmount),
                TotalPriceCents = ParseAmountToCentsOrNull(i.TotalPriceAmount),
                IsDiscount = i.IsDiscount,
                IsDeposit = i.IsDeposit,
                Confidence = i.Confidence,
                SourceText = i.SourceText,
            })
            .ToList();

        return new ReceiptParseResult
        {
            Success = true,
            MerchantName = string.IsNullOrWhiteSpace(raw.MerchantName) ? null : raw.MerchantName.Trim(),
            Date = ParseDateOrNull(raw.Date),
            Time = ParseTimeOrNull(raw.Time),
            Currency = string.IsNullOrWhiteSpace(raw.Currency) ? null : raw.Currency.Trim(),
            SubtotalCents = ParseAmountToCentsOrNull(raw.SubtotalAmount),
            DiscountCents = ParseAmountToCentsOrNull(raw.DiscountAmount),
            DepositCents = ParseAmountToCentsOrNull(raw.DepositAmount),
            TaxCents = ParseAmountToCentsOrNull(raw.TaxAmount),
            TotalCents = ParseAmountToCentsOrNull(raw.TotalAmount),
            PaymentMethod = string.IsNullOrWhiteSpace(raw.PaymentMethod) ? null : raw.PaymentMethod.Trim(),
            Items = items,
            ConfidenceWarnings = raw.ConfidenceWarnings?.Where(w => !string.IsNullOrWhiteSpace(w)).ToList() ?? [],
        };
    }

    /// <summary>Converts a decimal-string amount from the model (e.g. "12.34", "-1.00") to integer
    /// cents. Internal (not private) so unit tests can exercise this directly — it is the only place
    /// AI-supplied money ever gets converted to the cent values the rest of the app trusts.</summary>
    internal static long? ParseAmountToCentsOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            return null;
        }

        return (long)Math.Round(amount * 100, MidpointRounding.AwayFromZero);
    }

    private static decimal? ParseDecimalOrNull(string? value) =>
        !string.IsNullOrWhiteSpace(value) && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;

    private static DateOnly? ParseDateOrNull(string? value) =>
        !string.IsNullOrWhiteSpace(value) && DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    private static TimeOnly? ParseTimeOrNull(string? value) =>
        !string.IsNullOrWhiteSpace(value) && TimeOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : null;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static Dictionary<string, JsonElement> BuildSchema()
    {
        const string schemaJson = """
            {
              "type": "object",
              "properties": {
                "merchantName": { "type": ["string", "null"] },
                "date": { "type": ["string", "null"], "description": "ISO 8601 date YYYY-MM-DD, or null if not visible" },
                "time": { "type": ["string", "null"], "description": "24h HH:MM, or null if not visible" },
                "currency": { "type": ["string", "null"] },
                "subtotalAmount": { "type": ["string", "null"] },
                "discountAmount": { "type": ["string", "null"] },
                "depositAmount": { "type": ["string", "null"] },
                "taxAmount": { "type": ["string", "null"] },
                "totalAmount": { "type": ["string", "null"] },
                "paymentMethod": { "type": ["string", "null"] },
                "items": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "properties": {
                      "description": { "type": "string" },
                      "quantity": { "type": ["string", "null"] },
                      "unitPriceAmount": { "type": ["string", "null"] },
                      "totalPriceAmount": { "type": ["string", "null"] },
                      "isDiscount": { "type": "boolean" },
                      "isDeposit": { "type": "boolean" },
                      "confidence": { "type": ["number", "null"] },
                      "sourceText": { "type": ["string", "null"] }
                    },
                    "required": ["description", "isDiscount", "isDeposit"],
                    "additionalProperties": false
                  }
                },
                "confidenceWarnings": { "type": "array", "items": { "type": "string" } }
              },
              "required": ["items", "confidenceWarnings"],
              "additionalProperties": false
            }
            """;

        using var document = JsonDocument.Parse(schemaJson);
        var result = new Dictionary<string, JsonElement>();
        foreach (var property in document.RootElement.EnumerateObject())
        {
            result[property.Name] = property.Value.Clone();
        }

        return result;
    }

    private sealed class RawReceipt
    {
        public string? MerchantName { get; set; }
        public string? Date { get; set; }
        public string? Time { get; set; }
        public string? Currency { get; set; }
        public string? SubtotalAmount { get; set; }
        public string? DiscountAmount { get; set; }
        public string? DepositAmount { get; set; }
        public string? TaxAmount { get; set; }
        public string? TotalAmount { get; set; }
        public string? PaymentMethod { get; set; }
        public List<RawReceiptItem>? Items { get; set; }
        public List<string>? ConfidenceWarnings { get; set; }
    }

    private sealed class RawReceiptItem
    {
        public string? Description { get; set; }
        public string? Quantity { get; set; }
        public string? UnitPriceAmount { get; set; }
        public string? TotalPriceAmount { get; set; }
        public bool IsDiscount { get; set; }
        public bool IsDeposit { get; set; }
        public double? Confidence { get; set; }
        public string? SourceText { get; set; }
    }
}
