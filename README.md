# Rundfrage

Umfragen und Terminfindung für Gruppen, die sich nicht auf einer weiteren Seite anmelden
wollen. Teilnehmende antworten über einen geteilten Link — ohne Konto, ohne Login.

> **Stand**: Grundgerüst (Feature `001-platform-scaffold`). Es gibt noch keine Umfragen —
> dieses Feature belegt, dass die Kette Browser → Anwendung → Datenbank trägt, und richtet
> die Automatisierung ein, die sie trägt hält.

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
| Text-Endpunkt | <http://localhost:8080/api/v1/message> |
| Datenbankstatus | <http://localhost:8080/api/v1/status/database> |

Beenden mit `Ctrl-C` oder `docker compose down`. Daten überleben beides.
`docker compose down -v` verwirft zusätzlich das Volume und damit die Datenbank.

## Was die Seite anzeigt

Die Seite zeigt den Text aus dem Backend und einen von drei Zuständen:

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
docker compose up -d --build && cd e2e && npx playwright test
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
