<script>
  import logo from "../img/logo.png";
  import { language, langs, setLanguage, t } from "../lib/i18n.js";

  const languageLabels = {
    en: "English",
    de: "Deutsch",
    es: "Español",
    fr: "Français",
    it: "Italiano",
    pt: "Português",
    ro: "Română",
    ru: "Русский",
    pl: "Polski",
    zh: "中文",
    hi: "हिन्दी",
    ar: "العربية",
    bn: "বাংলা",
    ur: "اردو",
  };
</script>

<header class="z">
  <div class="wrap container">
    <div class="nav-inner d-flex align-items-center">
      <a href="/" class="brand">
        <div class="brand-mark">
          <img src={logo} alt="Voltrok" />
        </div>
      </a>
      <div class="lang-wrap" style="margin-left:auto">
        <label for="language-select">{$t("ui.languageLabel")}</label>
        <select
          id="language-select"
          value={$language}
          on:change={(event) => setLanguage(event.currentTarget.value)}
        >
          {#each langs as lang}
            <option value={lang}>{languageLabels[lang] ?? lang.toUpperCase()}</option>
          {/each}
        </select>
      </div>
      <a href="https://game.voltrok.com" class="btn-nav">{$t("ui.startGame")}</a>
    </div>
  </div>
</header>
