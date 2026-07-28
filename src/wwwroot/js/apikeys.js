const { api, requireUser, formatDate } = window.SslApi;

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

function showMessage(text, className) {
  const msg = document.getElementById("message");
  msg.textContent = text;
  msg.className = className;
}

async function loadKeys() {
  const keys = await api("/api/banker/api-keys");
  const el = document.getElementById("keys");
  if (!keys.length) {
    el.innerHTML = `<p class="muted">No API keys yet. Create one above for a chore or other app.</p>`;
    return;
  }

  el.innerHTML = keys
    .map((k) => {
      const active = !k.revokedAt;
      const status = active ? "Active" : `Revoked ${formatDate(k.revokedAt)}`;
      const revokeBtn = active
        ? `<button class="btn btn-secondary btn-sm" type="button" data-action="revoke" data-id="${k.id}">Revoke</button>`
        : "";
      return `
        <div class="task-card" data-key-id="${k.id}">
          <div class="list-title">${escapeHtml(k.name)} <span class="muted">(${escapeHtml(k.source)})</span></div>
          <div class="task-meta">
            <span>Prefix ${escapeHtml(k.keyPrefix)}…</span>
            <span>Created ${formatDate(k.createdAt)}</span>
            <span>${escapeHtml(status)}</span>
          </div>
          <div class="actions">${revokeBtn}</div>
        </div>
      `;
    })
    .join("");
}

document.getElementById("logout").addEventListener("click", async () => {
  await api("/api/auth/logout", { method: "POST" });
  window.location.href = "/";
});

document.getElementById("create-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  const name = document.getElementById("name").value.trim();
  const source = document.getElementById("source").value.trim().toLowerCase();
  const newKey = document.getElementById("new-key");

  try {
    const result = await api("/api/banker/api-keys", {
      method: "POST",
      body: { name, source },
    });
    document.getElementById("name").value = "";
    document.getElementById("source").value = "";
    showMessage(`Created key for ${result.name}. Copy the secret now — it will not be shown again.`, "success");
    newKey.className = "success";
    newKey.innerHTML = `
      <p><strong>API key</strong> (copy now)</p>
      <p><code id="raw-api-key">${escapeHtml(result.apiKey)}</code></p>
      <div class="actions">
        <button class="btn btn-sm" type="button" id="copy-key">Copy</button>
      </div>
    `;
    document.getElementById("copy-key").addEventListener("click", async () => {
      try {
        await navigator.clipboard.writeText(result.apiKey);
        showMessage("API key copied to clipboard.", "success");
      } catch {
        showMessage("Could not copy automatically — select the key and copy manually.", "error");
      }
    });
    await loadKeys();
  } catch (err) {
    newKey.className = "hidden";
    newKey.innerHTML = "";
    showMessage(err.message || "Could not create API key.", "error");
  }
});

document.getElementById("keys").addEventListener("click", async (e) => {
  const btn = e.target.closest("[data-action=revoke]");
  if (!btn) return;

  const id = btn.getAttribute("data-id");
  if (!confirm("Revoke this API key? External apps using it will stop working.")) return;

  try {
    await api(`/api/banker/api-keys/${id}/revoke`, { method: "POST" });
    showMessage("API key revoked.", "success");
    document.getElementById("new-key").className = "hidden";
    await loadKeys();
  } catch (err) {
    showMessage(err.message || "Could not revoke API key.", "error");
  }
});

(async function init() {
  const me = await requireUser("Banker");
  if (!me) return;
  await loadKeys();
})();
