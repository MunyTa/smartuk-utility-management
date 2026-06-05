# API SmartUK

Документ описывает внутренние HTTP API, которые используются сервисами SmartUK и браузером жильца. Основной пользовательский интерфейс работает через Razor Pages, поэтому публичного REST API для всех операций системы нет.

## Общие сведения

- Базовый адрес при локальном запуске: `http://localhost:5000`
- Формат данных: JSON
- Аутентификация пользователей: OpenID Connect через Keycloak
- Внутренний обмен показаниями: LoRaWAN mock bridge -> MQTT -> ASP.NET Core

## GET /health

Проверка доступности веб-приложения.

### Ответ 200

```json
{
  "status": "ok",
  "service": "uk-management-web"
}
```

## GET /api/simulator/meters

Внутренний endpoint для Python-симулятора приборов учета. Симулятор периодически получает каталог приборов, чтобы автоматически подхватывать новые устройства, добавленные администратором или через заявку на установку/замену прибора.

### Назначение

Endpoint не используется жителями, диспетчером или администратором напрямую. Он нужен только сервису `iot-simulator`.

### Аутентификация

Endpoint защищен внутренним API-ключом. Ключ хранится в `.env` как `SIMULATOR_API_KEY` и передается:

- в `web` как `SimulatorCatalog__ApiKey`;
- в `iot-simulator` как `SIMULATOR_API_KEY`.

Симулятор отправляет ключ в заголовке:

```http
X-Simulator-Api-Key: smartuk-local-simulator-token
```

### Ответ 200

```json
[
  {
    "deviceId": "meter-101-cold-water",
    "unit": "m3",
    "meterType": "ColdWater",
    "serialNumber": "CW-101-2026",
    "apartmentNumber": "101",
    "floor": 10,
    "lastValue": 184.235
  }
]
```

### Поля

- `deviceId` - технический идентификатор прибора, по нему сайт связывает MQTT-показание с прибором в базе.
- `unit` - единица измерения: `m3`, `kWh`, `Gcal`.
- `meterType` - тип прибора: холодная вода, горячая вода, электричество, газ, отопление.
- `serialNumber` - серийный номер прибора.
- `apartmentNumber` - номер квартиры.
- `floor` - этаж.
- `lastValue` - последнее сохраненное показание.

### Замечание по безопасности

Endpoint предназначен для локального Docker-стенда и защищен внутренним ключом. Для промышленного запуска demo-значение ключа нужно заменить на случайную секретную строку и не хранить ее в репозитории.

### Ответ 401

Если заголовок `X-Simulator-Api-Key` отсутствует или ключ неверный.

### Ответ 503

Если ключ не настроен на стороне web-приложения.

## GET /api/push/vapid-public-key

Возвращает публичный VAPID-ключ для подписки браузера жильца на Web Push.

### Ответ 200

```json
{
  "publicKey": "B..."
}
```

### Ответ 503

Если VAPID-ключи не настроены:

```json
{
  "title": "VAPID-ключи для Web Push не настроены.",
  "status": 503
}
```

## POST /api/push/subscriptions

Сохраняет Web Push подписку браузера для текущего авторизованного жильца.

### Аутентификация

Требуется вход пользователя с ролью `Resident`.

### Тело запроса

```json
{
  "endpoint": "https://push.example/browser-subscription-id",
  "p256dh": "browser-public-key",
  "auth": "browser-auth-secret"
}
```

### Ответ 200

```json
{
  "saved": true
}
```

### Ответ 400

Если подписка неполная или некорректная.

### Ответ 401/403

Если пользователь не вошел или не является жильцом.

## MQTT: uk/meters/readings

Сайт подписан на MQTT-тему `uk/meters/readings`. В эту тему публикует данные Python-сервис `iot-simulator` после декодирования LoRaWAN uplink frame.

### MQTT payload

```json
{
  "deviceId": "meter-101-cold-water",
  "value": 184.235,
  "unit": "m3",
  "measuredAt": "2026-06-05T12:00:00+00:00",
  "signalRssi": -72,
  "batteryVoltage": 3.42,
  "gatewayId": "smartuk-lorawan-gw-1",
  "devEui": "A1B2C3D4E5F60708",
  "frameCounter": 15
}
```

### Поля

- `deviceId` - идентификатор прибора в SmartUK.
- `value` - измеренное значение.
- `unit` - единица измерения.
- `measuredAt` - время измерения на приборе.
- `signalRssi` - уровень радиосигнала, полученный mock-шлюзом.
- `batteryVoltage` - напряжение батареи прибора.
- `gatewayId` - идентификатор LoRaWAN mock gateway.
- `devEui` - эмулированный LoRaWAN DevEUI прибора.
- `frameCounter` - счетчик LoRaWAN uplink-пакетов.

### Обработка на сервере

1. `MqttReadingConsumer` получает сообщение из MQTT.
2. Сообщение десериализуется в `MeterReadingPayload`.
3. `MeterReadingIngestionService` ищет прибор по `deviceId`.
4. Система проверяет дубли, отрицательные значения, единицы измерения и резкие скачки.
5. Показание сохраняется в PostgreSQL.
6. Последнее значение прибора обновляется.
7. При аномалии прибор получает статус предупреждения.
8. Система отправляет аварийные уведомления только по тем каналам, которые выбрал житель в личном кабинете.

## LoRaWAN mock bridge

Для курсового стенда физические LoRaWAN-счетчики заменены Python-эмулятором. Внутри эмулятора есть два слоя:

1. Генератор показаний умных приборов.
2. LoRaWAN mock bridge, который формирует uplink frame и декодирует payload перед публикацией в MQTT.

Пример LoRaWAN frame внутри симулятора:

```json
{
  "devEui": "A1B2C3D4E5F60708",
  "gatewayId": "smartuk-lorawan-gw-1",
  "fPort": 10,
  "frameCounter": 15,
  "payloadBase64": "AQAAAAAAAtA...",
  "receivedAt": "2026-06-05T12:00:01+00:00",
  "rssi": -72,
  "snr": 8.4
}
```

Это позволяет показать полный поток:

```text
умный прибор -> LoRaWAN uplink -> mock gateway/decoder -> MQTT -> веб-приложение -> PostgreSQL
```
