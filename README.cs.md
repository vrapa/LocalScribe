# LocalScribe

[English](README.md) | [Čeština](README.cs.md)

> **Komunitní fork:** tento repozitář přidává na upstream LocalScribe 0.9.2 několik oprav pro
> spolehlivější provoz na Windows a přepis češtiny. Přesný seznam změn, testů a omezení je v
> [COMMUNITY-FORK.md](COMMUNITY-FORK.md). Nejde o oficiální upstream release.

**Lokální přepis pracovních hovorů pro Windows 11. Bez cloudu, předplatného a odesílání audia.**

LocalScribe zachytává mikrofon a zvuk vzdálené strany jako dva samostatné proudy, lokálně je přepisuje
pomocí Whisperu a ukládá časovaný transcript na vašem počítači. Díky odděleným proudům rozlišuje
„já“ a „vzdálená strana“ bez cloudové diarizace.

## Rychlá instalace tohoto forku

1. Z [release v0.9.2-vrapa.1](https://github.com/vrapa/LocalScribe/releases/tag/v0.9.2-vrapa.1)
   stáhněte `LocalScribe-vrapa-0.9.2-1-win-x64.zip` a `SHA256SUMS.txt`.
2. Ověřte archiv:

   ```powershell
   Get-FileHash -Algorithm SHA256 .\LocalScribe-vrapa-0.9.2-1-win-x64.zip
   ```

3. Archiv rozbalte a spusťte `app\LocalScribe.App.exe`.

Balíček je přenosný a self-contained; samostatný .NET runtime není potřeba. Binárky nejsou digitálně
podepsané, takže Windows SmartScreen může zobrazit varování. Ověřte SHA-256 a spouštějte pouze archiv
stažený z tohoto GitHub releasu.

Preview obsahuje vícejazyčné modely Whisper `base-q8_0` a `small-q8_0`, Silero VAD a MCP server.
Neobsahuje FFmpeg, diarizační helper, lokálního asistenta ani component-download helper. Podrobnosti
jsou v [PORTABLE-RELEASE.md](PORTABLE-RELEASE.md).

## Co tento fork opravuje

- automatická volba modelu nyní pro češtinu opravdu hledá nainstalované vícejazyčné modely;
- LiveRunner čeká na dokončení asynchronní finalizace a ověřuje uložené metadata;
- oba runnery podporují izolované settings, output a language parametry pro opakovatelné testy;
- Settings umožňuje pro nové sessions zvolit uchování audia `keep` nebo `never`;
- MCP je ověřený přes skutečný stdio handshake a search→read round-trip.

## Doporučené nastavení pro češtinu

- `Language`: `cs`;
- `Backend`: `cpu` na počítači bez podporované GPU;
- `Model`: `base` jako rychlý výchozí kompromis;
- `small-q8_0` pro vyšší přesnost, pokud počítač stíhá přepis v reálném čase;
- `Microphone`: `Follow default`, pokud Windows během práce zařízení nemění;
- `Remote capture`: `Per-process` a aplikace `Slack` nebo `Discord`;
- používejte sluchátka, zvlášť při fallbacku na celý systémový mix.

Změna `Audio retention` se týká jen nových sessions a nemaže dříve uložené soubory.

## Ověřený stav

Na Windows 11 a Intel Core i5-9400 bez CUDA:

- prošlo 1 414 model-free Core testů;
- z kompletní App sady prošlo 1 169 z 1 171 testů; jeden neúspěch je locale-sensitive očekávání
  `3.3 MB` proti českému `3,3 MB`, druhý časovací test prošel samostatně;
- 26,6sekundový český live system-audio test vytvořil tři segmenty, oddělený local/remote zdroj a
  korektně finalizovanou session;
- `audioRetention=never` nevytvořilo retained FLAC;
- MCP initialize, seznam nástrojů a search→read test prošly.

Slack i Discord lze zvolit jako per-process cíl. Úplné tvrzení o spolehlivosti hovoru ale vyžaduje
ještě přehrát známý zvuk přímo každou aplikací a porovnat výsledný remote transcript.

## Používání

### Nahrávání

- Aplikace běží primárně v system tray.
- Záznam lze spustit a zastavit z tray menu, record console nebo zkratkou `Ctrl+Alt+R`.
- Pauza používá `Ctrl+Alt+P`.
- Mikrofon a vzdálený zvuk se zachytávají a ukládají odděleně.
- Indikátor v tray ukazuje stav nahrávání; aplikace nic nespouští automaticky bez vašeho rozhodnutí.

### Přepis a data

- Silero VAD dělí oba audio proudy na promluvy.
- Whisper je přepisuje lokálně.
- Segmenty se slučují podle session clock do jednoho časovaného transcriptu.
- Opravy a přiřazení speakerů jsou nedestruktivní overlay; původní `transcript.jsonl` se nepřepisuje.
- Manifest se SHA-256 umožňuje odhalit změněné nebo chybějící soubory.

### MCP

MCP server používá lokální transport `stdio` a nabízí šest nástrojů:

- `list_sessions`
- `read_transcript`
- `search_transcripts`
- `search_transcripts_semantic`
- `list_matters`
- `get_summary`

Nástroje nemění session ani transcript. Server ale zapisuje append-only audit log a sémantické
vyhledávání může vytvářet odvoditelné indexy. Přístup je ve výchozím stavu vypnutý a řídí se lokálním
consent souborem podle Matter; poškozený nebo chybějící consent znamená zamítnutí.

## Jak to funguje

```text
Mikrofon (Local) ─────┐                          ┌─→ overlay / record console
                      ├─ VAD → Whisper → merge ──┼─→ živý transcript
Zvuk app (Remote) ────┘      podle session času   └─→ session adresář + local/remote audio
```

Per-process loopback izoluje zvuk vybrané aplikace. U Teams a browserových hovorů může být nutný
fallback na celý systémový mix; remote audio potom může obsahovat zvuky jiných programů. Aplikace na
fallback upozorní a vloží marker do transcriptu.

## Požadavky

- Windows 11 x64; remote capture vyžaduje build Windows 20348 nebo novější.
- Pro distribuovaný self-contained build není potřeba samostatný runtime.
- Pro kompilaci ze zdrojů je potřeba .NET 10 SDK (`net10.0-windows`).
- GPU je volitelná: podporuje se CUDA, Vulkan a CPU fallback.
- Záznam vyžaduje nejméně 2 GiB volného místa; pod 1 GiB aplikace varuje.
- Import formátů jiných než WAV potřebuje FFmpeg, který preview balíček neobsahuje.

## Sestavení ze zdrojů

```powershell
pwsh tools/fetch-models.ps1
pwsh tools/fetch-ffmpeg.ps1
dotnet build LocalScribe.slnx
dotnet run --project src/LocalScribe.App
```

Kompletní balíček sestaví `build.ps1`; pro Velopack potřebuje `dotnet tool install -g vpk`. Běžící
`LocalScribe.App.exe` během buildu zamyká `Core.dll`, proto jej nejdřív ukončete.

Testy bez privátních audio fixtures:

```powershell
dotnet test LocalScribe.slnx --filter "Category!=Fixture"
```

## Umístění dat

Výchozí storage root je `%USERPROFILE%\LocalScribe`:

```text
LocalScribe/
├─ sessions/       session.json, transcript.jsonl, manifest, local/remote audio
├─ matters/        Matter metadata
├─ index/          odvoditelné search indexy
├─ mcp/            consent.json a append-only audit
├─ diagnostics/    diagnostické JSONL; text transcriptu je standardně skrytý
└─ people/         uložené voiceprinty
```

Nastavení je v `%APPDATA%\LocalScribe\settings.json`. Změna storage root data automaticky
nepřesouvá. Názvy session adresářů mohou obsahovat název hovoru, takže nejsou anonymní.

## Soukromí a právo

Živá aplikace a Core neobsahují síťový kód; model downloads jsou v odděleném helper procesu a spouští
se jen na vyžádání. Aplikace nic neodesílá do cloudové transkripční služby.

Za zákonnost nahrávání odpovídá uživatel. Některé jurisdikce vyžadují souhlas jedné, více nebo všech
stran. LocalScribe stav záznamu zobrazuje, ale souhlas účastníků nemůže získat za vás.

Zdrojový kód tohoto forku zůstává pod licencí MIT a zachovává upstream copyright. Modely a závislosti
mají vlastní licence; viz [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Známá omezení

- Nepodepsaný build může vyvolat SmartScreen a firemní politika jej může zablokovat.
- Live přepis je záměrně omezen na menší modely; vyšší přesnost řeší re-transcription.
- Čeština s `base-q8_0` funguje, ale není bezchybná.
- Teams a browser hovory mohou vyžadovat celý system mix.
- Integritní manifest změnu odhalí, ale kryptograficky jí nezabrání.
- Změna storage root nic nemigruje.
- Diagnostické a MCP audit logy se automaticky nemažou.
- Preview release neobsahuje import, diarizaci ani assistant runtime komponenty.

## Licence a autorství

[MIT](LICENSE) © 2026 imnotwallace. Tento fork zachovává původní licenci a autorství; změny forku jsou
zdokumentované v [COMMUNITY-FORK.md](COMMUNITY-FORK.md). Přehled licencí přibalených modelů a knihoven
je v [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

Příspěvky: [CONTRIBUTING.md](CONTRIBUTING.md).
