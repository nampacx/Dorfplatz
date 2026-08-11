export default function PrivacyPage() {
  return (
    <div className="panel" style={{ maxWidth: 760, margin: '0 auto' }}>
      <h1 style={{ fontFamily: 'var(--serif)' }}>Datenschutzerklärung</h1>
      <p style={{ color: 'var(--ink-soft)' }}>
        Diese Vorlage fasst zusammen, wie das Schwarze Brett personenbezogene Daten verarbeitet.
        Bitte an die konkreten Gegebenheiten des Vereins anpassen und rechtlich prüfen lassen.
      </p>

      <h3>Welche Daten wir verarbeiten</h3>
      <ul>
        <li>Anmeldedaten deines Microsoft- oder Google-Kontos (Name, E-Mail-Adresse, Nutzer-ID).</li>
        <li>Von dir erstellte Inhalte: Events, Dienstleistungen, Marktplatz-Anzeigen inkl. Bilder.</li>
        <li>Nachrichten, die du anderen Mitgliedern sendest.</li>
      </ul>

      <h3>Rechtsgrundlage & Zweck</h3>
      <p>
        Die Verarbeitung erfolgt zur Bereitstellung des Nachbarschafts-Boards (Art. 6 Abs. 1 lit. b DSGVO)
        sowie auf Grundlage deiner Einwilligung (Art. 6 Abs. 1 lit. a DSGVO).
      </p>

      <h3>Deine Rechte</h3>
      <ul>
        <li><strong>Auskunft & Datenübertragbarkeit:</strong> Export als JSON im Bereich „Mein Konto“.</li>
        <li><strong>Löschung:</strong> „Konto löschen“ anonymisiert dein Profil und entfernt deine Inhalte.</li>
        <li><strong>Widerruf:</strong> Du kannst deine Einwilligung jederzeit widerrufen.</li>
      </ul>

      <h3>Speicherung & Sicherheit</h3>
      <p>
        Daten werden in Azure (EU-Region) gespeichert. Verbindungen sind TLS-verschlüsselt, Secrets liegen
        in Azure Key Vault, der Zugriff erfolgt über Managed Identity. Es werden nur technisch notwendige
        Cookies für die Anmeldung gesetzt.
      </p>
    </div>
  )
}
