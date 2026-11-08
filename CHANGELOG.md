# Changelog

Istotne zmiany w SubEdit-Player Next. Sekcja `Unreleased` jest podstawą przyszłych Release Notes; nie oznacza nowego opublikowanego wydania.

## [Unreleased]

## [1.1.0] - 2026-11-08

### Added

- Zaznaczanie i kopiowanie fragmentów raportu we Właściwościach pliku wideo.

### Changed

- Właściwości pliku wideo i Diagnostyka programu otwierają się na środku SubEdit; po ręcznym przesunięciu każde okno pamięta swoją pozycję do końca sesji.
- Pozycje okien inspektora są korygowane do dostępnego obszaru monitora, a ich dopasowanie rozmiaru nie powoduje niepotrzebnego przesuwania.
- Uporządkowano menu kontekstowe wideo, dodając bezpośrednie powiększanie i pomniejszanie, przełączniki napisów i czasu filmu ze znacznikami stanu oraz polecenie „Zakończ program”.
- Uporządkowano nazwy poleceń menu i zsynchronizowano je z pomocą, FAQ, skrótami oraz tooltipami.
- Przeniesiono przełączniki wyświetlania do menu Widok, uporządkowano pozycje Napisy i Film oraz zebrano ustawienia formatu i FPS w menu Ustawienia. Skróty klawiszowe pozostały bez zmian, a Edytor napisów i Zawsze na wierzchu pokazują aktualny stan.
- Ujednolicono dialogi i siedem zakładek Opcji: czytelniejsze grupy, kompaktowe pola i naturalna kolejność Tab.
- Odświeżono Właściwości pliku wideo i Diagnostykę programu: przewijany raport oraz stałe przyciski Kopiuj/Zamknij.
- Zakresy czasów edycji wynoszą 0,00–99,00 s, własnych kroków przewijania 1–999 s, a czasu OSD 0,0–99,0 s.
- Ujednolicono wygląd suwaków przewijania i głośności.
- Zaktualizowano biblioteki edytora x64: Scintilla z 5.6.7 do 5.6.9 oraz Lexilla z 5.5.4 do 5.5.5.

### Fixed

- Zmiana rozmiaru czcionki edytora w Opcjach zeruje tymczasowe powiększenie ustawione przez Ctrl+kółko myszy i przywraca dokładnie wybraną wartość.
- Dzielenie i łączenie napisów SRT przerywa operację i wskazuje linię z błędem, jeśli zapis wyniku mógłby pominąć tekst.
- Anulowanie zamknięcia programu lub zakończenia sesji Windows pozostawia odtwarzacz w pełni sprawny. Zasoby okna są zwalniane jednokrotnie po potwierdzeniu zamknięcia lub bezpośrednim zwolnieniu okna.
- Przywrócono identyfikację kodeków AVI na podstawie FourCC i poprawiono opis formatu obrazu we Właściwościach pliku wideo.
- Suwak głośności zachowuje właściwą szerokość przy przenoszeniu okna między monitorami o różnym DPI.
- Uproszczono podmenu kodowania napisów i zsynchronizowano dostępność poleceń oraz znaczniki menu ze stanem aplikacji; właściwości filmu są nieaktywne bez otwartego filmu, a F9 nadal wyświetla właściwy komunikat.
- Ścieżka filmu mieści się w polu paska stanu po zmianie DPI i pozostaje widoczna po zamknięciu filmu.
- Poprawiono skalowanie dolnego paska stanu na monitorach o różnym DPI. Pola mDVD i MPL2 dopasowują szerokość do długości filmu i pokazują pełne wartości podczas odtwarzania.
- Poprawiono skalowanie menu na monitorach o różnym DPI.
- Usunięto nadmierne skalowanie fontów standardowych dialogów na dodatkowym monitorze.
- Poprawiono układ i walidację pól czasu w oknach Przejdź do napisu i Przejdź do czasu filmu.
- Poprawiono skalowanie Seek Bara przy przenoszeniu okna między monitorami o różnym DPI.
- Usunięto opóźnienia regulacji głośności w trybie pełnoekranowym.
- Poprawiono skalowanie OSD podczas przenoszenia okna między monitorami o różnym DPI.
- Ujednolicono przezroczystość tła napisów i OSD: 0% oznacza pełne krycie, a 100% pełną przezroczystość.
- Poprawiono mieszanie kolorów tła i obrysu OSD podczas odtwarzania przy częściowej przezroczystości tła.
- Usunięto biały błysk i prostokątne artefakty podczas zamykania programu, także w trybie pełnoekranowym.
- Usunięto błysk i późną zmianę rozmiaru standardowych dialogów podczas otwierania.
- Poprawiono skalowanie tekstu i kontrolek dialogów na monitorach 100%/125% oraz po zmianie czcionki.
- Poprawiono rozmiary przycisków i szerokość pól wyszukiwania oraz centrowanie okna Znajdź i zamień.
- Poprawiono rozdzielenie koloru tła i obrysu w podglądzie OSD w Opcjach.

### Performance

- Ograniczono zbędne odświeżanie OSD i napisów, zmniejszając obciążenie GPU.
- Przyspieszono otwieranie okna Opcje.

## [1.0.0] - 2026-10-25

### Added
- Pierwsze publiczne wydanie SubEdit-Player Next dla Windows x64.
