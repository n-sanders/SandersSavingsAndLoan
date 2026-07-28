const { api, requireUser } = window.SslApi;

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

async function loadKids() {
  const accounts = await api("/api/banker/accounts");
  const select = document.getElementById("userId");
  select.innerHTML = accounts
    .map(
      (a) =>
        `<option value="${a.userId}">${escapeHtml(a.displayName)} (@${escapeHtml(a.username)})</option>`
    )
    .join("");
}

document.getElementById("logout").addEventListener("click", async () => {
  await api("/api/auth/logout", { method: "POST" });
  window.location.href = "/";
});

document.getElementById("passphrase-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  const msg = document.getElementById("message");
  const userId = Number(document.getElementById("userId").value);
  const passphrase = document.getElementById("passphrase").value;
  const confirm = document.getElementById("confirm").value;

  if (!userId) {
    msg.textContent = "Choose a kid.";
    msg.className = "error";
    return;
  }

  if (!passphrase.trim()) {
    msg.textContent = "Passphrase is required.";
    msg.className = "error";
    return;
  }

  if (passphrase !== confirm) {
    msg.textContent = "Passphrases do not match.";
    msg.className = "error";
    return;
  }

  try {
    const result = await api(`/api/banker/kids/${userId}/passphrase`, {
      method: "POST",
      body: { passphrase },
    });
    document.getElementById("passphrase").value = "";
    document.getElementById("confirm").value = "";
    msg.textContent = `Passphrase updated for ${result.displayName}.`;
    msg.className = "success";
  } catch (err) {
    msg.textContent = err.message || "Could not update passphrase.";
    msg.className = "error";
  }
});

(async function init() {
  const me = await requireUser("Banker");
  if (!me) return;
  await loadKids();
})();
