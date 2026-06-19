using System.Text.Json.Serialization; // Make sure this namespace is at the top of your file!

public class ProductCreateDTO
{
    [JsonPropertyName("itemID")]
    public int ItemID { get; set; }

    [JsonPropertyName("itemName")]
    public string ItemName { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("utDmonth")]
    public int UTDmonth { get; set; }

    [JsonPropertyName("utDday")]
    public int UTDday { get; set; }

    [JsonPropertyName("utDyear")]
    public int UTDyear { get; set; }

    [JsonPropertyName("dAmonth")]
    public int DAmonth { get; set; }

    [JsonPropertyName("dAday")]
    public int DAday { get; set; }

    [JsonPropertyName("dAyear")]
    public int DAyear { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}

public class FullBatchUpdateDTO
{
    [JsonPropertyName("batchID")]
    public int BatchID { get; set; }

    [JsonPropertyName("itemID")]
    public int ItemID { get; set; }

    [JsonPropertyName("itemName")]
    public string ItemName { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("utDmonth")]
    public int UTDmonth { get; set; }

    [JsonPropertyName("utDday")]
    public int UTDday { get; set; }

    [JsonPropertyName("utDyear")]
    public int UTDyear { get; set; }
}
