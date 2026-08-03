import { useState } from "react";
import { useInaraSettings } from "../useInaraSettings";

export default function SettingsPage() {
  const { settings, save, clear } = useInaraSettings();
  const [apiKey, setApiKey] = useState("");

  const handleSave = (e: React.FormEvent) => {
    e.preventDefault();
    if (!apiKey.trim()) return;
    save(apiKey.trim());
    setApiKey("");
  };

  return (
    <div className="grid">
      <section className="card wide">
        <h2>Inara</h2>
        <p className="empty-hint">
          Pushes your commander&apos;s credits, ranks, Powerplay standing, and lifetime statistics to Inara every 10
          minutes. Get your personal API key from your Inara account settings (Referral &amp; API section).
        </p>

        <div className="card-row">
          <span className="label">Status</span>
          <span className={`value chip ${settings?.hasApiKey ? "legal-clean" : "legal-caution"}`}>
            {settings?.hasApiKey ? "Configured" : "Not configured"}
          </span>
        </div>
        {settings?.lastSyncStatus ? (
          <div className="card-row muted">
            <span className="label">Last sync</span>
            <span className="value">
              {settings.lastSyncStatus}
              {settings.lastSyncAtUtc ? ` · ${new Date(settings.lastSyncAtUtc).toLocaleString()}` : ""}
            </span>
          </div>
        ) : null}

        <form className="wishlist-form" onSubmit={handleSave}>
          <input
            type="password"
            placeholder="Inara API key"
            value={apiKey}
            onChange={(e) => setApiKey(e.target.value)}
            autoComplete="off"
          />
          <button type="submit">Save</button>
        </form>

        {settings?.hasApiKey ? (
          <button className="link-button link-button-danger" onClick={clear}>
            Remove key
          </button>
        ) : null}
      </section>

      <section className="card wide">
        <h2>Attribution</h2>
        <p className="empty-hint">
          Elite Dangerous Dashboard was created using assets and imagery from Elite Dangerous, with the permission of
          Frontier Developments plc, for non-commercial purposes. It is not endorsed by nor reflects the views or
          opinions of Frontier Developments and no employee of Frontier Developments was involved in the making of
          it.
        </p>
        <p className="empty-hint">
          Power and station icons courtesy of{" "}
          <a href="https://edassets.org/" target="_blank" rel="noreferrer">
            edassets.org
          </a>
          , a fan-maintained repository of Elite Dangerous assets.
        </p>
      </section>
    </div>
  );
}
