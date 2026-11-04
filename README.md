# Ricerca contratti Rinoleggio

Demo ASP.NET Core 10 per la ricerca a filtri e l’inserimento di contratti. Lo schema SQLite è semplificato in una tabella wide, opzione prevista dal documento `abp-modello-dati-sql.md` per i prototipi. I campi seguono il modello Listing / Contract / Vehicle / Services.

## Avvio

```powershell
dotnet run --urls http://localhost:5080
```

Apri http://localhost:5080. Al primo avvio l’app crea `App_Data/contracts.db` e inserisce 20 contratti dimostrativi. I contratti aggiunti dal modulo vengono salvati nello stesso database.

## Ricerca

La pagina supporta testo libero, marca/modello, società, alimentazione, cambio, anno, canone, anticipo, IVA, km totali/attuali/residui, durata residua, penale e credito km, scadenza, contributo di subentro, servizi e coperture assicurative.

## API demo

- `GET /api/contracts` — risultati, con filtri come query string
- `GET /api/options` — opzioni società presenti
- `POST /api/contracts` — aggiunge un contratto
