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
const exampleButtons = document.querySelectorAll("[data-topic]");
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
const zhButton = document.querySelector("#zhButton");
const enButton = document.querySelector("#enButton");

let startNewConversation = false;
let savedConversationNames = new Set();
let activeCarFilter = "all";
let allToyotaCars = [];
const visitorId = getOrCreateVisitorId();
let currentLanguage = window.localStorage.getItem("toyotaAdvisorLanguage") || "zh";

const translations = {
  zh: {
    brandSubtitle: "Toyota 車款推薦展示",
    sessionLabel: "私人瀏覽器工作階段",
    savedLocally: "已儲存在本機",
    newChat: "點我諮詢",
    openDatabase: "開啟車款資料庫",
    savedChats: "已儲存對話",
    refresh: "重新整理",
    eyebrow: "Toyota 車款推薦助手",
    headline: "幫每位客戶找到適合的 Toyota",
    subheadline: "依照預算、乘坐人數、通勤、家庭用途、停車需求、省油需求與車型偏好，推薦合適車款。",
    focusLabel: "定位",
    focusValue: "只推薦 Toyota",
    memoryLabel: "資料來源",
    memoryValue: "車款資料庫",
    lineupLabel: "車款數量",
    loading: "載入中",
    records: "筆資料",
    unavailable: "無法取得",
    chatTab: "對話推薦",
    databaseTab: "車款資料庫",
    exampleBudgetTitle: "通勤省油",
    exampleBudgetText: "平價、好開、省油",
    exampleFamilyTitle: "家庭休旅",
    exampleFamilyText: "適合小孩與出遊",
    exampleParkingTitle: "市區好停",
    exampleParkingText: "小巧、實用、好停車",
    welcomeMessage: "您好，我可以依照客戶需求推薦 Toyota 車款。我會專注在 Toyota，不推薦其他品牌。",
    messagePlaceholder: "請輸入 Toyota 車款推薦問題...",
    send: "送出",
    maxPrice: "最高預算",
    all: "全部",
    compare: "比較",
    with: "與",
    serviceWaking: "服務正在啟動或重新部署，請稍等一下再試一次。",
    historyNotFound: "找不到這個已儲存對話。",
    historyLoadFailed: "無法載入這個已儲存對話。",
    deleteFailed: "無法刪除這個已儲存對話。",
    renameFailed: "無法重新命名這個已儲存對話。",
    conversationsFailed: "無法載入已儲存對話。",
    noSavedChats: "目前沒有已儲存對話。",
    untitledChat: "未命名對話",
    noMessages: "沒有訊息",
    rename: "重新命名",
    delete: "刪除",
    renamePrompt: "重新命名這個對話：",
    duplicateName: "這個對話名稱已經存在，請換一個名稱。",
    deleteConfirm: "要刪除「{name}」嗎？",
    deletedMessage: "這個已儲存對話已刪除。準備好時可以開始新的 Toyota 推薦。",
    newChatMessage: "新的 Toyota 推薦對話已開始。請告訴我客戶需求，我會自動命名這個對話。",
    loadingCars: "正在載入 Toyota 車款資料...",
    loadCarsFailed: "無法載入 Toyota 車款資料。",
    chooseTwo: "請選擇兩台不同的 Toyota 車款。",
    comparing: "正在比較 Toyota 車款...",
    compareFailed: "無法比較這兩台 Toyota 車款。",
    noCars: "沒有符合篩選條件的 Toyota 車款。",
    factor: "比較項目",
    price: "價格",
    seats: "座位",
    fuel: "動力",
    hybrid: "油電",
    yes: "是",
    no: "否",
    officialSource: "官方資料來源",
    comparisonLabels: {
      "Price": "價格",
      "Fuel / Powertrain": "動力系統",
      "Fuel Economy": "油耗",
      "Interior / Seats": "座位 / 車型",
      "Best Use": "適合用途",
      "Safety / Source": "安全 / 資料來源"
    }
  },
  en: {
    brandSubtitle: "Recommendation demo",
    sessionLabel: "Private browser session",
    savedLocally: "Saved locally",
    newChat: "Start consultation",
    openDatabase: "Open vehicle database",
    savedChats: "Saved chats",
    refresh: "Refresh",
    eyebrow: "Toyota vehicle recommendation assistant",
    headline: "Find the right Toyota for each customer",
    subheadline: "Match customer needs to Toyota choices by budget, passengers, commute, family use, parking, fuel economy, and vehicle preference.",
    focusLabel: "Focus",
    focusValue: "Toyota only",
    memoryLabel: "Memory",
    memoryValue: "Vehicle data",
    lineupLabel: "Lineup",
    loading: "Loading",
    records: "records",
    unavailable: "Unavailable",
    chatTab: "Chat",
    databaseTab: "Vehicle Database",
    exampleBudgetTitle: "Budget commuter",
    exampleBudgetText: "Affordable and fuel-saving",
    exampleFamilyTitle: "Family SUV",
    exampleFamilyText: "Space for kids and trips",
    exampleParkingTitle: "City parking",
    exampleParkingText: "Compact and practical",
    welcomeMessage: "Hello, I can help recommend Toyota models based on the customer's needs. I focus on Toyota choices, not other car brands.",
    messagePlaceholder: "Ask a Toyota recommendation question...",
    send: "Send",
    maxPrice: "Max price",
    all: "All",
    compare: "Compare",
    with: "With",
    serviceWaking: "The service is waking up or redeploying. Please wait a moment and try again.",
    historyNotFound: "This saved chat could not be found.",
    historyLoadFailed: "This saved chat could not be loaded.",
    deleteFailed: "This saved chat could not be deleted.",
    renameFailed: "This saved chat could not be renamed.",
    conversationsFailed: "Could not load saved chats.",
    noSavedChats: "No saved chats yet.",
    untitledChat: "untitled-chat",
    noMessages: "No messages",
    rename: "Rename",
    delete: "Delete",
    renamePrompt: "Rename this saved chat:",
    duplicateName: "That chat name already exists. Please choose a different name.",
    deleteConfirm: "Delete \"{name}\"?",
    deletedMessage: "That saved chat was deleted. Start a new Toyota recommendation when you are ready.",
    newChatMessage: "New Toyota recommendation chat started. Ask about the customer's needs, and I will name the chat automatically.",
    loadingCars: "Loading Toyota database records...",
    loadCarsFailed: "Could not load Toyota database records.",
    chooseTwo: "Choose two different Toyota models.",
    comparing: "Comparing Toyota models...",
    compareFailed: "Could not compare these Toyota models.",
    noCars: "No Toyota records match this filter.",
    factor: "Factor",
    price: "Price",
    seats: "Seats",
    fuel: "Fuel",
    hybrid: "Hybrid",
    yes: "Yes",
    no: "No",
    officialSource: "Official source",
    comparisonLabels: {}
  }
};

function t(key, replacements = {}) {
  let value = translations[currentLanguage][key] || translations.en[key] || key;
  for (const [name, replacement] of Object.entries(replacements)) {
    value = value.replace(`{${name}}`, replacement);
  }
  return value;
}

function translateComparisonLabel(label) {
  return translations[currentLanguage].comparisonLabels?.[label] || label;
}

function applyLanguage(language) {
  currentLanguage = language;
  window.localStorage.setItem("toyotaAdvisorLanguage", language);
  document.documentElement.lang = language === "zh" ? "zh-Hant" : "en";
  zhButton.classList.toggle("active", language === "zh");
  enButton.classList.toggle("active", language === "en");

  for (const element of document.querySelectorAll("[data-i18n]")) {
    element.textContent = t(element.dataset.i18n);
  }

  messageInput.placeholder = t("messagePlaceholder");
  visitorLabel.textContent = t("savedLocally");
  if (allToyotaCars.length > 0) {
    lineupCount.textContent = `${allToyotaCars.length} ${t("records")}`;
  }
  loadConversations();
  if (!databaseView.hidden) {
    loadCars();
  }
}

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

async function readJsonResponse(response, fallbackMessage) {
  const contentType = response.headers.get("content-type") || "";

  if (!contentType.includes("application/json")) {
    await response.text().catch(() => "");
    throw new Error(fallbackMessage);
  }

  const data = await response.json();

  if (!response.ok) {
    throw new Error(data.error || fallbackMessage);
  }

  return data;
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

function hasAnyKeyword(value, keywords) {
  return keywords.some((keyword) => value.includes(keyword));
}

function createConversationName(message) {
  const lowerMessage = message.toLowerCase();
  let topic = "shopping-advice";

  const wantsLuxury = hasAnyKeyword(lowerMessage, [
    "expensive", "luxury", "premium", "high budget", "no budget", "rich", "executive", "chauffeur", "vip",
    "貴", "很貴", "高級", "豪華", "預算高", "沒有預算", "商務", "老闆", "有錢", "接送", "主管", "貴賓", "舒適"
  ]);
  const wantsFamily = hasAnyKeyword(lowerMessage, [
    "family", "kids", "kid", "children", "child", "baby", "parents", "school pickup",
    "小孩", "孩子", "兒童", "孩童", "寶寶", "嬰兒", "家庭", "家人", "親子", "小朋友", "給小孩", "爸媽", "接小孩", "載小孩"
  ]);
  const wantsTrip = hasAnyKeyword(lowerMessage, [
    "trip", "travel", "weekend", "camping", "outdoor", "road trip", "long distance",
    "露營", "旅行", "旅遊", "出去玩", "出遊", "週末", "假日", "長途", "爬山", "戶外", "郊遊"
  ]);
  const wantsCommute = hasAnyKeyword(lowerMessage, [
    "commute", "daily", "work", "school", "errands",
    "通勤", "上班", "上課", "每天", "代步", "日常", "買菜", "短程", "平常開"
  ]);
  const wantsParking = hasAnyKeyword(lowerMessage, [
    "parking", "city", "compact", "small car", "first car", "beginner",
    "停車", "好停", "市區", "城市", "小台", "小車", "新手", "第一台車", "巷子", "窄路"
  ]);
  const wantsHybrid = hasAnyKeyword(lowerMessage, [
    "fuel", "hybrid", "efficient", "gas mileage", "phev", "electric", "ev",
    "省油", "油電", "油耗", "節能", "hybrid", "插電", "電動", "純電", "充電"
  ]);
  const wantsSuv = hasAnyKeyword(lowerMessage, [
    "suv", "crossover", "off-road", "4wd", "awd",
    "休旅", "休旅車", "越野", "四輪傳動", "底盤高", "空間大"
  ]);
  const wantsBudget = hasAnyKeyword(lowerMessage, [
    "budget", "affordable", "cheap", "low price",
    "預算", "便宜", "平價", "划算", "省錢"
  ]);
  const wantsSports = hasAnyKeyword(lowerMessage, [
    "sports car", "sporty", "performance", "coupe", "fun to drive", "gr86", "supra", "gr yaris", "track",
    "跑車", "性能", "雙門", "熱血", "駕駛樂趣", "開快", "帥", "操控", "賽道", "甩尾"
  ]);
  const wantsCommercial = hasAnyKeyword(lowerMessage, [
    "truck", "van", "cargo", "delivery", "business", "commercial",
    "貨車", "廂型車", "貨卡", "載貨", "送貨", "做生意", "公司用", "商用", "工具車"
  ]);
  const wantsCompare = hasAnyKeyword(lowerMessage, [
    "compare", "versus", " vs ", "difference",
    "比較", "對比", "哪個", "差別"
  ]);

  if (wantsSports) {
    topic = "sports-car";
  } else if (wantsCommercial) {
    topic = "work-vehicle";
  } else if (wantsLuxury) {
    topic = "luxury-options";
  } else if (wantsCompare) {
    topic = "model-comparison";
  } else if (wantsFamily && (wantsSuv || wantsTrip)) {
    topic = "family-suv";
  } else if (wantsFamily) {
    topic = "family-car";
  } else if (wantsCommute && wantsHybrid) {
    topic = "budget-commute";
  } else if (wantsParking) {
    topic = "city-parking";
  } else if (wantsSuv) {
    topic = "suv-choice";
  } else if (wantsHybrid) {
    topic = "fuel-saving";
  } else if (wantsBudget) {
    topic = "budget-choice";
  } else if (wantsCommute) {
    topic = "daily-commute";
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
      language: currentLanguage,
      message
    })
  });

  const data = await readJsonResponse(
    response,
    t("serviceWaking")
  );

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
    appendMessage("assistant", t("historyNotFound"), true);
    return;
  }

  const data = await readJsonResponse(response, t("historyLoadFailed"));
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
    await readJsonResponse(response, t("deleteFailed"));
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

  await readJsonResponse(response, t("renameFailed"));
}

async function loadConversations() {
  const userId = encodeURIComponent(getUserId());
  const response = await fetch(`/ChatBot/users/${userId}/conversations`);

  conversationList.innerHTML = "";
  if (!response.ok) {
    conversationList.textContent = t("conversationsFailed");
    return;
  }

  const conversations = await readJsonResponse(response, t("conversationsFailed"));
  savedConversationNames = new Set(
    conversations
      .map((conversation) => conversation.conversationName || "")
      .filter(Boolean)
      .map((name) => name.toLowerCase())
  );
  const savedChats = conversations.filter((conversation) => conversation.conversationName);

  if (savedChats.length === 0) {
    conversationList.textContent = t("noSavedChats");
    return;
  }

  for (const conversation of savedChats) {
    const item = document.createElement("div");
    item.className = "conversation-item";
    item.innerHTML = `
      <button type="button" class="conversation-open">
        <strong>${conversation.conversationName || t("untitledChat")}</strong>
        <span>${conversation.lastMessage || t("noMessages")}</span>
      </button>
      <div class="conversation-actions">
        <button type="button" class="conversation-rename" title="${t("rename")}">${t("rename")}</button>
        <button type="button" class="conversation-delete" title="${t("delete")}">${t("delete")}</button>
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
      const newName = prompt(t("renamePrompt"), oldName)?.trim();

      if (!oldName || !newName || newName === oldName) {
        return;
      }

      if (savedConversationNames.has(newName.toLowerCase())) {
        appendMessage("assistant", t("duplicateName"), true);
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
      if (!name || !confirm(t("deleteConfirm", { name }))) {
        return;
      }

      try {
        await deleteConversation(name);
        if (conversationNameInput.value.trim() === name) {
          messages.innerHTML = "";
          appendMessage("assistant", t("deletedMessage"));
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

  carsGrid.innerHTML = `<p class="empty-state">${t("loadingCars")}</p>`;

  try {
    const response = await fetch(`/ToyotaCars${params.toString() ? `?${params}` : ""}`);
    const cars = await readJsonResponse(response, t("loadCarsFailed"));
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
    const cars = await readJsonResponse(response, t("unavailable"));
    allToyotaCars = cars;
    lineupCount.textContent = `${cars.length} ${t("records")}`;
    renderComparisonOptions(cars);
  } catch {
    lineupCount.textContent = t("unavailable");
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
    comparisonResult.innerHTML = `<p class="empty-state error-text">${t("chooseTwo")}</p>`;
    return;
  }

  const params = new URLSearchParams();
  params.append("models", first);
  params.append("models", second);
  comparisonResult.innerHTML = `<p class="empty-state">${t("comparing")}</p>`;

  try {
    const response = await fetch(`/ToyotaCars/compare?${params}`);
    const comparison = await readJsonResponse(response, t("compareFailed"));
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
      <th>${translateComparisonLabel(row.label)}</th>
      <td>${row.first}</td>
      <td>${row.second}</td>
    </tr>
  `).join("");

  comparisonResult.innerHTML = `
    <table class="comparison-table">
      <thead>
        <tr>
          <th>${t("factor")}</th>
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
    carsGrid.innerHTML = `<p class="empty-state">${t("noCars")}</p>`;
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
        <div><dt>${t("price")}</dt><dd>${car.priceRangeWan} ${currentLanguage === "zh" ? "萬" : "wan"}</dd></div>
        <div><dt>${t("seats")}</dt><dd>${car.seats}</dd></div>
        <div><dt>${t("fuel")}</dt><dd>${car.fuelType}</dd></div>
        <div><dt>${t("hybrid")}</dt><dd>${car.hasHybridOption ? t("yes") : t("no")}</dd></div>
      </dl>
      <p>${car.bestFor}</p>
      <a href="${car.sourceUrl}" target="_blank" rel="noreferrer">${t("officialSource")}</a>
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
    messageInput.value = currentLanguage === "zh"
      ? button.dataset.exampleZh || ""
      : button.dataset.exampleEn || "";
    messageInput.focus();
  });
}

newChatButton.addEventListener("click", () => {
  startNewConversation = true;
  conversationNameInput.value = "";
  messages.innerHTML = "";
  appendMessage("assistant", t("newChatMessage"));
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
zhButton.addEventListener("click", () => applyLanguage("zh"));
enButton.addEventListener("click", () => applyLanguage("en"));
maxPriceFilter.addEventListener("input", () => {
  window.clearTimeout(maxPriceFilter.searchTimeout);
  maxPriceFilter.searchTimeout = window.setTimeout(loadCars, 250);
});

applyLanguage(currentLanguage);
loadLineupCount();
loadCars();


