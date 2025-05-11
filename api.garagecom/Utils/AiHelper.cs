#region

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenAI;
using OpenAI.Chat;
using JsonSerializer = System.Text.Json.JsonSerializer;

#endregion

namespace api.garagecom.Utils;
// ─── Models ────────────────────────────────────────────────────────────────

public class DetectDashboardRequest
{
    [JsonPropertyName("model")] public string Model { get; set; }
    [JsonPropertyName("input")] public List<InputItem> Input { get; set; }
    [JsonPropertyName("text")] public Text Text { get; set; }
    [JsonPropertyName("reasoning")] public Dictionary<string, object> Reasoning { get; set; }
    [JsonPropertyName("tools")] public List<object> Tools { get; set; }
    [JsonPropertyName("temperature")] public double Temperature { get; set; }

    [JsonPropertyName("max_output_tokens")]
    public int MaxOutputTokens { get; set; }

    [JsonPropertyName("top_p")] public int TopP { get; set; }
    [JsonPropertyName("store")] public bool Store { get; set; }
}

public class InputItem
{
    [JsonPropertyName("role")] public string Role { get; set; }
    [JsonPropertyName("content")] public List<ContentElement> Content { get; set; }
}

public class ContentElement
{
    [JsonPropertyName("type")] public string Type { get; set; }
    [JsonPropertyName("text")] public string? Text { get; set; }
    [JsonPropertyName("image_url")] public string? ImageUrl { get; set; }
}

public class Text
{
    [JsonPropertyName("format")] public Format Format { get; set; }
}

public class Format
{
    [JsonPropertyName("type")] public string Type { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("strict")] public bool Strict { get; set; }
    [JsonPropertyName("schema")] public Schema Schema { get; set; }
}

public class Schema
{
    [JsonPropertyName("type")] public string Type { get; set; }
    [JsonPropertyName("properties")] public Properties Properties { get; set; }
    [JsonPropertyName("required")] public List<string> Required { get; set; }

    [JsonPropertyName("additionalProperties")]
    public bool AdditionalProperties { get; set; }
}

public class Properties
{
    [JsonPropertyName("defects")] public DefectsProperty Defects { get; set; }
}

public class DefectsProperty
{
    [JsonPropertyName("type")] public string Type { get; set; }
    [JsonPropertyName("description")] public string Description { get; set; }
    [JsonPropertyName("items")] public Items Items { get; set; }
}

public class Items
{
    [JsonPropertyName("type")] public string Type { get; set; }
    [JsonPropertyName("enum")] public List<string> Enum { get; set; }
}

public class DefectsModel
{
    public List<string> defects { get; set; }
}

public class ValidateUserTextModel
{
    public bool IsValid { get; set; }
}

// ─── Helper ─────────────────────────────────────────────────────────────────

public static class AiHelper
{
    private static readonly string OpenaiApiKey =
        Environment.GetEnvironmentVariable("OPENAI_API_KEY")!;

    private static readonly string OpenaiApiUrl =
        "https://api.openai.com/v1/responses";

    // truly async!
    private static async Task<string?> ConvertToBase64Async(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return null;

        await using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
        var bytes = memoryStream.ToArray();
        return Convert.ToBase64String(bytes);
    }

    public static async Task<bool> ValidateUserText(string userText)
    {
        var validationModel = new ValidateUserTextModel
        {
            IsValid = true
        };
        try
        {
            var openAiClient = new OpenAIClient(OpenaiApiKey);
            var chatClient = openAiClient.GetChatClient("gpt-4.1");
            List<ChatMessage> messages =
            [
                new UserChatMessage(userText),
                new SystemChatMessage(
                    "make sure the text has no offensive language or hate language, a text that will spread hate or cause problems as well as its related to cars customizing, repairing, inquiries, troubleshooting, or anything related to cars community only as well as no dating context is allowed. (if the user is posting something like with my car or anything related to cars atleast 20%-30% then allow it do not be very strict.) Make sure to return an object as follows:\n{\n     \"IsValid\": true/false\n}\n- also make sure you validate it for any input language\n\nfor example:\n1- \nuser: Hi\nresponse: {\n     \"IsValid\": false\n}\n\n2-\nuser: where is the nearest garage?\nresponse: {\n     \"IsValid\": true\n}\n3-\nuser: dodge is a shitty car.\nresponse: {\n     \"IsValid\": false\n}\n\n4-\nuser: what is this sign on my dashboard\nresponse: {\n     \"IsValid\": true\n}\n\n5-\nuser: الدوج ما يمشي\nresponse: {\n     \"IsValid\": false\n}\n\n6-\nuser: الموستنق حنطور\nresponse: {\n     \"IsValid\": false\n}\n\n7-\nuser: من تطلع وياي؟\nresponse: {\n     \"IsValid\": false\n}")
            ];
            ChatCompletionOptions options = new()
            {
                ResponseModalities = ChatResponseModalities.Text,
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    "user_text_validation",
                    BinaryData.FromBytes("""
                                         {
                                           "type": "object",
                                           "properties": {
                                             "IsValid": {
                                               "type": "boolean",
                                               "description": "An indicator to test if the user text is valid or not."
                                             }
                                           },
                                           "required": ["IsValid"],
                                           "additionalProperties": false
                                         }
                                         """u8.ToArray()),
                    jsonSchemaIsStrict: true)
            };
            ChatCompletion completion = await chatClient.CompleteChatAsync(messages, options);
            using var structuredJson = JsonDocument.Parse(completion.Content[0].Text);
            validationModel =
                JsonConvert.DeserializeObject<ValidateUserTextModel>(structuredJson.RootElement.ToString());
        }
        catch
        {
            // ignored
        }

        return validationModel?.IsValid ?? true;
    }

    public static async Task<List<string>> GetDashboardSigns(IFormFile file)
    {
        byte[] imageBytes;
        using (var memoryStream = new MemoryStream())
        {
            await file.CopyToAsync(memoryStream);
            imageBytes = memoryStream.ToArray();
        }

        var binaryData = BinaryData.FromBytes(imageBytes);
        var openAiClient = new OpenAIClient(OpenaiApiKey);
        var chatClient = openAiClient.GetChatClient("gpt-4.1");
        List<ChatMessage> messages =
        [
            new UserChatMessage(
                ChatMessageContentPart.CreateImagePart(binaryData, file.ContentType, ChatImageDetailLevel.High)),
            new SystemChatMessage(
                "Detect the dashboard signs and return an empty array if nothing is detected or no image has been provided or detected.")
        ];
        ChatCompletionOptions options = new()
        {
            ResponseModalities = ChatResponseModalities.Text,
            // ReasoningEffortLevel = ChatReasoningEffortLevel.High,
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                "dashboard_signs",
                BinaryData.FromBytes("""
                                     {
                                       "type": "object",
                                       "properties": {
                                         "defects": {
                                           "type": "array",
                                           "description": "An array of detected elements \u2026",
                                           "items": {
                                             "type": "string",
                                             "enum": [
                                               "Abs",
                                               "Air Bag",
                                               "Air Suspension",
                                               "All Wheel Drive",
                                               "Battery",
                                               "Brake",
                                               "Check Engine",
                                               "Dashboard Signs",
                                               "High Temperature",
                                               "Lamp",
                                               "Low Fuel",
                                               "Oil",
                                               "Open Doors",
                                               "Open Hood",
                                               "Open Trunk",
                                               "Parking Brake",
                                               "Power Steering",
                                               "Seat Belt Reminder",
                                               "Tire Pressure",
                                               "Traction Control",
                                               "Transmission Temperature",
                                               "Unlock Gear Selector",
                                               "Washer Fluid"
                                             ]
                                           }
                                         }
                                       },
                                       "required": ["defects"],
                                       "additionalProperties": false
                                     }
                                     """u8.ToArray()),
                jsonSchemaIsStrict: true)
        };
        ChatCompletion completion = await chatClient.CompleteChatAsync(messages, options);
        using var structuredJson = JsonDocument.Parse(completion.Content[0].Text);
        var defects = JsonConvert.DeserializeObject<DefectsModel>(structuredJson.RootElement.ToString());
        return defects?.defects ?? new List<string>();
    }

    public static async Task<DefectsModel> GetDictionary(IFormFile? file)
    {
        var defectsModel = new DefectsModel();
        // 1) build content elements
        var textElement = new ContentElement
        {
            Type = "input_text",
            Text =
                "Detect the dashboard signs and return an empty array if nothing is detected or no image has been provided or detected.",
            ImageUrl = null
        };

        var base64 = await ConvertToBase64Async(file);
        var imageElement = new ContentElement
        {
            Type = "input_image",
            Text = null,
            ImageUrl = base64 != null
                ? $"data:{file?.ContentType};base64,{base64}"
                : null
        };

        // 2) build full payload
        var payload = new DetectDashboardRequest
        {
            Model = "gpt-4.1-mini",
            Input =
            [
                new InputItem { Role = "system", Content = [textElement] },
                new InputItem { Role = "user", Content = [imageElement] }
            ],
            Text = new Text
            {
                Format = new Format
                {
                    Type = "json_schema",
                    Name = "detected_elements",
                    Strict = true,
                    Schema = new Schema
                    {
                        Type = "object",
                        Properties = new Properties
                        {
                            Defects = new DefectsProperty
                            {
                                Type = "array",
                                Description = "An array of detected elements …",
                                Items = new Items
                                {
                                    Type = "string",
                                    Enum =
                                    [
                                        "Abs", "Air Bag", "Air Suspension", "All Wheel Drive",
                                        "Battery", "Brake", "Check Engine", "Dashboard Signs",
                                        "High Temperature", "Lamp", "Low Fuel", "Oil",
                                        "Open Doors", "Open Hood", "Open Trunk",
                                        "Parking Brake", "Power Steering",
                                        "Seat Belt Reminder", "Tire Pressure",
                                        "Traction Control", "Transmission Temperature",
                                        "Unlock Gear Selector", "Washer Fluid"
                                    ]
                                }
                            }
                        },
                        Required = ["defects"],
                        AdditionalProperties = false
                    }
                }
            },
            Reasoning = new Dictionary<string, object>(),
            Tools = [],
            Temperature = 0.2,
            MaxOutputTokens = 10000,
            TopP = 1,
            Store = true
        };

        // 3) serialize including nulls
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        var json = JsonSerializer.Serialize(payload, options);

        // 4) send
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", OpenaiApiKey);

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync(OpenaiApiUrl, content);

        // 5) output
        var body = await response.Content.ReadAsStringAsync();

        // This part requires testing and refinement i was tired while developing it.
        var parsedJson = JObject.Parse(body);
        if (parsedJson["output"] is not JArray output) return defectsModel;
        foreach (var property in output)
            defectsModel =
                JsonConvert.DeserializeObject<DefectsModel>(property["content"]?[0]?["text"]?.ToString() ??
                                                            string.Empty) ?? new DefectsModel();

        return defectsModel;
    }
}