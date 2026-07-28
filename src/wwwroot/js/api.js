window.SslApi = (function () {
  async function api(path, options = {}) {
    const opts = {
      credentials: "same-origin",
      headers: {
        "Content-Type": "application/json",
        ...(options.headers || {}),
      },
      ...options,
    };

    if (opts.body && typeof opts.body === "object") {
      opts.body = JSON.stringify(opts.body);
    }

    const res = await fetch(path, opts);

    if (res.status === 401) {
      if (!window.location.pathname.endsWith("/") && !window.location.pathname.endsWith("/index.html")) {
        window.location.href = "/";
      }
      const err = new Error("Unauthorized");
      err.status = 401;
      throw err;
    }

    if (res.status === 204) return null;

    const text = await res.text();
    let data = null;
    if (text) {
      try {
        data = JSON.parse(text);
      } catch {
        data = { error: text };
      }
    }

    if (!res.ok) {
      const err = new Error((data && data.error) || res.statusText || "Request failed");
      err.status = res.status;
      err.data = data;
      throw err;
    }

    return data;
  }

  function formatMoney(cents) {
    const value = (Number(cents) || 0) / 100;
    return value.toLocaleString(undefined, {
      style: "currency",
      currency: "USD",
    });
  }

  function dollarsToCents(dollars) {
    const n = Number(dollars);
    if (!Number.isFinite(n)) return NaN;
    return Math.round(n * 100);
  }

  function formatDate(iso) {
    if (!iso) return "";
    const d = new Date(iso);
    return d.toLocaleString(undefined, {
      month: "short",
      day: "numeric",
      year: "numeric",
      hour: "numeric",
      minute: "2-digit",
    });
  }

  async function requireUser(expectedRole) {
    const me = await api("/api/auth/me");
    if (expectedRole && me.role !== expectedRole) {
      window.location.href = me.role === "Banker" ? "/banker.html" : "/kid.html";
      return null;
    }
    return me;
  }

  return { api, formatMoney, dollarsToCents, formatDate, requireUser };
})();
