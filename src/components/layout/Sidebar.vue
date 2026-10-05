<script setup lang="ts">
import { computed } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import {
  LayoutDashboard,
  FolderLock,
  FolderCheck,
  ShieldCheck,
  History,
  LifeBuoy,
  RefreshCw,
  Settings,
  Info,
  ShieldAlert,
} from 'lucide-vue-next';
import { useSecurityStore } from '../../stores/security';
import { useFolderStore } from '../../stores/folders';
import { useUpdateStore } from '../../stores/updates';
import logoUrl from '../../assets/branding/secapper-logo.png';

const route = useRoute();
const router = useRouter();
const securityStore = useSecurityStore();
const folderStore = useFolderStore();
const updateStore = useUpdateStore();

const currentVersion = computed(() => securityStore.status.version || '1.1.0');
const hasUpdate = computed(() => updateStore.updateInfo.hasUpdate);
const lockedCount = computed(() => folderStore.lockedCount);
const hasAttention = computed(() => securityStore.hasAttention);

interface NavItem {
  name: string;
  path: string;
  icon: any;
  badge?: () => string | null;
}

const securityNavItems: NavItem[] = [
  {
    name: 'Dashboard',
    path: '/dashboard',
    icon: LayoutDashboard,
  },
  {
    name: 'Folder Locker',
    path: '/folder-locker',
    icon: FolderLock,
  },
  {
    name: 'Protected Folders',
    path: '/protected-folders',
    icon: FolderCheck,
    badge: () => (lockedCount.value > 0 ? lockedCount.value.toString() : null),
  },
  {
    name: 'Ransomware Protection',
    path: '/ransomware',
    icon: ShieldCheck,
  },
  {
    name: 'Security Events',
    path: '/security-events',
    icon: History,
  },
  {
    name: 'Recovery',
    path: '/recovery',
    icon: LifeBuoy,
    badge: () => (securityStore.status.hasRecoveryIssues ? '!' : null),
  },
];

const systemNavItems: NavItem[] = [
  {
    name: 'Updates',
    path: '/updates',
    icon: RefreshCw,
    badge: () => (hasUpdate.value ? 'New' : null),
  },
  {
    name: 'Settings',
    path: '/settings',
    icon: Settings,
  },
  {
    name: 'About',
    path: '/about',
    icon: Info,
  },
];

function navigate(path: string) {
  router.push(path);
}

function isActive(path: string) {
  return route.path === path || (path !== '/dashboard' && route.path.startsWith(path));
}
</script>

<template>
  <aside
    class="w-[250px] shrink-0 h-full flex flex-col justify-between bg-[#091D38] border-r border-[#102747] text-white select-none relative z-20"
  >
    <!-- Brand Header -->
    <div class="pt-5 px-5 pb-4 border-b border-[#122D55]/60 bg-[#06152A]/40">
      <div class="flex items-center justify-center">
        <!-- Official Logo supplied by brand - DO NOT stretch, recolor, or distort -->
        <img
          :src="logoUrl"
          alt="SecApper — Secure Your World"
          class="h-10 w-auto object-contain cursor-pointer transition-opacity hover:opacity-95"
          @click="navigate('/dashboard')"
        />
      </div>
      <div class="text-[10px] tracking-[0.2em] font-semibold text-center text-[#94A3B8] uppercase mt-2">
        Secure Your World
      </div>
    </div>

    <!-- Navigation Scroll Area -->
    <div class="flex-1 overflow-y-auto py-4 px-2 space-y-6">
      <!-- Section: SECURITY -->
      <div>
        <div class="px-3 pb-2 text-[10px] font-bold tracking-wider text-[#64748B] uppercase">
          Security
        </div>
        <nav class="space-y-0.5">
          <button
            v-for="item in securityNavItems"
            :key="item.path"
            @click="navigate(item.path)"
            :class="[
              'w-full flex items-center justify-between px-3 py-2 text-[13px] rounded-md transition-all duration-200 group text-left relative',
              isActive(item.path)
                ? 'bg-[#122D55] text-white font-medium shadow-sm'
                : 'text-[#94A3B8] hover:bg-[#0F284B] hover:text-[#F8FAFC]'
            ]"
          >
            <!-- Left Red Indicator for Active Tab -->
            <div
              v-if="isActive(item.path)"
              class="absolute left-0 top-1.5 bottom-1.5 w-1 bg-[#C5202B] rounded-r"
            ></div>

            <div class="flex items-center gap-2.5 truncate pl-1">
              <component
                :is="item.icon"
                :class="[
                  'w-4 h-4 shrink-0 transition-colors',
                  isActive(item.path) ? 'text-white' : 'text-[#64748B] group-hover:text-white'
                ]"
              />
              <span class="truncate">{{ item.name }}</span>
            </div>

            <!-- Optional Badge -->
            <span
              v-if="item.badge && item.badge()"
              :class="[
                'text-[10px] font-semibold px-1.5 py-0.5 rounded-full shrink-0',
                item.badge() === '!'
                  ? 'bg-[#C5202B] text-white'
                  : 'bg-[#163765] text-[#93C5FD]'
              ]"
            >
              {{ item.badge() }}
            </span>
          </button>
        </nav>
      </div>

      <!-- Section: SYSTEM -->
      <div>
        <div class="px-3 pb-2 text-[10px] font-bold tracking-wider text-[#64748B] uppercase">
          System
        </div>
        <nav class="space-y-0.5">
          <button
            v-for="item in systemNavItems"
            :key="item.path"
            @click="navigate(item.path)"
            :class="[
              'w-full flex items-center justify-between px-3 py-2 text-[13px] rounded-md transition-all duration-200 group text-left relative',
              isActive(item.path)
                ? 'bg-[#122D55] text-white font-medium shadow-sm'
                : 'text-[#94A3B8] hover:bg-[#0F284B] hover:text-[#F8FAFC]'
            ]"
          >
            <div
              v-if="isActive(item.path)"
              class="absolute left-0 top-1.5 bottom-1.5 w-1 bg-[#C5202B] rounded-r"
            ></div>

            <div class="flex items-center gap-2.5 truncate pl-1">
              <component
                :is="item.icon"
                :class="[
                  'w-4 h-4 shrink-0 transition-colors',
                  isActive(item.path) ? 'text-white' : 'text-[#64748B] group-hover:text-white'
                ]"
              />
              <span class="truncate">{{ item.name }}</span>
            </div>

            <span
              v-if="item.badge && item.badge()"
              class="text-[10px] font-bold px-1.5 py-0.5 rounded-full bg-[#C5202B] text-white shrink-0"
            >
              {{ item.badge() }}
            </span>
          </button>
        </nav>
      </div>
    </div>

    <!-- Bottom Status Card -->
    <div class="p-3 m-2.5 bg-[#06152A] rounded-lg border border-[#14325C]/80">
      <div class="flex items-center justify-between mb-1.5">
        <div class="flex items-center gap-1.5">
          <span
            :class="[
              'w-2 h-2 rounded-full shrink-0',
              hasAttention ? 'bg-[#C5202B] animate-pulse' : 'bg-[#12B76A]'
            ]"
          ></span>
          <span class="text-xs font-semibold text-white">SecApper Security</span>
        </div>
        <span class="text-[11px] font-mono text-[#64748B]">v{{ currentVersion }}</span>
      </div>
      <div class="text-[11px] text-[#94A3B8] flex items-center justify-between">
        <span>Protected locally</span>
        <span class="text-[10px] text-[#12B76A] font-medium">NTFS Active</span>
      </div>
    </div>
  </aside>
</template>
