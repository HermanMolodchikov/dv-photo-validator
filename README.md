# DV Photo Validator

Настольное приложение для Windows 10/11 (x64). Оно помогает подготовить фотографию для Diversity Visa (DV Lottery): открыть фото, вручную кадрировать его в квадрат, отметить ориентиры головы, проверить измеримые параметры и сохранить JPEG 600×600 размером до 240 КиБ.

> Программа не гарантирует, что фотографию примут, и не заменяет официальную проверку. Перед подачей сверьтесь с действующей инструкцией Государственного департамента США.

Фотографии обрабатываются только локально: программа ничего не отправляет в интернет, не ведёт журналов с изображениями и не хранит историю после закрытия.

## Статус

| Этап | Содержание | Статус |
|---|---|---|
| 1. MVP | Импорт, ручное кадрирование, JPEG 600×600, лимит размера, технические проверки, тесты | **готово (v0.1.0)** |
| 2. Автокадрирование | Детекция лица, автоматические ориентиры, «Автоматически выровнять» | не начат |
| 3. Визуальные проверки | Фон, резкость, освещение, поза, глаза, очки и головные уборы | не начат: пока это пункты «Проверьте вручную» |
| 4. Релиз | Установщик MSIX, инструкция пользователя, проверка лицензий | не начат |

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

Готовая сборка для Windows x64 (требует установленного .NET 10 Desktop Runtime):

```bash
dotnet publish src/DvPhotoValidator.App -c Release -r win-x64 --self-contained false -o publish
```

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
├── src/DvPhotoValidator.App        — WPF/MVVM: Views, ViewModels, Commands, Services, Resources
└── tests/DvPhotoValidator.Tests    — xUnit: UnitTests, IntegrationTests (WIC и UI smoke-тест), TestImages (генерация в коде)
```

Проекта `DvPhotoValidator.Vision` из ТЗ пока нет: он появится на этапе 2 вместе с детектором лица.

## Документация

- [docs/algorithms.md](docs/algorithms.md) — алгоритмы, правила проверки и ограничения точности
- [docs/rules-update.md](docs/rules-update.md) — как обновить требования DV на новый год
- [THIRD-PARTY.md](THIRD-PARTY.md) — зависимости и лицензии
