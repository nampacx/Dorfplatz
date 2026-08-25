const HOME_IMAGES = [
  'https://github.com/user-attachments/assets/2fb0f6e0-ab02-4975-9a8f-b30da48922d0',
  'https://github.com/user-attachments/assets/d96699e3-9a03-4d46-9e40-daa71b477e4b',
  'https://github.com/user-attachments/assets/14866961-a512-43e2-a344-99cc8ea7eea3',
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
