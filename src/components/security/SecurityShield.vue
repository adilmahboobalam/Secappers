<script setup lang="ts">
import { computed } from 'vue';
import shieldUrl from '../../assets/branding/secapper-shield.png';

interface Props {
  isProtected?: boolean;
  size?: 'sm' | 'md' | 'lg' | 'xl';
}

const props = withDefaults(defineProps<Props>(), {
  isProtected: true,
  size: 'lg',
});

const sizeClasses = computed(() => {
  switch (props.size) {
    case 'sm':
      return 'w-10 h-10 p-1.5';
    case 'md':
      return 'w-16 h-16 p-2.5';
    case 'xl':
      return 'w-32 h-32 p-4';
    case 'lg':
    default:
      return 'w-24 h-24 p-3.5';
  }
});
</script>

<template>
  <div class="relative flex items-center justify-center">
    <!-- Subtle Ambient Glow -->
    <div
      :class="[
        'absolute inset-0 rounded-full blur-xl transition-all duration-500 opacity-20',
        isProtected ? 'bg-[#12B76A]' : 'bg-[#C5202B]'
      ]"
    ></div>

    <!-- Outer Protective Ring -->
    <div
      :class="[
        'relative rounded-full flex items-center justify-center border transition-all duration-300 shadow-shield',
        sizeClasses,
        isProtected
          ? 'bg-gradient-to-b from-[#122D55] to-[#091D38] border-[#1E427B]/60'
          : 'bg-gradient-to-b from-[#C5202B] to-[#781219] border-[#E03A46]/60'
      ]"
    >
      <!-- Official SecApper Shield Icon -->
      <img
        :src="shieldUrl"
        alt="SecApper Security Shield"
        class="w-full h-full object-contain filter drop-shadow select-none pointer-events-none"
      />
    </div>
  </div>
</template>
