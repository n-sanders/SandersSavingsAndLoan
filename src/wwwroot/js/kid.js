const { api, formatMoney, dollarsToCents, formatDate, requireUser } = window.SslApi;

async function loadAccount() {
  const account = await api("/api/me/account");
  document.getElementById("greeting").textContent = `Hi ${account.displayName} — here’s your savings.`;
  document.getElementById("balance").textContent = formatMoney(account.balanceCents);
}

function badgeClass(status) {
  return `badge badge-${String(status).toLowerCase()}`;
}

async function loadTasks() {
  const tasks = await api("/api/me/tasks");
  const el = document.getElementById("tasks");
  if (!tasks.length) {
    el.innerHTML = `<li class="muted">No tasks yet. Finish a chore or reading session and submit it above.</li>`;
    return;
  }

  el.innerHTML = tasks.map((t) => `
    <li>
      <div class="list-title">${escapeHtml(t.description)}</div>
      <div class="muted">
        <span class="${badgeClass(t.status)}">${escapeHtml(t.status)}</span>
        Suggested ${formatMoney(t.suggestedAmountCents)}
        ${t.finalAmountCents != null ? ` · Paid ${formatMoney(t.finalAmountCents)}` : ""}
        · ${formatDate(t.createdAt)}
      </div>
      ${t.bankerNote ? `<div class="muted">Banker note: ${escapeHtml(t.bankerNote)}</div>` : ""}
    </li>
  `).join("");
}

async function loadTransactions() {
  const txs = await api("/api/me/transactions");
  const el = document.getElementById("transactions");
  if (!txs.length) {
    el.innerHTML = `<li class="muted">No deposits or withdrawals yet.</li>`;
    return;
  }

  el.innerHTML = txs.map((t) => `
    <li>
      <div class="list-title">
        <span class="badge badge-${t.type.toLowerCase()}">${escapeHtml(t.type)}</span>
        ${formatMoney(t.amountCents)}
      </div>
      <div class="muted">${escapeHtml(t.note)} · ${formatDate(t.createdAt)}</div>
    </li>
  `).join("");
}

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

document.getElementById("logout").addEventListener("click", async () => {
  await api("/api/auth/logout", { method: "POST" });
  window.location.href = "/";
});

document.getElementById("task-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  const msg = document.getElementById("task-message");
  msg.className = "hidden";

  const description = document.getElementById("description").value.trim();
  const suggestedAmountCents = dollarsToCents(document.getElementById("suggested").value);

  if (!description || !Number.isFinite(suggestedAmountCents) || suggestedAmountCents <= 0) {
    msg.textContent = "Enter a description and a positive dollar amount.";
    msg.className = "error";
    return;
  }

  try {
    await api("/api/me/tasks", {
      method: "POST",
      body: { description, suggestedAmountCents },
    });
    document.getElementById("task-form").reset();
    msg.textContent = "Submitted! The banker will review it soon.";
    msg.className = "success";
    await Promise.all([loadTasks(), loadAccount()]);
  } catch (err) {
    msg.textContent = err.message || "Could not submit task.";
    msg.className = "error";
  }
});

(async function init() {
  const me = await requireUser("Kid");
  if (!me) return;
  await Promise.all([loadAccount(), loadTasks(), loadTransactions()]);
})();
