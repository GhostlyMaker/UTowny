# Chat Translator for OpenMod / Unturned

Automatic per-recipient English <-> Korean chat translation using DeepL.

## Player commands

- `/lang en` - receive chat in English
- `/lang ko` - receive chat in Korean

Language preferences are persisted by Steam64 ID.

## Configuration

After installing the plugin, edit its generated `config.yaml` and set:

```yaml
deepl:
  api_key: "YOUR_DEEPL_API_KEY"
  use_free_api: true

translator:
  default_language: "EN"
  show_original: false
  prefix: ""
```

Use `use_free_api: true` for a DeepL API Free / Developer key. Set it to `false` for the paid DeepL API endpoint.

## Behavior

If a Korean player (`/lang ko`) sends a message to an English player (`/lang en`), only the English recipient gets the translated English text. The Korean sender keeps the original Korean text. The same works in reverse.

If DeepL is unavailable or the API key is missing/invalid, the original message is shown rather than blocking chat.
