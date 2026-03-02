В репозитории есть два набора приложений:

- Static - “жёстко” ориентирован на сущность `Client` и таблицу клиентов.
- Dynamic (опционально) - универсальные утилиты, где структура таблицы описывается в конфиге (`DynamicSchema`) и может применяться к БД автоматически.


## Конфигурация

У каждого запускаемого проекта есть свой `appsettings.json` с подключением к БД:

```json
"ConnectionStrings": {
  "Default": "Server=...;Database=...;Trusted_Connection=True;..."
}
```
### Static/ExcelToSqlClients.Import.Console (консольный импорт)


```bash
dotnet run --project Static/ExcelToSqlClients.Import.Console -- -f "C:\path\clients.xlsx"
```

Опции:

- `-f, --file` — путь к Excel-файлу (**обязательно**)
- `-b, --batch` — размер батча (если не указан — берётся из `Import:BatchSize` в `appsettings.json`)

Пример с батчем:

```bash
dotnet run --project Static/ExcelToSqlClients.Import.Console -- -f "C:\path\clients.xlsx" -b 2000
```

---

### Static/ExcelToSqlClients.Desktop.WPF (WPF-редактор клиентов)

Что умеет (по UI/логике ViewModel):

- поиск по строке `Search`
- загрузка данных порциями (страницами)
- сохранение изменений кнопкой **Save**
- обновление списка кнопкой **Reload**

**Запуск:**

```bash
dotnet run --project Static/ExcelToSqlClients.Desktop.WPF
```

---

## Dynamic (опционально)


### Dynamic/ExcelToSqlClients.Dynamic.Import.Console (универсальный импорт по схеме)

Ключевая настройка в `appsettings.json` проекта:

```json
{
  "ConnectionStrings": {
    // Строка подключения к MS SQL Server
    "Default": "Server=localhost;Database=ClientsDb;User Id=sa;Password=Your_password123;TrustServerCertificate=True;"
  },

  "DynamicSchema": {
    // SQL schema (например: dbo, sales, import)
    "Schema": "dbo",

    // Имя целевой таблицы, куда будет идти импорт
    "Table": "Clients",

    // Описание колонок таблицы
    "Columns": [
      {
        // Имя колонки в SQL-таблице
        "Name": "CardCode",

        // SQL-тип колонки (как в DDL): int, bigint, nvarchar(200), decimal(18,2), datetime2 и т.п
        "SqlType": "nvarchar(50)",
        // Разрешено ли NULL
        "Nullable": false,
        // Является ли колонка частью первичного ключа
        "IsPrimaryKey": true,
        // Должны ли значения быть уникальными (уникальный индекс/ограничение)
        "IsUnique": true
      }...
      
    ]
  },

  "Import": {
    // Размер пачки записей, вставляемых/обрабатываемых за одну итерацию
    "BatchSize": 1000,

    // Продолжать импорт при ошибках в строках/значениях (true) или падать сразу (false)
    "IgnoreErrors": false
  },

  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```


**Запуск:**

```bash
dotnet run --project Dynamic/ExcelToSqlClients.Dynamic.Import.Console -- -f "C:\path\clients.xlsx"
```

Опции:

- `-f, --file` — путь к Excel-файлу (**обязательно**)
- `-b, --batch` — размер батча (если не указан — берётся из `Import:BatchSize`)
- `--ignore-errors` — игнорировать ошибки импорта и продолжать

Пример:

```bash
dotnet run --project Dynamic/ExcelToSqlClients.Dynamic.Import.Console -- -f "C:\path\clients.xlsx" -b 1000 --ignore-errors
```

---

### Dynamic/ExcelToSqlClients.Dynamic.Desktop.WPF (универсальный SQL viewer/editor)

Поведение:

- показывает список таблиц (**Refresh tables**)
- грузит данные страницами
- **редактирование доступно только если у таблицы есть PK**
- PK/Identity/Computed колонки помечаются как read-only

**Запуск:**

```bash
dotnet run --project Dynamic/ExcelToSqlClients.Dynamic.Desktop.WPF
```

---

## Библиотеки

### ExcelToSqlClients.Core
Контракты/абстракции, модели и сущности.

### ExcelToSqlClients.Infrastructure
Инфраструктурный слой (DI, доступ к БД, чтение Excel, импортеры/репозитории и т.п.). Подключается через:
`services.AddInfrastructure(configuration);`

---

## Быстрый старт

1) Настройте `ConnectionStrings:Default` в нужном `appsettings.json` проекта, который запускаете.  
2) Для импорта:
   - Static: запустите `Static/ExcelToSqlClients.Import.Console` с `-f`.
   - Dynamic (опционально): заполните `DynamicSchema` и запустите `Dynamic/...Import.Console` с `-f` .
3) Для проверки/правок данных:
   - Static: `Static/...Desktop.WPF`
   - Dynamic (опционально): `Dynamic/...Desktop.WPF`
