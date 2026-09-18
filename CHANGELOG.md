# Änderungen

## 18. September 2026

### Neu

- **Event-Zeiträume:** Events können mit optionalem Enddatum beziehungsweise einer Endzeit angelegt werden. Kalenderansicht, Detailansicht und ICS-Download berücksichtigen den gesamten Zeitraum.
- **PDF-Export für Events:** Moderatoren können veröffentlichte Events aus der Moderation für einen gewählten Zeitraum als PDF exportieren.

### Verbesserungen

- **Moderationsdetails:** Angaben zur Einreichung von Events sind für Moderatoren sichtbar, für normale Mitglieder dagegen ausgeblendet.
- **Erscheinungsbild:** Die Oberfläche nutzt jetzt durchgängig die grün-petrolfarbene Gestaltung von Dorfplatz.
- **Buttons:** Gemeinsame Button-Stile wurden vereinheitlicht, damit Aktionen auf der gesamten Seite konsistenter wirken.
- **PDF-Layout:** Exportierte Event-PDFs sind klarer strukturiert und besser lesbar.

### Behoben

- **Kalenderdateien:** ICS-Downloads übernehmen Uhrzeiten zeitgesteuerter Events wieder korrekt.
- **Event-Archiv:** Vergangene Events werden erst nach ihrem tatsächlichen Ende ausgeblendet.

### Dokumentation

- **Projektprofil:** Das README grenzt Dorfplatz klarer von Messenger-, Kleinanzeigen- und Vereinsverwaltungs-Apps ab.
- **Präsentation:** Eine deutsche Präsentation und eine PDF-Fassung für die Vorstellung des Projekts vor Ort wurden ergänzt.

### Technisches

- **CI/CD:** GitHub Actions sind nach Frontend, API und Infrastruktur getrennt.
- **Beitragsrichtlinien:** Projektanweisungen für Changelog-Pflege, Frontend-Konsistenz, Backend-Tests und skalierbare Listen wurden ergänzt.

## 3. September 2026

### Neu

- **Dorfplatz:** Die Plattform heißt jetzt Dorfplatz und begrüßt die Nachbarschaft mit einer Einführung in Asch und die verfügbaren Bereiche.
- **Kalenderansicht:** Events können neben der Kartenansicht in einer Monatsansicht durchsucht werden. Die gewählte Ansicht bleibt beim nächsten Besuch erhalten.
- **Kalender-Download:** Event-Details können als ICS-Datei in den persönlichen Kalender übernommen werden.
- **Rechtliche Hinweise:** Impressum und Datenschutzerklärung sind direkt über den Footer jeder Seite erreichbar.
- **Mitgliederverwaltung:** Administratoren können Zugänge sperren oder wiederherstellen. Eine Begründung wird dabei erfasst; Sperrgründe werden dem betroffenen Mitglied angezeigt.

### Verbesserungen

- Vergangene Events werden standardmäßig ausgeblendet. Moderatoren können sie bei Bedarf wieder einblenden.
- Namen von Erstellern werden auf Event-, Dienstleistungs- und Marktplatzkarten für normale Mitglieder ausgeblendet.
- Die Nachrichtenfunktion wird klarer als Werkzeug für konkrete Absprachen zu Angeboten und Gesuchen beschrieben.
- Der Login-Button ist kontrastreicher und dadurch besser lesbar.
