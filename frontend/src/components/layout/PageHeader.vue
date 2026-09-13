<script setup lang="ts">
/**
 * The top of every routed view: a title, optionally something beside it, optionally a line
 * under it.
 *
 * A component rather than a convention, because the convention had already failed. Six views
 * each wrote their own header and no two agreed: the dashboard put 24px under its title, the
 * settings page 8px, and the three list pages wrapped theirs in a flex row that collapsed on a
 * narrow screen because nothing told it to wrap. Here the arrangement is decided once.
 *
 * The `actions` slot sits on the title's line on a wide screen and drops below it on a narrow
 * one, which is why this is a wrapping flex row rather than `justify-space-between` on a fixed
 * pair - that arrangement pushed the action off the edge at 375px (FR-058).
 */
withDefaults(
  defineProps<{
    title: string
    /** One line under the title. Context, never a control. */
    subtitle?: string | null
    /** `h1` for a page, `h2` for a section within one. The size follows the level. */
    level?: 1 | 2
    /** Put on the heading itself, for the pages whose title a test addresses by name. */
    titleTestid?: string | null
  }>(),
  { subtitle: null, level: 1, titleTestid: null },
)
</script>

<template>
  <header class="rf-page-header">
    <div class="rf-page-header__row">
      <div class="rf-page-header__title">
        <component
          :is="level === 1 ? 'h1' : 'h2'"
          class="rf-title"
          :class="level === 1 ? 'text-h4' : 'text-h5'"
          :data-testid="titleTestid ?? undefined"
        >
          {{ title }}
        </component>

        <!-- A state that belongs *to the title* - "geschlossen" - rather than beside it. Kept in
             the header so the one page that needs it does not hand-roll its own title row and
             drift from the rest by a few pixels. -->
        <slot name="badge" />
      </div>

      <!-- Only rendered when a caller fills it, so an empty header keeps no reserved gap. -->
      <div v-if="$slots.actions" class="rf-page-header__actions">
        <slot name="actions" />
      </div>
    </div>

    <p v-if="subtitle" class="rf-page-header__subtitle text-body-2">{{ subtitle }}</p>

    <!-- Figures or context under the title, on the header's own rhythm. -->
    <slot name="meta" />
  </header>
</template>

<style scoped>
.rf-page-header {
  margin-block-end: var(--rf-section);
}

.rf-page-header__row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 12px 24px;
}

.rf-page-header__title {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 12px;
  min-width: 0;
}

.rf-page-header__actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.rf-page-header__subtitle {
  margin-block-start: 6px;
  color: rgb(var(--v-theme-on-surface));
  opacity: 0.7;
  max-width: 68ch;
}
</style>
