# Зависимости и лицензии

## Время выполнения

| Компонент | Назначение | Лицензия |
|---|---|---|
| .NET 10 (Microsoft.NETCore.App, Microsoft.WindowsDesktop.App: WPF) | Платформа и интерфейс | MIT |
| Windows Imaging Component (часть Windows) | Декодирование, управление цветом, кодирование JPEG | Компонент ОС |
| Microsoft.ML.OnnxRuntime 1.20.1 | Запуск модели лица на CPU | MIT |
| YuNet `face_detection_yunet_2023mar.onnx` (OpenCV Zoo, © 2020 Shiqi Yu) | Детекция лица и 5 ключевых точек | MIT, текст: `src/DvPhotoValidator.Vision/Models/YUNET-LICENSE.txt` |

Других сторонних компонентов во время выполнения нет.

## Только для тестов

| Пакет | Версия | Лицензия |
|---|---|---|
| xunit | 2.9.3 | Apache-2.0 |
| xunit.runner.visualstudio | 3.1.4 | Apache-2.0 |
| Microsoft.NET.Test.Sdk | 17.14.1 | MIT |
| coverlet.collector | 6.0.4 | MIT |

## Будущие этапы (лицензии проверить до включения)

- Модели лица и ориентиров: лицензию каждой модели проверить отдельно (например, MediaPipe — Apache-2.0; у части моделей на основе InsightFace есть ограничения на коммерческое использование).
