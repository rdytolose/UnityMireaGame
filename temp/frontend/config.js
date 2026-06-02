// Frontend runtime config.
// Leave empty ("") when the site and backend are served behind the same domain
// (nginx proxies /api -> backend, as in the provided docker-compose).
// For local dev where the backend runs separately, set the full URL, e.g.:
//   window.API_BASE = "http://localhost:8000";
window.API_BASE = "";
