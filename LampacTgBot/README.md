# LampacTgBot

Телеграм-бот, отвечающий исключительно по репозиторию [immisterio/Lampac](https://github.com/immisterio/Lampac) с использованием .NET 8, Telegram.Bot и официального OpenAI .NET SDK.

## Запуск

```bash
export TELEGRAM_BOT_TOKEN="<ваш_токен>"
export OPENAI_API_KEY="<ключ_openai>"

cd LampacTgBot
(dotnet restore)
dotnet run
```

При старте бот загружает и кэширует текстовые файлы Lampac, строит простую базу чанков и после этого готов принимать текстовые запросы по проекту.
