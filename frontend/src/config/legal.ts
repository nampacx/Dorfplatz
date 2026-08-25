/**
 * Betreiberangaben — die einzige Quelle für alle rechtlich vorgeschriebenen
 * Angaben der App.
 *
 * Das deutsche Recht verlangt dieselben Fakten an zwei Stellen: im Impressum
 * (§ 5 DDG, seit 14.05.2024 Nachfolger des § 5 TMG) und in der
 * Datenschutzerklärung (Verantwortlicher, Art. 13 Abs. 1 lit. a DSGVO).
 * Deshalb stehen sie hier einmal und werden von beiden Seiten gerendert.
 *
 * VOR DEM PRODUKTIVBETRIEB: jedes TODO durch die echten Daten des
 * betreibenden Vereins ersetzen und das Ergebnis rechtlich prüfen lassen.
 * `docs/IMPRESSUM.md` erklärt Feld für Feld, wozu es dient und wo die Angabe
 * zu finden ist. Solange ein TODO stehen bleibt, blendet /impressum einen
 * sichtbaren Warnhinweis ein — die Seite kann also nicht unbemerkt
 * unvollständig online gehen.
 */

/**
 * Platzhalter für eine Pflichtangabe, die noch niemand eingetragen hat.
 * Freiwillige Angaben bleiben stattdessen leer (`''`) und werden dann
 * einfach nicht gerendert.
 */
export const TODO = 'TODO'

export interface LegalAddress {
  street: string
  postalCode: string
  city: string
  country: string
}

/**
 * Bereitschaft zur Verbraucherstreitbeilegung nach § 36 VSBG.
 *
 * Die EU-Plattform zur Online-Streitbeilegung (OS-Plattform) wurde zum
 * 20.07.2025 abgeschaltet; der früher übliche Link darf nicht mehr im
 * Impressum stehen. Die Pflicht aus § 36 VSBG trifft Unternehmer mit mehr
 * als zehn Beschäftigten — ein kleiner Verein wählt in der Regel 'none'.
 */
export type DisputeResolution = 'none' | 'voluntary' | 'obliged'

export interface LegalData {
  /** Name des Betreibers, exakt wie im Vereins-/Handelsregister eingetragen. */
  name: string
  /** Rechtsform, z. B. 'eingetragener Verein (e. V.)'. Leer lassen bei Privatperson. */
  legalForm: string
  /** Ladungsfähige Anschrift — ein Postfach genügt nicht. */
  address: LegalAddress
  /** Vertretungsberechtigte, beim Verein der Vorstand nach § 26 BGB. */
  representedBy: string[]
  /** § 5 Abs. 1 Nr. 2 DDG: E-Mail ist Pflicht. */
  email: string
  /** Zweiter, unmittelbarer Kontaktweg (Telefon oder Rückrufformular). */
  phone: string
  /** Registergericht, z. B. 'Amtsgericht Musterstadt'. Leer, wenn nicht eingetragen. */
  registerCourt: string
  /** Registernummer, z. B. 'VR 1234'. */
  registerNumber: string
  /** USt-IdNr. nach § 27a UStG. Nur angeben, wenn tatsächlich vorhanden. */
  vatId: string
  /** Zuständige Aufsichtsbehörde, falls die Tätigkeit behördlich zugelassen ist. */
  supervisoryAuthority: string
  /**
   * Verantwortlich für den Inhalt nach § 18 Abs. 2 MStV — eine natürliche,
   * volljährige Person mit Wohnsitz in Deutschland bzw. der EU. Adresse leer
   * lassen, wenn sie mit der Vereinsanschrift übereinstimmt.
   */
  contentResponsible: { name: string; address: LegalAddress | null }
  /** Datenschutzbeauftragte Person — nur nötig nach Art. 37 DSGVO / § 38 BDSG. */
  dataProtectionOfficer: { name: string; email: string } | null
  disputeResolution: DisputeResolution
  /** Stand der Datenschutzerklärung, ISO-Datum, z. B. '2026-01-31'. */
  privacyPolicyDate: string
}

export const legal: LegalData = {
  name: TODO,
  legalForm: TODO,
  address: {
    street: TODO,
    postalCode: TODO,
    city: TODO,
    country: 'Deutschland',
  },
  representedBy: [TODO],
  email: TODO,
  phone: TODO,
  registerCourt: '',
  registerNumber: '',
  vatId: '',
  supervisoryAuthority: '',
  contentResponsible: { name: TODO, address: null },
  dataProtectionOfficer: null,
  disputeResolution: 'none',
  privacyPolicyDate: '',
}

/** Wahr, solange das Feld noch den Platzhalter trägt oder leer ist. */
function isPending(value: string): boolean {
  return value.trim() === '' || value.trim() === TODO
}

/**
 * Namen aller Pflichtangaben, die noch offen sind. Leeres Array = das
 * Impressum ist vollständig ausgefüllt (nicht: rechtlich geprüft).
 */
export function pendingLegalFields(): string[] {
  const required: Array<[string, string]> = [
    ['Name des Betreibers', legal.name],
    ['Rechtsform', legal.legalForm],
    ['Straße und Hausnummer', legal.address.street],
    ['Postleitzahl', legal.address.postalCode],
    ['Ort', legal.address.city],
    ['Vertretungsberechtigte', legal.representedBy.join('')],
    ['E-Mail-Adresse', legal.email],
    ['Telefon oder zweiter Kontaktweg', legal.phone],
    ['Verantwortlich für den Inhalt (§ 18 Abs. 2 MStV)', legal.contentResponsible.name],
  ]
  return required.filter(([, value]) => isPending(value)).map(([label]) => label)
}

/** Einzeilige Anschrift für Fließtext und Footer. */
export function formatAddress(address: LegalAddress): string {
  return [address.street, `${address.postalCode} ${address.city}`.trim(), address.country]
    .filter((part) => !isPending(part))
    .join(', ')
}
