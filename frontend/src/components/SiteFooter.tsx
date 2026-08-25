import { NavLink } from 'react-router-dom'
import { legal, TODO } from '../config/legal'

/**
 * Rechts-Footer auf jeder Route.
 *
 * § 5 DDG verlangt, dass das Impressum „leicht erkennbar, unmittelbar
 * erreichbar und ständig verfügbar" ist — nach gefestigter Rechtsprechung
 * also von jeder Seite aus mit höchstens zwei Klicks und unter genau diesem
 * Wort. Ein Link, der nur im Cookie-Banner oder auf der Kontoseite steht,
 * erfüllt das nicht; deshalb steht der Footer im App-Rahmen und nicht in
 * einzelnen Seiten.
 *
 * Er liegt im Flow (nicht fixed): eine zweite fixierte Leiste über der
 * TabBar würde auf 812px Höhe Platz kosten, den der Rest der App gerade
 * mühsam zurückgewonnen hat. Stattdessen trägt der Footer die Freistellung
 * für die TabBar, die vorher am .container hing.
 */
export default function SiteFooter() {
  const operator = legal.name.trim() === TODO || legal.name.trim() === '' ? null : legal.name

  return (
    <footer className="site-footer">
      <nav aria-label="Rechtliche Hinweise">
        <NavLink to="/impressum">Impressum</NavLink>
        <NavLink to="/privacy">Datenschutz</NavLink>
      </nav>
      {operator && <p>{operator}</p>}
    </footer>
  )
}
