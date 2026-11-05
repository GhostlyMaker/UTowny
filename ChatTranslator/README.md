# Chat Translator for OpenMod / Unturned

Automatic per-recipient English, Korean, and Spanish player-chat translation using DeepL.

## Player commands

- `/lang en` — receive incoming chat in English (the server default)
- `/lang ko` — receive incoming chat in Korean
- `/lang es` — receive incoming chat in Spanish
- `/lang off` — disable incoming translation

Only the person who wants Spanish or Korean needs to use a command. Preferences are saved by Steam64 ID, including `off`, and survive server restarts. Command permission: `ChatTranslator:commands.lang`.

## Configuration

Edit the plugin's `config.yaml` and enter your DeepL API key:

```yaml
deepl:
  api_key: "YOUR_DEEPL_API_KEY"
  use_free_api: true

translator:
  default_language: "EN"
  show_original: false
  prefix: ""
```

Keep `default_language: "EN"` for your server so everyone automatically receives Korean and Spanish messages in English. Use `use_free_api: false` for the paid DeepL endpoint. Do not publish your real API key.

## How it works

- English-speaking players do not need a command. Korean or Spanish chat is translated into English for them.
- The Korean speaker runs `/lang ko`, and receives English or Spanish chat translated into Korean.
- The Spanish speaker runs `/lang es`, and receives English or Korean chat translated into Spanish.
- The sender sees their own original messages; only recipients' messages are modified.
- Untranslated messages and player commands are never sent to DeepL unless a recipient needs translation.
- The plugin recognizes Hangul for Korean. English and Spanish both use the Latin alphabet, so players writing Spanish should select `/lang es`; that preference tells the plugin the source language when they type Spanish.
- Existing chat recipient routing (global/team/local) is kept. Non-player/server messages are left alone.
- Translations are cached per message and direction to reduce repeated API calls.
- If DeepL fails, the original message is shown and error warnings are rate-limited.

Built against OpenMod.Unturned 3.8.10. Compilation is tested by GitHub Actions; verify behavior on your live Unturned server.
