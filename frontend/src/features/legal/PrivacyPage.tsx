import { Link } from 'react-router-dom'
import { legal, TODO } from '../../config/legal'

/**
 * Datenschutzerklärung nach Art. 13 DSGVO.
 *
 * Der Verantwortliche (Art. 13 Abs. 1 lit. a) kommt aus `src/config/legal.ts`,
 * damit Impressum und Datenschutzerklärung nicht auseinanderlaufen können.
 */

function isPending(value: string): boolean {
  return value.trim() === '' || value.trim() === TODO
}

export default function PrivacyPage() {
  const controllerKnown = !isPending(legal.name) && !isPending(legal.email)
  const stand = legal.privacyPolicyDate
    ? new Date(legal.privacyPolicyDate).toLocaleDateString('de-DE', {
        day: '2-digit',
        month: 'long',
        year: 'numeric',
      })
    : null

  return (
    <div className="panel legal-page">
      <h1>Datenschutzerklärung</h1>
      <p style={{ color: 'var(--ink-soft)' }}>
        Diese Vorlage fasst zusammen, wie der Dorfplatz personenbezogene Daten verarbeitet.
        Bitte an die konkreten Gegebenheiten des Vereins anpassen und rechtlich prüfen lassen.
        {stand && <> Stand: {stand}.</>}
      </p>

      <h3>Verantwortlicher</h3>
      {controllerKnown ? (
        <p>
          {legal.name}
          {legal.legalForm && !isPending(legal.legalForm) && <>, {legal.legalForm}</>}
          <br />
          {legal.address.street}
          <br />
          {legal.address.postalCode} {legal.address.city}
          <br />
          E-Mail: <a href={`mailto:${legal.email}`}>{legal.email}</a>
        </p>
      ) : (
        <p className="notice error" role="alert">
          Der Verantwortliche ist noch nicht hinterlegt. Die Angaben stehen in{' '}
          <code>src/config/legal.ts</code> und erscheinen auch im{' '}
          <Link to="/impressum">Impressum</Link>.
        </p>
      )}
      {legal.dataProtectionOfficer && (
        <p>
          Datenschutzbeauftragte Person: {legal.dataProtectionOfficer.name}
          {legal.dataProtectionOfficer.email && (
            <>
              {' '}
              (<a href={`mailto:${legal.dataProtectionOfficer.email}`}>
                {legal.dataProtectionOfficer.email}
              </a>)
            </>
          )}
        </p>
      )}

      <h3>Welche Daten wir verarbeiten</h3>
      <ul>
        <li>Anmeldedaten deines Microsoft- oder Google-Kontos (Name, E-Mail-Adresse, Nutzer-ID).</li>
        <li>Von dir erstellte Inhalte: Events, Dienstleistungen, Marktplatz-Anzeigen inkl. Bilder.</li>
        <li>Nachrichten, die du anderen Mitgliedern sendest.</li>
      </ul>

      <h3>Rechtsgrundlage &amp; Zweck</h3>
      <p>
        Die Verarbeitung erfolgt zur Bereitstellung des Nachbarschafts-Boards (Art. 6 Abs. 1 lit. b DSGVO)
        sowie auf Grundlage deiner Einwilligung (Art. 6 Abs. 1 lit. a DSGVO).
      </p>

      <h3>Deine Rechte</h3>
      <ul>
        <li><strong>Auskunft &amp; Datenübertragbarkeit:</strong> Export als JSON im Bereich „Mein Konto“.</li>
        <li><strong>Löschung:</strong> „Konto löschen“ anonymisiert dein Profil und entfernt deine Inhalte.</li>
        <li><strong>Widerruf:</strong> Du kannst deine Einwilligung jederzeit widerrufen.</li>
        <li>
          <strong>Beschwerde:</strong> Du kannst dich bei einer Datenschutz-Aufsichtsbehörde
          beschweren.
        </li>
      </ul>

      <h3>Speicherung &amp; Sicherheit</h3>
      <p>
        Daten werden in Azure (EU-Region) gespeichert. Verbindungen sind TLS-verschlüsselt, Secrets liegen
        in Azure Key Vault, der Zugriff erfolgt über Managed Identity. Es werden nur technisch notwendige
        Cookies für die Anmeldung gesetzt.
      </p>

      <p style={{ color: 'var(--ink-soft)' }}>
        Die Betreiberangaben findest du im <Link to="/impressum">Impressum</Link>.
      </p>
    </div>
  )
}
