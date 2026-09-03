# Rundfrage

Umfragen und Terminfindung für Gruppen, die sich nicht auf einer weiteren Seite anmelden
wollen. Teilnehmende antworten über einen geteilten Link — ohne Konto, ohne Login.

> **Stand**: Terminfindung im Doodle-Stil (`002-date-poll`), gespeichert in einer Datei
> (`003-sqlite-and-export`), aufgesetzt auf dem Grundgerüst aus `001-platform-scaffold`.

## Starten

Vorausgesetzt wird ausschließlich eine Container-Laufzeit. Kein .NET-SDK, kein Node,
keine Datenbank auf dem Rechner.

```bash
docker compose up --build
```

Das ist der gesamte Aufbau: **ein Container**. Es gibt keine `.env` zu kopieren und keine
Datenbank anzulegen — Compose bringt funktionierende Standardwerte mit, das Schema entsteht beim
ersten Start in einem leeren Verzeichnis.

| | |
|---|---|
| **Anwendung** | <http://localhost:8080> |
| Adminbereich | <http://localhost:8080/admin> |

Beenden mit `Ctrl-C` oder `docker compose down`. Daten überleben beides.
`docker compose down -v` verwirft zusätzlich das Volume und damit alle Umfragen.

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

## Exportieren

Jede Umfrage lässt sich im Adminbereich als **JSON** herunterladen: Titel, Nachricht, die Tage in
zeitlicher Reihenfolge und jede Antwort mit Namen und Angaben je Tag. Der Dateiname nennt Umfrage
und Zeitpunkt, sodass mehrere Exporte nebeneinander liegen können.

```json
{
  "formatVersion": 1,
  "exportedAt": "2026-09-03T10:15:00Z",
  "poll": { "title": "Grillabend", "message": "Wer kann wann?",
            "days": [{ "date": "2026-09-12" }] },
  "responses": [
    { "displayName": "Anna", "answers": [{ "date": "2026-09-12", "availability": "yes" }] }
  ]
}
```

Zwei Auslassungen sind Absicht:

- **Kein Token, keiner Art.** Der Teilnehmerlink ist das Recht zu antworten, der persönliche Link
  das Recht, eine fremde Antwort zu ändern. Eine Datei, die eines von beiden enthielte, gäbe
  dieses Recht an jeden weiter, der die Datei bekommt.
- **Ein nicht beantworteter Tag fehlt**, statt einen vierten Wert zu tragen. Abwesenheit *ist* der
  Zustand — auch im Speicher. Ein Platzhalter würde behaupten, es sei etwas festgehalten worden.

`formatVersion` ist ein Signal, keine Zusage: Ein zusätzliches Feld lässt die Zahl stehen,
ein entferntes oder umbenanntes erhöht sie. Ein Import existiert nicht.

## Sichern und Wiederherstellen

**Über den Knopf, nicht mit `cp`.** Das ist die eine Betriebsanweisung hier, deren Missachtung
Daten kostet.

Im Adminbereich erzeugt *Sicherung herunterladen* eine in sich stimmige Kopie, **während das
System weiterläuft**, und liefert sie als eine einzelne Datei aus, die nichts weiter braucht.

Warum nicht einfach die Datei aus dem Volume kopieren? Gemessen:

```text
Situation im Moment der Kopie                         Handkopie der Hauptdatei
-----------------------------------------------------------------------------------
System gestoppt                                       vollständig
Eine Verbindung offen, Commits noch im Begleiter      20 von 40 Antworten
Ein Lesevorgang hält eine ältere Sicht fest           40 von 60 Antworten
Frischer Speicher, erste Verbindung noch offen        UNBRAUCHBAR — „no such table"
```

Bei gestopptem System ist `cp` in Ordnung. Bei laufendem — und genau dann greift man dazu, weil
man niemanden unterbrechen will — ist die Kopie stillschweigend unvollständig: mal um ein paar
Antworten, mal um alle einschließlich des Schemas. Der Datei sieht man nicht an, welcher Fall
vorliegt. Sie wiegt ungefähr richtig und versagt an dem Tag, an dem man sie braucht.

**Wiederherstellen** heißt: die Datei als `rundfrage.db` in das gemountete Verzeichnis legen und
starten. Ein Import existiert nicht.

```bash
docker compose down
docker run --rm -v rundfrage_rundfrage-data:/data -v "$PWD":/in alpine \
  sh -c 'cp /in/rundfrage-2026-09-03T101500Z.db /data/rundfrage.db && chown -R 1654:1654 /data'
docker compose up -d
```

Das `chown` ist nicht kosmetisch. Die Anwendung läuft als Nicht-Root-Konto und legt neben der
Datei zwei Begleitdateien an — dafür braucht sie Schreibrecht **am Verzeichnis**, nicht nur an der
Datei. Fehlt es, meldet der Speicher `attempt to write a readonly database`, und die Anwendung
zeigt „Daten nicht erreichbar", obwohl die Sicherung in Ordnung ist.

## Löschen und Aufbewahrung

Der Betreiber kann eine einzelne Antwort oder die ganze Umfrage löschen; vor dem Löschen steht,
wie viele Antworten dabei vernichtet werden. Unabhängig davon verschwindet jede Umfrage 30 Tage
nach ihrem letzten Kandidatentag von selbst. Beides entfernt die Daten wirklich — es versteckt
sie nicht.

Ein abgelaufener Link ist in dem Moment tot, in dem die Frist fällt, nicht erst wenn der
Aufräumlauf ihn erreicht. Unbekannte, kaputte, abgelaufene und gelöschte Links sehen dabei alle
gleich aus: Wer keinen gültigen Link hat, erfährt nicht einmal, ob es ihn je gab.

## Wo die Daten liegen

Eine Datei in einem gemounteten Verzeichnis. Kein Host, kein Port, kein Zugangsdatum — den
Speicher erreicht, wer den Pfad hat.

**Und das ist genau die Offenlegung, über die man Bescheid wissen sollte:** Wer diese Datei lesen
kann, kann jede Umfrage und jede Antwort darin lesen, ohne Passwort. Die Anwendung legt sie für
ihr eigenes Konto an (`0600`, Verzeichnis `0700`) — mehr kann sie nicht tun.

Sie ist **bewusst nicht verschlüsselt**: Ein Schlüssel, der in derselben Konfiguration danebenläge,
hielte niemanden auf, der an die Daten kommt, und schüfe einen zusätzlichen Weg, alles zu
verlieren — nämlich den Schlüssel. Das Verzeichnis zu schützen ist Sache des Hosts.

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

## Wenn der Speicher nicht erreichbar ist

Die Anwendung startet und antwortet weiter. Der Adminbereich sagt dann ausdrücklich, dass die
Daten gerade nicht gelesen werden können — und zeigt **nicht** eine leere Liste. Beide sehen
gleich aus und bedeuten das Gegenteil voneinander: „noch keine angelegt" gegen „gerade nicht
erreichbar".

Eine Antwort wird in diesem Zustand nie bestätigt. Oben zu bleiben ist nur dann eine Verbesserung,
wenn das System aufhört, so zu tun als ob.

Neuladen genügt, sobald der Speicher wieder da ist — ein Neustart ist nie nötig.

## Entwickeln

Ein Container plus der Frontend-Dev-Server daneben, der `/api` an das Backend weiterleitet,
sodass der Browser auch hier eine einzige Origin sieht:

```bash
docker compose up -d            # die Anwendung
cd frontend && npm run dev      # Vite auf :5173, Proxy → :8080
```

## Tests

Entwickelt wird testgetrieben — der Test steht vor der Implementierung.

```bash
dotnet test backend/Rundfrage.slnx    # xUnit: Unit + Integration, ohne Docker
cd frontend && npm run test:unit      # Vitest: Unit + Komponenten

# E2E laufen gegen die laufende Instanz und brauchen deren Zugangsdaten.
# Bewusst ohne Rückfallwert: ein Passwort im Repository wäre eines, das jemand deployen kann.
docker compose up -d --build
cd e2e && E2E_ADMIN_USER=... E2E_ADMIN_PASSWORD=... npx playwright test
```

Die Integrationstests geben jeder Testklasse eine eigene temporäre Speicherdatei. Sie brauchen
keinen Docker-Daemon mehr — das war vorher der Preis für eine Datenbank in einem Container.

Für `dotnet test` und `npm test` werden SDK und Node lokal gebraucht — die Zusage „nur eine
Container-Laufzeit" gilt fürs *Starten*, nicht fürs Entwickeln.

Tests prüfen gegen `data-testid` und Übersetzungsschlüssel, nie gegen deutsche Literale.
Eine geänderte Übersetzung kann daher keinen Test brechen.

## Konfiguration

| Variable | Standard | Zweck |
|---|---|---|
| `DATA_DIR` | `/data` | Verzeichnis der Speicherdatei im Container |
| `LOG_LEVEL` | `Information` | Serilog-Mindeststufe, ohne Neubau änderbar |
| `APP_PORT` | `8080` | Host-Port der Anwendung |
| `SUBMISSION_LIMIT_PER_HOUR` | `10` | Antworten pro Stunde und Quelle. In produktionsnahen Umgebungen unverändert lassen — E2E-Läufe heben ihn an, weil sie mehr als zehn von einer Maschine senden. |
| `ADMIN_USER` | — | Betreiberkonto, ohne Standard |
| `ADMIN_PASSWORD_HASH` | — | Hash des Passworts, ohne Standard |

Überschreiben über eine `.env` (git-ignoriert) oder Umgebungsvariablen. `.env.example`
dokumentiert den vollständigen Satz.

## Logs

Serilog schreibt strukturiert nach stdout — mehr als die Container-Laufzeit braucht es nicht:

```bash
docker compose logs -f app
LOG_LEVEL=Debug docker compose up      # mehr Details, kein Neubau
```

Zugangsdaten und Speicherpfade erscheinen nie im Log, auch nicht in Ausnahmemeldungen — ein Test
erzwingt das, und ein weiterer sorgt dafür, dass nicht die rohe Ausnahme danebengehängt wird,
deren `ToString()` alles wieder mitbrächte.

## Branches und CI

Entwickelt wird auf `dev`; `main` steht für den freigegebenen Stand. Pushes auf beide Branches
und Pull Requests dorthin bauen das System und führen alle drei Testsuiten aus. Die Pipeline
baut und testet ausschließlich — sie veröffentlicht keine Images und deployt nicht.

> Spec-Kit-Befehle brauchen in diesem Repository `export SPECIFY_FEATURE=003-sqlite-and-export`,
> weil die Branch-Prüfung sonst auf `dev` abbricht.

## Aufbau

```text
backend/     ASP.NET Core: /api/v1 und die gebauten Web-Assets aus einer Origin
frontend/    Vue 3 + Vuetify + Pinia + vue-i18n, gebaut nach wwwroot/
  assets/    Wort- und Bildmarke als SVG — vom Build gehasht und aus der eigenen Origin
             ausgeliefert, wie jede andere Datei auch (Prinzip IV: kein CDN, keine Fremdorigin)
e2e/         Playwright gegen den echten Container
docker/      Mehrstufiger Build: Node baut das Frontend, .NET veröffentlicht
specs/       Spezifikation, Plan, Entwurfsentscheidungen
```

Warum eine Datei statt einer Datenbank, warum die Sicherung ein Endpunkt und kein `cp` ist, und
warum jeder gespeicherte Zeitpunkt UTC ist: `specs/003-sqlite-and-export/research.md`.
