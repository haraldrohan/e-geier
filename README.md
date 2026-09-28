# E-Geier

**Wo tanke ich in der Nähe von Mödling am günstigsten Diesel?** – E-Geier lässt deinen KI-Assistenten diese Frage beantworten.

E-Geier ist eine .NET-Bibliothek und ein lokaler [MCP](https://modelcontextprotocol.io/)-Server für die öffentlichen APIs der E-Control Austria. Den Anfang macht der **Spritpreisrechner**; das Ladestellenverzeichnis für E-Autos soll folgen.

> **Warum „E-Geier“?** Der Geier kreist geduldig über der Landschaft und stürzt sich dann zielsicher auf das günstigste Angebot. Das „E“ steht für Energie – Sprit heute, Strom morgen. Mit Bindestrich, bitte: Wir sind kein „Egeier“. 🦅⛽

> [!IMPORTANT]
> **Nicht mit der E-Control Austria verbunden.** Alle Daten stammen aus der öffentlichen Spritpreisrechner-API. Keine Gewähr für Richtigkeit oder Aktualität.
>
> **Not affiliated with E-Control Austria.** All data comes from the public Spritpreisrechner (fuel price calculator) API. No guarantee of accuracy or timeliness.

| Paket | Inhalt |
|---|---|
| `EGeier.Net` | Bibliothek: Client für die Spritpreisrechner-API, Geocoding über OpenStreetMap Nominatim |
| `EGeier.Mcp` | MCP-Server (stdio), der lokal bei dir läuft |

E-Geier ist ein reines Open-Source-Hobbyprojekt: kein gehosteter Dienst, keine Zugangsdaten, keine API-Schlüssel. Alles läuft auf deinem Rechner.

## MCP-Server

### Werkzeuge

Alle Werkzeuge lesen nur.

| Werkzeug | Was es tut |
|---|---|
| `find_cheapest_stations(location, fuel_type, include_closed = false)` | Ort oder Adresse → günstigste Tankstellen in der Nähe, mit Preis, Adresse, Luftlinie, geöffnet ja/nein und Navigationslink |
| `find_stations_by_region(region, fuel_type, include_closed = false)` | Günstigste Tankstellen in einem Bundesland oder Bezirk, z. B. „Steiermark“, „Bezirk Mödling“, „Favoriten“. Gemeinden und Postleitzahlen werden dem Bezirk zugeordnet. |
| `list_regions()` | Alle Bundesländer mit ihren Bezirken |

Kraftstoffe: `diesel`, `super` (Super 95) und `cng` (Erdgas). Deutsche und englische Bezeichnungen wie „Benzin“ oder „petrol“ funktionieren auch.

### Voraussetzungen

[.NET 10 SDK oder Runtime](https://dotnet.microsoft.com/download/dotnet/10.0).

### Einrichtung in Claude Desktop

Öffne *Einstellungen → Entwickler → Konfiguration bearbeiten* und ergänze `claude_desktop_config.json`:

**Über NuGet** (sobald veröffentlicht; `dnx` ist ab dem .NET 10 SDK dabei):

```json
{
  "mcpServers": {
    "e-geier": {
      "command": "dnx",
      "args": ["EGeier.Mcp", "--yes"]
    }
  }
}
```

**Alternativ als globales .NET-Tool:**

```sh
dotnet tool install --global EGeier.Mcp
```

```json
{
  "mcpServers": {
    "e-geier": {
      "command": "e-geier-mcp"
    }
  }
}
```

**Aus dem Quellcode:**

```sh
git clone https://github.com/haraldrohan/e-geier.git
cd e-geier
dotnet build -c Release
```

```json
{
  "mcpServers": {
    "e-geier": {
      "command": "dotnet",
      "args": ["run", "--project", "C:\\Pfad\\zu\\e-geier\\src\\EGeier.Mcp", "-c", "Release", "--no-build"]
    }
  }
}
```

Danach Claude Desktop neu starten. Andere MCP-fähige Clients (VS Code, Claude Code, …) werden genauso mit einem stdio-Befehl eingerichtet, z. B. in Claude Code:

```sh
claude mcp add e-geier -- dnx EGeier.Mcp --yes
```

### Konfiguration (optional)

Es ist nichts zu konfigurieren. Wer die Geocoding-Anfragen mit einer Kontaktadresse versehen will (von OpenStreetMap bei häufiger Nutzung empfohlen), setzt eine Umgebungsvariable:

```json
"env": { "Nominatim__Email": "du@example.org" }
```

Mit `Nominatim__BaseAddress` lässt sich eine eigene Nominatim-Instanz verwenden.

### Beispiel-Fragen

- „Wo tanke ich in der Nähe von Mödling am günstigsten Diesel?“
- „Was kostet Super in Graz gerade am wenigsten?“
- „Ich bin in der Mariahilfer Straße in Wien – welche Tankstelle hat jetzt noch offen und ist billig?“
- „Gibt es in Niederösterreich eine günstige CNG-Tankstelle?“
- „Vergleich die Dieselpreise in den Bezirken Mödling und Baden.“
- „Welche Bezirke gibt es in Tirol?“

## Bibliothek

```sh
dotnet add package EGeier.Net
```

```csharp
using EGeier;
using EGeier.Geocoding;
using EGeier.Sprit;

services.AddSpritClient();
services.AddNominatimGeocoder();

// ...

var place = await geocoder.GeocodeAsync("Mödling");
var stations = await sprit.SearchByLocationAsync(place!.Latitude, place.Longitude, FuelType.Diesel);

foreach (var station in stations.Where(s => s.Prices.Count > 0))
{
    Console.WriteLine($"{station.GetPrice(FuelType.Diesel):0.000} €  {station.Name}, {station.Location?.City} ({station.DistanceKm:0.0} km)");
}
```

Ohne Dependency Injection geht es auch: `new SpritClient(new HttpClient())`.

Überblick:

- `ISpritClient` – `SearchByLocationAsync`, `SearchByRegionAsync`, `GetRegionsAsync`, `GetAdministrativeUnitsAsync`, `PingAsync`, `GetMonitoringAsync`
- `FuelType` – `Diesel`, `Super95`, `Cng` (keine Magic Strings)
- `RegionResolver` – findet zu „Bezirk Mödling“, „Graz“, „Perchtoldsdorf“ oder „2340“ den passenden Regionscode
- `IGeocoder` / `NominatimGeocoder` – austauschbar; die Nominatim-Implementierung ist auf Österreich beschränkt, hält max. 1 Anfrage pro Sekunde ein, sendet einen eigenen User-Agent und cacht Ergebnisse 24 Stunden
- `SpritApiException` – verständliche Fehlermeldungen, inkl. `DuringNoonPriceUpdate`

### Gut zu wissen über die API

- Die Suche nach Koordinaten liefert bis zu 10 Tankstellen, **Preise aber nur für die günstigsten** (gesetzliche Vorgabe). Die Suche nach Region liefert die 5 günstigsten.
- Preiserhöhungen sind in Österreich nur um 12:00 Uhr erlaubt. Rund um diese Zeit ist die API oft ein paar Minuten nicht erreichbar; E-Geier meldet das mit einem entsprechenden Hinweis.
- `DistanceKm` ist Luftlinie in Kilometern und bei der Regionssuche immer 0.

## Entwicklung

```sh
dotnet build
dotnet test --project tests/EGeier.Tests                               # offline, mit aufgezeichneten API-Antworten
EGEIER_LIVE_TESTS=1 dotnet test --project tests/EGeier.IntegrationTests  # gegen die echte API
```

Ohne `EGEIER_LIVE_TESTS=1` werden die Live-Tests übersprungen.

## Datenquellen und Lizenzen

- Spritpreise: [Spritpreisrechner der E-Control Austria](https://www.e-control.at/spritpreisrechner), öffentliche API
- Geocoding: [OpenStreetMap Nominatim](https://nominatim.org/), Daten © [OpenStreetMap-Mitwirkende](https://www.openstreetmap.org/copyright), ODbL. Bitte die [Nutzungsrichtlinie](https://operations.osmfoundation.org/policies/nominatim/) beachten.
- E-Geier selbst: [MIT-Lizenz](LICENSE)

## Haftungsausschluss / Disclaimer

**Deutsch:** E-Geier ist nicht mit der E-Control Austria verbunden. Alle Daten stammen aus der öffentlichen Spritpreisrechner-API. Keine Gewähr für Richtigkeit oder Aktualität. Maßgeblich ist der an der Zapfsäule angezeigte Preis.

**English:** E-Geier is not affiliated with E-Control Austria. All data comes from the public Spritpreisrechner API. No guarantee of accuracy or timeliness. The price shown at the pump is what counts.
