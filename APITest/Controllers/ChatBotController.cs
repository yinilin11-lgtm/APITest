using System.Collections.Concurrent;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.AspNetCore.Mvc;

namespace APITest.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ChatBotController : ControllerBase
    {
        private const int MaxHistoryMessages = 20;
        private const int MaxMessageLength = 4000;
        private static readonly ConcurrentDictionary<string, List<ChatMessage>> Conversations = new();
        private static readonly ConcurrentDictionary<string, string> UserConversations = new();
        private static readonly ConcurrentDictionary<string, string> UserNamedConversations = new();
        private readonly IConfiguration _configuration;
        private readonly ILogger<ChatBotController> _logger;

        public ChatBotController(IConfiguration configuration, ILogger<ChatBotController> logger)
        {
            _configuration = configuration;
            _logger = logger;
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
            var conversationId = GetConversationId(
                request.ConversationId,
                userId,
                conversationName,
                request.StartNewConversation);
            var intent = DetectIntent(normalizedMessage);

            try
            {
                var reply = await CreateReply(conversationId, normalizedMessage, intent);

                return Ok(new ChatBotResponse
                {
                    UserId = userId,
                    ConversationName = conversationName,
                    ConversationId = conversationId,
                    Message = normalizedMessage,
                    Reply = reply,
                    Intent = intent,
                    HistoryCount = Conversations[conversationId].Count,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Chatbot configuration or response error. TraceId: {TraceId}", HttpContext.TraceIdentifier);
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new ErrorResponse(ex.Message, HttpContext.TraceIdentifier));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gemini API request failed. TraceId: {TraceId}", HttpContext.TraceIdentifier);
                return StatusCode(
                    StatusCodes.Status502BadGateway,
                    new ErrorResponse("Gemini API request failed.", HttpContext.TraceIdentifier));
            }
        }

        [HttpDelete("{conversationId}", Name = "ClearChatConversation")]
        public IActionResult Delete(string conversationId)
        {
            Conversations.TryRemove(conversationId, out _);
            RemoveConversationReferences(conversationId);
            return NoContent();
        }

        private static string GetConversationId(
            string? requestedConversationId,
            string? userId,
            string? conversationName,
            bool startNewConversation)
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
                return conversationId;
            }

            if (!string.IsNullOrWhiteSpace(userId))
            {
                return UserConversations.GetOrAdd(userId, _ => Guid.NewGuid().ToString("N"));
            }

            return Guid.NewGuid().ToString("N");
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

            return conversationId;
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
        }

        private static string GetNamedConversationKey(string userId, string conversationName)
        {
            return $"{userId.Trim().ToLowerInvariant()}::{conversationName.Trim().ToLowerInvariant()}";
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

        private async Task<string> CreateReply(string conversationId, string message, string intent)
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

            var model = _configuration["Gemini:Model"];
            if (string.IsNullOrWhiteSpace(model))
            {
                model = "gemini-3.5-flash";
            }

            var history = Conversations.GetOrAdd(conversationId, _ => new List<ChatMessage>());
            List<ChatMessage> snapshot;

            lock (history)
            {
                snapshot = history
                    .Append(new ChatMessage("user", $"Intent: {intent}\nUser message: {message}"))
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
                            Text = "You are a helpful conversational chatbot. Use previous messages as context. Reply in Traditional Chinese unless the user asks for another language. If the user asks about real-time information, explain that you may not have live data unless tools are connected."
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

            var trimmedReply = reply.Trim();
            lock (history)
            {
                history.Add(new ChatMessage("user", $"Intent: {intent}\nUser message: {message}"));
                history.Add(new ChatMessage("model", trimmedReply));
                TrimHistory(history);
            }

            return trimmedReply;
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
    }

    public class ChatBotRequest
    {
        public string? UserId { get; set; }

        public string? ConversationId { get; set; }

        public string? ConversationName { get; set; }

        public bool StartNewConversation { get; set; }

        public string Message { get; set; } = string.Empty;
    }

    public class ChatBotResponse
    {
        public string? UserId { get; set; }

        public string? ConversationName { get; set; }

        public string ConversationId { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string Reply { get; set; } = string.Empty;

        public string Intent { get; set; } = string.Empty;

        public int HistoryCount { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
    }

    public record ErrorResponse(string Error, string TraceId);

    public record ChatMessage(string Role, string Text);
}
