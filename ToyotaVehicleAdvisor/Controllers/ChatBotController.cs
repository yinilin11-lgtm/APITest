using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ToyotaVehicleAdvisor.Services;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.AspNetCore.Mvc;

namespace ToyotaVehicleAdvisor.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ChatBotController : ControllerBase
    {
        private const int MaxHistoryMessages = 20;
        private const int MaxMessageLength = 4000;
        private const int MaxSourceCharacters = 12000;
        private static readonly Regex UrlRegex = new(@"https?://[^\s]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly string[] NonToyotaBrandKeywords =
        [
            "porsche", "macan", "911", "bmw", "benz", "mercedes", "audi", "volkswagen", "vw",
            "tesla", "honda", "nissan", "mazda", "subaru", "mitsubishi", "suzuki", "hyundai",
            "kia", "ford", "volvo", "peugeot", "skoda", "lexgen", "納智捷", "保時捷", "賓士",
            "奧迪", "福斯", "特斯拉", "本田", "日產", "馬自達", "速霸陸", "三菱", "鈴木",
            "現代", "起亞", "福特", "富豪", "標緻"
        ];
        private static readonly object StoreLock = new();
        private static readonly string StoreFilePath = Path.Combine(GetProjectRootPath(), "Data", "chat-history.json");
        private static readonly string LegacyStoreFilePath = Path.Combine(AppContext.BaseDirectory, "Data", "chat-history.json");
        private static readonly ConcurrentDictionary<string, List<ChatMessage>> Conversations = new();
        private static readonly ConcurrentDictionary<string, string> UserConversations = new();
        private static readonly ConcurrentDictionary<string, string> UserNamedConversations = new();
        private readonly IConfiguration _configuration;
        private readonly ILogger<ChatBotController> _logger;
        private readonly ToyotaCarSearchService _toyotaCarSearchService;

        static ChatBotController()
        {
            LoadChatStore();
        }

        public ChatBotController(
            IConfiguration configuration,
            ILogger<ChatBotController> logger,
            ToyotaCarSearchService toyotaCarSearchService)
        {
            _configuration = configuration;
            _logger = logger;
            _toyotaCarSearchService = toyotaCarSearchService;
        }

        [HttpPost(Name = "SendChatMessage")]
        public async Task<ActionResult<ChatBotResponse>> Post(ChatBotRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new ErrorResponse("Message is required.", HttpContext.TraceIdentifier));
            }

            var normalizedMessage = request.Message.Trim();
            if (normalizedMessage.Length > MaxMessageLength)
            {
                return BadRequest(new ErrorResponse($"Message cannot exceed {MaxMessageLength} characters.", HttpContext.TraceIdentifier));
            }

            var userId = NormalizeValue(request.UserId);
            var conversationName = NormalizeValue(request.ConversationName);
            var conversationId = GetConversationId(request.ConversationId, userId, conversationName, request.StartNewConversation);
            var intent = DetectIntent(normalizedMessage);

            try
            {
                var route = "toyota-database";
                var sources = new List<SourceItem>();
                var reply = CreateOutOfScopeReply();
                var matchedCarCount = 0;
                var recommendationCriteria = string.Empty;

                if (!IsAskingAboutNonToyotaBrand(normalizedMessage))
                {
                    var recommendation = await _toyotaCarSearchService.SearchAsync(normalizedMessage);
                    var matchedCars = recommendation.Cars;
                    matchedCarCount = matchedCars.Count;
                    sources = matchedCars
                        .Select(car => new SourceItem(car.Model, car.SourceUrl))
                        .DistinctBy(source => source.Url)
                        .ToList();
                    var databaseContext = ToyotaCarSearchService.BuildPromptContext(recommendation);
                    var databasePrompt = BuildSourcePrompt(
                        "Use the backend recommendation criteria and Toyota vehicle database below before answering. Explain the recommendation by matching the customer's budget, family size, commute, parking, hybrid preference, and SUV preference when those needs are detected. For exact Toyota facts such as price, fuel type, seats, engine, horsepower, and fuel economy, only use the database records. If the database does not contain a needed detail, say that the database does not list it.",
                        "Toyota Taiwan database records",
                        normalizedMessage,
                        databaseContext);

                    reply = await CreateReply(conversationId, databasePrompt, normalizedMessage);
                    recommendationCriteria = recommendation.Criteria.ToPromptText();
                }

                return Ok(new ChatBotResponse
                {
                    UserId = userId,
                    ConversationName = conversationName,
                    ConversationId = conversationId,
                    Message = normalizedMessage,
                    Reply = reply,
                    Intent = intent,
                    Route = route,
                    Sources = sources,
                    MatchedCarCount = matchedCarCount,
                    RecommendationCriteria = recommendationCriteria,
                    HistoryCount = Conversations[conversationId].Count,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Chatbot configuration or response error. TraceId: {TraceId}", HttpContext.TraceIdentifier);
                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse(ex.Message, HttpContext.TraceIdentifier));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chatbot request failed. TraceId: {TraceId}", HttpContext.TraceIdentifier);
                return StatusCode(StatusCodes.Status502BadGateway, new ErrorResponse("Chatbot request failed.", HttpContext.TraceIdentifier));
            }
        }

        [HttpPost("read-url", Name = "ReadUrlWithChatBot")]
        public async Task<ActionResult<ReadUrlResponse>> ReadUrl(ReadUrlRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Url))
            {
                return BadRequest(new ErrorResponse("Url is required.", HttpContext.TraceIdentifier));
            }

            if (!Uri.TryCreate(request.Url.Trim(), UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return BadRequest(new ErrorResponse("Url must be a valid http or https URL.", HttpContext.TraceIdentifier));
            }

            var question = string.IsNullOrWhiteSpace(request.Question)
                ? "請用簡單的方式整理這個網頁重點。"
                : request.Question.Trim();
            var userId = NormalizeValue(request.UserId);
            var conversationName = NormalizeValue(request.ConversationName);
            var conversationId = GetConversationId(request.ConversationId, userId, conversationName, request.StartNewConversation);

            try
            {
                var sourceContent = await ReadUrlContent(uri);
                var sources = ExtractSources(sourceContent, uri.ToString());
                var prompt = BuildSourcePrompt(
                    "請根據以下網頁內容回答使用者問題。如果內容裡沒有答案，請直接說網頁內容沒有提到。",
                    $"網址：{uri}",
                    question,
                    sourceContent);
                var userMessage = $"請根據這個網址回答：{uri}\n問題：{question}";
                var reply = await CreateReply(conversationId, prompt, userMessage);

                return Ok(new ReadUrlResponse
                {
                    UserId = userId,
                    ConversationName = conversationName,
                    ConversationId = conversationId,
                    Url = uri.ToString(),
                    Question = question,
                    Reply = reply,
                    Sources = sources,
                    SourceCharacterCount = sourceContent.Length,
                    HistoryCount = Conversations[conversationId].Count,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Read URL configuration or response error. TraceId: {TraceId}", HttpContext.TraceIdentifier);
                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse(ex.Message, HttpContext.TraceIdentifier));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Read URL request failed. TraceId: {TraceId}", HttpContext.TraceIdentifier);
                return StatusCode(StatusCodes.Status502BadGateway, new ErrorResponse("Read URL request failed.", HttpContext.TraceIdentifier));
            }
        }

        [HttpPost("search", Name = "SearchWithChatBot")]
        public async Task<ActionResult<SearchResponse>> Search(SearchRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest(new ErrorResponse("Query is required.", HttpContext.TraceIdentifier));
            }

            var query = request.Query.Trim();
            var question = string.IsNullOrWhiteSpace(request.Question)
                ? "請根據搜尋結果用簡單中文回答。"
                : request.Question.Trim();
            var userId = NormalizeValue(request.UserId);
            var conversationName = NormalizeValue(request.ConversationName);
            var conversationId = GetConversationId(request.ConversationId, userId, conversationName, request.StartNewConversation);

            try
            {
                var searchContent = await SearchWeb(query);
                var sources = ExtractSources(searchContent);
                var prompt = BuildSourcePrompt(
                    "請根據以下搜尋結果回答使用者問題。如果搜尋結果沒有足夠資訊，請直接說目前搜尋結果不足。",
                    $"搜尋關鍵字：{query}",
                    question,
                    searchContent);
                var userMessage = $"請搜尋並回答：{query}\n問題：{question}";
                var reply = await CreateReply(conversationId, prompt, userMessage);

                return Ok(new SearchResponse
                {
                    UserId = userId,
                    ConversationName = conversationName,
                    ConversationId = conversationId,
                    Query = query,
                    Question = question,
                    Reply = reply,
                    Sources = sources,
                    SourceCharacterCount = searchContent.Length,
                    HistoryCount = Conversations[conversationId].Count,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Search configuration or response error. TraceId: {TraceId}", HttpContext.TraceIdentifier);
                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse(ex.Message, HttpContext.TraceIdentifier));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Search request failed. TraceId: {TraceId}", HttpContext.TraceIdentifier);
                return StatusCode(StatusCodes.Status502BadGateway, new ErrorResponse("Search request failed.", HttpContext.TraceIdentifier));
            }
        }

        [HttpGet("health", Name = "GetChatBotHealth")]
        public ActionResult<HealthResponse> Health()
        {
            return Ok(new HealthResponse
            {
                Server = "ok",
                GeminiKeyConfigured = IsConfigured("Gemini:ApiKey", "GEMINI_API_KEY"),
                JinaKeyConfigured = IsConfigured("Jina:ApiKey", "JINA_API_KEY"),
                GeminiModel = GetGeminiModel(),
                ToyotaDatabasePath = Path.Combine(GetProjectRootPath(), "Data", "toyota-cars.db"),
                HistoryStorePath = StoreFilePath,
                HistoryStoreReady = Directory.Exists(Path.GetDirectoryName(StoreFilePath)!) ||
                    Directory.Exists(GetProjectRootPath()),
                ConversationCount = Conversations.Count,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        [HttpGet("conversations", Name = "GetAllConversations")]
        public ActionResult<IEnumerable<ConversationListItem>> GetAllConversations()
        {
            var namedConversations = UserNamedConversations
                .Select(pair => new
                {
                    Key = ParseNamedConversationKey(pair.Key),
                    ConversationId = pair.Value
                })
                .Select(item => CreateConversationListItem(
                    item.Key.UserId,
                    item.Key.ConversationName,
                    item.ConversationId));

            var currentUserConversations = UserConversations
                .Where(pair => !HasNamedConversation(pair.Key, pair.Value))
                .Select(pair => CreateConversationListItem(pair.Key, null, pair.Value));

            var conversations = namedConversations
                .Concat(currentUserConversations)
                .OrderBy(item => item.UserId)
                .ThenBy(item => item.ConversationName)
                .ToList();

            return Ok(conversations);
        }

        [HttpGet("users/{userId}/conversations", Name = "GetUserConversations")]
        public ActionResult<IEnumerable<ConversationSummary>> GetUserConversations(string userId)
        {
            var normalizedUserId = NormalizeValue(userId);
            if (string.IsNullOrWhiteSpace(normalizedUserId))
            {
                return BadRequest(new ErrorResponse("UserId is required.", HttpContext.TraceIdentifier));
            }

            var summaries = UserNamedConversations
                .Select(pair => new
                {
                    Key = ParseNamedConversationKey(pair.Key),
                    ConversationId = pair.Value
                })
                .Where(item => item.Key.UserId == normalizedUserId.ToLowerInvariant())
                .Select(item => CreateConversationSummary(item.Key.ConversationName, item.ConversationId))
                .OrderBy(summary => summary.ConversationName)
                .ToList();

            return Ok(summaries);
        }

        [HttpGet("users/{userId}/history", Name = "GetCurrentUserChatHistory")]
        public ActionResult<ChatHistoryResponse> GetCurrentUserChatHistory(string userId, [FromQuery] string? conversationName = null)
        {
            var normalizedUserId = NormalizeValue(userId);
            var normalizedConversationName = NormalizeValue(conversationName);

            if (string.IsNullOrWhiteSpace(normalizedUserId))
            {
                return BadRequest(new ErrorResponse("UserId is required.", HttpContext.TraceIdentifier));
            }

            var conversationId = GetExistingConversationId(normalizedUserId, normalizedConversationName);
            if (conversationId is null)
            {
                return NotFound(new ErrorResponse("Conversation was not found.", HttpContext.TraceIdentifier));
            }

            Conversations.TryGetValue(conversationId, out var history);
            var messages = new List<ChatMessageItem>();

            if (history is not null)
            {
                lock (history)
                {
                    messages = history
                        .Select(message => new ChatMessageItem
                        {
                            Role = message.Role,
                            Text = message.Text,
                            CreatedAt = message.CreatedAt
                        })
                        .ToList();
                }
            }

            return Ok(new ChatHistoryResponse
            {
                UserId = normalizedUserId,
                ConversationName = normalizedConversationName,
                ConversationId = conversationId,
                MessageCount = messages.Count,
                Messages = messages
            });
        }

        [HttpDelete("users/{userId}/history", Name = "ClearUserChatHistory")]
        public ActionResult<DeleteConversationResponse> DeleteUserChatHistory(string userId, [FromQuery] string? conversationName = null)
        {
            var normalizedUserId = NormalizeValue(userId);
            var normalizedConversationName = NormalizeValue(conversationName);

            if (string.IsNullOrWhiteSpace(normalizedUserId))
            {
                return BadRequest(new ErrorResponse("UserId is required.", HttpContext.TraceIdentifier));
            }

            var conversationId = GetExistingConversationId(normalizedUserId, normalizedConversationName);
            if (conversationId is null)
            {
                return NotFound(new ErrorResponse("Conversation was not found.", HttpContext.TraceIdentifier));
            }

            Conversations.TryRemove(conversationId, out _);
            RemoveConversationReferences(conversationId);
            return Ok(new DeleteConversationResponse
            {
                Message = "Conversation deleted successfully.",
                UserId = normalizedUserId,
                ConversationName = normalizedConversationName,
                ConversationId = conversationId,
                DeletedAt = DateTimeOffset.UtcNow
            });
        }

        [HttpDelete("{conversationId}", Name = "ClearChatConversation")]
        public ActionResult<DeleteConversationResponse> Delete(string conversationId)
        {
            Conversations.TryRemove(conversationId, out _);
            RemoveConversationReferences(conversationId);
            return Ok(new DeleteConversationResponse
            {
                Message = "Conversation deleted successfully.",
                ConversationId = conversationId,
                DeletedAt = DateTimeOffset.UtcNow
            });
        }

        private static string GetConversationId(string? requestedConversationId, string? userId, string? conversationName, bool startNewConversation)
        {
            if (startNewConversation)
            {
                return SaveConversationReference(Guid.NewGuid().ToString("N"), userId, conversationName);
            }

            if (!string.IsNullOrWhiteSpace(requestedConversationId))
            {
                return SaveConversationReference(requestedConversationId.Trim(), userId, conversationName);
            }

            if (!string.IsNullOrWhiteSpace(userId) && !string.IsNullOrWhiteSpace(conversationName))
            {
                var namedKey = GetNamedConversationKey(userId, conversationName);
                var conversationId = UserNamedConversations.GetOrAdd(namedKey, _ => Guid.NewGuid().ToString("N"));
                UserConversations[userId] = conversationId;
                SaveChatStore();
                return conversationId;
            }

            if (!string.IsNullOrWhiteSpace(userId))
            {
                var conversationId = UserConversations.GetOrAdd(userId, _ => Guid.NewGuid().ToString("N"));
                SaveChatStore();
                return conversationId;
            }

            return Guid.NewGuid().ToString("N");
        }

        private static string? GetExistingConversationId(string userId, string? conversationName)
        {
            if (!string.IsNullOrWhiteSpace(conversationName))
            {
                var namedKey = GetNamedConversationKey(userId, conversationName);
                return UserNamedConversations.TryGetValue(namedKey, out var namedConversationId)
                    ? namedConversationId
                    : null;
            }

            return UserConversations.TryGetValue(userId, out var currentConversationId)
                ? currentConversationId
                : null;
        }

        private static string SaveConversationReference(string conversationId, string? userId, string? conversationName)
        {
            if (!string.IsNullOrWhiteSpace(userId))
            {
                UserConversations[userId] = conversationId;

                if (!string.IsNullOrWhiteSpace(conversationName))
                {
                    UserNamedConversations[GetNamedConversationKey(userId, conversationName)] = conversationId;
                }
            }

            SaveChatStore();
            return conversationId;
        }

        private static ConversationSummary CreateConversationSummary(string conversationName, string conversationId)
        {
            Conversations.TryGetValue(conversationId, out var history);

            if (history is null)
            {
                return new ConversationSummary
                {
                    ConversationName = conversationName,
                    ConversationId = conversationId
                };
            }

            lock (history)
            {
                var lastMessage = history.LastOrDefault();
                return new ConversationSummary
                {
                    ConversationName = conversationName,
                    ConversationId = conversationId,
                    MessageCount = history.Count,
                    LastRole = lastMessage?.Role,
                    LastMessage = lastMessage?.Text,
                    UpdatedAt = lastMessage?.CreatedAt
                };
            }
        }

        private static ConversationListItem CreateConversationListItem(string userId, string? conversationName, string conversationId)
        {
            Conversations.TryGetValue(conversationId, out var history);

            if (history is null)
            {
                return new ConversationListItem
                {
                    UserId = userId,
                    ConversationName = conversationName,
                    ConversationId = conversationId
                };
            }

            lock (history)
            {
                var lastMessage = history.LastOrDefault();
                return new ConversationListItem
                {
                    UserId = userId,
                    ConversationName = conversationName,
                    ConversationId = conversationId,
                    MessageCount = history.Count,
                    LastRole = lastMessage?.Role,
                    LastMessage = lastMessage?.Text,
                    UpdatedAt = lastMessage?.CreatedAt
                };
            }
        }

        private static bool HasNamedConversation(string userId, string conversationId)
        {
            return UserNamedConversations.Any(pair =>
            {
                var key = ParseNamedConversationKey(pair.Key);
                return key.UserId == userId.Trim().ToLowerInvariant() &&
                    pair.Value == conversationId;
            });
        }

        private static void RemoveConversationReferences(string conversationId)
        {
            foreach (var userConversation in UserConversations)
            {
                if (userConversation.Value == conversationId)
                {
                    UserConversations.TryRemove(userConversation.Key, out _);
                }
            }

            foreach (var namedConversation in UserNamedConversations)
            {
                if (namedConversation.Value == conversationId)
                {
                    UserNamedConversations.TryRemove(namedConversation.Key, out _);
                }
            }

            SaveChatStore();
        }

        private static string GetNamedConversationKey(string userId, string conversationName)
        {
            return $"{userId.Trim().ToLowerInvariant()}::{conversationName.Trim().ToLowerInvariant()}";
        }

        private static NamedConversationKey ParseNamedConversationKey(string key)
        {
            var parts = key.Split("::", 2, StringSplitOptions.None);
            return new NamedConversationKey(parts[0], parts.Length == 2 ? parts[1] : string.Empty);
        }

        private static string? NormalizeValue(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string DetectIntent(string message)
        {
            var lowerMessage = message.ToLowerInvariant();

            if (lowerMessage.Contains("hello") ||
                lowerMessage.Contains("hi") ||
                lowerMessage.Contains("你好") ||
                lowerMessage.Contains("哈囉"))
            {
                return "Greeting";
            }

            if (lowerMessage.Contains("help") ||
                lowerMessage.Contains("幫") ||
                lowerMessage.Contains("協助"))
            {
                return "Help";
            }

            if (lowerMessage.Contains("?") ||
                lowerMessage.Contains("？") ||
                lowerMessage.Contains("什麼") ||
                lowerMessage.Contains("如何") ||
                lowerMessage.Contains("怎麼"))
            {
                return "Question";
            }

            return "General";
        }

        private static Uri? ExtractFirstUrl(string message)
        {
            var match = UrlRegex.Match(message);
            if (!match.Success)
            {
                return null;
            }

            var url = match.Value.TrimEnd('.', ',', ';', '，', '。', '；');
            return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri : null;
        }

        private static List<SourceItem> ExtractSources(string content, params string[] extraUrls)
        {
            return extraUrls
                .Concat(UrlRegex.Matches(content).Select(match => match.Value))
                .Select(url => url.TrimEnd('.', ',', ';', ')', ']', '"', '\''))
                .Select(CreateSourceItem)
                .OfType<SourceItem>()
                .GroupBy(source => source.Url, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Take(3)
                .ToList();
        }

        private static SourceItem? CreateSourceItem(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return null;
            }

            var title = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
                ? uri.Host[4..]
                : uri.Host;

            return new SourceItem(title, uri.ToString());
        }

        private async Task<bool> ShouldUseSearch(string message)
        {
            try
            {
                return await ShouldUseSearchWithGemini(message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gemini route classification failed. Falling back to keyword route detection.");
                return ShouldUseSearchByKeyword(message);
            }
        }

        private async Task<bool> ShouldUseSearchWithGemini(string message)
        {
            var apiKey = GetGeminiApiKey();
            var model = GetGeminiModel();
            var client = new Client(apiKey: apiKey);
            var config = new GenerateContentConfig
            {
                SystemInstruction = new Content
                {
                    Parts = new List<Part>
                    {
                        new Part
                        {
                            Text = "Decide whether the user's message needs current web search results. Reply with exactly one word: search or chat. Choose search for questions about current weather, news, prices, stock prices, exchange rates, schedules, recent events, availability, opening hours, or information likely to change. Choose chat for emotional support, brainstorming, writing help, general advice, explanations, or timeless knowledge."
                        }
                    }
                }
            };

            var response = await client.Models.GenerateContentAsync(
                model: model,
                contents:
                [
                    new Content
                    {
                        Role = "user",
                        Parts =
                        [
                            new Part
                            {
                                Text = message
                            }
                        ]
                    }
                ],
                config: config);

            var decision = NormalizeReply(response.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ?? string.Empty)
                .ToLowerInvariant();

            if (decision == "search")
            {
                return true;
            }

            if (decision == "chat")
            {
                return false;
            }

            return ShouldUseSearchByKeyword(message);
        }

        private static bool ShouldUseSearchByKeyword(string message)
        {
            var lowerMessage = message.ToLowerInvariant();
            string[] keywords =
            [
                "今天", "今日", "現在", "目前", "最近", "最新", "新聞", "即時",
                "天氣", "股價", "匯率", "價格", "排行", "消息",
                "today", "latest", "recent", "news", "weather", "price"
            ];

            return keywords.Any(lowerMessage.Contains);
        }

        private async Task<string> CreateReply(string conversationId, string message)
        {
            return await CreateReply(conversationId, message, message);
        }

        private static bool IsAskingAboutNonToyotaBrand(string message)
        {
            var lowerMessage = message.ToLowerInvariant();
            return NonToyotaBrandKeywords.Any(lowerMessage.Contains);
        }

        private static string CreateOutOfScopeReply()
        {
            return "I focus on Toyota vehicle recommendations, so I do not analyze or recommend other brands. Share the customer's budget, driving needs, passenger count, and preferred vehicle type, and I can suggest suitable Toyota options.";
        }

        private async Task<string> CreateReply(string conversationId, string promptMessage, string historyMessage)
        {
            var apiKey = GetGeminiApiKey();
            var model = GetGeminiModel();

            var history = Conversations.GetOrAdd(conversationId, _ => new List<ChatMessage>());
            var userMessage = new ChatMessage("user", historyMessage, DateTimeOffset.UtcNow);
            var promptUserMessage = new ChatMessage(
                "user",
                $"Important: Reply in English only, even if the conversation history or user message contains Chinese.\n\n{promptMessage}",
                userMessage.CreatedAt);
            List<ChatMessage> snapshot;

            lock (history)
            {
                snapshot = history
                    .Append(promptUserMessage)
                    .TakeLast(MaxHistoryMessages)
                    .ToList();
            }

            var client = new Client(apiKey: apiKey);
            var config = new GenerateContentConfig
            {
                SystemInstruction = new Content
                {
                    Parts = new List<Part>
                    {
                        new Part
                        {
                            Text = "You are a Toyota vehicle recommendation assistant for a graduate school application demo. Your main job is to help customers choose suitable Toyota vehicles. Use previous messages as context. Always reply in English. Ask follow-up questions when needed, such as budget, number of passengers, city or highway driving, parking space, fuel efficiency, family use, cargo needs, and whether the customer prefers sedan, hatchback, SUV, MPV, hybrid, or electric. Recommend Toyota models by matching needs, for example Corolla Altis for practical commuting, Corolla Cross or RAV4 for SUV needs, Yaris Cross for compact city use, Camry for comfort, Sienta for family space, or Prius/Hybrid options for fuel economy. Do not invent exact prices, promotions, inventory, loan terms, or official specs unless they are provided by official data. Do not recommend or analyze non-Toyota brands. If the user's question is unrelated to Toyota vehicle buying, choosing, owning, maintaining, or using cars, politely say you mainly help with Toyota car recommendations and car-related questions. Keep replies short, simple, friendly, and sales-consultant-like. Use 1 to 3 short sentences by default. Reply as one paragraph without line breaks. You may occasionally add one simple emoji or emoticon when it feels natural, but do not overuse them."
                        }
                    }
                }
            };

            var response = await client.Models.GenerateContentAsync(
                model: model,
                contents: snapshot.Select(ToGeminiContent).ToList(),
                config: config);

            var reply = response.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

            if (string.IsNullOrWhiteSpace(reply))
            {
                throw new InvalidOperationException("Gemini API returned an empty response.");
            }

            var trimmedReply = NormalizeReply(reply);
            lock (history)
            {
                history.Add(userMessage);
                history.Add(new ChatMessage("model", trimmedReply, DateTimeOffset.UtcNow));
                TrimHistory(history);
            }

            SaveChatStore();
            return trimmedReply;
        }

        private string GetGeminiApiKey()
        {
            var apiKey = _configuration["Gemini:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                apiKey = System.Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("Gemini API key is missing. Set Gemini:ApiKey or GEMINI_API_KEY.");
            }

            return apiKey;
        }

        private string GetGeminiModel()
        {
            var model = _configuration["Gemini:Model"];
            return string.IsNullOrWhiteSpace(model) ? "gemini-3.5-flash" : model;
        }

        private bool IsConfigured(string configurationKey, string environmentVariableName)
        {
            return !string.IsNullOrWhiteSpace(_configuration[configurationKey]) ||
                !string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable(environmentVariableName));
        }

        private static string NormalizeReply(string reply)
        {
            return string.Join(' ', reply.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }

        private async Task<string> ReadUrlContent(Uri uri)
        {
            var jinaReaderUrl = $"https://r.jina.ai/{uri}";
            return await GetJinaContent(
                jinaReaderUrl,
                "目前無法讀取這個網址，請確認網址是否正確，或稍後再試。");
        }

        private async Task<string> SearchWeb(string query)
        {
            var jinaSearchUrl = $"https://s.jina.ai/?q={Uri.EscapeDataString(query)}";
            return await GetJinaContent(
                jinaSearchUrl,
                "目前搜尋服務暫時無法使用，請稍後再試。");
        }

        private async Task<string> GetJinaContent(string url, string unavailableMessage)
        {
            var jinaApiKey = _configuration["Jina:ApiKey"];
            if (string.IsNullOrWhiteSpace(jinaApiKey))
            {
                jinaApiKey = System.Environment.GetEnvironmentVariable("JINA_API_KEY");
            }

            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            if (!string.IsNullOrWhiteSpace(jinaApiKey))
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jinaApiKey);
            }

            string content;
            try
            {
                content = await httpClient.GetStringAsync(url);
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException(unavailableMessage, ex);
            }
            catch (TaskCanceledException ex)
            {
                throw new InvalidOperationException(unavailableMessage, ex);
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                throw new InvalidOperationException(unavailableMessage);
            }

            return content.Trim();
        }

        private static string BuildSourcePrompt(string instruction, string sourceLabel, string question, string sourceContent)
        {
            var trimmedSource = sourceContent.Length > MaxSourceCharacters
                ? sourceContent[..MaxSourceCharacters]
                : sourceContent;

            var prompt = new StringBuilder();
            prompt.AppendLine(instruction);
            prompt.AppendLine(sourceLabel);
            prompt.AppendLine($"問題：{question}");
            prompt.AppendLine("資料內容：");
            prompt.AppendLine(trimmedSource);

            return prompt.ToString();
        }

        private static Content ToGeminiContent(ChatMessage message)
        {
            return new Content
            {
                Role = message.Role,
                Parts = new List<Part>
                {
                    new Part
                    {
                        Text = message.Text
                    }
                }
            };
        }

        private static void TrimHistory(List<ChatMessage> history)
        {
            if (history.Count > MaxHistoryMessages)
            {
                history.RemoveRange(0, history.Count - MaxHistoryMessages);
            }
        }

        private static void LoadChatStore()
        {
            var storeFilePath = GetReadableStoreFilePath();
            if (storeFilePath is null)
            {
                return;
            }

            lock (StoreLock)
            {
                var json = System.IO.File.ReadAllText(storeFilePath);
                var store = JsonSerializer.Deserialize<ChatStoreData>(json);
                if (store is null)
                {
                    return;
                }

                foreach (var conversation in store.Conversations)
                {
                    Conversations[conversation.Key] = conversation.Value;
                }

                foreach (var userConversation in store.UserConversations)
                {
                    UserConversations[userConversation.Key] = userConversation.Value;
                }

                foreach (var namedConversation in store.UserNamedConversations)
                {
                    UserNamedConversations[namedConversation.Key] = namedConversation.Value;
                }

                if (!string.Equals(storeFilePath, StoreFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    SaveChatStore();
                }
            }
        }

        private static string? GetReadableStoreFilePath()
        {
            if (System.IO.File.Exists(StoreFilePath))
            {
                return StoreFilePath;
            }

            return System.IO.File.Exists(LegacyStoreFilePath) ? LegacyStoreFilePath : null;
        }

        private static string GetProjectRootPath()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                if (System.IO.File.Exists(Path.Combine(directory.FullName, "ToyotaVehicleAdvisor.csproj")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            return Directory.GetCurrentDirectory();
        }

        private static void SaveChatStore()
        {
            lock (StoreLock)
            {
                var conversations = Conversations.ToDictionary(
                    pair => pair.Key,
                    pair =>
                    {
                        lock (pair.Value)
                        {
                            return pair.Value.ToList();
                        }
                    });

                var store = new ChatStoreData
                {
                    Conversations = conversations,
                    UserConversations = UserConversations.ToDictionary(pair => pair.Key, pair => pair.Value),
                    UserNamedConversations = UserNamedConversations.ToDictionary(pair => pair.Key, pair => pair.Value)
                };

                Directory.CreateDirectory(Path.GetDirectoryName(StoreFilePath)!);
                var json = JsonSerializer.Serialize(store, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                System.IO.File.WriteAllText(StoreFilePath, json);
            }
        }
    }

    public class ChatBotRequest
    {
        public string? UserId { get; set; }

        public string? ConversationId { get; set; }

        public string? ConversationName { get; set; }

        public bool StartNewConversation { get; set; }

        public string Message { get; set; } = string.Empty;
    }

    public class ReadUrlRequest
    {
        public string? UserId { get; set; }

        public string? ConversationId { get; set; }

        public string? ConversationName { get; set; }

        public bool StartNewConversation { get; set; }

        public string Url { get; set; } = string.Empty;

        public string? Question { get; set; }
    }

    public class SearchRequest
    {
        public string? UserId { get; set; }

        public string? ConversationId { get; set; }

        public string? ConversationName { get; set; }

        public bool StartNewConversation { get; set; }

        public string Query { get; set; } = string.Empty;

        public string? Question { get; set; }
    }

    public class ChatBotResponse
    {
        public string? UserId { get; set; }

        public string? ConversationName { get; set; }

        public string ConversationId { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string Reply { get; set; } = string.Empty;

        public string Intent { get; set; } = string.Empty;

        public string Route { get; set; } = string.Empty;

        public List<SourceItem> Sources { get; set; } = [];

        public int MatchedCarCount { get; set; }

        public string RecommendationCriteria { get; set; } = string.Empty;

        public int HistoryCount { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
    }

    public class ReadUrlResponse
    {
        public string? UserId { get; set; }

        public string? ConversationName { get; set; }

        public string ConversationId { get; set; } = string.Empty;

        public string Url { get; set; } = string.Empty;

        public string Question { get; set; } = string.Empty;

        public string Reply { get; set; } = string.Empty;

        public List<SourceItem> Sources { get; set; } = [];

        public int SourceCharacterCount { get; set; }

        public int HistoryCount { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
    }

    public class SearchResponse
    {
        public string? UserId { get; set; }

        public string? ConversationName { get; set; }

        public string ConversationId { get; set; } = string.Empty;

        public string Query { get; set; } = string.Empty;

        public string Question { get; set; } = string.Empty;

        public string Reply { get; set; } = string.Empty;

        public List<SourceItem> Sources { get; set; } = [];

        public int SourceCharacterCount { get; set; }

        public int HistoryCount { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
    }

    public class ConversationSummary
    {
        public string ConversationName { get; set; } = string.Empty;

        public string ConversationId { get; set; } = string.Empty;

        public int MessageCount { get; set; }

        public string? LastRole { get; set; }

        public string? LastMessage { get; set; }

        public DateTimeOffset? UpdatedAt { get; set; }
    }

    public class ConversationListItem
    {
        public string UserId { get; set; } = string.Empty;

        public string? ConversationName { get; set; }

        public string ConversationId { get; set; } = string.Empty;

        public int MessageCount { get; set; }

        public string? LastRole { get; set; }

        public string? LastMessage { get; set; }

        public DateTimeOffset? UpdatedAt { get; set; }
    }

    public class ChatHistoryResponse
    {
        public string UserId { get; set; } = string.Empty;

        public string? ConversationName { get; set; }

        public string ConversationId { get; set; } = string.Empty;

        public int MessageCount { get; set; }

        public List<ChatMessageItem> Messages { get; set; } = [];
    }

    public class HealthResponse
    {
        public string Server { get; set; } = string.Empty;

        public bool GeminiKeyConfigured { get; set; }

        public bool JinaKeyConfigured { get; set; }

        public string GeminiModel { get; set; } = string.Empty;

        public string ToyotaDatabasePath { get; set; } = string.Empty;

        public string HistoryStorePath { get; set; } = string.Empty;

        public bool HistoryStoreReady { get; set; }

        public int ConversationCount { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
    }

    public class DeleteConversationResponse
    {
        public string Message { get; set; } = string.Empty;

        public string? UserId { get; set; }

        public string? ConversationName { get; set; }

        public string ConversationId { get; set; } = string.Empty;

        public DateTimeOffset DeletedAt { get; set; }
    }

    public class ChatMessageItem
    {
        public string Role { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; }
    }

    public record ErrorResponse(string Error, string TraceId);

    public record ChatMessage(string Role, string Text, DateTimeOffset CreatedAt);

    public record NamedConversationKey(string UserId, string ConversationName);

    public record SourceItem(string Title, string Url);

    public class ChatStoreData
    {
        public Dictionary<string, List<ChatMessage>> Conversations { get; set; } = [];

        public Dictionary<string, string> UserConversations { get; set; } = [];

        public Dictionary<string, string> UserNamedConversations { get; set; } = [];
    }
}
