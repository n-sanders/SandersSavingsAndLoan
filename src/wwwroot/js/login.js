const { api } = window.SslApi;

async function redirectIfLoggedIn() {
  try {
    const me = await api("/api/auth/me");
    window.location.href = me.role === "Banker" ? "/banker.html" : "/kid.html";
  } catch {
    // stay on login
  }
}

document.getElementById("login-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  const errorEl = document.getElementById("error");
  errorEl.classList.add("hidden");

  const username = document.getElementById("username").value.trim();
  const passphrase = document.getElementById("passphrase").value;

  try {
    const me = await api("/api/auth/login", {
      method: "POST",
      body: { username, passphrase },
    });
    window.location.href = me.role === "Banker" ? "/banker.html" : "/kid.html";
  } catch (err) {
    errorEl.textContent = err.status === 401
      ? "Invalid username or passphrase."
      : (err.message || "Login failed.");
    errorEl.classList.remove("hidden");
  }
});

redirectIfLoggedIn();
