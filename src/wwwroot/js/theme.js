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

  function applyFont(name) {
    const font = SslThemes.getFont(name);
    const root = document.documentElement;
    root.style.setProperty("--font-display", font.display);
    root.style.setProperty("--font-body", font.body);
  }

  function resolveFontName(name) {
    if (name && SslThemes.fonts[name]) return name;
    return SslThemes.DEFAULT_FONT;
  }

  function defaultState() {
    return {
      mode: "preset",
      name: SslThemes.DEFAULT_THEME,
      colors: SslThemes.getTheme(SslThemes.DEFAULT_THEME),
      font: SslThemes.DEFAULT_FONT,
    };
  }

  function loadState() {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) return defaultState();
      const parsed = JSON.parse(raw);
      const font = resolveFontName(parsed.font);
      if (parsed.mode === "custom" && parsed.colors) {
        const colors = SslThemes.cloneColors({
          ...SslThemes.getTheme(SslThemes.DEFAULT_THEME),
          ...parsed.colors,
        });
        return { mode: "custom", colors, font };
      }
      if (parsed.mode === "preset" && parsed.name && SslThemes.themes[parsed.name]) {
        return {
          mode: "preset",
          name: parsed.name,
          colors: SslThemes.getTheme(parsed.name),
          font,
        };
      }
    } catch {
      /* ignore corrupt storage */
    }
    return defaultState();
  }

  function saveState(state) {
    const font = resolveFontName(state.font);
    const payload =
      state.mode === "custom"
        ? { mode: "custom", colors: SslThemes.cloneColors(state.colors), font }
        : { mode: "preset", name: state.name, font };
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
    applyFont(state.font);
    return state;
  }

  function setPreset(name) {
    if (!SslThemes.themes[name]) return null;
    const current = loadState();
    const state = {
      mode: "preset",
      name,
      colors: SslThemes.getTheme(name),
      font: resolveFontName(current.font),
    };
    applyTheme(state.colors);
    saveState(state);
    return state;
  }

  function setCustom(colors) {
    const current = loadState();
    const state = {
      mode: "custom",
      colors: SslThemes.cloneColors(colors),
      font: resolveFontName(current.font),
    };
    applyTheme(state.colors);
    saveState(state);
    return state;
  }

  function setFont(name) {
    if (!SslThemes.fonts[name]) return null;
    const current = loadState();
    const state = {
      ...current,
      colors: resolveColors(current),
      font: name,
    };
    applyFont(name);
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
    applyFont,
    loadState,
    saveState,
    applySaved,
    setPreset,
    setCustom,
    setFont,
    getActiveColors,
    resolveColors,
  };
})();
