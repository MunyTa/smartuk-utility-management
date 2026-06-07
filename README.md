# SmartUK

SmartUK - курсовой проект по варианту 16: автоматизация деятельности управляющей компании ЖКХ.

Система включает:

- веб-интерфейс диспетчера на ASP.NET Core;
- хранилище PostgreSQL для домов, квартир, жителей, приборов учета, показаний и уведомлений;
- авторизацию через Keycloak и OpenID Connect;
- MQTT-брокер Mosquitto;
- Python-симулятор LoRaWAN/IoT-устройств с mock gateway/decoder, публикующий декодированные показания через MQTT;
- SMTP-доставку через Mail.ru для настоящей отправки писем;
- SMS-доставку через SMS Aero;
- сообщения в личном кабинете жильца и Web Push через VAPID/service worker;
- настройки аварийных уведомлений в личном кабинете жильца;
- отчеты по потреблению ресурсов с фильтрами по квартире, типу прибора и выгрузкой в Word;
- модульные тесты для C#-логики приема показаний и Python-генератора сообщений;
- GitHub Actions для CI, проверки зависимостей, coverage-отчетов и CodeQL/SAST.
- техническую API-документацию в `docs/api.md`.

## Запуск

Перед запуском должен быть открыт Docker Desktop.

```powershell
docker compose -p smartuk up --build
```

Если нужно поднять стенд на полностью чистой базе, сначала выполните:

```powershell
docker compose -p smartuk down -v
docker compose -p smartuk up --build
```

Открыть:

- Веб-интерфейс: http://localhost:5000
- Keycloak: http://localhost:8080

Демо-пользователи:

- dispatcher / dispatcher
- admin / admin
- resident101 / resident101
- resident42 / resident42

`admin` выполняет административные операции: регистрации жильцов, квартиры, жильцы и аккаунты, приборы учета, отчеты, журнал действий и технические заявки на приборы. `dispatcher` работает с эксплуатационными заявками жильцов, справочником жильцов и уведомлениями. `resident101` и `resident42` открывают личный кабинет жильца, где доступны свои данные, сообщения от УК, приборы, показания и заявки.

## Тестирование

```powershell
docker run --rm -v "${PWD}:/workspace" -w /workspace mcr.microsoft.com/dotnet/sdk:8.0 dotnet test
```

Запуск .NET-тестов с отчетом покрытия:

```powershell
docker run --rm -v "${PWD}:/workspace" -w /workspace mcr.microsoft.com/dotnet/sdk:8.0 dotnet test tests/UkManagement.Tests/UkManagement.Tests.csproj --collect:"XPlat Code Coverage" --settings coverlet.runsettings --results-directory TestResults
```

```powershell
New-Item -ItemType Directory -Force -Path .tmp\pydeps | Out-Null
python -m pip install --target .tmp\pydeps -r src\iot-simulator\requirements-dev.txt
$env:PYTHONPATH=(Resolve-Path .tmp\pydeps).Path + ';' + (Resolve-Path src\iot-simulator).Path
python -m pytest -p no:cacheprovider src\iot-simulator\tests
```

Запуск Python-тестов с отчетом покрытия:

```powershell
$env:PYTHONPATH=(Resolve-Path .tmp\pydeps).Path + ';' + (Resolve-Path src\iot-simulator).Path
python -m coverage run --source src\iot-simulator\iot_simulator -m pytest -p no:cacheprovider src\iot-simulator\tests
python -m coverage report -m
python -m coverage xml -o TestResults\python-coverage.xml
```

Явное имя проекта Compose `smartuk` важно на этой машине, потому что Docker Compose не может автоматически получить корректное имя проекта из папки с кириллицей.

## Конфигурация и секреты

Реальные ключи и пароли не должны храниться в репозитории. Для локального запуска скопируйте `.env.example` в `.env` и заполните только те каналы, которые нужно проверить: SMTP Mail.ru, SMS Aero, VAPID и внутренний ключ симулятора.

Демо-логины Keycloak и пароль PostgreSQL в `docker-compose.yml` предназначены только для локального учебного стенда. Для промышленного запуска их необходимо заменить на уникальные значения и хранить во внешнем хранилище секретов.

## Уведомления

Email-канал работает через внешний SMTP. Для Mail.ru проект читает параметры из локального файла `.env`: хост, порт, TLS-режим, логин, пароль внешнего приложения, адрес и имя отправителя. Пример переменных лежит в `.env.example`, подробная инструкция - в `docs/mailru-smtp.md`.

SMS-канал работает через SMS Aero. Для реальной доставки нужен аккаунт SMS Aero, API-ключ, баланс и переменные `SMS_LOGIN`, `SMS_API_KEY`, `SMS_SENDER=SMSAero`. Инструкция находится в `docs/sms-aero.md`.

Сообщения в профиле жильца сохраняются в журнале уведомлений. Если житель разрешил уведомления в браузере, дополнительно срабатывает Web Push через VAPID и service worker. Инструкция по проверке находится в `docs/web-push.md`.

Житель может в личном кабинете включить или отключить автоматические аварийные уведомления по приборам и выбрать каналы: Email, SMS, сообщение в профиле с Web Push.

## Отчеты

Администратор формирует отчеты по потреблению коммунальных ресурсов за выбранный период. Доступны фильтры по квартире и типу прибора, предпросмотр на сайте, отчет по проблемным показаниям и выгрузка в Word-документ `.docx`.

## API и интеграции

Внутренние HTTP endpoint'ы, MQTT payload и схема LoRaWAN mock bridge описаны в `docs/api.md`.
