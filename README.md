# URL Shortener

Сервис коротких ссылок на ASP.NET Core. Редиректы отдаются из Redis, клики не пишутся в PostgreSQL по одному, а собираются в памяти и сохраняются пачками через `BackgroundService`.

Проект писался как пет-проект под highload-сценарий: хотелось показать не только CRUD, но и работу с кэшем, очередью и батчевой записью.

## Стек

- .NET 11, ASP.NET Core Minimal API
- PostgreSQL 17 + EF Core 11 (Npgsql)
- Redis 7 (`IDistributedCache`, StackExchange.Redis)
- `System.Threading.Channels` для очереди кликов
- Swashbuckle / OpenAPI + Swagger UI
- Docker и Docker Compose

## Как это работает

### Создание ссылки

`POST /api/links` принимает оригинальный URL, опциональный кастомный код и опциональную дату истечения. Код генерируется в base62 длиной 7 символов через `RandomNumberGenerator`. Уникальность обеспечивает индекс `ux_short_links_code`: если при вставке случается конфликт, сервис пробует сгенерировать код ещё раз. Для кастомного кода конфликт сразу возвращается как `409`.

### Редирект и кэш

`GET /{code}` работает по схеме cache-aside:

1. Идём в Redis за ключом `urlshortener:link:{code}`.
2. При промахе читаем ссылку из PostgreSQL.
3. Кладём её в Redis с TTL, но не больше времени жизни самой ссылки.
4. Возвращаем `302` на оригинальный URL.

Если Redis недоступен, чтение и запись кэша не роняют редирект: ошибка логируется, запрос уходит в PostgreSQL. TTL истёкших ссылок не превышает `expires_at`, поэтому просроченная запись не может жить в кэше дольше самой ссылки. При деактивации ключ удаляется явно.

### Клики

Редирект не делает `INSERT` в базу. Событие клика кладётся в ограниченный `Channel<ClickRecord>` (по умолчанию 100 000 записей), а `ClickFlushWorker` раз в секунду или при наборе 500 событий:

- вставляет клики одним `SaveChanges`;
- агрегирует количество по каждой ссылке;
- одним SQL-запросом обновляет `click_count` и `last_accessed_at` через `unnest` массивов;
- делает всё это в транзакции.

Очередь не блокирует HTTP-поток. Если она переполнена, клик отбрасывается и увеличивается счётчик потерь, а в лог пишется предупреждение. При остановке приложения воркер пытается дописать остаток очереди в течение 10 секунд.

Из-за батчинга статистика может отставать на секунду — это осознанный компромисс.

## API

| Метод | Путь | Что делает |
|---|---|---|
| `POST` | `/api/links` | Создать короткую ссылку |
| `GET` | `/api/links/{code}` | Получить информацию о ссылке |
| `GET` | `/api/links/{code}/stats` | Статистика: всего кликов и за последние 24 часа |
| `DELETE` | `/api/links/{code}` | Деактивировать ссылку |
| `GET` | `/{code}` | Редирект на оригинальный URL |
| `GET` | `/health` | Общий health check |
| `GET` | `/health/ready` | Проверка PostgreSQL и Redis |
| `GET` | `/swagger` | Swagger UI |

Создание ссылки:

```bash
curl -X POST http://localhost:8080/api/links \
  -H "Content-Type: application/json" \
  -d '{"originalUrl":"https://example.com/some/long/path","customCode":"example"}'
```

```json
{
  "id": "c6c45aa8-e7fb-4c0f-aff5-e314bd035296",
  "code": "example",
  "shortUrl": "http://localhost:8080/example",
  "originalUrl": "https://example.com/some/long/path",
  "createdAt": "2026-11-20T16:25:12.055095+00:00",
  "isActive": true,
  "clickCount": 0
}
```

Проверка редиректа:

```bash
curl -i http://localhost:8080/example
```

```text
HTTP/1.1 302 Found
Location: https://example.com/some/long/path
```

Статистика:

```bash
curl http://localhost:8080/api/links/example/stats
```

```json
{
  "code": "example",
  "originalUrl": "https://example.com/some/long/path",
  "totalClicks": 10,
  "clicksLast24Hours": 10,
  "createdAt": "2026-11-20T16:25:12.055095+00:00",
  "lastAccessedAt": "2026-11-20T16:25:13.281472+00:00",
  "isActive": true
}
```

Ошибки возвращаются в формате Problem Details. Валидация — `400`, занятый код — `409`, отсутствующая или просроченная ссылка — `404`.

## Запуск через Docker

```bash
cp .env.example .env
docker compose up -d --build
```

Поднимаются три контейнера: API, PostgreSQL и Redis. Миграции применяются при старте API, отдельная команда не нужна.

- API и Swagger UI: http://localhost:8080/swagger
- Health check: http://localhost:8080/health
- PostgreSQL: `localhost:5432`
- Redis: `localhost:6379`

Пароль к базе и публичный адрес можно переопределить в `.env`:

```dotenv
POSTGRES_PASSWORD=postgres
PUBLIC_BASE_URL=http://localhost:8080
```

`PUBLIC_BASE_URL` нужен, чтобы в ответе API был правильный короткий адрес. Если переменная пустая, берётся схема и хост из текущего запроса — это удобно локально, но за reverse proxy лучше задать значение явно.

Остановить:

```bash
docker compose down
```

Удалить данные вместе с томами:

```bash
docker compose down -v
```

## Локальный запуск

Нужен .NET SDK 11. PostgreSQL и Redis можно поднять только сервисами из compose:

```bash
docker compose up -d postgres redis
dotnet run --project src/UrlShortener.Api
```

Приложение возьмёт строки подключения из `appsettings.json` (`localhost:5432` и `localhost:6379`) и применит миграции. Swagger будет доступен на порту из `launchSettings.json`: http://localhost:5128/swagger.

Если менялась модель данных, миграция создаётся так:

```bash
dotnet ef migrations add NameOfMigration \
  --project src/UrlShortener.Infrastructure \
  --startup-project src/UrlShortener.Api \
  --output-dir Persistence/Migrations
```

## Конфигурация

| Ключ | По умолчанию | Назначение |
|---|---|---|
| `ConnectionStrings__Postgres` | `Host=localhost;...` | Подключение к PostgreSQL |
| `ConnectionStrings__Redis` | `localhost:6379` | Подключение к Redis |
| `ShortLinks__CodeLength` | `7` | Длина генерируемого кода |
| `ShortLinks__MinCustomCodeLength` | `4` | Минимальная длина кастомного кода |
| `ShortLinks__MaxCustomCodeLength` | `16` | Максимальная длина кастомного кода |
| `ShortLinks__CacheTtl` | `01:00:00` | Базовый TTL ссылки в Redis |
| `ShortLinks__PublicBaseUrl` | пусто | Базовый адрес для коротких ссылок |
| `ClickIngestion__QueueCapacity` | `100000` | Размер очереди кликов |
| `ClickIngestion__BatchSize` | `500` | Максимальный размер батча |
| `ClickIngestion__FlushIntervalMs` | `1000` | Как долго ждать перед сбросом неполного батча |

Кастомный код: от 4 до 16 символов, только латиница, цифры, `_` и `-`. Оригинальный URL — абсолютный, только `http` или `https`, до 2048 символов.

## Структура

```text
src/
  UrlShortener.Domain/          сущности ShortLink и ClickEvent, правила жизненного цикла
  UrlShortener.Application/     DTO, порты, LinkService, исключения
  UrlShortener.Infrastructure/  EF Core, репозитории, Redis, Channel, фоновый воркер
  UrlShortener.Api/             Program.cs, эндпоинты, обработка ошибок, health checks
```

Зависимости направлены внутрь: `Api -> Infrastructure -> Application -> Domain`. Application не знает про EF Core и Redis, ему доступны только интерфейсы `ILinkRepository`, `ILinkCache`, `IClickQueue` и остальные порты.

## Схема данных

`short_links`

| Колонка | Тип | Комментарий |
|---|---|---|
| `id` | `uuid` | PK |
| `code` | `varchar(16)` | Уникальный код |
| `original_url` | `varchar(2048)` | Куда редиректим |
| `created_at` | `timestamptz` | Время создания |
| `expires_at` | `timestamptz` | Может быть `NULL` |
| `is_active` | `boolean` | Деактивация без удаления |
| `click_count` | `bigint` | Денормализованный счётчик |
| `last_accessed_at` | `timestamptz` | Обновляет воркер |

`click_events`

| Колонка | Тип | Комментарий |
|---|---|---|
| `id` | `bigint` | Identity PK |
| `link_id` | `uuid` | FK на `short_links`, `ON DELETE CASCADE` |
| `occurred_at` | `timestamptz` | Время клика |
| `ip_address` | `varchar(45)` | Хватает и для IPv6 |
| `user_agent` | `varchar(512)` | Обрезается при постановке в очередь |
| `referrer` | `varchar(2048)` | Обрезается при постановке в очередь |

Индексы:

- `ux_short_links_code` — уникальный, обслуживает и поиск по коду, и защиту от коллизий;
- `ix_short_links_active_expires` — для выборок активных/просроченных ссылок;
- `ix_click_events_link_id_occurred_at` — для статистики по ссылке за период;
- `ix_click_events_occurred_at` — для будущей очистки старых событий.

## Решения и компромиссы

Redis выбран как общий кэш, а не in-memory: при нескольких инстансах API они видят одинаковые данные, а рестарт приложения не сбрасывает кэш. Ошибки Redis не являются критичными для редиректа, потому что база остаётся источником истины.

Очередь в памяти сознательно ограничена. Это защита от OOM при всплеске: лучше потерять часть аналитики, чем уронить процесс. Счётчик потерь и предупреждения в логе позволяют заметить проблему. Для сценария, где терять клики нельзя, очередь нужно выносить во внешний брокер.

Батчевая вставка уменьшает число транзакций и нагрузку на WAL PostgreSQL. Счётчики обновляются отдельным запросом через `unnest`, поэтому на батч из 500 кликов по 200 ссылкам не будет 200 отдельных `UPDATE`.

Короткие коды не шифруются и не подписываются: это обычные идентификаторы, а не секреты. Приватность ссылок не гарантируется, для этого понадобился бы отдельный режим с паролем или токеном.

## Ограничения

- Очередь кликов живёт в памяти одного инстанса. При падении процесса несохранённые события теряются.
- Горизонтальное масштабирование API требует внешней очереди (Kafka, RabbitMQ, Redis Streams) либо партиционирования по инстансам.
- Нет аутентификации и rate limiting: любой может создавать ссылки.
- При холодном кэше популярная ссылка даст всплеск запросов в PostgreSQL. Для этого стоит добавить single-flight/распределённую блокировку.
- Нет очистки старых `click_events`; для долгой эксплуатации нужен retention-джоб или партиционирование по дате.

## Что можно добавить

- API-ключи и квоты на создание ссылок;
- кэширование отрицательных ответов и single-flight при промахе;
- метрики Prometheus (cache hit ratio, размер батча, потери очереди);
- интеграционные тесты с Testcontainers;
- экспорт статистики по дням и реферрерам;
- удаление старых кликов по расписанию.
