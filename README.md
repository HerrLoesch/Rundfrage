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

Das Raster zeigt jede Antwort mit Namen und je Tag ein Zeichen. Über den Daten sitzt die
**Zusammenfassung je Tag** — die Zahl der *Ja*, *Vielleicht* und *Nein* —, zugeklappt beim
Ankommen und mit einem Klick oder einem Tastendruck ausklappbar. Sie steht dort, wo man die
Antwort auf „welcher Tag passt?" sucht, und kostet erst Platz, wenn man sie haben will.

Diese drei Zahlen ergeben **nicht** zwangsläufig die Zahl der Antworten: *keine Angabe* wird
nicht mitgezählt. Und sie umfassen **alle** Antworten, nicht die gerade sichtbare Seite von
fünfzig — eine Umfrage mit 1000 Antworten zeigt oben alle 1000, während das Raster die ersten
fünfzig auflistet.

Jeder Zustand trägt ein Zeichen, nicht nur eine Farbe — das Raster bleibt ohne Farbwahrnehmung
lesbar, und eine leere Zelle ist von *Nein* unterscheidbar.

Jede Adresse, die das System anzeigt, ist ein Link: in der Umfrageliste, nach dem Anlegen und
nach dem Absenden einer Antwort. Sie öffnet in einem neuen Tab, damit die Seite, auf der man war,
stehen bleibt. Der Text bleibt trotzdem die reine Adresse — diese Links werden weit häufiger
irgendwo eingefügt als angeklickt.

## Wunschliste

Neben der Terminfindung kann Rundfrage die zweite Frage stellen, die vor einem Treffen ansteht:
**wer bringt was mit?** Eine Wunschliste hat einen Titel, ein Zieldatum, optional eine
Beschreibung — und die Dinge, die gebraucht werden, jeweils mit einer Anzahl. Wer nichts angibt,
wünscht sich das Ding genau einmal.

Das Anlegen erzeugt einen Link. Wer ihn hat, sieht die ganze Liste: jeden Eintrag, wie viele davon
noch frei sind und wer sich schon eingetragen hat. Man trägt seinen Namen ein und ist fertig —
kein Konto, keine Anmeldung, kein Schritt davor. Mehrere Sachen auf einmal gehen in einem Rutsch.

**Die Anzahl ist verbindlich.** Ein Eintrag, der zweimal gewünscht ist, nimmt genau zwei Namen an;
danach steht dort „Vollständig" und es wird nichts mehr angeboten. Auch wenn zwei Leute im selben
Moment auf den letzten Platz klicken, kommt genau einer durch — der andere erfährt, dass der Platz
gerade weg ist.

Nach dem Absenden bekommt man einen **persönlichen Link**. Damit — und nur damit — kann man den
eigenen Eintrag wieder zurückziehen, der Platz ist dann sofort wieder frei. Wer den Link verliert,
wendet sich an die Person, die die Liste angelegt hat; sie kann jeden Eintrag entfernen.

Der Admin kann die Liste jederzeit ändern: Titel, Beschreibung und Zieldatum, Einträge
hinzufügen, umbenennen, in der Anzahl erhöhen oder entfernen. Der Teilnehmer-Link bleibt dabei
derselbe, und schon eingetragene Namen bleiben stehen. Zwei Dinge sind bewusst unbequem: eine
Anzahl lässt sich **nicht** unter die bereits eingetragenen Namen senken — die Meldung sagt, wie
viele es sind —, und das Entfernen eines Eintrags nennt vorher, wie viele Zusagen dabei
verlorengehen.

**Ist das Zieldatum vorbei, schließt die Liste.** Sie bleibt vollständig lesbar und wird als
„Geschlossen" gekennzeichnet, nimmt aber nichts Neues mehr an — auch kein Zurückziehen, weil ein
frei gewordener Platz danach niemandem mehr nützt. Ein späteres Zieldatum öffnet sie wieder; einen
Schalter dafür gibt es nicht, weil „geschlossen" nichts Gespeichertes ist, sondern ein Blick auf
den Kalender.

**Gelöscht wird eine Wunschliste nur, wenn der Admin es sagt.** Anders als eine Terminfindung hat
sie kein Ablaufdatum und wird von keinem Hintergrundlauf angefasst. Die Bestätigung nennt vorher,
wie viele Zusagen mit verschwinden.

Im Adminbereich steht zu jeder Liste, wie weit sie ist: wie viele Zusagen es gibt, wie viele der
gewünschten Plätze belegt sind (in Prozent, abgerundet — 999 von 1000 sind 99 %), und wie viele
Einträge noch **gar niemand** übernommen hat. Das Dashboard zeigt dieselben Zahlen für alle
Wunschlisten auf einen Blick, offene zuerst und das nächste Zieldatum oben. Namen von Teilnehmern
stehen dort nicht.

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

`formatVersion` ist ein Signal: Ein zusätzliches Feld lässt die Zahl stehen, ein entferntes oder
umbenanntes erhöht sie. **Version 1 wird eingelesen** — siehe *Einlesen und Wiederherstellen*.
Eine höhere Zahl wird abgelehnt statt gedeutet: Sie bedeutet, dass ein Feld seine Bedeutung
geändert hat.

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

**Wiederherstellen** geht auf zwei Wegen. Im Adminbereich — Wartungsmodus einschalten, Sicherung
hochladen, Vorschau bestätigen; siehe *Einlesen und Wiederherstellen*. Oder von Hand, wenn das
System nicht mehr erreichbar ist: die Datei als `rundfrage.db` in das gemountete Verzeichnis legen
und starten.

```bash
docker compose down

# Den Namen des Volumes nachschlagen, statt ihn zu raten. Lokal ist er aus dem Projektnamen
# abgeleitet, unter Coolify aus der Resource — und in eine Sicherung, die ins falsche Volume
# gelegt wird, sieht man erst hinein, wenn die Liste leer bleibt.
volume=$(docker volume ls --quiet --filter name=rundfrage-data)
echo "$volume"    # muss GENAU EINEN Namen zeigen

docker run --rm -v "$volume":/data -v "$PWD":/in alpine \
  sh -c 'cp /in/rundfrage-2026-09-03T101500Z.db /data/rundfrage.db && chown -R 1654:1654 /data'
docker compose up -d
```

Zeigt `docker volume ls` mehrere Treffer, ist einer davon der produktive und die anderen sind
Reste früherer Deployments. Welcher gerade in Benutzung ist, sagt
`docker inspect <container> --format '{{json .Mounts}}'` — nicht die Änderungszeit und nicht die
Größe.

Das `chown` ist nicht kosmetisch. Die Anwendung läuft als Nicht-Root-Konto und legt neben der
Datei zwei Begleitdateien an — dafür braucht sie Schreibrecht **am Verzeichnis**, nicht nur an der
Datei. Fehlt es, meldet der Speicher `attempt to write a readonly database`, und die Anwendung
zeigt „Daten nicht erreichbar", obwohl die Sicherung in Ordnung ist.

## Einlesen und Wiederherstellen

Zwei Dinge, die gleich klingen und es nicht sind. Beide liegen im Adminbereich, absichtlich
getrennt und absichtlich unterschiedlich aussehend.

| | **Umfrage aus Datei einlesen** | **Sicherung wiederherstellen** |
|---|---|---|
| Liest | den JSON-Export *einer* Umfrage | die vollständige `.db`-Sicherung |
| Wirkung | legt **eine neue** Umfrage an | ersetzt **alles** |
| Links | neu — alte funktionieren nicht | bleiben, alte funktionieren wieder |
| Rückgängig | nicht nötig, nichts wird angefasst | nur über eine ältere Sicherung |
| Wartungsmodus | nicht nötig | **erforderlich** |

### Eine Umfrage einlesen

Datei wählen, *Einlesen*. Danach steht da, was übernommen wurde — und was nicht, mit Grund.

**Der Teilnehmerlink ist ein neuer.** Der Export enthält bewusst keinerlei Token (siehe
*Exportieren*), also kann er auch keinen wiederherstellen. Wer den alten Link hat, kommt nicht zur
eingelesenen Umfrage; der neue muss erneut verteilt werden. Wer schon geantwortet hatte, kann seine
Antwort nicht mehr ändern — sein persönlicher Link ist mit der alten Umfrage verschwunden.

Nicht alles muss durchkommen, und was fehlt, wird benannt statt verschwiegen:

```text
Übernommen: 3 Tage, 12 Antworten.
Nicht übernommen
 • Eine Antwort nannte einen Tag, den die Umfrage nicht anbietet: 2027-06-11
 • Ein Name war länger als 100 Zeichen.
```

„Nichts übernommen" ist ein gültiges Ergebnis, kein Fehler: Eine Datei, deren Umfrage bereits über
dem Löschdatum liegt, ergibt genau das.

Abgelehnt — und dann entsteht gar nichts — wird eine Datei, die kein Export dieses Systems ist,
eine mit zu hoher `formatVersion`, oder eine, die eine Grenze sprengt (100 Tage, 1000 Antworten,
300 Zeichen Titel).

### Eine Sicherung wiederherstellen

```text
1. Wartungsmodus einschalten        ohne ihn wird abgelehnt, nicht nur gewarnt
2. Sicherungsdatei wählen, Prüfen   liest nur die Datei, ändert nichts
3. Vorschau lesen                   „Verloren gehen 2 Umfragen und 5 Antworten"
4. Bestätigen                       der Knopf nennt den Verlust, nicht bloß „OK"
5. Wartungsmodus ausschalten
```

Danach ist der Stand der Sicherung wieder da — **einschließlich aller Links**. Die Sicherung
enthält die Token, anders als der JSON-Export, und deshalb ist sie der Weg für den Ernstfall.

Der Wartungsmodus ist Pflicht und nicht Empfehlung. Das hat einen gemessenen Grund: Die
Wiederherstellung braucht den Speicher exklusiv, und eine offene Transaktion lässt sie mit
`database is locked` scheitern. Der Wartungsmodus sperrt die Teilnehmenden aus; die stündliche
Aufräumroutine wird zusätzlich angehalten, weil sie keine Anfrage ist und der Wartungsmodus sie
nicht erreicht. Ohne beides schlüge eine Wiederherstellung gelegentlich fehl — abhängig davon, zu
welcher Uhrzeit man sie startet.

Fehlgeschlagene Wiederherstellungen kosten keine Daten: Vor dem Austausch wird eine Sicherheitskopie
des aktuellen Stands gezogen und bei einem Fehler zurückgespielt.

## Wartungsmodus

Im Adminbereich schaltbar. Solange er an ist, sehen Teilnehmende unter jedem Link nur einen
Wartungshinweis — nicht den Titel, nicht die Tage, nicht die Ergebnisse. Antworten werden nicht
angenommen und auch nicht scheinbar angenommen.

Der Adminbereich bleibt vollständig nutzbar, einschließlich des Schalters zum Ausschalten. Ein
Banner steht sichtbar oben, kein Hinweis, der wegblendet — der wahrscheinliche Fehler ist nicht,
den Wartungsmodus zu vergessen einzuschalten, sondern ihn nach getaner Arbeit anzulassen.

Drei Eigenschaften, die man beim Betrieb kennen sollte:

- **Er überlebt einen Neustart.** Der Zustand liegt in einer Datei neben dem Speicher, nicht im
  Speicher selbst. Ein Redeploy öffnet die Teilnehmerseite nicht heimlich wieder.
- **Eine Wiederherstellung schaltet ihn nicht um.** Genau deshalb liegt er *neben* der Datenbank:
  Läge er darin, würde das Einspielen einer älteren Sicherung ihn mitten in der Wartung ausschalten.
- **Der Healthcheck bleibt grün.** Wartung ist ein gewollter Zustand, kein Fehler. Wäre er rot,
  würde Coolify das Deployment mitten in der Wartung zurückrollen.

Der Hinweis sagt für jeden Link dasselbe — auch für einen, hinter dem gar keine Umfrage steht. Sonst
verriete er, welche Links echt sind.

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

Für die lokale Testumgebung und die E2E-Tests wird derzeit folgendes Konto
verwendet:

```text
Benutzer: admin
Passwort: rundfrage-test-2026
```

Dieses Passwort ist ausschließlich für lokale Tests gedacht. In jeder
Produktivumgebung müssen `ADMIN_USER` und `ADMIN_PASSWORD_HASH` mit einem neu
gewählten Passwort gesetzt werden. Das Testpasswort darf dort nicht verwendet
werden.

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

# E2E laufen gegen die laufende Instanz. Die lokale, git-ignorierte .env enthält
# dafür E2E_ADMIN_USER und E2E_ADMIN_PASSWORD.
docker compose up -d --build
set -a && source .env && set +a
cd e2e && npx playwright test
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
| `APP_BIND` | `127.0.0.1` | Host-Adresse, auf der dieser Port veröffentlicht wird. Hinter einem Proxy so lassen. |
| `SUBMISSION_LIMIT_PER_HOUR` | `10` | Antworten pro Stunde und Quelle. In produktionsnahen Umgebungen unverändert lassen — E2E-Läufe heben ihn an, weil sie mehr als zehn von einer Maschine senden. |
| `TRUSTED_PROXY_COUNT` | `0` | Zahl der Reverse Proxies davor. Siehe *Hinter einem Reverse Proxy*. |
| `ADMIN_USER` | — | Betreiberkonto, ohne Standard |
| `ADMIN_PASSWORD_HASH` | — | Hash des Passworts, ohne Standard |

Überschreiben über eine `.env` (git-ignoriert) oder Umgebungsvariablen. `.env.example`
dokumentiert den vollständigen Satz.

## Hinter einem Reverse Proxy

Terminiert ein Proxy davor das TLS — Traefik, nginx, Caddy, Coolify —, dann muss er angesagt
werden:

```bash
TRUSTED_PROXY_COUNT=1
```

Ohne diese Angabe sieht die Anwendung nicht den Browser, sondern den Proxy, und **zwei Dinge sind
falsch, ohne dass eines davon sich meldet**:

- **Das Limit trifft alle gemeinsam.** „Zehn Antworten pro Stunde und Quelle" wird zu zehn pro
  Stunde für die ganze Instanz, weil alle Teilnehmenden von derselben Adresse kommen — der des
  Proxys. Die elfte Person, die auf eine Umfrage antwortet, wird abgewiesen, und der Hinweis, es
  später zu versuchen, stimmt auch später nicht.
- **Das Sitzungs-Cookie bekommt kein `Secure`.** Der Browser spricht HTTPS, die Anwendung sieht
  eine gewöhnliche HTTP-Anfrage und richtet das Flag danach.

Es ist eine **Zahl, kein Schalter**, und das ist keine Kosmetik: Ein Proxy *hängt* die Adresse an,
die er gesehen hat. Bei einem Proxy ist der letzte Eintrag also der, den der Proxy geschrieben
hat, und alles links davon hat der Aufrufende erfunden. Genau so viele Einträge von rechts zu
lesen, wie Proxies davorstehen, *ist* der Schutz. Deshalb ist die Voreinstellung `0` und nicht
etwa „glaub dem Header": Bei `docker compose up` liegt kein Proxy davor, und der Header wäre dann
nur die Behauptung des Aufrufenden über sich selbst.

Beim Start steht in einer Zeile im Log, welcher der beiden Fälle gilt.

## Betrieb unter Coolify

Coolify baut aus diesem Repository (Build Pack *Docker Compose*, Datei `compose.yaml`). Vor dem
ersten Deploy müssen vier Umgebungsvariablen in der Resource stehen:

| Variable | Wert | Ohne sie |
|---|---|---|
| `ADMIN_USER` | frei gewählt | Die Anwendung **startet nicht** und läuft in eine Neustartschleife |
| `ADMIN_PASSWORD_HASH` | siehe *Betreiberkonto* | dito |
| `TRUSTED_PROXY_COUNT` | `1` | Das Antwortlimit trifft alle gemeinsam, das Sitzungs-Cookie bekommt kein `Secure` — siehe *Hinter einem Reverse Proxy* |
| `APP_BIND` | `127.0.0.1` (Standard) | Die Anwendung hängt zusätzlich ungeschützt am öffentlichen Port 8080, neben dem Proxy und außerhalb seines TLS |

Den Hash erzeugt man auf dem Server im Terminal der Resource — das Passwort selbst gehört nirgends
in die Konfiguration:

```bash
dotnet Rundfrage.Api.dll --hash-password
```

### Das Volume ist die einzige Sache, die nicht wiederherstellbar ist

Das Image ist aus dem Repository jederzeit neu baubar. Das Volume nicht. Coolify leitet dessen
Namen aus der Resource ab, und solange die Resource dieselbe bleibt, überlebt es jedes Update.

Verschiebt sich der Name aber je — Resource neu angelegt, Service umbenannt —, dann bekommt die
Anwendung ein **leeres** Volume, legt das Schema an und liefert eine leere Umfrageliste aus.
Das sieht nach einem normalen Start aus. Genau dagegen steht seit diesem Stand eine Warnung im
Log, und sie ist die Zeile, nach der man nach jedem Update sucht:

```text
No storage was present at start, so a new and empty one was created.
```

Beim ersten Start ist sie richtig. Bei jedem späteren heißt sie: **sofort stoppen, bevor jemand
antwortet.** Die alten Daten liegen dann noch im anderen Volume — aber nur so lange, bis jemand
aufräumt. Der Normalfall ist die Zeile darüber:

```text
Existing storage opened.
```

Wer den Namen festnageln will, damit er sich nicht verschieben *kann*, tut das in zwei Schritten
und nie in einem: erst nachsehen, wie das Volume in Benutzung heißt
(`docker volume ls --filter name=rundfrage-data`), dann genau diesen Namen als `name:` in den
`volumes:`-Block von `compose.yaml` eintragen. Andersherum — erst eintragen, dann deployen —
mountet Coolify ein neues, leeres Volume, und das ist der Datenverlust, den das Festnageln
verhindern sollte.

### Ein Update fahren

```text
1. Sicherung ziehen        Adminbereich → „Sicherung herunterladen", NICHT cp
2. Deploy auslösen         Coolify baut, startet neu, Healthcheck muss grün werden
3. Log prüfen              „Existing storage opened."  — nicht die Warnung oben
4. Anmelden                Umfrageliste zeigt, was vorher da war
```

Schritt 1 ist nicht optional, und zwar wegen Schritt 5, den es nicht gibt: **ein Image-Rollback
in Coolify ist nach einer Schema-Migration kein Rollback.** Migrationen laufen beim Start
automatisch und werden nicht zurückgenommen; die alte Anwendungsversion trifft danach auf ein
Schema, das sie nicht kennt. Der Rückweg ist immer *alte Version deployen **und** die Sicherung
von vorher einspielen* — und die gibt es nur, wenn sie vorher gezogen wurde.

Was dabei **nicht** schiefgehen kann: eine fehlgeschlagene Migration. SQLite führt DDL
transaktional aus, und `DatabaseStartup` meldet den Fehler, statt ihn zu werfen — die Anwendung
startet, sagt „Speicher nicht erreichbar" und hat nichts angefasst.

### Neustart, nicht Rolling Update

Für diesen Dienst muss der alte Container **weg sein, bevor der neue startet**. Zwei Prozesse auf
einer SQLite-Datei überstehen Lesen und Schreiben dank WAL und `busy_timeout` — zwei gleichzeitig
laufende Migrationen nicht. Es gibt genau eine Instanz; horizontal skalieren lässt sich das hier
nicht, und das ist eine Eigenschaft der Speicherform, kein Versäumnis.

### Healthcheck

Das Image bringt seinen eigenen mit (`docker/Dockerfile`), Coolify übernimmt ihn. Er fragt
`/api/v1/health` und meldet ausschließlich, **ob dieser Prozess antwortet** — bewusst ohne den
Speicher anzufassen. Sonst würde ein unerreichbarer Speicher einen Container abräumen lassen, der
sich exakt so verhält, wie FR-024 es verlangt.

Damit ist die Neustartschleife aus der Tabelle oben sichtbar: Ohne `ADMIN_USER` wird der Deploy
rot, statt als erfolgreich gemeldet zu werden.

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
