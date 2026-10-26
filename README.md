<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/media/banner-ru-dark.png">
    <img src="docs/media/banner-ru-light.png" width="880" alt="AppBridge — перенос приложений из App Store на другой iPhone">
  </picture>
</p>

<p align="center">
  <a href="https://github.com/kr0nsch/AppBridge/releases/latest"><img src="https://img.shields.io/github/v/release/kr0nsch/AppBridge?label=%D1%81%D0%BA%D0%B0%D1%87%D0%B0%D1%82%D1%8C&color=007aff&style=for-the-badge" alt="Скачать последнюю версию"></a>
</p>

<p align="center">
  <a href="https://github.com/kr0nsch/AppBridge/actions/workflows/ci.yml"><img src="https://github.com/kr0nsch/AppBridge/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078d4" alt="Windows 10 и 11">
  <a href="LICENSE"><img src="https://img.shields.io/github/license/kr0nsch/AppBridge?color=34c759" alt="Лицензия MIT"></a>
  <a href="https://github.com/kr0nsch/AppBridge/releases"><img src="https://img.shields.io/github/downloads/kr0nsch/AppBridge/total?color=5856d6" alt="Загрузки"></a>
  <a href="README.en.md"><img src="https://img.shields.io/badge/README-English-lightgrey" alt="English README"></a>
</p>

**AppBridge** переносит ваши приложения из App Store с одного iPhone на другой прямо с компьютера на Windows. Подключите оба телефона, отметьте нужные приложения и нажмите «Установить» — AppBridge скачает оригинальные приложения у Apple с вашим Apple Account и установит их по USB. Нового iPhone ещё нет? Сохраните приложения на компьютер сейчас и установите их потом. Бесплатно, без джейлбрейка и без рекламы.

<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/media/demo-ru-dark.gif">
    <img src="docs/media/demo-ru-light.gif" width="760" alt="Как работает AppBridge: выбор iPhone, выбор приложений, вход в App Store и установка">
  </picture>
  <br>
  <sub>Запись демонстрационного режима: телефоны и приложения смоделированы.</sub>
</p>

## Как это работает

На первом экране выберите **«Перенести сейчас»**, если оба iPhone под рукой, или **«Перенести потом»**, если новый телефон появится позже.

### Перенести сейчас

1. **Подключите iPhone с приложениями.** AppBridge покажет все приложения, установленные из App Store.
2. **Отметьте нужные.** Есть поиск и «Выбрать все».
3. **Подключите второй iPhone.** Оба телефона можно держать подключёнными или подключать по очереди.
4. **Войдите в App Store и нажмите «Установить».** Пока одно приложение устанавливается, следующее уже скачивается.

<p align="center">
  <img src="docs/media/how-it-works-ru.svg" width="880" alt="Схема: iPhone с приложениями → AppBridge на ПК ← App Store; AppBridge → другой iPhone">
</p>

### Перенести потом

Два шага, между которыми может пройти хоть месяц:

1. **Сохранить приложения.** Подключите старый iPhone, отметьте приложения и войдите в App Store — AppBridge скачает их в папку `Загрузки\AppBridge`. Так можно сохранить и приложения, которые могут пропасть из App Store.
2. **Установить на новый iPhone.** Подключите новый телефон, и AppBridge покажет сохранённые приложения: что уже установлено, что обновится и что не подойдёт (например, если нужна более новая iOS). Входить в Apple Account на компьютере для этого не нужно.

<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/media/later-ru-dark.gif">
    <img src="docs/media/later-ru-light.gif" width="760" alt="Перенести потом: установка сохранённых приложений на новый iPhone">
  </picture>
</p>

## Скриншоты

<table>
  <tr>
    <td width="33%" align="center">
      <picture>
        <source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/ru-dark/1-connect.png">
        <img src="docs/screenshots/ru-light/1-connect.png" alt="Подключение iPhone">
      </picture>
      <br><sub><b>Подключение</b></sub>
    </td>
    <td width="33%" align="center">
      <picture>
        <source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/ru-dark/2-choose-apps.png">
        <img src="docs/screenshots/ru-light/2-choose-apps.png" alt="Выбор приложений">
      </picture>
      <br><sub><b>Выбор приложений</b></sub>
    </td>
    <td width="33%" align="center">
      <picture>
        <source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/ru-dark/3-target.png">
        <img src="docs/screenshots/ru-light/3-target.png" alt="Выбор второго iPhone">
      </picture>
      <br><sub><b>Второй iPhone</b></sub>
    </td>
  </tr>
  <tr>
    <td width="33%" align="center">
      <picture>
        <source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/ru-dark/5-ready.png">
        <img src="docs/screenshots/ru-light/5-ready.png" alt="Всё готово к установке">
      </picture>
      <br><sub><b>Параметры</b></sub>
    </td>
    <td width="33%" align="center">
      <picture>
        <source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/ru-dark/6-installing.png">
        <img src="docs/screenshots/ru-light/6-installing.png" alt="Установка приложений">
      </picture>
      <br><sub><b>Установка</b></sub>
    </td>
    <td width="33%" align="center">
      <picture>
        <source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/ru-dark/7-done.png">
        <img src="docs/screenshots/ru-light/7-done.png" alt="Готово">
      </picture>
      <br><sub><b>Готово</b></sub>
    </td>
  </tr>
</table>

**Перенести потом**

<table>
  <tr>
    <td width="33%" align="center">
      <picture>
        <source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/ru-dark/later-1-start.png">
        <img src="docs/screenshots/ru-light/later-1-start.png" alt="Стартовый экран: перенести сейчас или потом">
      </picture>
      <br><sub><b>Начало</b></sub>
    </td>
    <td width="33%" align="center">
      <picture>
        <source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/ru-dark/later-2-hub.png">
        <img src="docs/screenshots/ru-light/later-2-hub.png" alt="Сохранить приложения или установить на новый iPhone">
      </picture>
      <br><sub><b>Перенести потом</b></sub>
    </td>
    <td width="33%" align="center">
      <picture>
        <source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/ru-dark/save-3-done.png">
        <img src="docs/screenshots/ru-light/save-3-done.png" alt="Приложения сохранены на компьютере">
      </picture>
      <br><sub><b>Приложения сохранены</b></sub>
    </td>
  </tr>
  <tr>
    <td width="33%" align="center">
      <picture>
        <source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/ru-dark/later-3-new-iphone.png">
        <img src="docs/screenshots/ru-light/later-3-new-iphone.png" alt="Подключение нового iPhone">
      </picture>
      <br><sub><b>Новый iPhone</b></sub>
    </td>
    <td width="33%" align="center">
      <picture>
        <source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/ru-dark/later-4-saved.png">
        <img src="docs/screenshots/ru-light/later-4-saved.png" alt="Сохранённые приложения, проверенные для нового iPhone">
      </picture>
      <br><sub><b>Сохранённые приложения</b></sub>
    </td>
    <td width="33%" align="center">
      <picture>
        <source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/ru-dark/later-6-done.png">
        <img src="docs/screenshots/ru-light/later-6-done.png" alt="Сохранённые приложения установлены">
      </picture>
      <br><sub><b>Установлено</b></sub>
    </td>
  </tr>
</table>

## Возможности

- **Четыре простых шага** и одна кнопка на каждом экране.
- **Перенести сейчас или потом.** Сохраните приложения на компьютер, пока под рукой старый iPhone, и установите их на новый, когда он появится, — без входа в Apple Account на компьютере. AppBridge сам выберет версию, которая подойдёт к iOS нового телефона.
- **Оригинальные приложения от Apple.** Каждое приложение скачивается с серверов Apple с вашим Apple Account — ничего не берётся со сторонних сайтов.
- **Всё по USB.** Не нужны iTunes, Python и командная строка: AppBridge сам находит iPhone, показывает запрос «Доверять этому компьютеру?», читает список приложений с иконками и устанавливает их.
- **Быстро.** Скачивание и установка идут параллельно, по каждому приложению видно прогресс, а если что-то не установилось — понятная причина и кнопка «Повторить».
- **Версия на выбор:** последняя из App Store или та же, что на первом iPhone (если Apple её ещё выдаёт).
- **Бесплатные приложения** можно автоматически добавить в ваш Apple Account, если их ещё нет в покупках.
- **Светлая и тёмная тема, русский и английский язык.**
- **Установщик и портативная версия.** Ничего дополнительно ставить не нужно: .NET и Windows App SDK уже внутри.

## Проверено на реальных устройствах

| Откуда | Куда | Приложения | Результат |
|---|---|---|---|
| iPhone 16 Pro | iPhone 14 | Сбер, VK, Почта Mail.ru | ✅ Перенесены |

Проверили на своих устройствах? Расскажите в [issues](https://github.com/kr0nsch/AppBridge/issues) — добавим в таблицу.

## Скачать

Последняя версия — на странице **[Releases](https://github.com/kr0nsch/AppBridge/releases/latest)**:

| Файл | Что это |
|---|---|
| `AppBridge-<версия>-setup-x64.exe` | **Установщик (рекомендуется).** Ставится для текущего пользователя, права администратора не нужны. Ярлык в меню «Пуск» и удаление через «Параметры». |
| `AppBridge-<версия>-portable-x64.zip` | **Портативная версия.** Распакуйте куда угодно, например на флешку, и запустите `AppBridge.exe`. Настройки и загрузки хранятся в папке `Data` рядом с программой. |
| `SHA256SUMS.txt` | Контрольные суммы для проверки файлов. |

**Системные требования:** Windows 10 версии 2004 или новее, Windows 11. ПК с процессором x64; на ПК с Arm — Windows 11 (через встроенную эмуляцию).

> [!NOTE]
> Все файлы релиза собираются в GitHub Actions прямо из этого репозитория и подписаны подписью происхождения (Sigstore). Проверить подлинность: `gh attestation verify <файл> -R kr0nsch/AppBridge`.
> Сертификата Authenticode пока нет, поэтому SmartScreen может предупредить о неизвестном издателе — нажмите **Подробнее → Выполнить в любом случае**.

## Что понадобится

- **Приложение «Устройства Apple»** из [Microsoft Store](https://apps.microsoft.com/detail/9NP83LWLPZ9K) (или iTunes) — в нём драйвер, через который Windows работает с iPhone. Если его нет, AppBridge подскажет.
- **USB-кабель** с передачей данных и разблокированные iPhone. На вопрос «Доверять этому компьютеру?» нажмите «Доверять».
- **Apple Account**, которым приложения были получены (или который может получить их бесплатно).
- Подключение к интернету.

## Вопросы и ответы

<details>
<summary><b>Переносятся ли данные приложений?</b></summary>

Нет, AppBridge устанавливает сами приложения. Документы, переписка и настройки восстанавливаются как обычно — из iCloud, резервной копии или после входа в аккаунт внутри приложения.
</details>

<details>
<summary><b>Какие приложения можно перенести?</b></summary>

Те, что доступны вашему Apple Account в App Store. Бесплатные приложения AppBridge может сам добавить в ваш аккаунт — как кнопка «Загрузить» в App Store (это можно отключить). Платные должны быть уже куплены этим аккаунтом. Приложения, удалённые из App Store, получится установить, только пока Apple выдаёт их владельцам. Приложения не из App Store (TestFlight, сборки разработчиков) не показываются: их нельзя скачать заново.
</details>

<details>
<summary><b>Нового iPhone ещё нет. Как перенести приложения потом?</b></summary>

Выберите «Перенести потом» → «Сохранить приложения», подключите старый iPhone и сохраните нужные приложения. Когда появится новый телефон, откройте AppBridge, выберите «Перенести потом» → «Установить на новый iPhone» и подключите его. Чтобы приложения открывались, войдите на новом iPhone в App Store с тем же Apple Account, с которым они сохранены. Файлы `.ipa`, оставшиеся после переноса в версии 1.0 или скопированные в эту папку, тоже появятся в списке.
</details>

<details>
<summary><b>Безопасно ли вводить пароль Apple Account?</b></summary>

Пароль передаётся напрямую во встроенный открытый [ipatool](https://github.com/majd/ipatool), который входит в Apple. AppBridge нигде его не сохраняет, не пишет в журналы и никуда больше не отправляет. Сеанс App Store хранится в зашифрованном виде, а ключ защищён Windows (DPAPI) для вашей учётной записи; кнопка «Выйти» удаляет его. У AppBridge нет своих серверов, рекламы и телеметрии. Подробнее — в [SECURITY.md](SECURITY.md).
</details>

<details>
<summary><b>Нужен ли джейлбрейк? Обходится ли защита Apple?</b></summary>

Нет и нет. Приложения устанавливаются так же, как из App Store, и остаются привязаны к вашему Apple Account (защита FairPlay не обходится).
</details>

<details>
<summary><b>iPhone не появляется в списке</b></summary>

Используйте кабель с передачей данных и подключите его напрямую к компьютеру, разблокируйте iPhone и проверьте, что установлено приложение «Устройства Apple». Попробуйте другой USB-порт.
</details>

<details>
<summary><b>Не исчезает «Нажмите „Доверять“ на iPhone»</b></summary>

Разблокируйте iPhone — запрос показывается только на разблокированном экране. Если вы нажали «Не доверять», нажмите «Повторить» или переподключите кабель.
</details>

<details>
<summary><b>«Нет в покупках этого Apple Account»</b></summary>

Приложение получено другим Apple Account. Войдите под ним или включите «Получать бесплатные приложения» (для бесплатных).
</details>

<details>
<summary><b>Где AppBridge хранит данные?</b></summary>

| | Установщик | Портативная версия |
|---|---|---|
| Настройки, журналы, сеанс App Store | `%LOCALAPPDATA%\AppBridge` | `Data\` рядом с `AppBridge.exe` |
| Скачанные и сохранённые приложения (`.ipa`) | `Загрузки\AppBridge` (меняется в настройках) | `Data\Downloads` |
| Иконки сохранённых приложений | `%LOCALAPPDATA%\AppBridge\Icons` | `Data\Icons` |

В файлах `.ipa` есть email вашего Apple Account (так Apple оформляет покупки) — не передавайте их другим.
</details>

<details>
<summary><b>Что-то пошло не так</b></summary>

Откройте **Настройки → Журналы диагностики → Показать** и приложите последний журнал к [issue](https://github.com/kr0nsch/AppBridge/issues/new/choose). Паролей и кодов в журналах нет.
</details>

## Сборка из исходников

Нужны Windows 10/11, [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) и [Inno Setup 6](https://jrsoftware.org/isinfo.php) для установщика.

```powershell
# ipatool (закреплённая версия, проверка SHA-256)
./build/Get-Ipatool.ps1

# Тесты
dotnet test tests/AppBridge.Core.Tests

# Запуск
dotnet run --project src/AppBridge -p:Platform=x64

# Портативный ZIP и установщик в artifacts/release
./build/build.ps1 -Version 1.1.0
```

Запуск с ключом `--demo` (или с переменной `APPBRIDGE_DEMO=1`) включает демонстрационный режим со смоделированными iPhone и App Store — удобно для работы над интерфейсом без устройств и Apple Account. Ключ `--demo-saved` добавляет несколько сохранённых приложений для «Перенести потом».

Тег вида `v1.2.3` запускает [GitHub Actions](.github/workflows/release.yml): сборка, тесты и публикация релиза с установщиком, портативной версией и контрольными суммами.

<details>
<summary><b>Структура проекта</b></summary>

| Путь | Что внутри |
|---|---|
| `src/AppBridge` | Приложение WinUI 3: экраны, модели представления, стили, переводы (`Strings/`) |
| `src/AppBridge.Core` | Работа с iPhone по USB (доверие, список приложений, установка), интеграция с ipatool, конвейер переноса |
| `tests/AppBridge.Core.Tests` | Модульные тесты, в том числе проверка с настоящим ipatool |
| `installer` | Скрипт Inno Setup и оформление установщика |
| `build` | Сборка релиза, загрузка ipatool с проверкой контрольной суммы, генерация иконки |
| `legacy/python` | Первый кроссплатформенный прототип на Python (не поддерживается) |

AppBridge построен на [WinUI 3](https://github.com/microsoft/microsoft-ui-xaml) / Windows App SDK, [Netimobiledevice](https://github.com/artehe/Netimobiledevice) (работа с iPhone по USB) и [ipatool](https://github.com/majd/ipatool) (загрузки из App Store). Лицензии — в [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).
</details>

## Лицензия

[MIT](LICENSE). AppBridge не связан с Apple Inc. и не одобрен ею. iPhone и App Store — товарные знаки Apple Inc. Используйте программу только с приложениями, на которые у вас есть права.
