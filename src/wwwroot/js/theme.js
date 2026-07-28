window.SslTheme = (function () {
  const STORAGE_KEY = "ssl-theme";

  function applyTheme(colors) {
    const root = document.documentElement;
    const map = SslThemes.CSS_VAR_MAP;
    for (const key of SslThemes.COLOR_KEYS) {
      if (colors[key]) {
        root.style.setProperty(map[key], colors[key]);
      }
    }
  }

  function defaultState() {
    return {
      mode: "preset",
      name: SslThemes.DEFAULT_THEME,
      colors: SslThemes.getTheme(SslThemes.DEFAULT_THEME),
    };
  }

  function loadState() {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) return defaultState();
      const parsed = JSON.parse(raw);
      if (parsed.mode === "custom" && parsed.colors) {
        const colors = SslThemes.cloneColors({
          ...SslThemes.getTheme(SslThemes.DEFAULT_THEME),
          ...parsed.colors,
        });
        return { mode: "custom", colors };
      }
      if (parsed.mode === "preset" && parsed.name && SslThemes.themes[parsed.name]) {
        return {
          mode: "preset",
          name: parsed.name,
          colors: SslThemes.getTheme(parsed.name),
        };
      }
    } catch {
      /* ignore corrupt storage */
    }
    return defaultState();
  }

  function saveState(state) {
    const payload =
      state.mode === "custom"
        ? { mode: "custom", colors: SslThemes.cloneColors(state.colors) }
        : { mode: "preset", name: state.name };
    localStorage.setItem(STORAGE_KEY, JSON.stringify(payload));
  }

  function resolveColors(state) {
    if (state.mode === "custom" && state.colors) {
      return SslThemes.cloneColors(state.colors);
    }
    return SslThemes.getTheme(state.name || SslThemes.DEFAULT_THEME);
  }

  function applySaved() {
    const state = loadState();
    applyTheme(resolveColors(state));
    return state;
  }

  function setPreset(name) {
    if (!SslThemes.themes[name]) return null;
    const state = {
      mode: "preset",
      name,
      colors: SslThemes.getTheme(name),
    };
    applyTheme(state.colors);
    saveState(state);
    return state;
  }

  function setCustom(colors) {
    const state = {
      mode: "custom",
      colors: SslThemes.cloneColors(colors),
    };
    applyTheme(state.colors);
    saveState(state);
    return state;
  }

  function getActiveColors() {
    return resolveColors(loadState());
  }

  // Apply as soon as this script runs to limit FOUC.
  applySaved();

  return {
    STORAGE_KEY,
    applyTheme,
    loadState,
    saveState,
    applySaved,
    setPreset,
    setCustom,
    getActiveColors,
    resolveColors,
  };
})();
