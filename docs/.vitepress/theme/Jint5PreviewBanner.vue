<script setup lang="ts">
import { withBase } from "vitepress";

const previewPackagesUrl = withBase("/guide/preview-packages");
</script>

<template>
  <aside class="jint-preview-banner" aria-label="Jint 5 preview notice">
    <strong>Jint 5 preview</strong>
    <span>
      This section documents APIs planned for Jint 5 that are not available in
      stable Jint 4 packages.
      <a :href="previewPackagesUrl">Install the preview packages</a>
      before trying these examples.
    </span>
  </aside>
</template>
