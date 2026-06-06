const API_BASE = (window.API_BASE || "").replace(/\/$/, "");
const TOKEN_KEY = "cd_site_token";

function getToken() { return localStorage.getItem(TOKEN_KEY); }
function setToken(t) { localStorage.setItem(TOKEN_KEY, t); }
function clearToken() { localStorage.removeItem(TOKEN_KEY); }

async function api(path, { method = "GET", body, auth = false } = {}) {
  const headers = { "Content-Type": "application/json" };
  if (auth) {
    const t = getToken();
    if (t) headers["Authorization"] = "Bearer " + t;
  }
  const res = await fetch(API_BASE + path, {
    method,
    headers,
    body: body ? JSON.stringify(body) : undefined,
  });
  let data = null;
  try { data = await res.json(); } catch (_) {}
  if (!res.ok) {
    const detail = (data && (data.detail || data.message)) || ("Error " + res.status);
    throw new Error(typeof detail === "string" ? detail : JSON.stringify(detail));
  }
  return data;
}

const Auth = {
  me: () => api("/api/me", { auth: true }),
};

const Pairing = {
  quickConfirm: (code) =>
    api("/api/pair/quick-confirm", { method: "POST", body: { code } }),
};

const Game = {
  shopItems: () => api("/api/shop/items", { auth: true }),
  buy: (item_id) => api("/api/shop/buy", { method: "POST", body: { item_id }, auth: true }),
  equipWeapon: (item_id) => api("/api/loadout/weapon", { method: "POST", body: { item_id }, auth: true }),
  myClues: () => api("/api/clues", { auth: true }),
};

const State = {
  getScreen: () => api("/api/state/screen", { auth: true }),
};

const Chat = {
  getQuestion: () => api("/api/chat/question", { auth: true }),
  answer: (id, choice) => api("/api/chat/answer", { method: "POST", body: { id, choice }, auth: true }),
};

const Dialogue = {
  get: (key) => api("/api/dialogue/" + encodeURIComponent(key), { auth: true }),
  questions: () => api("/api/intro/questions", { auth: true }),
  gateDone: () => api("/api/gate/done", { method: "POST", auth: true }),
};

const SCREEN_PAGE = {
  wait:  "wait.html?mode=loading",
  clues: "clues.html",
  shop:  "shop.html",
  chat:  "chat.html",
  talk:  "story.html?key=doom_to_shop",
  pause: "wait.html?mode=pause",
  over_win:  "over.html?r=win",
  over_lose: "over.html?r=lose",
  over_quit: "over.html?r=quit",
};

function pageForScreen(s) { return SCREEN_PAGE[s] || SCREEN_PAGE.wait; }

async function goToCurrentScreen() {
  try {
    const r = await State.getScreen();
    window.location.href = pageForScreen(r.screen);
  } catch (_) {
    window.location.href = SCREEN_PAGE.wait;
  }
}

function startScreenWatcher(thisScreen) {
  setInterval(async () => {
    try {
      const r = await State.getScreen();
      if (r.screen && r.screen !== thisScreen) {
        window.location.href = pageForScreen(r.screen);
      }
    } catch (_) {}
  }, 2000);
}

function requireAuth() {
  if (!getToken()) {
    window.location.href = "index.html";
    return false;
  }
  return true;
}

function getQueryParam(name) {
  return new URLSearchParams(window.location.search).get(name);
}
