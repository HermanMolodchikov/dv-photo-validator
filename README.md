<p align="center"><img src="branding/logo-256.png" width="128" alt="DV Photo Validator"></p>

# DV Photo Validator

Настольное приложение для Windows 10/11 (x64). Оно помогает подготовить фотографию для Diversity Visa (DV Lottery): открыть фото, вручную кадрировать его в квадрат, отметить ориентиры головы, проверить измеримые параметры и сохранить JPEG 600×600 размером до 240 КиБ.

> Программа не гарантирует, что фотографию примут, и не заменяет официальную проверку. Перед подачей сверьтесь с действующей инструкцией Государственного департамента США.

Фотографии обрабатываются только локально: программа ничего не отправляет в интернет, не ведёт журналов с изображениями и не хранит историю после закрытия.

## Статус

| Этап | Содержание | Статус |
|---|---|---|
| 1. MVP | Импорт, ручное кадрирование, JPEG 600×600, лимит размера, технические проверки, тесты | **готово (v0.1.0)** |
| 2. Автокадрирование | Детекция лица (YuNet), автоматические ориентиры с уверенностью, «Автоматически выровнять», проверка центрирования | **готово (v0.2.0)** |
| 3. Визуальные проверки | Фон, шум, резкость, освещение, поза, открытые глаза, признаки очков; отметки «Проверено мной» | **готово (v0.3.0)**; выражение лица и головной убор — только вручную |
| 4. Релиз | Сборка-архив для Windows x64, инструкция пользователя, примеры отчётов, CI, лицензии | **готово (v1.0.0)**; установщик MSIX — после получения сертификата подписи |

## Требования для сборки

- Windows 10/11 x64
- .NET SDK 10.0 (проверено на 10.0.401)

## Сборка, тесты, запуск

```bash
dotnet build DvPhotoValidator.sln -c Release
```

```bash
dotnet test DvPhotoValidator.sln
```

```bash
dotnet run --project src/DvPhotoValidator.App
```

Релиз: прогон тестов, автономная сборка для Windows x64 (на компьютере пользователя .NET не нужен) и архив `dist\DvPhotoValidator-<версия>-win-x64.zip`:

```bash
powershell -ExecutionPolicy Bypass -File build/release.ps1 -Version 1.0.0
```

CI (GitHub Actions, `.github/workflows/build.yml`) собирает решение, запускает тесты и публикует сборку как артефакт.

Путь к фото можно передать аргументом: `DvPhotoValidator.exe photo.jpg`.

## Структура решения

```
DvPhotoValidator.sln
├── src/DvPhotoValidator.Core       — модели, профили правил, ValidationEngine, геометрия (без зависимостей от UI)
│   ├── Models        ValidationIssue/Status/Report, RgbImage, ImageFileFacts, HeadLandmarks
│   ├── Rules         RuleProfile + dv-baseline.json (версионируемые требования)
│   ├── Validation    правила: файл, размеры, цвет, высота головы и глаз, возраст фото, ручной чек-лист
│   ├── Geometry      CropRect, CropGeometry (сдвиг/масштаб/ограничение рамки)
│   └── Interfaces    IImageLoader, IImageInspector, ICropService, IJpegExporter
├── src/DvPhotoValidator.Imaging    — WIC: ImageLoader, ImageNormalizer (EXIF), ColorSpaceConverter (sRGB),
│                                     CropService (ресемплинг Митчелла), JpegExporter (подбор качества и перепроверка)
├── src/DvPhotoValidator.Vision     — ONNX Runtime: FaceDetector (YuNet), LandmarkDetector, HeadTopEstimator, FaceAnalyzer;
│                                     PhotoQualityAnalyzer (фон, шум, резкость, свет, поза, глаза, очки)
├── src/DvPhotoValidator.App        — WPF/MVVM: Views, ViewModels, Commands, Services, Resources
└── tests/DvPhotoValidator.Tests    — xUnit: UnitTests, IntegrationTests (WIC и UI smoke-тест), TestImages (генерация в коде)
```

Модель лица (`face_detection_yunet_2023mar.onnx`, около 230 КБ) поставляется вместе с программой в папке `Models` и работает локально на процессоре.

## Документация

- [docs/user-guide.md](docs/user-guide.md) — инструкция пользователя
- [docs/algorithms.md](docs/algorithms.md) — алгоритмы, правила проверки и ограничения точности
- [docs/report-examples.md](docs/report-examples.md) — примеры отчётов проверки
- [docs/rules-update.md](docs/rules-update.md) — как обновить требования DV на новый год
- [THIRD-PARTY.md](THIRD-PARTY.md) — зависимости и лицензии

## Лицензия

[MIT](LICENSE) © 2026 German Molodchikov. Лицензии сторонних компонентов перечислены в [THIRD-PARTY.md](THIRD-PARTY.md).
