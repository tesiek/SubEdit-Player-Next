# SubEdit-Player Next

Odtwarzacz filmów i edytor napisów dla Windows. Łączy oglądanie filmu z poprawianiem tekstu, synchronizacją i konwersją napisów w jednym oknie.

## Pochodzenie projektu

Projekt nie powstałby bez oryginalnego SubEdit-Playera i pracy Artura Sikory, któremu należy się uznanie za stworzenie programu będącego przez wiele lat ważnym narzędziem polskiej społeczności napisowej. Powstał jako współczesna kontynuacja jego koncepcji, sposobu pracy i rozwiązań interfejsu. Zachowuje nawiązania do jego sposobu obsługi, korzystając ze współczesnego silnika odtwarzania.

[Strona oryginalnego SubEdit-Playera](https://subedit.com.pl)

## Najważniejsze możliwości

- Odtwarzanie popularnych plików wideo, m.in. MKV, MP4 i AVI, z dekodowaniem sprzętowym lub programowym.
- Widok filmu z edytorem napisów oraz tryb pełnoekranowy.
- Dokładne przewijanie, nawigacja klatka po klatce i odtwarzanie od bieżącego napisu.
- Regulacja prędkości od 0,25× do 4,00× oraz powiększanie i przesuwanie obrazu.
- Praca z napisami MicroDVD, MPL2, TMPlayer i SubRip (SRT), konwersja formatów oraz zapis jako SRT.
- Synchronizacja napisów: przesuwanie czasu, zmiana czasu wyświetlania i przeliczanie współczynnika FPS.
- Korekta tekstu, wyszukiwanie i zamiana, sortowanie napisów, dzielenie długich linii i łączenie szybko znikających napisów.
- Wybór dostępnych ścieżek audio, napisów osadzonych i rozdziałów filmu.

## Wymagania

- **Windows 10 lub Windows 11, x64.**
- **.NET 10 Desktop Runtime x64** — [pobierz ze strony Microsoft](https://dotnet.microsoft.com/pl-pl/download/dotnet/10.0), wybierając „.NET Desktop Runtime” dla Windows x64.

Aplikacja jest portable i nie wymaga instalatora. Do działania wymaga środowiska **.NET 10 Desktop Runtime x64**. Jeśli nie jest ono zainstalowane w systemie, przy pierwszym uruchomieniu Windows wyświetli komunikat z informacją o konieczności jego zainstalowania.

## Pobieranie i uruchomienie

1. Pobierz archiwum portable.
2. Wypakuj całe archiwum do wybranego katalogu, zachowując strukturę podkatalogów.
3. Uruchom `SubEdit.Player.Next.exe` z wypakowanego katalogu.

Ustawienia są zapisywane w pliku `SubEdit.Player.Next.json` w katalogu programu.

### Ostrzeżenia Windows i podpisy

**Uwaga:** Aplikacja nie jest obecnie podpisana komercyjnym certyfikatem podpisywania kodu. Przy pierwszym uruchomieniu Microsoft Defender SmartScreen może wyświetlić ostrzeżenie. W niektórych konfiguracjach zabezpieczeń systemu Windows może również pojawić się komunikat dotyczący podpisu jednego z dołączonych komponentów.

Jeśli pojawi się ostrzeżenie Microsoft Defender SmartScreen, wybierz **Więcej informacji (More info)**, a następnie **Uruchom mimo to (Run anyway)**.

## Pomoc

Menu **Pomoc** udostępnia tematy pomocy, pełną listę skrótów klawiszowych i FAQ dotyczące edycji napisów.

W repozytorium: [tematy pomocy](src/SubEdit.Player.WinForms/doc/SubEditHelp.html), [skróty klawiszowe](src/SubEdit.Player.WinForms/doc/SkrotyKlawiszowe.html), [FAQ — edycja napisów](src/SubEdit.Player.WinForms/doc/faq_edycja.html).

## Budowanie ze źródeł

W systemie Windows z zainstalowanym **.NET 10 SDK** uruchom w głównym katalogu repozytorium:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build-SubEdit.ps1 -c Release
```

Rozwiązanie znajduje się w `SubEdit.Player.slnx`. Kod aplikacji jest napisany w C# i korzysta z Windows Forms. Odtwarzanie zapewniają Flyleaf i FFmpeg, a edytor tekstu wykorzystuje Scintilla.NET.

### Flyleaf / FFmpeg

SubEdit-Player Next wykorzystuje bibliotekę **Flyleaf 3.11.8**, której autorem jest [SuRGeoNix](https://github.com/SuRGeoNix).

Dołączone natywne biblioteki **FFmpeg** pochodzą z gotowych plików binarnych udostępnionych przez SuRGeoNix w [oficjalnych wydaniach Flyleaf na GitHub](https://github.com/SuRGeoNix/Flyleaf/releases).

Biblioteki FFmpeg są wykorzystywane w oryginalnej postaci, **bez jakichkolwiek modyfikacji ani ponownej kompilacji**.

W projekcie SubEdit-Player Next wprowadzono natomiast własne modyfikacje biblioteki **Flyleaf .NET**, opisane w pliku [`README_SUBEDIT_PATCH.md`](third_party/FlyleafLib/README_SUBEDIT_PATCH.md).

## Licencja i komponenty zewnętrzne

Kod SubEdit-Player Next jest udostępniany jako open source na licencji **GPL-3.0-or-later**. Kod można analizować, modyfikować i rozpowszechniać zgodnie z [GNU GPL w wersji 3 lub nowszej](https://www.gnu.org/licenses/gpl-3.0.html).

Komponenty zewnętrzne zachowują własne licencje:

| Komponent | Licencja |
|---|---|
| FlyleafLib — lokalnie zmodyfikowany | [LGPL-3.0-or-later](third_party/FlyleafLib/LICENSE.txt) |
| Flyleaf.FFmpeg.Bindings | LGPL-3.0-or-later, według metadanych używanego pakietu |
| Dołączony build FFmpeg | GPL w wersji 3 lub nowszej, według informacji raportowanej przez biblioteki |
| Scintilla.NET (Scintilla5.NET) — lokalnie zmodyfikowana warstwa .NET | [MIT](third_party/Scintilla.NET/LICENSE) |
| Natywne Scintilla i Lexilla | [Licencja projektu Lexilla, Scintilla i SciTE](https://www.scintilla.org/License.txt) |

Szczegółowe informacje o używanych komponentach, ich wersjach, licencjach i lokalnych modyfikacjach znajdują się w pliku [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).

Podziękowania dla autorów [Flyleaf](https://github.com/SuRGeoNix/Flyleaf), [FFmpeg](https://ffmpeg.org/), [Scintilla.NET](https://github.com/desjarlais/Scintilla.NET), [Scintilla i Lexilla](https://www.scintilla.org/) oraz pozostałych wykorzystywanych bibliotek.