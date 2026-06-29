# DigitalCoreAnalyser

[![.NET](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-blue)](https://www.microsoft.com/windows)

**Программный комплекс для статистического и континуального анализа цифровых моделей керна**

---

## 📝 Описание

**DigitalCoreAnalyser** — это десктопное приложение на C# (WinUI 3, .NET 8), предназначенное для анализа трёхмерных моделей горных пород (кернов). Программа позволяет загружать томографические данные, визуализировать поровое пространство в 2D и 3D, а также вычислять ключевые фильтрационно-емкостные свойства.

Проект разработан в рамках ВКР, апробирован на реальных образцах песчаников (Berea, S7) и продолжает активно развиваться.

---

## Основные возможности

- **Загрузка данных:** Чтение бинарных RAW-файлов с воксельными данными (поддержка .raw, .bin, .dat).
- **2D-визуализация:** Отображение срезов в плоскостях XY, XZ, YZ с навигацией по слоям.
- **3D-визуализация:** Интерактивная визуализация порового пространства (облако точек) с вращением и масштабированием.
- **Расчёт свойств:**
  - Общая и эффективная пористость.
  - Формула Арчи (Фактор формации).
  - Формула Козени–Кармана (Абсолютная проницаемость).
  - Гидравлический радиус и извилистость.
- **Статистический анализ:** Выделение связных компонент пор (алгоритм BFS), распределение пор по размерам.
- **Работа с подвыборками:** Создание субсэмплов для оценки RVE и SVE.

---

## 🛠 Установка и запуск

### Требования
- Windows 10 (версия 1809+) или Windows 11.
- [.NET 8 SDK](https://dotnet.microsoft.com/download).
- Visual Studio Code (рекомендуется) или любая среда с поддержкой C#.

### Инструкция по сборке и запуску

1. Клонируйте репозиторий:
   ```bash
   git clone https://github.com/4y8a4ek/DigitalCoreAnalyser.git
   cd DigitalCoreAnalyser
   ```

2. Восстановите зависимости:
```bash
dotnet restore
```
3. Соберите проект:
```bash
dotnet build
```
4. Запустите приложение:
```
bash
dotnet run --project DigitalCoreAnalyser/DigitalCoreAnalyser.csproj
```

# 📂 Структура проекта
```
DigitalCoreAnalyser/
├── DigitalCoreAnalyser/          # Основной код приложения
│   ├── Models/                   # Модели данных (CoreSample, SubSample)
│   ├── Services/                 # Сервисы (загрузка, расчёты, рендеринг)
│   ├── ViewModels/               # MVVM-модели представлений
│   └── Views/                    # UI-элементы (WinUI 3)
├── docs/                         # Документация
├── samples/                      # Примеры данных для тестирования
└── README.md                     # Этот файл
```

# 📊 Примеры использования

![2D-режим: отображение срезов с навигацией по слоям.](https://github.com/4y8a4ek/Software-for-the-analysis-of-pore-spaces/blob/main/Screenshots/2D.png)

![3D-режим: интерактивная визуализация порового пространства.](https://github.com/4y8a4ek/Software-for-the-analysis-of-pore-spaces/blob/main/Screenshots/3D.png)

![Окно результатов статистического анализа](https://github.com/4y8a4ek/Software-for-the-analysis-of-pore-spaces/blob/main/Screenshots/Statistical.png)





# 👤 Автор
## Горшков Артём
[Telegram](https://t.me/mode_apathy)

> Подробный план развития проекта с разбивкой по этапам и срокам см. в [ROADMAP.md](ROADMAP.md).

