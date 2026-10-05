export interface PreferenceStorage {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
}

// Only presentation preferences are persisted here. Entity IDs, selections,
// confirmation states and simulation settings must stay in the game systems.
export function readPreference(storage: PreferenceStorage | undefined, key: string, fallback: boolean): boolean {
  try {
    const value = storage?.getItem(key);
    return value === "true" ? true : value === "false" ? false : fallback;
  } catch { return fallback; }
}

export function writePreference(storage: PreferenceStorage | undefined, key: string, value: boolean): void {
  try { storage?.setItem(key, String(value)); } catch { /* UI still works without storage. */ }
}

export function formatSnapshot(name: string, version: string, values: Record<string, unknown>): string {
  const rows = [`${name} — UI version ${version}`, "UI status snapshot; no game logs or automatic error attribution included."];
  for (const key of Object.keys(values).sort()) {
    const value = values[key];
    if (typeof value === "boolean" || (typeof value === "number" && Number.isFinite(value))) {
      rows.push(`${key}: ${value}`);
    }
  }
  rows.push("Describe the problem and attach relevant log excerpts separately.");
  return rows.join("\n");
}
