# APITest

ASP.NET Core Web API project with a simple chatbot endpoint using the Gemini API.

## Requirements

- Visual Studio 2026 or later
- .NET 10 SDK
- A Gemini API key
- Postman, if you want to test the API with the included collection

## Setup

1. Clone or download this repository.
2. Open `APITest.slnx` in Visual Studio.
3. Set your Gemini API key with one of these options:

```powershell
dotnet user-secrets set "Gemini:ApiKey" "YOUR_API_KEY" --project APITest
```

Or set an environment variable:

```powershell
setx GEMINI_API_KEY "YOUR_API_KEY"
```

4. Run the project from Visual Studio, or use:

```powershell
dotnet run --project APITest
```

The local HTTP address is:

```text
http://localhost:5048
```

## API Endpoints

### Weather forecast

```http
GET /WeatherForecast
```

### Chatbot

```http
POST /ChatBot
Content-Type: application/json
```

Example body:

```json
{
  "userId": "iris",
  "conversationName": "咖啡推薦",
  "message": "請推薦台北適合工作的咖啡廳"
}
```

### Clear a conversation

```http
DELETE /ChatBot/{conversationId}
```

## Postman

Import this file into Postman:

```text
postman/APITest.postman_collection.json
```

Then make sure the `baseUrl` collection variable is set to:

```text
http://localhost:5048
```

## Notes

- Do not commit real API keys to GitHub.
- Use `appsettings.Example.json` as a reference for configuration.
- Runtime conversation history is stored in memory, so it resets when the app restarts.
