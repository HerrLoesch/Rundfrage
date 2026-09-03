# Rundfrage

Umfragen und Terminfindung für Gruppen, die sich nicht auf einer weiteren Seite anmelden
wollen. Teilnehmende antworten über einen geteilten Link — ohne Konto, ohne Login.

> **Stand**: Terminfindung im Doodle-Stil (Feature `002-date-poll`), aufgesetzt auf dem
> Grundgerüst aus `001-platform-scaffold`.

## Starten

Vorausgesetzt wird ausschließlich eine Container-Laufzeit. Kein .NET-SDK, kein Node,
kein PostgreSQL auf dem Rechner.

```bash
docker compose up --build
```

Das ist der gesamte Aufbau. Es gibt keine `.env` zu kopieren und keine Datenbank anzulegen:
Compose bringt funktionierende Standardwerte mit, das Schema entsteht beim ersten Start.

| | |
|---|---|
| **Anwendung** | <http://localhost:8080> |
| Adminbereich | <http://localhost:8080/admin> |
| Systemdiagnose | <http://localhost:8080/status> |
| Text-Endpunkt | <http://localhost:8080/api/v1/message> |
| Datenbankstatus | <http://localhost:8080/api/v1/status/database> |

Die Startadresse führt in die Anwendung. Die Diagnoseseite mit Backend-Text und Datenbankstatus
aus dem Grundgerüst liegt unter `/status` — sie ist weiterhin da, nur nicht mehr die Eingangstür.

Beenden mit `Ctrl-C` oder `docker compose down`. Daten überleben beides.
`docker compose down -v` verwirft zusätzlich das Volume und damit die Datenbank.

## Eine Terminfindung anlegen

1. <http://localhost:8080> öffnen und anmelden (siehe *Betreiberkonto* weiter unten).
2. Titel, optional eine kurze Nachricht, und die Tage zur Auswahl eintragen.
3. Speichern. Der Teilnehmerlink erscheint — kopieren und teilen.

Die Liste zeigt jede Umfrage mit ihrem Link, der Zahl der Antworten und dem **Löschdatum**:
letzter Kandidatentag plus 30 Tage. Nichts verschwindet, ohne vorher gesagt zu haben, wann.

## Antworten — ohne Konto

Wer den Link öffnet, sieht Titel, Nachricht, Tage, das Antwortformular und den bisherigen
Stand — alles beim ersten Laden. **Kein Konto, keine Anmeldung, keine E-Mail-Adresse.**

Je Tag stehen drei Möglichkeiten zur Wahl: *Ja*, *Vielleicht*, *Nein*. Einen Tag offen zu
lassen ist eine gültige Antwort und bedeutet *keine Angabe* — gespeichert wird dafür nichts.

Vor dem Namensfeld steht ausdrücklich, dass Name und Antworten für alle sichtbar sind, die den
Link haben. Nach dem Absenden erscheint ein **persönlicher Link**: der einzige Weg zurück zur
eigenen Antwort, denn es gibt kein Konto, über das man sie wiederfinden könnte.

Dass jemand zweimal antwortet, lässt sich nicht verhindern — jeder ehrliche Mechanismus dagegen
würde verlangen, Teilnehmende zu identifizieren, und genau das ist ausgeschlossen.

## Ergebnisse

Das Raster zeigt jede Antwort mit Namen und je Tag die Zahl der *Ja*, *Vielleicht* und *Nein*.
Diese drei Zahlen ergeben **nicht** zwangsläufig die Zahl der Antworten: *keine Angabe* wird
nicht mitgezählt. Wie viele überhaupt geantwortet haben, verrät die Zeilenzahl.

Jeder Zustand trägt ein Zeichen, nicht nur eine Farbe — das Raster bleibt ohne Farbwahrnehmung
lesbar, und eine leere Zelle ist von *Nein* unterscheidbar.

## Löschen und Aufbewahrung

Der Betreiber kann eine einzelne Antwort oder die ganze Umfrage löschen; vor dem Löschen steht,
wie viele Antworten dabei vernichtet werden. Unabhängig davon verschwindet jede Umfrage 30 Tage
nach ihrem letzten Kandidatentag von selbst. Beides entfernt die Daten wirklich — es versteckt
sie nicht.

Ein abgelaufener Link ist in dem Moment tot, in dem die Frist fällt, nicht erst wenn der
Aufräumlauf ihn erreicht. Unbekannte, kaputte, abgelaufene und gelöschte Links sehen dabei alle
gleich aus: Wer keinen gültigen Link hat, erfährt nicht einmal, ob es ihn je gab.

## Betreiberkonto

Es gibt genau ein Konto, gesetzt über Umgebungsvariablen. Keine Registrierung, keine
Nutzerverwaltung, keine Passwort-Ändern-Maske. Das Passwort selbst steht **nie** in der
Konfiguration — nur sein Hash:

```bash
docker compose run --rm app dotnet Rundfrage.Api.dll --hash-password
# fragt auf stderr, gibt den Hash auf stdout aus
```

```bash
# .env (git-ignoriert)
ADMIN_USER=...
ADMIN_PASSWORD_HASH=pbkdf2-sha256:600000:...:...
```

Ohne beide Variablen startet die Anwendung nicht. Ein Adminbereich mit erratbarem
Standardpasswort wäre schlimmer als gar kein Schutz, weil er nach Schutz aussieht.

Nach fünf Fehlversuchen ist das Konto 15 Minuten gesperrt — auch für das richtige Passwort,
sonst wäre die Sperre selbst eine Auskunft. Einen Rücksetzweg gibt es bewusst nicht: Bei einem
einzigen Konto könnte ihn niemand autorisieren, und er wäre der schwächste Weg hinein.

## Was die Diagnoseseite anzeigt

Unter `/status` liegt die Durchstich-Seite aus dem Grundgerüst. Sie zeigt den Text aus dem
Backend und einen von drei Zuständen:

| Anzeige | Bedeutung | So reproduzierbar |
|---|---|---|
| **Datenbank erreichbar** | Backend hat geantwortet, die Abfrage lief durch. | Normalbetrieb |
| **Datenbank nicht erreichbar** | Backend antwortet, aber die Datenbankabfrage schlug fehl oder überschritt ihr 2-Sekunden-Budget. | `docker compose stop db`, dann neu laden |
| **Backend nicht erreichbar** | Die Seite hat das Backend überhaupt nicht erreicht. | Netzwerkfehler beim Statusaufruf |

Nach einem Ausfall genügt Neuladen — ein Neustart ist nie nötig.

> Warum der Statusendpunkt auch bei toter Datenbank **200** liefert: Das Frontend leitet
> „Backend nicht erreichbar" aus jeder Nicht-2xx-Antwort ab. Ein 503 für einen
> Datenbankausfall würde also als Backend-Ausfall angezeigt und die Unterscheidung
> zerstören. Der Zustand der Datenbank gehört in den Rumpf, nicht in den Statuscode.

## Entwickeln

Der Container-Satz bleibt bei zwei Diensten. Der Frontend-Dev-Server läuft daneben und
leitet `/api` an das Backend weiter, sodass der Browser auch hier eine einzige Origin sieht:

```bash
docker compose up -d          # Backend + Datenbank
cd frontend && npm run dev    # Vite auf :5173, Proxy → :8080
```

## Tests

Entwickelt wird testgetrieben — der Test steht vor der Implementierung.

```bash
dotnet test backend/Rundfrage.slnx    # xUnit: Unit + Integration (braucht Docker)
cd frontend && npm run test:unit      # Vitest: Unit + Komponenten

# E2E laufen gegen die laufende Instanz und brauchen deren Zugangsdaten.
# Bewusst ohne Rückfallwert: ein Passwort im Repository wäre eines, das jemand deployen kann.
docker compose up -d --build
cd e2e && E2E_ADMIN_USER=... E2E_ADMIN_PASSWORD=... npx playwright test
```

Für `dotnet test` und `npm test` werden SDK und Node lokal gebraucht — die Zusage „nur eine
Container-Laufzeit" gilt fürs *Starten*, nicht fürs Entwickeln.

Tests prüfen gegen `data-testid` und Übersetzungsschlüssel, nie gegen deutsche Literale.
Eine geänderte Übersetzung kann daher keinen Test brechen.

## Konfiguration

| Variable | Standard | Zweck |
|---|---|---|
| `POSTGRES_USER` | `rundfrage` | Datenbankrolle |
| `POSTGRES_PASSWORD` | `rundfrage_dev` | Nur für lokale Arbeit. Nirgends sonst verwenden. |
| `POSTGRES_DB` | `rundfrage` | Datenbankname |
| `LOG_LEVEL` | `Information` | Serilog-Mindeststufe, ohne Neubau änderbar |
| `APP_PORT` | `8080` | Host-Port der Anwendung |

Überschreiben über eine `.env` (git-ignoriert) oder Umgebungsvariablen. `.env.example`
dokumentiert den vollständigen Satz.

## Logs

Serilog schreibt strukturiert nach stdout — mehr als die Container-Laufzeit braucht es nicht:

```bash
docker compose logs -f app
LOG_LEVEL=Debug docker compose up      # mehr Details, kein Neubau
```

Jede Datenbankprüfung erzeugt genau einen Eintrag mit Ergebnis und Dauer. Zugangsdaten und
Verbindungszeichenfolgen erscheinen nie im Log, auch nicht in Ausnahmemeldungen — ein Test
erzwingt das.

## Branches und CI

Entwickelt wird auf `dev`; `main` steht für den freigegebenen Stand. Pushes auf beide Branches
und Pull Requests dorthin bauen das System und führen alle drei Testsuiten aus. Die Pipeline
baut und testet ausschließlich — sie veröffentlicht keine Images und deployt nicht.

> Spec-Kit-Befehle brauchen in diesem Repository `export SPECIFY_FEATURE=001-platform-scaffold`,
> weil die Branch-Prüfung sonst auf `dev` abbricht.

## Aufbau

```text
backend/     ASP.NET Core: /api/v1 und die gebauten Web-Assets aus einer Origin
frontend/    Vue 3 + Vuetify + Pinia + vue-i18n, gebaut nach wwwroot/
e2e/         Playwright gegen den echten Container-Satz
docker/      Mehrstufiger Build: Node baut das Frontend, .NET veröffentlicht
specs/       Spezifikation, Plan, Entwurfsentscheidungen
```

Warum genau zwei Container und eine Origin, warum die Migration beim Start nicht abbricht,
warum kein globales EF-Core-Retry: `specs/001-platform-scaffold/research.md`.
