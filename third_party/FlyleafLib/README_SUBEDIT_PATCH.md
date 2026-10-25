# FlyleafLib — lokalny fork SubEdit-Player Next

## Pochodzenie i wersje

- Projekt upstream: https://github.com/SuRGeoNix/Flyleaf
- Rodzina wydania: FlyleafLib 3.11.8.
- Dokładna baza źródeł: ffa8c630b64c5449586be911d52ad9ae9551e1c8.
- Tag v3.11.8 wskazuje późniejszy commit fc96062f9c731a0a4018039f23f4c6305a89ed05. Baza forka poprzedza ten tag; nie jest z nim identyczna.
- AssemblyVersion: 3.11.8.0; InformationalVersion zaczyna się od 3.11.8-subedit.e11+commit.ffa8c630b64c5449586be911d52ad9ae9551e1c8.
- Flyleaf.FFmpeg.Bindings: 9.0.0.
- Build bibliotek natywnych FFmpeg: N-126175-g0056dd32fd-20260816.
- Licencja FlyleafLib: LGPL-3.0-or-later; pełny tekst w LICENSE.txt. Biblioteki natywne FFmpeg zachowują własną licencję.

## Modyfikacje względem bazy upstream

Zmiany zachowania i konfiguracji obejmują 13 plików. Ścieżki są względne do tego katalogu.

| Plik | Zmiana |
|---|---|
| FlyleafLib.csproj | Celowanie w net10.0-windows, metadane forka, InternalsVisibleTo dla testów, konfiguracja ostrzeżeń oraz wyłączenie generowania XML dokumentacji i elementów pakowania upstream |
| Engine/Engine.Plugins.cs | Statyczny rejestr OpenDefault → OpenSubtitles → StreamSuggester dla pluginów wbudowanych; odkrywanie pluginów zewnętrznych pozostaje dynamiczne |
| MediaPlayer/SeekAccurateMode.cs | Nowy enum Legacy=0, Conditional=1 |
| MediaPlayer/SeekArbitrator.cs | Nowy koordynator intencji seek, atomowe pobieranie najnowszego żądania, unieważnianie starszych i arbitraż z frame-step |
| MediaPlayer/Player.SeekAccurate.cs | Nowa implementacja polityki dokładnego seeka, obserwacja overshootu, fallback i anulowanie |
| MediaPlayer/Player.cs | Zastąpienie ConcurrentStack przez SeekArbitrator i dostosowanie obsługi czasu oraz inicjalizacji |
| MediaPlayer/Player.Playback.cs | IntentId i tryb dokładności w SeekData, worker seeka w pauzie i przekazanie żądań między Pause/Play |
| MediaPlayer/Player.Extra.cs | Unieważnianie starszych żądań przed ShowFrame, ShowFrameNext i ShowFramePrev |
| MediaPlayer/Player.Screamers.cs | Konsumowanie najnowszego żądania, kontrola unieważnienia i wspólna ścieżka dokładnego seeka |
| MediaPlayer/Player.Screamers.VASD.cs | Ten sam arbitraż w odtwarzaniu audio/wideo |
| MediaFramework/MediaContext/DecoderContext.cs | GetVideoFrameAccurate z obserwacją klatek, anulowaniem i zachowaniem GetVideoFrame |
| MediaFramework/MediaDecoder/VideoDecoder.cs | EnqueuedPts ustawiane dla klatki umieszczanej w kolejce |
| MediaFramework/MediaRenderer/Renderer.VP.FL.cs | Guard scfg == null w FLUpdateTargetNits podczas wczesnej inicjalizacji renderera |

SwapChain.cs różni się od bazy także brakiem BOM; nie jest to zmiana zachowania. LICENSE.txt jest kopią licencji upstream.

## Polityka dokładnego seeka

Legacy zawsze wykonuje preroll 3000 ms przed celem. Conditional zaczyna od preroll 0 ms, obserwuje pierwszą klatkę i przy braku wyniku lub overshoocie wykonuje najwyżej jedną próbę z preroll 3000 ms. Nowsza intencja użytkownika przerywa starszą operację. Domyślną polityką samego Player pozostaje Legacy.

Adapter aplikacji wybiera Conditional dla MKV/WebM, MP4/MOV i AVI, a Legacy dla MPEG-TS i formatów nieznanych. Frame-step unieważnia wcześniejsze żądania. FAST i PRECISE pozostają rozdzielone.

EnqueuedPts ma aktywnych konsumentów w dokładnym seeku. FinalPtsTicks i HalfFrameTicks w SeekAccurateAttempt pozostają polami pomocniczymi bez konsumentów produkcyjnych; nie należy usuwać ich przy przenoszeniu forka bez osobnej weryfikacji.

## Budowanie i porównywanie zmian

W publicznym repozytorium fork jest częścią rozwiązania SubEdit.Player.slnx i jest używany przez ProjectReference. Z katalogu głównego uruchom:

    powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build-SubEdit.ps1 -c Release

Pełny aktualny stan zmian stanowią dołączone źródła. Historyczny, niekompletny patch nie jest dołączany ani traktowany jako instrukcja odtworzenia forka. Przy aktualizacji należy porównać te źródła z dokładną bazą upstream wskazaną wyżej, zachować wszystkie lokalne modyfikacje i ponowić testy oraz regresję odtwarzania.

Wersje biblioteki zarządzanej Bindings i natywnego FFmpeg są odrębne. Aktualizacja pakietu .NET nie oznacza automatycznej zgodności z innym buildem natywnym. Opisy zmian Scintilla.NET znajdują się w jego osobnym SUBEDIT_PATCH.md.
