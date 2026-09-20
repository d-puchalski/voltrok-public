<script>
  import { t } from "../lib/i18n.js";
</script>

<div class="marquee-wrap z">
  <div class="marquee-inner">
    {#each [...$t("marquee"), ...$t("marquee")] as item}
      <span class="mi">{item}</span><span class="mi-dot">◆</span>
    {/each}
  </div>
</div>
