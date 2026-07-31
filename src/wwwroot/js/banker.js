const { api, formatMoney, dollarsToCents, formatDate, requireUser } = window.SslApi;

let accountsCache = [];

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

function toDateInputValue(iso) {
  if (!iso) return "";
  const d = new Date(iso);
  const y = d.getUTCFullYear();
  const m = String(d.getUTCMonth() + 1).padStart(2, "0");
  const day = String(d.getUTCDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
}

function fillAccountSelects() {
  const options = accountsCache
    .map((a) => `<option value="${a.id}">${escapeHtml(a.displayName)} (${formatMoney(a.balanceCents)})</option>`)
    .join("");
  document.getElementById("accountId").innerHTML = options;
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
      <div class="list-title">${escapeHtml(t.displayName)} — ${escapeHtml(t.description)}${t.source ? ` <span class="muted">via ${escapeHtml(t.source)}</span>` : ""}</div>
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

async function loadActiveLoans() {
  const loans = await api("/api/banker/loans?status=Approved");
  const el = document.getElementById("active-loans");
  if (!loans.length) {
    el.innerHTML = `<p class="muted">No active loans.</p>`;
    return;
  }

  el.innerHTML = loans.map((loan) => `
    <div class="task-card" data-loan-id="${loan.id}">
      <div class="list-title">${escapeHtml(loan.displayName)} — ${escapeHtml(loan.purpose)}</div>
      <div class="task-meta">
        <span>Borrowed ${formatMoney(loan.amountCents)}</span>
        <span>${loan.termWeeks} weeks</span>
        <span>Weekly ${formatMoney(loan.weeklyPaymentCents)}</span>
        <span>${formatDate(loan.reviewedAt || loan.createdAt)}</span>
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
        <label for="active-loan-note-${loan.id}">Note (optional)</label>
        <input id="active-loan-note-${loan.id}" placeholder="Changed their mind…" />
      </div>
      <div class="actions">
        <button class="btn btn-danger btn-sm" type="button" data-action="cancel">Cancel loan</button>
      </div>
    </div>
  `).join("");
}

async function loadInterestPreview() {
  const preview = await api("/api/banker/interest/preview");
  const el = document.getElementById("interest-preview");

  if (!preview.pending) {
    el.innerHTML = `<p class="muted">Interest is caught up. Nothing to pay right now.</p>`;
    return;
  }

  const ratePct = Math.round(Number(preview.monthlyRate) * 100);
  const rows = preview.accounts.map((a) => `
    <li>
      <div class="list-title">
        <span>${escapeHtml(a.displayName)}</span>
        <span class="interest-amount">${formatMoney(a.interestCents)}</span>
      </div>
      <div class="muted">Average daily balance ${formatMoney(a.averageDailyBalanceCents)}</div>
    </li>
  `).join("");

  el.innerHTML = `
    <p>Interest for <strong>${escapeHtml(preview.accrualMonthLabel)}</strong>
      at ${ratePct}% monthly — deposits dated ${escapeHtml(preview.payoutDate)}.</p>
    <ul class="list interest-preview-list">${rows}</ul>
    <div class="list-title" style="margin-top:0.75rem">
      <span>Total</span>
      <span class="interest-amount">${formatMoney(preview.totalInterestCents)}</span>
    </div>
    <div class="actions" style="margin-top:1rem">
      <button class="btn" type="button" id="pay-interest">Pay interest for ${escapeHtml(preview.accrualMonthLabel)}</button>
    </div>
  `;
}

async function loadHistory() {
  const el = document.getElementById("history");
  const txs = await api("/api/banker/transactions");
  if (!txs.length) {
    el.innerHTML = `<p class="muted">No transactions yet.</p>`;
    return;
  }

  el.innerHTML = `
    <table class="ledger-table">
      <thead>
        <tr>
          <th scope="col">Date</th>
          <th scope="col">Account</th>
          <th scope="col">Type</th>
          <th scope="col" class="num">Amount</th>
          <th scope="col">Note</th>
        </tr>
      </thead>
      <tbody>
        ${txs.map((t) => `
          <tr data-tx-id="${t.id}">
            <td>
              <input
                id="tx-date-${t.id}"
                class="ledger-date"
                type="date"
                value="${toDateInputValue(t.createdAt)}"
                data-original="${toDateInputValue(t.createdAt)}"
                aria-label="Ledger date for ${escapeHtml(t.displayName)}"
              />
            </td>
            <td>${escapeHtml(t.displayName)}</td>
            <td><span class="badge badge-${t.type.toLowerCase()}">${escapeHtml(t.type)}</span></td>
            <td class="num">${formatMoney(t.amountCents)}</td>
            <td class="ledger-note">${escapeHtml(t.note)}</td>
          </tr>
        `).join("")}
      </tbody>
    </table>
  `;
}

document.getElementById("logout").addEventListener("click", async () => {
  await api("/api/auth/logout", { method: "POST" });
  window.location.href = "/";
});

document.getElementById("interest-preview").addEventListener("click", async (e) => {
  const btn = e.target.closest("#pay-interest");
  if (!btn) return;

  const msg = document.getElementById("interest-message");
  msg.className = "hidden";

  if (!confirm("Pay interest for the oldest pending month? Deposits will be dated on the 1st.")) return;

  try {
    const result = await api("/api/banker/interest/pay", { method: "POST" });
    const paid = result.paid;
    msg.textContent = `Paid ${formatMoney(paid.totalInterestCents)} interest for ${paid.accrualMonthLabel}.`;
    msg.className = "success";
    await Promise.all([loadAccounts(), loadInterestPreview(), loadHistory()]);
  } catch (err) {
    msg.textContent = err.message || "Could not pay interest.";
    msg.className = "error";
  }
});

document.getElementById("history").addEventListener("change", async (e) => {
  const input = e.target.closest(".ledger-date");
  if (!input) return;

  const row = input.closest("[data-tx-id]");
  const txId = row?.dataset.txId;
  const msg = document.getElementById("interest-message");
  if (!txId || !input.value) return;

  if (input.value === input.dataset.original) return;

  try {
    await api(`/api/banker/transactions/${txId}`, {
      method: "PATCH",
      body: { createdAt: input.value },
    });
    input.dataset.original = input.value;
    await loadInterestPreview();
  } catch (err) {
    input.value = input.dataset.original || "";
    msg.textContent = err.message || "Could not update transaction date.";
    msg.className = "error";
  }
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
    await Promise.all([loadAccounts(), loadPendingTasks(), loadHistory(), loadInterestPreview()]);
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
    await Promise.all([loadAccounts(), loadPendingLoans(), loadActiveLoans(), loadHistory(), loadInterestPreview()]);
  } catch (err) {
    msg.textContent = err.message || "Could not update loan.";
    msg.className = "error";
  }
});

document.getElementById("active-loans").addEventListener("click", async (e) => {
  const btn = e.target.closest("[data-action]");
  if (!btn || btn.dataset.action !== "cancel") return;
  const card = btn.closest(".task-card");
  const loanId = card.dataset.loanId;
  const msg = document.getElementById("active-loan-message");
  msg.className = "hidden";

  if (!confirm("Cancel this loan? Principal will be withdrawn and the repayment schedule stopped.")) return;

  const note = document.getElementById(`active-loan-note-${loanId}`).value.trim();

  try {
    await api(`/api/banker/loans/${loanId}/cancel`, {
      method: "POST",
      body: { note: note || null },
    });
    msg.textContent = "Loan canceled — principal withdrawn and schedule stopped.";
    msg.className = "success";
    await Promise.all([loadAccounts(), loadPendingLoans(), loadActiveLoans(), loadHistory(), loadInterestPreview()]);
  } catch (err) {
    msg.textContent = err.message || "Could not cancel loan.";
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
    await Promise.all([loadAccounts(), loadHistory(), loadInterestPreview()]);
  } catch (err) {
    msg.textContent = err.message || "Could not record transaction.";
    msg.className = "error";
  }
});

(async function init() {
  const me = await requireUser("Banker");
  if (!me) return;
  await loadAccounts();
  await Promise.all([loadPendingTasks(), loadPendingLoans(), loadActiveLoans(), loadInterestPreview()]);
  await loadHistory();
})();
