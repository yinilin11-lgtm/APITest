# Toyota Recommendation Assistant

ASP.NET Core Web API demo that uses Gemini to recommend Toyota vehicles based on customer needs. The project includes a simple browser chat UI, Postman collection, Swagger UI, and local conversation history.

## Features

- Toyota-focused AI recommendation assistant
- Gemini-powered replies in English
- Browser chat UI at the site root
- Conversation tracking by `userId` and `conversationName`
- Local chat history in `APITest/Data/chat-history.json`
- Conversation list, history lookup, and history deletion
- Health check endpoint
- Swagger UI for API testing
- Postman collection for quick testing

## Requirements

- Visual Studio 2026 or later
- .NET 10 SDK
- Gemini API key
- Postman, optional

## API Key Setup

Do not commit real API keys to GitHub. Use User Secrets or environment variables.

```powershell
dotnet user-secrets set "Gemini:ApiKey" "YOUR_GEMINI_API_KEY" --project APITest
```

Or set an environment variable:

```powershell
setx GEMINI_API_KEY "YOUR_GEMINI_API_KEY"
```

## Run the Project

Open `APITest.slnx` in Visual Studio and run the `http` profile.

You can also run from the command line:

```powershell
dotnet run --project APITest/APITest.csproj --launch-profile http
```

Local app:

```text
http://localhost:5048
```

Swagger UI:

```text
http://localhost:5048/swagger
```

## Main API Endpoints

### Health Check

Checks whether the API is running and whether the Gemini key/history storage are configured.

```http
GET /ChatBot/health
```

Example:

```text
GET http://localhost:5048/ChatBot/health
```

### Toyota Recommendation Chat

Main chatbot endpoint. It replies as a Toyota vehicle recommendation assistant.

```http
POST /ChatBot
Content-Type: application/json
```

Example:

```json
{
  "userId": "iris",
  "conversationName": "toyota-shopping",
  "message": "I commute every day and want an affordable, fuel-saving Toyota. What would you recommend?"
}
```

### List User Conversations

```http
GET /ChatBot/users/{userId}/conversations
```

Example:

```text
GET http://localhost:5048/ChatBot/users/iris/conversations
```

### Get Chat History

```http
GET /ChatBot/users/{userId}/history?conversationName={conversationName}
```

Example:

```text
GET http://localhost:5048/ChatBot/users/iris/history?conversationName=toyota-shopping
```

### Delete Chat History

Deletes a conversation by `userId` and `conversationName`.

```http
DELETE /ChatBot/users/{userId}/history?conversationName={conversationName}
```

Example:

```text
DELETE http://localhost:5048/ChatBot/users/iris/history?conversationName=toyota-shopping
```

## Postman

Import this collection into Postman:

```text
postman/APITest.postman_collection.json
```

The default `baseUrl` is:

```text
http://localhost:5048
```

## Notes

- API keys are not stored in GitHub.
- Chat history is saved locally in `APITest/Data/chat-history.json`.
- If you clone the project on another computer, set the Gemini API key again.
- Health check does not call Gemini, so it does not use API quota.
