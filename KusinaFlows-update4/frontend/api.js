// ============================================================================
// KUSINAFLOW SHARED API CLIENT
// Wraps fetch() so every call automatically carries the session token issued
// by /api/auth/login, and centralizes what happens when a session has
// expired (401 from the backend's auth middleware).
// ============================================================================
(function () {
  // Builds an absolute path to another page under frontend/, regardless of
  // how deep the current page is nested or what the site's root path is.
  // Mirrors the routing logic already used by nav.js's go().
  function buildFrontendPath(folderAndPage) {
    const currentLoc = window.location.href;
    const frontendIndex = currentLoc.indexOf("/frontend/");

    if (frontendIndex !== -1) {
      return currentLoc.substring(0, frontendIndex + 10) + folderAndPage;
    }
    return "../" + folderAndPage; // file:// fallback
  }

  function redirectToLogin() {
    window.location.href = buildFrontendPath("login/login.html");
  }

  function getAuthToken() {
    return localStorage.getItem("authToken");
  }

  // Drop-in replacement for fetch(): same signature, same Response object,
  // but attaches "Authorization: Bearer <token>" automatically and bounces
  // the user back to the login page on a 401 (expired/missing session).
  async function kfFetch(url, options = {}) {
    const token = getAuthToken();
    const headers = new Headers(options.headers || {});
    if (token && !headers.has("Authorization")) {
      headers.set("Authorization", `Bearer ${token}`);
    }

    const response = await fetch(url, { ...options, headers });

    if (response.status === 401) {
      localStorage.clear();
      redirectToLogin();
    }

    return response;
  }

  window.KFApi = { kfFetch, getAuthToken, buildFrontendPath, redirectToLogin };
  // Most pages call fetch() directly today — exposing kfFetch globally lets
  // existing code switch over with a one-word rename.
  window.kfFetch = kfFetch;
})();
