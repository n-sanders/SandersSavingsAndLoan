const { api, formatMoney, dollarsToCents, formatDate, requireUser } = window.SslApi;

let previewTimer = null;

async function loadAccount() {
  const account = await api("/api/me/account");
  document.getElementById("greeting").textContent = `Hi ${account.displayName} — borrow carefully.`;
  document.getElementById("balance").textContent = formatMoney(account.balanceCents);
  document.getElementById("balance").classList.toggle("balance-negative", account.balanceCents < 0);
}

function badgeClass(status) {
  return `badge badge-${String(status).toLowerCase()}`;
}

function formatDateOnly(isoDate) {
  if (!isoDate) return "";
  const d = new Date(`${isoDate}T00:00:00`);
  return d.toLocaleDateString(undefined, {
    month: "short",
    day: "numeric",
    year: "numeric",
  });
}

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

async function loadLoans() {
  const loans = await api("/api/me/loans");
  const el = document.getElementById("loans");
  const pendingEl = document.getElementById("pending-withdrawals");

  if (!loans.length) {
    el.innerHTML = `<li class="muted">No loan requests yet.</li>`;
  } else {
    el.innerHTML = loans.map((loan) => {
      const scheduleRows = (loan.installments || []).map((i) => `
        <tr>
          <td>${i.sequence}</td>
          <td>${formatDateOnly(i.dueDate)}</td>
          <td>${formatMoney(i.amountCents)}</td>
          <td><span class="${badgeClass(i.status)}">${escapeHtml(i.status)}</span></td>
        </tr>
      `).join("");

      return `
        <li>
          <div class="list-title">${escapeHtml(loan.purpose)} — ${formatMoney(loan.amountCents)}</div>
          <div class="muted">
            <span class="${badgeClass(loan.status)}">${escapeHtml(loan.status)}</span>
            ${loan.termWeeks} weeks · weekly ${formatMoney(loan.weeklyPaymentCents)}
            · ${formatDate(loan.createdAt)}
          </div>
          <div class="interest-inline">Interest cost: <strong>${formatMoney(loan.totalInterestCents)}</strong> · Total repay ${formatMoney(loan.totalRepayCents)}</div>
          ${loan.bankerNote ? `<div class="muted">Banker note: ${escapeHtml(loan.bankerNote)}</div>` : ""}
          ${loan.installments?.length ? `
            <table class="schedule-table compact">
              <thead>
                <tr><th>#</th><th>Date</th><th>Payment</th><th>Status</th></tr>
              </thead>
              <tbody>${scheduleRows}</tbody>
            </table>
          ` : ""}
        </li>
      `;
    }).join("");
  }

  const upcoming = loans
    .filter((l) => l.status === "Approved")
    .flatMap((l) => (l.installments || [])
      .filter((i) => i.status === "Scheduled")
      .map((i) => ({ ...i, purpose: l.purpose })))
    .sort((a, b) => String(a.dueDate).localeCompare(String(b.dueDate)));

  if (!upcoming.length) {
    pendingEl.innerHTML = `<li class="muted">No upcoming loan withdrawals.</li>`;
    return;
  }

  pendingEl.innerHTML = upcoming.map((i) => `
    <li>
      <div class="list-title">
        <span class="badge badge-pending">Pending withdrawal</span>
        ${formatMoney(i.amountCents)}
      </div>
      <div class="muted">${escapeHtml(i.purpose)} · due ${formatDateOnly(i.dueDate)}</div>
    </li>
  `).join("");
}

async function refreshLoanPreview() {
  const preview = document.getElementById("loan-preview");
  const amountCents = dollarsToCents(document.getElementById("loan-amount").value);
  const termWeeks = Number(document.getElementById("loan-term").value);

  if (!Number.isFinite(amountCents) || amountCents <= 0 || !termWeeks) {
    preview.classList.add("hidden");
    return;
  }

  try {
    const data = await api("/api/me/loans/preview", {
      method: "POST",
      body: { amountCents, termWeeks },
    });
    document.getElementById("preview-weekly").textContent = formatMoney(data.weeklyPaymentCents);
    document.getElementById("preview-total").textContent = formatMoney(data.totalRepayCents);
    document.getElementById("preview-interest").textContent = formatMoney(data.totalInterestCents);
    const tbody = document.querySelector("#preview-schedule tbody");
    tbody.innerHTML = data.schedule.map((row) => `
      <tr>
        <td>${row.sequence}</td>
        <td>${formatDateOnly(row.dueDate)}</td>
        <td>${formatMoney(row.amountCents)}</td>
      </tr>
    `).join("");
    preview.classList.remove("hidden");
  } catch {
    preview.classList.add("hidden");
  }
}

function schedulePreviewRefresh() {
  clearTimeout(previewTimer);
  previewTimer = setTimeout(() => {
    refreshLoanPreview().catch((err) => console.error(err));
  }, 200);
}

document.getElementById("logout").addEventListener("click", async () => {
  await api("/api/auth/logout", { method: "POST" });
  window.location.href = "/";
});

document.getElementById("loan-amount").addEventListener("input", schedulePreviewRefresh);
document.getElementById("loan-term").addEventListener("change", schedulePreviewRefresh);

document.getElementById("loan-form").addEventListener("submit", async (e) => {
  e.preventDefault();
  const msg = document.getElementById("loan-message");
  msg.className = "hidden";

  const purpose = document.getElementById("loan-purpose").value.trim();
  const amountCents = dollarsToCents(document.getElementById("loan-amount").value);
  const termWeeks = Number(document.getElementById("loan-term").value);

  if (!purpose || !Number.isFinite(amountCents) || amountCents <= 0 || !termWeeks) {
    msg.textContent = "Enter an amount, purpose, and term.";
    msg.className = "error";
    return;
  }

  try {
    await api("/api/me/loans", {
      method: "POST",
      body: { purpose, amountCents, termWeeks },
    });
    document.getElementById("loan-form").reset();
    document.getElementById("loan-term").value = "4";
    document.getElementById("loan-preview").classList.add("hidden");
    msg.textContent = "Loan request submitted! The banker will review it soon.";
    msg.className = "success";
    await loadLoans();
  } catch (err) {
    msg.textContent = err.message || "Could not submit loan request.";
    msg.className = "error";
  }
});

(async function init() {
  const me = await requireUser("Kid");
  if (!me) return;
  await Promise.all([loadAccount(), loadLoans()]);
})();
