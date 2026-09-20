<script>
  import { t } from "../lib/i18n.js";

  const year = new Date().getFullYear();
</script>

<footer class="z">
  <div class="wrap container">
    <div class="footer-inner d-flex">
      <div class="footer-copy">
        ©{year} <span>{$t("footer.brand")}</span> - <span>{$t("ui.rightsReserved")}</span>
      </div>
      <div class="footer-links">
        <a href="mailto:info@voltrok.com">info@voltrok.com</a>
      </div>
    </div>
  </div>
</footer>
