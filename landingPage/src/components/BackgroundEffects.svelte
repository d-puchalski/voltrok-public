<script>
  import { onMount } from "svelte";

  let canvas;

  onMount(() => {
    const ctx = canvas.getContext("2d");
    let width = 0;
    let height = 0;
    let sparks = [];
    let rafId = 0;

    function resize() {
      width = canvas.width = window.innerWidth;
      height = canvas.height = window.innerHeight;
      sparks = [];
      for (let i = 0; i < 120; i += 1) {
        sparks.push({
          x: Math.random() * width,
          y: Math.random() * height,
          r: Math.random() * 1.2 + 0.2,
          a: Math.random(),
          da: (0.003 + Math.random() * 0.005) * (Math.random() < 0.5 ? 1 : -1),
          vx: (Math.random() - 0.5) * 0.15,
          vy: -(Math.random() * 0.06 + 0.015),
          hue: Math.random() < 0.55 ? 0 : 35
        });
      }
    }

    function draw() {
      ctx.clearRect(0, 0, width, height);
      sparks.forEach((spark) => {
        spark.x += spark.vx;
        spark.y += spark.vy;
        spark.a += spark.da;
        if (spark.a < 0 || spark.a > 1) {
          spark.da *= -1;
        }
        if (spark.y < 0) spark.y = height;
        if (spark.x < 0) spark.x = width;
        if (spark.x > width) spark.x = 0;
        ctx.beginPath();
        ctx.arc(spark.x, spark.y, spark.r, 0, Math.PI * 2);
        ctx.fillStyle = `hsla(${spark.hue},90%,60%,${spark.a * 0.6})`;
        ctx.fill();
      });
    }

    function loop() {
      draw();
      rafId = requestAnimationFrame(loop);
    }

    resize();
    window.addEventListener("resize", resize);
    loop();

    return () => {
      window.removeEventListener("resize", resize);
      cancelAnimationFrame(rafId);
    };
  });
</script>

<canvas id="canvas" bind:this={canvas}></canvas>
<div class="bg-layer bg-vignette"></div>
<div class="bg-layer bg-top-glow"></div>
<div class="bg-layer bg-mid-glow"></div>
<div class="bg-layer bg-lines"></div>
<div class="bg-layer bg-grain"></div>
