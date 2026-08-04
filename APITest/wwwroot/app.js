const userIdInput = document.querySelector("#userId");
const conversationNameInput = document.querySelector("#conversationName");
const newChatButton = document.querySelector("#newChatButton");
const refreshButton = document.querySelector("#refreshButton");
const conversationList = document.querySelector("#conversationList");
const messages = document.querySelector("#messages");
const chatForm = document.querySelector("#chatForm");
const messageInput = document.querySelector("#messageInput");
const sendButton = document.querySelector("#sendButton");
const exampleButtons = document.querySelectorAll("[data-example]");

let startNewConversation = false;
let savedConversationNames = new Set();

function appendMessage(role, text, isError = false) {
  const article = document.createElement("article");
  article.className = `message ${role}${isError ? " error" : ""}`;

  const paragraph = document.createElement("p");
  paragraph.textContent = text;
  article.append(paragraph);
  messages.append(article);
  messages.scrollTop = messages.scrollHeight;
}

function getUserId() {
  return userIdInput.value.trim() || "iris";
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
  let topic = "general";

  if ((lowerMessage.includes("family") || lowerMessage.includes("kids") || lowerMessage.includes("child")) &&
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
  }

  return createUniqueName(`toyota-${topic}`);
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
  const toyotaConversations = conversations.filter((conversation) =>
    (conversation.conversationName || "").toLowerCase().startsWith("toyota")
  );

  if (toyotaConversations.length === 0) {
    conversationList.textContent = "No saved chats yet.";
    return;
  }

  for (const conversation of toyotaConversations) {
    const item = document.createElement("div");
    item.className = "conversation-item";
    item.innerHTML = `
      <button type="button" class="conversation-open">
        <strong>${conversation.conversationName || "untitled-chat"}</strong>
        <span>${conversation.lastMessage || "No messages"}</span>
      </button>
      <button type="button" class="conversation-delete" title="Delete chat">Delete</button>
    `;

    const openButton = item.querySelector(".conversation-open");
    openButton.addEventListener("click", () => {
      conversationNameInput.value = conversation.conversationName || "";
      loadHistory(conversation.conversationName || "");
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
    appendMessage("assistant", data.reply);
    await loadConversations();
  } catch (error) {
    appendMessage("assistant", error.message, true);
  } finally {
    sendButton.disabled = false;
    messageInput.focus();
  }
}

chatForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  await submitMessage(messageInput.value.trim());
});

for (const button of exampleButtons) {
  button.addEventListener("click", () => {
    conversationNameInput.value = createUniqueName(button.dataset.topic || "toyota-general");
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

refreshButton.addEventListener("click", loadConversations);
userIdInput.addEventListener("change", loadConversations);

loadConversations();
