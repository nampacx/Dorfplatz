import { legal, pendingLegalFields, TODO } from '../../config/legal'

/**
 * Impressum nach § 5 DDG (bis 14.05.2024: § 5 TMG) und § 18 Abs. 2 MStV.
 *
 * Der Inhalt kommt vollständig aus `src/config/legal.ts` — hier steht nur,
 * welche Angabe unter welcher Überschrift erscheint. Freiwillige Angaben
 * (Register, USt-IdNr., Aufsichtsbehörde) werden weggelassen, solange sie
 * leer sind: ein Impressum mit der Zeile „Registernummer: —" wirft mehr
 * Fragen auf, als es beantwortet.
 *
 * Erreichbar ist die Seite über den Footer, der auf jeder Route steht. § 5
 * DDG verlangt „leicht erkennbar, unmittelbar erreichbar und ständig
 * verfügbar" — ein Link, der nur im angemeldeten Bereich auftaucht, erfüllt
 * das nicht.
 */

/** Ein noch nicht ausgefülltes Pflichtfeld — im Layout nicht zu übersehen. */
function Missing({ label }: { label: string }) {
  return <span className="legal-todo">{label} fehlt</span>
}

function Value({ value, label }: { value: string; label: string }) {
  const pending = value.trim() === '' || value.trim() === TODO
  return pending ? <Missing label={label} /> : <>{value}</>
}

const DISPUTE_TEXT: Record<typeof legal.disputeResolution, string> = {
  none: 'Wir sind nicht bereit und nicht verpflichtet, an Streitbeilegungsverfahren vor einer Verbraucherschlichtungsstelle teilzunehmen.',
  voluntary: 'Wir nehmen freiwillig an Streitbeilegungsverfahren vor einer Verbraucherschlichtungsstelle teil.',
  obliged: 'Wir sind zur Teilnahme an Streitbeilegungsverfahren vor einer Verbraucherschlichtungsstelle verpflichtet.',
}

export default function ImpressumPage() {
  const pending = pendingLegalFields()
  const responsibleAddress = legal.contentResponsible.address ?? legal.address

  return (
    <div className="panel legal-page">
      <h1>Impressum</h1>

      {pending.length > 0 && (
        <div className="notice error" role="alert">
          <strong>Noch nicht vollständig.</strong> Diese Angaben fehlen und müssen vor dem
          Produktivbetrieb in <code>src/config/legal.ts</code> eingetragen werden:{' '}
          {pending.join(', ')}.
        </div>
      )}

      <h3>Angaben gemäß § 5 DDG</h3>
      <p>
        <Value value={legal.name} label="Name des Betreibers" />
        <br />
        <Value value={legal.legalForm} label="Rechtsform" />
        <br />
        <Value value={legal.address.street} label="Straße und Hausnummer" />
        <br />
        <Value value={legal.address.postalCode} label="Postleitzahl" />{' '}
        <Value value={legal.address.city} label="Ort" />
        <br />
        {legal.address.country}
      </p>

      <h3>Vertreten durch</h3>
      <ul>
        {legal.representedBy.map((person, index) => (
          <li key={`${person}-${index}`}>
            <Value value={person} label="Vertretungsberechtigte Person" />
          </li>
        ))}
      </ul>

      <h3>Kontakt</h3>
      <p>
        E-Mail:{' '}
        {legal.email.trim() === '' || legal.email.trim() === TODO ? (
          <Missing label="E-Mail-Adresse" />
        ) : (
          <a href={`mailto:${legal.email}`}>{legal.email}</a>
        )}
        <br />
        Telefon: <Value value={legal.phone} label="Telefon oder zweiter Kontaktweg" />
      </p>

      {legal.registerCourt && legal.registerNumber && (
        <>
          <h3>Registereintrag</h3>
          <p>
            Registergericht: {legal.registerCourt}
            <br />
            Registernummer: {legal.registerNumber}
          </p>
        </>
      )}

      {legal.vatId && (
        <>
          <h3>Umsatzsteuer-Identifikationsnummer</h3>
          <p>Gemäß § 27a Umsatzsteuergesetz: {legal.vatId}</p>
        </>
      )}

      {legal.supervisoryAuthority && (
        <>
          <h3>Zuständige Aufsichtsbehörde</h3>
          <p>{legal.supervisoryAuthority}</p>
        </>
      )}

      <h3>Verantwortlich für den Inhalt nach § 18 Abs. 2 MStV</h3>
      <p>
        <Value value={legal.contentResponsible.name} label="Name der verantwortlichen Person" />
        <br />
        <Value value={responsibleAddress.street} label="Straße und Hausnummer" />
        <br />
        <Value value={responsibleAddress.postalCode} label="Postleitzahl" />{' '}
        <Value value={responsibleAddress.city} label="Ort" />
      </p>

      <h3>Verbraucherstreitbeilegung</h3>
      <p>{DISPUTE_TEXT[legal.disputeResolution]}</p>

      <h3>Haftung für Inhalte der Mitglieder</h3>
      <p>
        Events, Dienstleistungen, Marktplatz-Anzeigen und Nachrichten stammen von den jeweiligen
        Mitgliedern. Für diese fremden Inhalte sind wir nach §§ 7 bis 10 DDG nicht verantwortlich,
        bis wir von einer konkreten Rechtsverletzung Kenntnis erlangen. Sobald uns ein Verstoß
        bekannt wird, entfernen wir den Inhalt umgehend. Meldungen bitte an die oben genannte
        E-Mail-Adresse.
      </p>
    </div>
  )
}
