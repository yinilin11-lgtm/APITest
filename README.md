# Toyota Advisor Demo

Toyota Advisor Demo is an AI-powered vehicle recommendation web application built with ASP.NET Core and Gemini. The project simulates a customer-facing Toyota sales assistant that asks about user needs and recommends suitable Toyota models in English.

This project was designed as a graduate school application portfolio piece to demonstrate practical AI integration, API design, frontend implementation, conversation history management, and responsible handling of API keys.

## Demo Concept

Many car shoppers do not start with a specific model in mind. They usually describe their lifestyle first: budget, commute, family size, parking space, fuel economy, or preference for an SUV. Toyota Advisor Demo turns those natural-language needs into helpful Toyota recommendations.

Example user question:

```text
I commute every day and want an affordable, fuel-saving Toyota. What would you recommend?
```

Expected assistant behavior:

- Responds in English
- Recommends Toyota models only
- Asks follow-up questions when more details are needed
- Avoids inventing exact prices, promotions, inventory, or loan terms
- Refuses to analyze or recommend non-Toyota brands

## Key Features

- Toyota-focused AI recommendation assistant
- Gemini-powered conversational replies
- Browser-based chat UI
- Saved conversation list
- Auto-generated conversation names
- Chat history lookup
- Saved chat deletion
- Toyota-only brand guardrail
- Health check endpoint
- Swagger UI for API testing
- Postman collection for manual API testing
- Local chat history storage excluded from Git

## Tech Stack

- **Backend:** ASP.NET Core Web API
- **AI:** Gemini API through the official `Google.GenAI` C# package
- **Frontend:** HTML, CSS, JavaScript
- **API Testing:** Swagger UI and Postman
- **Storage:** Local JSON file for conversation history
- **Version Control:** Git and GitHub

## System Overview

```text
Browser UI
   |
   | POST /ChatBot
   v
ASP.NET Core API
   |
   | Gemini prompt + conversation history
   v
Gemini API
   |
   v
Toyota recommendation response
   |
   v
Browser UI + local chat history
```

## AI Behavior Design

The assistant is prompted to behave like a Toyota vehicle recommendation consultant. It focuses on matching customer needs to Toyota options such as:

- Corolla Altis for practical commuting
- Corolla Cross or RAV4 for SUV needs
- Yaris Cross for compact city driving
- Camry for comfort
- Sienta for family space
- Prius or hybrid options for fuel economy

The assistant is also constrained to avoid unsupported claims. It should not invent exact pricing, promotions, inventory availability, loan terms, or official specifications unless those details are provided by trusted data.

## Project Structure

```text
APITest/
  Controllers/
    ChatBotController.cs
  Data/
    chat-history.json
  wwwroot/
    index.html
    styles.css
    app.js
postman/
  APITest.postman_collection.json
README.md
```

## Requirements

- Visual Studio 2026 or later
- .NET 10 SDK
- Gemini API key
- Postman, optional

## API Key Setup

Do not commit real API keys to GitHub. Use User Secrets or environment variables.

Using User Secrets:

```powershell
dotnet user-secrets set "Gemini:ApiKey" "YOUR_GEMINI_API_KEY" --project APITest
```

Or using an environment variable:

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

## Demo Flow

1. Open the browser UI.
2. Click `New recommendation`.
3. Ask a customer-style vehicle question.
4. Review the Toyota recommendation.
5. Open saved chats from the sidebar.
6. Delete old saved chats when they are no longer needed.

Suggested test prompts:

```text
I commute every day and want an affordable, fuel-saving Toyota. What would you recommend?
```

```text
My family has two kids and we travel on weekends. Which Toyota would fit us best?
```

```text
I want a Toyota that is easy to park in the city but still comfortable. What should I look at?
```

## Main API Endpoints

### Health Check

Checks whether the API is running and whether the Gemini key and history storage are configured.

```http
GET /ChatBot/health
```

Example:

```text
GET http://localhost:5048/ChatBot/health
```

### Toyota Recommendation Chat

Main chatbot endpoint.

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

## Security Notes

- API keys are not stored in GitHub.
- Local chat history is saved in `APITest/Data/chat-history.json`.
- `APITest/Data/chat-history.json` is ignored by Git.
- If the project is cloned on another computer, the Gemini API key must be configured again.
- The health check endpoint does not call Gemini, so it does not use API quota.

## Future Improvements

- Deploy the app to a public cloud URL
- Replace local JSON history with a database
- Add authentication for multiple users
- Add official Toyota model data instead of relying only on model knowledge
- Add screenshots or a short demo video for portfolio presentation
