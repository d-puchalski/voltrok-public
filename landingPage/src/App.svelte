<script>
  import { onMount } from "svelte";
  import BackgroundEffects from "./components/BackgroundEffects.svelte";
  import SiteHeader from "./components/SiteHeader.svelte";
  import HeroSection from "./components/HeroSection.svelte";
  import BentoGrid from "./components/BentoGrid.svelte";
  import SiteFooter from "./components/SiteFooter.svelte";
  import { initLanguage } from "./lib/i18n.js";

  onMount(() => {
    initLanguage();
  });
</script>

<BackgroundEffects />
<SiteHeader />
<HeroSection />
<BentoGrid />
<SiteFooter />
