<script>
  import { IconPackage, IconScale, IconSatellite, IconUsersGroup } from "@tabler/icons-svelte";
  import { t } from "../lib/i18n.js";
</script>

<section id="bento" class="bento z wrap container">
  <div class="row g-4">
    <div class="col-12 col-md-6 col-lg-6">
      <div class="bcard bc-loop bc-dark h-100">
        <div class="bc-watermark">01</div>
        <div class="bc-topline">
          <div class="bc-tall-number">01</div>
          <div class="bc-icon"><IconUsersGroup size={24} stroke={1.8} /></div>
        </div>
        <div class="bc-card-title">{$t("cards.coop.title")}</div>
        <div class="bc-divider"></div>
        <ul class="bc-list">
          {#each $t("cards.coop.items") as item}
            <li>{item}</li>
          {/each}
        </ul>
      </div>
    </div>

    <div class="col-12 col-md-6 col-lg-6">
      <div class="bcard bc-economy bc-accent flex-col h-100">
        <div class="bc-watermark">02</div>
        <div class="bc-topline">
          <div class="bc-tall-number">02</div>
          <div class="bc-icon amber"><IconPackage size={24} stroke={1.8} /></div>
        </div>
        <div class="bc-card-title bc-card-title-md">{$t("cards.economy.title")}</div>
        <div class="bc-divider bc-divider-amber"></div>
        <ul class="bc-list amber fill">
          {#each $t("cards.economy.items") as item}
            <li>{item}</li>
          {/each}
        </ul>
      </div>
    </div>

    <div class="col-12 col-md-6 col-lg-6">
      <div class="bcard bc-politics bc-politics-card flex-col">
        <div class="bc-watermark bc-watermark-violet">03</div>
        <div class="bc-topline">
          <div class="bc-tall-number amber">03</div>
          <div class="bc-icon violet"><IconScale size={24} stroke={1.8} /></div>
        </div>
        <div class="bc-card-title bc-card-title-md">{$t("cards.politics.title")}</div>
        <div class="bc-divider bc-divider-violet"></div>
        <ul class="bc-list violet fill">
          {#each $t("cards.politics.items") as item}
            <li>{item}</li>
          {/each}
        </ul>
      </div>
    </div>

    <div class="col-12 col-md-6 col-lg-6">
      <div class="bcard bc-military bc-dark h-100">
        <div class="bc-watermark">04</div>
        <div class="bc-topline">
          <div class="bc-tall-number">04</div>
          <div class="bc-icon green"><IconSatellite size={24} stroke={1.8} /></div>
        </div>
        <div class="bc-card-title">{$t("cards.military.title")}</div>
        <div class="bc-divider bc-divider-green"></div>
        <ul class="bc-list green">
          {#each $t("cards.military.items") as item}
            <li>{item}</li>
          {/each}
        </ul>
      </div>
    </div>

    <div class="col-12">
      <div class="bcard bc-faq bc-dark">
        <div class="bc-section-label">{$t("ui.faqLabel")}</div>
        <div class="bc-card-title bc-card-title-md">{$t("ui.faqTitle")}</div>
        <div class="bc-faq-list">
          {#each $t("faq.items") as item}
            <details>
              <summary>{item.q}<span class="fchev">+</span></summary>
              <p>{item.a}</p>
            </details>
          {/each}
        </div>
      </div>
    </div>
  </div>
</section>
