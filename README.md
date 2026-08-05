# Toyota Vehicle Advisor

Live Demo: https://toyota-vehicle-advisor.onrender.com

Toyota Vehicle Advisor is an AI-powered vehicle recommendation web application built with ASP.NET Core, Gemini, and a SQLite Toyota vehicle database. The project simulates a customer-facing Toyota sales assistant that asks about user needs, compares vehicle options, and recommends suitable Toyota models in English.

This project was designed as a graduate school application portfolio piece to demonstrate practical AI integration, API design, frontend implementation, conversation history management, and responsible handling of API keys.

## Demo Concept

Many car shoppers do not start with a specific model in mind. They usually describe their lifestyle first: budget, commute, family size, parking space, fuel economy, or preference for an SUV. Toyota Vehicle Advisor turns those natural-language needs into helpful Toyota recommendations.

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
- SQLite Toyota vehicle database seeded from official Toyota Taiwan public model pages
- Gemini-powered conversational replies
- Browser-based chat UI
- Saved conversation list
- Auto-generated conversation names
- Anonymous per-browser visitor ID for demo chat separation
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
- **Storage:** SQLite for Toyota vehicle data, local JSON file for conversation history
- **Version Control:** Git and GitHub

## System Overview

```text
Browser UI
   |
   | POST /ChatBot
   v
ASP.NET Core API
   |
   | Query Toyota SQLite database
   v
Toyota vehicle records
   |
   | Gemini prompt + official vehicle context + conversation history
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

The assistant is also constrained to avoid unsupported claims. Exact vehicle facts such as price range, fuel type, seats, and selected specs are supplied from the local SQLite Toyota vehicle database. If the database does not include a detail, the assistant should say that the database does not list it instead of guessing.

## Recommendation Logic

Before calling Gemini, the backend analyzes the customer message and extracts practical buying criteria:

- Budget
- Family size or passenger count
- Daily commute needs
- City driving or easy parking needs
- Hybrid or fuel-saving preference
- SUV preference
- Family use

The API scores Toyota database records against those criteria, selects the best matching vehicles, and then passes both the structured criteria and matched vehicle records to Gemini. This makes the AI response explainable instead of simply asking the model to guess a Toyota recommendation.

## Vehicle Comparison

The project also includes a comparison workflow for two Toyota models. The API compares practical buying factors such as:

- Price
- Fuel or powertrain type
- Fuel economy when listed
- Interior category and seat count
- Best-use positioning
- Official data source date

This supports questions like comparing Camry and RAV4 by cost, efficiency, space, and intended use.

## Toyota Vehicle Database

The project creates a local SQLite database at runtime:

```text
ToyotaVehicleAdvisor/Data/toyota-cars.db
```

The database contains a `ToyotaCars` table with fields such as:

- Model
- Category
- StartingPriceWan and MaxPriceWan
- Seats
- FuelType
- HasHybridOption
- IsSuv
- IsElectric
- EngineCc
- HorsePower
- FuelEconomyKmPerLiter
- BestFor
- Description
- SourceUrl
- SourceCheckedDate

Initial seed data includes the Toyota Taiwan public lineup listed on the official offer/model page, including passenger cars, SUVs, EVs, GR performance models, MPVs, pickup, and light commercial vehicles. Vehicle data is summarized from Toyota Taiwan public model and offer pages, checked on 2026-08-05.

## Project Structure

```text
ToyotaVehicleAdvisor/
  Controllers/
    ChatBotController.cs
    ToyotaCarsController.cs
  Data/
    AppDbContext.cs
    chat-history.json
    toyota-cars.db
    ToyotaSeedData.cs
  Models/
    ToyotaCar.cs
  Services/
    ToyotaCarSearchService.cs
  wwwroot/
    index.html
    styles.css
    app.js
postman/
  ToyotaVehicleAdvisor.postman_collection.json
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
dotnet user-secrets set "Gemini:ApiKey" "YOUR_GEMINI_API_KEY" --project ToyotaVehicleAdvisor
```

Or using an environment variable:

```powershell
setx GEMINI_API_KEY "YOUR_GEMINI_API_KEY"
```

## Run the Project

Open `ToyotaVehicleAdvisor.slnx` in Visual Studio and run the `http` profile.

You can also run from the command line:

```powershell
dotnet run --project ToyotaVehicleAdvisor/ToyotaVehicleAdvisor.csproj --launch-profile http
```

Local app:

```text
http://localhost:5048
```

Swagger UI:

```text
http://localhost:5048/swagger
```

## Deploy a Free Web Demo on Render

This project includes a `Dockerfile` and `render.yaml`, so it can be deployed as a Docker web service on Render.

High-level steps:

1. Push this project to GitHub.
2. Create a Render account.
3. In Render, choose `New` > `Blueprint`.
4. Connect the GitHub repository.
5. Render will read `render.yaml` and create the web service.
6. Add the environment variable:

```text
GEMINI_API_KEY=YOUR_GEMINI_API_KEY
```

Do not put the real Gemini API key in GitHub.

After deployment, Render will provide a public URL similar to:

```text
https://toyota-advisor-demo.onrender.com
```

Free Render services may sleep when inactive, so the first request after a pause can take longer to load.

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

### Toyota Vehicle Database

Lists Toyota records from the local SQLite database.

```http
GET /ToyotaCars
```

Optional filters:

```text
GET /ToyotaCars?maxPriceWan=100
GET /ToyotaCars?category=SUV
GET /ToyotaCars?hybrid=true
```

### Toyota Vehicle Comparison

```http
GET /ToyotaCars/compare?models=CAMRY&models=RAV4
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
postman/ToyotaVehicleAdvisor.postman_collection.json
```

The default `baseUrl` is:

```text
http://localhost:5048
```

## Security Notes

- API keys are not stored in GitHub.
- The browser UI uses an anonymous visitor ID stored in `localStorage` to separate demo chat history per browser.
- This is suitable for a portfolio demo, but it is not a replacement for real authentication.
- Local chat history is saved in `ToyotaVehicleAdvisor/Data/chat-history.json`.
- `ToyotaVehicleAdvisor/Data/chat-history.json` is ignored by Git.
- Local Toyota seed data is saved in `ToyotaVehicleAdvisor/Data/toyota-cars.db`.
- If the project is cloned on another computer, the Gemini API key must be configured again.
- The health check endpoint does not call Gemini, so it does not use API quota.

## Future Improvements

- Deploy the app to a public cloud URL
- Replace local JSON conversation history with database-backed user history
- Add authentication for multiple users
- Expand Toyota model data with more official specifications and scheduled refreshes
- Add screenshots or a short demo video for portfolio presentation
