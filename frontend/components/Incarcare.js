// Ecranul afișat cât timp se încarcă datele unei pagini.
export default function Incarcare({ text = 'Se încarcă...' }) {
  return (
    <main className="tv-pagina tv-centru">
      <p className="tv-asteapta">{text}</p>
    </main>
  )
}
