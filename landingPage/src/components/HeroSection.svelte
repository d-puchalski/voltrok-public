<script>
  import mapImage from "../img/map.webp";
  import { t } from "../lib/i18n.js";
</script>

<section class="hero z">
  <div class="wrap container">
    <div class="hero-content row g-4">
      <div class="hero-left">
        <div class="hero-eyebrow">
          {$t("hero.eyebrow")}
        </div>
        <h1>
          <span class="h1-white">{$t("hero.line1")}</span><br />
          <span class="h1-red">{$t("hero.line2")}</span><br />
          <span class="h1-amber">{$t("hero.line3")}</span>
        </h1>
        <p class="hero-desc">{$t("hero.description")}</p>
        <div class="hero-actions">
          <a href="https://game.voltrok.com" class="btn-primary">{$t("ui.startGame")}</a>
        </div>
      </div>
      <div class="hero-right">
        <div class="world-map-panel" aria-hidden="true">
          <div class="world-map-frame"></div>
          <div class="world-map-shade"></div>
          <div class="world-map-orbit world-map-orbit-a"></div>
          <div class="world-map-orbit world-map-orbit-b"></div>
          <img class="world-map-image" src={mapImage} alt={$t("hero.mapAlt")} />
          <div class="map-fallback">
            <span class="map-fallback-text">{$t("hero.mapFallback")}</span>
            <a href="https://game.voltrok.com" target="_blank" rel="noopener noreferrer"
              >{$t("ui.openMap")}</a
            >
          </div>
        </div>
      </div>
    </div>
  </div>
</section>
