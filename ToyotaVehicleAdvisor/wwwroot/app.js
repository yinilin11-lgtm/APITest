const visitorLabel = document.querySelector("#visitorLabel");
const conversationNameInput = document.querySelector("#conversationName");
const newChatButton = document.querySelector("#newChatButton");
const openDatabaseButton = document.querySelector("#openDatabaseButton");
const refreshButton = document.querySelector("#refreshButton");
const conversationList = document.querySelector("#conversationList");
const messages = document.querySelector("#messages");
const chatForm = document.querySelector("#chatForm");
const messageInput = document.querySelector("#messageInput");
const sendButton = document.querySelector("#sendButton");
const exampleButtons = document.querySelectorAll("[data-example]");
const chatTab = document.querySelector("#chatTab");
const databaseTab = document.querySelector("#databaseTab");
const chatView = document.querySelector("#chatView");
const databaseView = document.querySelector("#databaseView");
const lineupCount = document.querySelector("#lineupCount");
const carsGrid = document.querySelector("#carsGrid");
const maxPriceFilter = document.querySelector("#maxPriceFilter");
const allCarsButton = document.querySelector("#allCarsButton");
const suvFilterButton = document.querySelector("#suvFilterButton");
const hybridFilterButton = document.querySelector("#hybridFilterButton");
const refreshCarsButton = document.querySelector("#refreshCarsButton");
const compareFirst = document.querySelector("#compareFirst");
const compareSecond = document.querySelector("#compareSecond");
const compareButton = document.querySelector("#compareButton");
const comparisonResult = document.querySelector("#comparisonResult");

let startNewConversation = false;
let savedConversationNames = new Set();
let activeCarFilter = "all";
let allToyotaCars = [];
const visitorId = getOrCreateVisitorId();

function appendMessage(role, text, isError = false, sources = []) {
  const article = document.createElement("article");
  article.className = `message ${role}${isError ? " error" : ""}`;

  const paragraph = document.createElement("p");
  paragraph.textContent = text;
  article.append(paragraph);

  if (sources.length > 0) {

    const sourcePanel = document.createElement("div");
    sourcePanel.className = "source-panel";

    const title = document.createElement("strong");
    title.textContent = `Vehicle data used (${sources.length})`;
    sourcePanel.append(title);

    const list = document.createElement("div");
    list.className = "source-list";

    for (const source of sources) {
      const link = document.createElement("a");
      link.href = source.url;
      link.target = "_blank";
      link.rel = "noreferrer";
      link.textContent = source.title;
      list.append(link);
    }

    sourcePanel.append(list);
    article.append(sourcePanel);
  }

  messages.append(article);
  messages.scrollTop = messages.scrollHeight;
}

function getUserId() {
  return visitorId;
}

function getOrCreateVisitorId() {
  const storageKey = "toyotaAdvisorVisitorId";
  const existingId = window.localStorage.getItem(storageKey);

  if (existingId) {
    return existingId;
  }

  const randomPart = crypto.randomUUID
    ? crypto.randomUUID().slice(0, 8)
    : Math.random().toString(16).slice(2, 10);
  const newId = `visitor-${randomPart}`;
  window.localStorage.setItem(storageKey, newId);
  return newId;
}

function getConversationName() {
  return conversationNameInput.value.trim();
}

function createUniqueName(baseName) {
  let name = baseName;
  let count = 2;

  while (savedConversationNames.has(name.toLowerCase())) {
    name = `${baseName}-${count}`;
    count += 1;
  }

  return name;
}

function createConversationName(message) {
  const lowerMessage = message.toLowerCase();
  let topic = "shopping-advice";

  if (lowerMessage.includes("expensive") ||
      lowerMessage.includes("luxury") ||
      lowerMessage.includes("premium") ||
      lowerMessage.includes("high budget") ||
      lowerMessage.includes("no budget") ||
      lowerMessage.includes("rich") ||
      lowerMessage.includes("executive")) {
    topic = "luxury-options";
  } else if ((lowerMessage.includes("family") || lowerMessage.includes("kids") || lowerMessage.includes("child")) &&
      (lowerMessage.includes("suv") || lowerMessage.includes("trip") || lowerMessage.includes("travel"))) {
    topic = "family-suv";
  } else if ((lowerMessage.includes("commute") || lowerMessage.includes("work") || lowerMessage.includes("daily")) &&
      (lowerMessage.includes("fuel") || lowerMessage.includes("hybrid") || lowerMessage.includes("efficient"))) {
    topic = "budget-commute";
  } else if (lowerMessage.includes("parking") || lowerMessage.includes("city") || lowerMessage.includes("compact")) {
    topic = "city-parking";
  } else if (lowerMessage.includes("family") || lowerMessage.includes("kids") || lowerMessage.includes("child")) {
    topic = "family-car";
  } else if (lowerMessage.includes("suv") || lowerMessage.includes("crossover")) {
    topic = "suv-choice";
  } else if (lowerMessage.includes("fuel") || lowerMessage.includes("hybrid") || lowerMessage.includes("efficient")) {
    topic = "fuel-saving";
  } else if (lowerMessage.includes("budget") || lowerMessage.includes("affordable") || lowerMessage.includes("cheap")) {
    topic = "budget-choice";
  } else if (lowerMessage.includes("commute") || lowerMessage.includes("daily") || lowerMessage.includes("work")) {
    topic = "daily-commute";
  } else if (lowerMessage.includes("compare") || lowerMessage.includes("versus") || lowerMessage.includes(" vs ")) {
    topic = "model-comparison";
  }

  return createUniqueName(topic);
}

async function sendChatMessage(message) {
  const conversationName = getConversationName() || createConversationName(message);
  conversationNameInput.value = conversationName;

  const response = await fetch("/ChatBot", {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify({
      userId: getUserId(),
      conversationName,
      startNewConversation,
      message
    })
  });

  const data = await response.json();
  if (!response.ok) {
    throw new Error(data.error || "The message could not be sent. Please try again.");
  }

  startNewConversation = false;
  if (data.conversationName) {
    conversationNameInput.value = data.conversationName;
  }
  return data;
}

async function loadHistory(conversationName) {
  const userId = encodeURIComponent(getUserId());
  const name = encodeURIComponent(conversationName);
  const response = await fetch(`/ChatBot/users/${userId}/history?conversationName=${name}`);

  if (!response.ok) {
    appendMessage("assistant", "This saved chat could not be found.", true);
    return;
  }

  const data = await response.json();
  messages.innerHTML = "";

  for (const item of data.messages) {
    appendMessage(item.role === "user" ? "user" : "assistant", item.text);
  }
}

async function deleteConversation(conversationName) {
  const userId = encodeURIComponent(getUserId());
  const name = encodeURIComponent(conversationName);
  const response = await fetch(`/ChatBot/users/${userId}/history?conversationName=${name}`, {
    method: "DELETE"
  });

  if (!response.ok) {
    const data = await response.json().catch(() => ({}));
    throw new Error(data.error || "This saved chat could not be deleted.");
  }
}
async function renameConversation(oldName, newName) {
  const userId = encodeURIComponent(getUserId());
  const response = await fetch(`/ChatBot/users/${userId}/history/name`, {
    method: "PATCH",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify({
      oldConversationName: oldName,
      newConversationName: newName
    })
  });

  if (!response.ok) {
    const data = await response.json().catch(() => ({}));
    throw new Error(data.error || "This saved chat could not be renamed.");
  }
}

async function loadConversations() {
  const userId = encodeURIComponent(getUserId());
  const response = await fetch(`/ChatBot/users/${userId}/conversations`);

  conversationList.innerHTML = "";
  if (!response.ok) {
    conversationList.textContent = "Could not load saved chats.";
    return;
  }

  const conversations = await response.json();
  savedConversationNames = new Set(
    conversations
      .map((conversation) => conversation.conversationName || "")
      .filter(Boolean)
      .map((name) => name.toLowerCase())
  );
  const savedChats = conversations.filter((conversation) => conversation.conversationName);

  if (savedChats.length === 0) {
    conversationList.textContent = "No saved chats yet.";
    return;
  }

  for (const conversation of savedChats) {
    const item = document.createElement("div");
    item.className = "conversation-item";
    item.innerHTML = `
      <button type="button" class="conversation-open">
        <strong>${conversation.conversationName || "untitled-chat"}</strong>
        <span>${conversation.lastMessage || "No messages"}</span>
      </button>
      <div class="conversation-actions">
        <button type="button" class="conversation-rename" title="Rename chat">Rename</button>
        <button type="button" class="conversation-delete" title="Delete chat">Delete</button>
      </div>
    `;

    const openButton = item.querySelector(".conversation-open");
    openButton.addEventListener("click", () => {
      conversationNameInput.value = conversation.conversationName || "";
      loadHistory(conversation.conversationName || "");
    });

        const renameButton = item.querySelector(".conversation-rename");
    renameButton.addEventListener("click", async () => {
      const oldName = conversation.conversationName || "";
      const newName = prompt("Rename this saved chat:", oldName)?.trim();

      if (!oldName || !newName || newName === oldName) {
        return;
      }

      if (savedConversationNames.has(newName.toLowerCase())) {
        appendMessage("assistant", "That chat name already exists. Please choose a different name.", true);
        return;
      }

      try {
        await renameConversation(oldName, newName);
        if (conversationNameInput.value.trim() === oldName) {
          conversationNameInput.value = newName;
        }
        await loadConversations();
      } catch (error) {
        appendMessage("assistant", error.message, true);
      }
    });

    const deleteButton = item.querySelector(".conversation-delete");
    deleteButton.addEventListener("click", async () => {
      const name = conversation.conversationName || "";
      if (!name || !confirm(`Delete "${name}"?`)) {
        return;
      }

      try {
        await deleteConversation(name);
        if (conversationNameInput.value.trim() === name) {
          messages.innerHTML = "";
          appendMessage("assistant", "That saved chat was deleted. Start a new Toyota recommendation when you are ready.");
        }
        await loadConversations();
      } catch (error) {
        appendMessage("assistant", error.message, true);
      }
    });

    conversationList.append(item);
  }
}

async function submitMessage(message) {
  if (!message) {
    return;
  }

  appendMessage("user", message);
  messageInput.value = "";
  sendButton.disabled = true;

  try {
    const data = await sendChatMessage(message);
    appendMessage("assistant", data.reply, false, data.sources || []);
    await loadConversations();
  } catch (error) {
    appendMessage("assistant", error.message, true);
  } finally {
    sendButton.disabled = false;
    messageInput.focus();
  }
}

function setActiveView(view) {
  const isChat = view === "chat";
  chatView.hidden = !isChat;
  databaseView.hidden = isChat;
  chatTab.classList.toggle("active", isChat);
  databaseTab.classList.toggle("active", !isChat);

  if (!isChat) {
    loadCars();
  }
}

function setActiveCarFilter(filter) {
  activeCarFilter = filter;
  allCarsButton.classList.toggle("active", filter === "all");
  suvFilterButton.classList.toggle("active", filter === "suv");
  hybridFilterButton.classList.toggle("active", filter === "hybrid");
  loadCars();
}

async function loadCars() {
  const params = new URLSearchParams();
  const maxPrice = maxPriceFilter.value.trim();

  if (maxPrice) {
    params.set("maxPriceWan", maxPrice);
  }

  if (activeCarFilter === "suv") {
    params.set("category", "SUV");
  } else if (activeCarFilter === "hybrid") {
    params.set("hybrid", "true");
  }

  carsGrid.innerHTML = "<p class=\"empty-state\">Loading Toyota database records...</p>";

  try {
    const response = await fetch(`/ToyotaCars${params.toString() ? `?${params}` : ""}`);
    if (!response.ok) {
      throw new Error("Could not load Toyota database records.");
    }

    const cars = await response.json();
    renderCars(cars);
  } catch (error) {
    carsGrid.innerHTML = "";
    const message = document.createElement("p");
    message.className = "empty-state error-text";
    message.textContent = error.message;
    carsGrid.append(message);
  }
}

async function loadLineupCount() {
  try {
    const response = await fetch("/ToyotaCars");
    if (!response.ok) {
      throw new Error("Lineup unavailable");
    }

    const cars = await response.json();
    allToyotaCars = cars;
    lineupCount.textContent = `${cars.length} records`;
    renderComparisonOptions(cars);
  } catch {
    lineupCount.textContent = "Unavailable";
  }
}

function renderComparisonOptions(cars) {
  compareFirst.innerHTML = "";
  compareSecond.innerHTML = "";

  for (const car of cars) {
    compareFirst.append(new Option(car.model, car.model));
    compareSecond.append(new Option(car.model, car.model));
  }

  compareFirst.value = cars.find((car) => car.model.toLowerCase() === "camry")?.model || cars[0]?.model || "";
  compareSecond.value = cars.find((car) => car.model.toLowerCase() === "rav4")?.model || cars[1]?.model || "";
}

async function compareSelectedCars() {
  const first = compareFirst.value;
  const second = compareSecond.value;

  if (!first || !second || first === second) {
    comparisonResult.innerHTML = "<p class=\"empty-state error-text\">Choose two different Toyota models.</p>";
    return;
  }

  const params = new URLSearchParams();
  params.append("models", first);
  params.append("models", second);
  comparisonResult.innerHTML = "<p class=\"empty-state\">Comparing Toyota models...</p>";

  try {
    const response = await fetch(`/ToyotaCars/compare?${params}`);
    if (!response.ok) {
      throw new Error("Could not compare these Toyota models.");
    }

    const comparison = await response.json();
    renderComparison(comparison);
  } catch (error) {
    comparisonResult.innerHTML = "";
    const message = document.createElement("p");
    message.className = "empty-state error-text";
    message.textContent = error.message;
    comparisonResult.append(message);
  }
}

function renderComparison(comparison) {
  const rows = comparison.rows.map((row) => `
    <tr>
      <th>${row.label}</th>
      <td>${row.first}</td>
      <td>${row.second}</td>
    </tr>
  `).join("");

  comparisonResult.innerHTML = `
    <table class="comparison-table">
      <thead>
        <tr>
          <th>Factor</th>
          <th>${comparison.firstModel}</th>
          <th>${comparison.secondModel}</th>
        </tr>
      </thead>
      <tbody>${rows}</tbody>
    </table>
  `;
}

function renderCars(cars) {
  carsGrid.innerHTML = "";

  if (cars.length === 0) {
    carsGrid.innerHTML = "<p class=\"empty-state\">No Toyota records match this filter.</p>";
    return;
  }

  for (const car of cars) {
    const article = document.createElement("article");
    article.className = "car-card";
    article.innerHTML = `
      <div class="car-card-header">
        <strong>${car.model}</strong>
        <span>${car.category}</span>
      </div>
      <dl>
        <div><dt>Price</dt><dd>${car.priceRangeWan} wan</dd></div>
        <div><dt>Seats</dt><dd>${car.seats}</dd></div>
        <div><dt>Fuel</dt><dd>${car.fuelType}</dd></div>
        <div><dt>Hybrid</dt><dd>${car.hasHybridOption ? "Yes" : "No"}</dd></div>
      </dl>
      <p>${car.bestFor}</p>
      <a href="${car.sourceUrl}" target="_blank" rel="noreferrer">Official source</a>
    `;
    carsGrid.append(article);
  }
}

chatForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  await submitMessage(messageInput.value.trim());
});

for (const button of exampleButtons) {
  button.addEventListener("click", () => {
    conversationNameInput.value = createUniqueName(button.dataset.topic || "shopping-advice");
    messageInput.value = button.dataset.example || "";
    messageInput.focus();
  });
}

newChatButton.addEventListener("click", () => {
  startNewConversation = true;
  conversationNameInput.value = "";
  messages.innerHTML = "";
  appendMessage("assistant", "New Toyota recommendation chat started. Ask about the customer's needs, and I will name the chat automatically.");
  messageInput.focus();
});
openDatabaseButton.addEventListener("click", () => setActiveView("database"));

refreshButton.addEventListener("click", loadConversations);
chatTab.addEventListener("click", () => setActiveView("chat"));
databaseTab.addEventListener("click", () => setActiveView("database"));
allCarsButton.addEventListener("click", () => setActiveCarFilter("all"));
suvFilterButton.addEventListener("click", () => setActiveCarFilter("suv"));
hybridFilterButton.addEventListener("click", () => setActiveCarFilter("hybrid"));
refreshCarsButton.addEventListener("click", loadCars);
compareButton.addEventListener("click", compareSelectedCars);
maxPriceFilter.addEventListener("input", () => {
  window.clearTimeout(maxPriceFilter.searchTimeout);
  maxPriceFilter.searchTimeout = window.setTimeout(loadCars, 250);
});

visitorLabel.textContent = "Saved locally";
loadConversations();
loadLineupCount();
loadCars();


