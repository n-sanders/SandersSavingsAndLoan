const { api, formatMoney, dollarsToCents, formatDate, requireUser } = window.SslApi;

let accountsCache = [];

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

function fillAccountSelects() {
  const options = accountsCache
    .map((a) => `<option value="${a.id}">${escapeHtml(a.displayName)} (${formatMoney(a.balanceCents)})</option>`)
    .join("");
  document.getElementById("accountId").innerHTML = options;
  document.getElementById("historyAccount").innerHTML = options;
}

async function loadAccounts() {
  accountsCache = await api("/api/banker/accounts");
  const el = document.getElementById("accounts");
  el.innerHTML = accountsCache.map((a) => `
    <div class="account-tile">
      <strong>${escapeHtml(a.displayName)}</strong>
      <div class="muted">@${escapeHtml(a.username)}</div>
      <div class="amount${a.balanceCents < 0 ? " balance-negative" : ""}">${formatMoney(a.balanceCents)}</div>
    </div>
  `).join("");
  fillAccountSelects();
}

async function loadPendingTasks() {
  const tasks = await api("/api/banker/tasks?status=Pending");
  const el = document.getElementById("pending-tasks");
  if (!tasks.length) {
    el.innerHTML = `<p class="muted">No pending tasks. Kids can submit chores and reading from their pages.</p>`;
    return;
  }

  el.innerHTML = tasks.map((t) => `
    <div class="task-card" data-task-id="${t.id}">
      <div class="list-title">${escapeHtml(t.displayName)} — ${escapeHtml(t.description)}</div>
      <div class="task-meta">
        <span>Suggested ${formatMoney(t.suggestedAmountCents)}</span>
        <span>${formatDate(t.createdAt)}</span>
      </div>
      <div class="row">
        <div>
          <label for="final-${t.id}">Pay amount ($)</label>
          <input id="final-${t.id}" type="number" min="0.01" step="0.01" value="${(t.suggestedAmountCents / 100).toFixed(2)}" />
        </div>
        <div>
          <label for="note-${t.id}">Note (optional)</label>
          <input id="note-${t.id}" placeholder="Adjusted for…" />
        </div>
      </div>
      <div class="actions">
        <button class="btn btn-sm" type="button" data-action="approve">Approve &amp; deposit</button>
        <button class="btn btn-secondary btn-sm" type="button" data-action="reject">Reject</button>
      </div>
    </div>
  `).join("");
}

async function loadPendingLoans() {
  const loans = await api("/api/banker/loans?status=Pending");
  const el = document.getElementById("pending-loans");
  if (!loans.length) {
    el.innerHTML = `<p class="muted">No pending loan requests.</p>`;
    return;
  }

  el.innerHTML = loans.map((loan) => `
    <div class="task-card" data-loan-id="${loan.id}">
      <div class="list-title">${escapeHtml(loan.displayName)} — ${escapeHtml(loan.purpose)}</div>
      <div class="task-meta">
        <span>Borrow ${formatMoney(loan.amountCents)}</span>
        <span>${loan.termWeeks} weeks</span>
        <span>Weekly ${formatMoney(loan.weeklyPaymentCents)}</span>
        <span>${formatDate(loan.createdAt)}</span>
      </div>
      <div class="loan-preview-stats banker-loan-stats">
        <div>
          <div class="muted">Total repay</div>
          <div class="loan-stat">${formatMoney(loan.totalRepayCents)}</div>
        </div>
        <div class="interest-callout">
          <div class="muted">Total interest</div>
          <div class="loan-stat interest-amount">${formatMoney(loan.totalInterestCents)}</div>
        </div>
      </div>
      <div>
        <label for="loan-note-${loan.id}">Note (optional)</label>
        <input id="loan-note-${loan.id}" placeholder="Looks good / wait until…" />
      </div>
      <div class="actions">
        <button class="btn btn-sm" type="button" data-action="approve">Approve &amp; fund</button>
        <button class="btn btn-secondary btn-sm" type="button" data-action="reject">Reject</button>
      </div>
    </div>
  `).join("");
}

async function loadHistory() {
  const accountId = document.getElementById("historyAccount").value;
  const el = document.getElementById("history");
  if (!accountId) {
    el.innerHTML = `<li class="muted">Select an account.</li>`;
    return;
  }

  const txs = await api(`/api/banker/accounts/${accountId}/transactions`);
  if (!txs.length) {
    el.innerHTML = `<li class="muted">No transactions yet.</li>`;
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

document.getElementById("logout").addEventListener("click", async () => {
  await api("/api/auth/logout", { method: "POST" });
  window.location.href = "/";
});

document.getElementById("historyAccount").addEventListener("change", () => {
  loadHistory().catch((err) => console.error(err));
});

document.getElementById("pending-tasks").addEventListener("click", async (e) => {
  const btn = e.target.closest("[data-action]");
  if (!btn) return;
  const card = btn.closest(".task-card");
  const taskId = card.dataset.taskId;
  const msg = document.getElementById("task-message");
  msg.className = "hidden";

  const note = document.getElementById(`note-${taskId}`).value.trim();

  try {
    if (btn.dataset.action === "approve") {
      const finalAmountCents = dollarsToCents(document.getElementById(`final-${taskId}`).value);
      if (!Number.isFinite(finalAmountCents) || finalAmountCents <= 0) {
        msg.textContent = "Enter a positive pay amount.";
        msg.className = "error";
        return;
      }
      await api(`/api/banker/tasks/${taskId}/approve`, {
        method: "POST",
        body: { finalAmountCents, note: note || null },
      });
      msg.textContent = "Approved and deposited.";
    } else {
      await api(`/api/banker/tasks/${taskId}/reject`, {
        method: "POST",
        body: { note: note || null },
      });
      msg.textContent = "Task rejected.";
    }
    msg.className = "success";
    await Promise.all([loadAccounts(), loadPendingTasks(), loadHistory()]);
  } catch (err) {
    msg.textContent = err.message || "Could not update task.";
    msg.className = "error";
  }
});

document.getElementById("pending-loans").addEventListener("click", async (e) => {
  const btn = e.target.closest("[data-action]");
  if (!btn) return;
  const card = btn.closest(".task-card");
  const loanId = card.dataset.loanId;
  const msg = document.getElementById("loan-message");
  msg.className = "hidden";

  const note = document.getElementById(`loan-note-${loanId}`).value.trim();

  try {
    if (btn.dataset.action === "approve") {
      await api(`/api/banker/loans/${loanId}/approve`, {
        method: "POST",
        body: { note: note || null },
      });
      msg.textContent = "Loan approved — principal deposited and schedule created.";
    } else {
      await api(`/api/banker/loans/${loanId}/reject`, {
        method: "POST",
        body: { note: note || null },
      });
      msg.textContent = "Loan rejected.";
    }
    msg.className = "success";
    await Promise.all([loadAccounts(), loadPendingLoans(), loadHistory()]);
  } catch (err) {
    msg.textContent = err.message || "Could not update loan.";
    msg.className = "error";
  }
});

document.getElementById("money-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  const action = e.submitter?.value || "deposit";
  const msg = document.getElementById("money-message");
  msg.className = "hidden";

  const accountId = Number(document.getElementById("accountId").value);
  const amountCents = dollarsToCents(document.getElementById("amount").value);
  const note = document.getElementById("note").value.trim();

  if (!accountId || !Number.isFinite(amountCents) || amountCents <= 0) {
    msg.textContent = "Choose an account and enter a positive amount.";
    msg.className = "error";
    return;
  }

  const path = action === "withdraw" ? "/api/banker/withdrawals" : "/api/banker/deposits";

  try {
    await api(path, {
      method: "POST",
      body: { accountId, amountCents, note: note || null },
    });
    document.getElementById("amount").value = "";
    document.getElementById("note").value = "";
    msg.textContent = action === "withdraw" ? "Withdrawal recorded." : "Deposit recorded.";
    msg.className = "success";
    await Promise.all([loadAccounts(), loadHistory()]);
  } catch (err) {
    msg.textContent = err.message || "Could not record transaction.";
    msg.className = "error";
  }
});

(async function init() {
  const me = await requireUser("Banker");
  if (!me) return;
  await loadAccounts();
  await Promise.all([loadPendingTasks(), loadPendingLoans()]);
  await loadHistory();
})();
