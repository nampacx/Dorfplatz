const HOME_IMAGES = [
  'https://www.blaubeuren.de/ceasy/resource/10351?&doCrop=1&width=800&height=450',
  'https://www.blaubeuren.de/ceasy/resource/10356?&doCrop=1&width=800&height=450',
  'https://www.blaubeuren.de/ceasy/resource/10357?&doCrop=1&width=800&height=450',
]

export default function HomePage() {
  return (
    <>
      <div className="page-head">
        <h1>Willkommen am Schwarzen Brett</h1>
        <p>
          Hier findest du alle wichtigen Informationen rund um unsere Gemeinschaft: aktuelle
          Neuigkeiten, kommende Veranstaltungen, Dienstleistungen in der Nachbarschaft und Angebote
          im Marktplatz.
        </p>
      </div>

      <section className="card" style={{ marginBottom: '1rem' }}>
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
