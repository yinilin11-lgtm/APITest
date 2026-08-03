# APITest ChatBot API

ASP.NET Core Web API chatbot project using Gemini for conversational replies and Jina Reader/Search for webpage reading and web search.

## Features

- Gemini chatbot replies
- Automatic route selection for chat, URL reading, and web search
- Webpage reading with Jina Reader
- Web search with Jina Search
- Conversation tracking by `userId` and `conversationName`
- Persistent chat history in `APITest/Data/chat-history.json`
- Conversation history lookup
- Conversation history deletion
- Source links in search and webpage responses
- Health check endpoint
- Swagger UI for API testing

## Requirements

- Visual Studio 2026 or later
- .NET 10 SDK
- Gemini API key
- Jina API key
- Postman, optional

## API Key Setup

Do not commit real API keys to GitHub. Use User Secrets or environment variables.

### Gemini API Key

```powershell
dotnet user-secrets set "Gemini:ApiKey" "YOUR_GEMINI_API_KEY" --project APITest
```

Or set an environment variable:

```powershell
setx GEMINI_API_KEY "YOUR_GEMINI_API_KEY"
```

### Jina API Key

```powershell
dotnet user-secrets set "Jina:ApiKey" "YOUR_JINA_API_KEY" --project APITest
```

Or set an environment variable:

```powershell
setx JINA_API_KEY "YOUR_JINA_API_KEY"
```

## Run the Project

Open `APITest.slnx` in Visual Studio and run the `http` or `https` profile.

You can also run from the command line:

```powershell
dotnet run --project APITest/APITest.csproj --launch-profile http
```

Local URLs:

```text
http://localhost:5048
https://localhost:7023
```

Swagger UI:

```text
http://localhost:5048/swagger
```

## API Endpoints

### Health Check

Checks whether the API is running and whether keys/history storage are configured.

```http
GET /ChatBot/health
```

Example:

```text
GET http://localhost:5048/ChatBot/health
```

### Chat

Main chatbot endpoint. It automatically chooses normal chat, URL reading, or web search.

```http
POST /ChatBot
Content-Type: application/json
```

Example:

```json
{
  "userId": "iris",
  "conversationName": "搜尋",
  "message": "今天台北天氣適合出門嗎"
}
```

### Search

Manually search the web with Jina Search, then ask Gemini to answer from the search results.

```http
POST /ChatBot/search
Content-Type: application/json
```

Example:

```json
{
  "userId": "iris",
  "conversationName": "搜尋",
  "query": "2026 iPhone 最新消息",
  "question": "請用簡單中文整理三個重點"
}
```

### Read URL

Read one webpage with Jina Reader, then ask Gemini to answer from the webpage content.

```http
POST /ChatBot/read-url
Content-Type: application/json
```

Example:

```json
{
  "userId": "iris",
  "conversationName": "網頁摘要",
  "url": "https://example.com",
  "question": "請用簡單中文整理這個網頁重點"
}
```

### List Conversations

```http
GET /ChatBot/users/{userId}/conversations
```

Example:

```text
GET http://localhost:5048/ChatBot/users/iris/conversations
```

### Get History

```http
GET /ChatBot/users/{userId}/history?conversationName={conversationName}
```

Example:

```text
GET http://localhost:5048/ChatBot/users/iris/history?conversationName=搜尋
```

### Delete History

Deletes a conversation by `userId` and `conversationName`.

```http
DELETE /ChatBot/users/{userId}/history?conversationName={conversationName}
```

Example:

```text
DELETE http://localhost:5048/ChatBot/users/iris/history?conversationName=搜尋
```

### Delete by Conversation ID

```http
DELETE /ChatBot/{conversationId}
```

## Postman

Import this collection into Postman:

```text
postman/APITest.postman_collection.json
```

Set the `baseUrl` collection variable to:

```text
http://localhost:5048
```

## Notes

- API keys are not stored in GitHub.
- Chat history is saved locally in `APITest/Data/chat-history.json`.
- If you clone the project on another computer, set the API keys again.
- Search and URL reading require internet access.
- Health check does not call Gemini or Jina, so it does not use API quota.
