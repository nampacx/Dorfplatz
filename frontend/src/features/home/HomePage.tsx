const HOME_IMAGES = [
  'https://www.blaubeuren.de/ceasy/resource/10351?&doCrop=1&width=800&height=450',
  'https://www.blaubeuren.de/ceasy/resource/10356?&doCrop=1&width=800&height=450',
  'https://www.blaubeuren.de/ceasy/resource/10357?&doCrop=1&width=800&height=450',
]

export default function HomePage() {
  return (
    <>
      <div className="page-head">
        <h1>Willkommen auf dem Dorfplatz in Asch</h1>
        <p>
          Der Dorfplatz ist der digitale Treffpunkt für unser Dorf. Hier teilen wir wichtige
          Informationen, bleiben miteinander in Kontakt und unterstützen uns im Alltag.
        </p>
      </div>

      <section className="card" style={{ marginBottom: '1rem' }}>
        <h2>Über den Dorfplatz</h2>
        <p>
          Die Plattform wurde von der Gruppe <strong>Lebenswert</strong> in Asch initiiert. Sie soll
          einen verlässlichen und vertrauensvollen Raum schaffen, der unser Dorf stärkt, Angebote
          sichtbar macht und die Versorgung und Zusammenarbeit vor Ort verbessert.
        </p>
        <p>
          Hier können sich Nachbarinnen und Nachbarn informieren, austauschen und gegenseitig helfen.
          Bitte begegnet einander respektvoll, freundlich und fair. Ein wertschätzender Umgang ist
          die Grundlage für diese gemeinsame Plattform.
        </p>
        <h2>Was du hier findest</h2>
        <p>
          <strong>Events:</strong> Vereinsfeste, Treffen und Ankündigungen aus dem Ort.
        </p>
        <p>
          <strong>Dienstleistungen:</strong> Hilfe anbieten oder Unterstützung suchen.
        </p>
        <p>
          <strong>Marktplatz:</strong> Dinge aus der Nachbarschaft kaufen, verkaufen oder verschenken.
        </p>
        <p>
          <strong>Nachrichten:</strong> Kontaktdaten und weitere wichtige Einzelheiten zu
          Dienstleistungen oder Marktplatz-Anzeigen direkt mit anderen Mitgliedern austauschen.
          Die Funktion ist für konkrete Absprachen gedacht, nicht als allgemeine Chat-Plattform.
        </p>
      </section>

      <section className="grid" aria-label="Eindrücke aus unserer Gemeinde">
        {HOME_IMAGES.map((url, index) => (
          <article key={url} className="card">
            <img
              src={url}
              alt={`Eindruck aus der Gemeinde ${index + 1}`}
              style={{ width: '100%', display: 'block', borderRadius: '12px', objectFit: 'cover', aspectRatio: '16 / 9' }}
            />
          </article>
        ))}
      </section>
    </>
  )
}
