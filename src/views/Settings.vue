<script setup lang="ts">
import { ref, watch } from 'vue';
import PageHeader from '../components/layout/PageHeader.vue';
import Button from '../components/common/Button.vue';
import ConfirmationDialog from '../components/dialogs/ConfirmationDialog.vue';
import { useSettingsStore } from '../stores/settings';
import { useSecurityStore } from '../stores/security';
import { useUserStore } from '../stores/user';
import { nativeBridge } from '../services/nativeBridge';
import { useToast } from '../composables/useToast';
import {
  Sliders,
  Shield,
  ShieldAlert,
  Bell,
  RefreshCw,
  Eye,
  KeyRound,
  CheckCircle2,
  Moon,
  Sun,
  Monitor,
  User,
  Cpu,
  Award,
  Sparkles,
  Copy,
  Check,
  RotateCcw,
} from 'lucide-vue-next';
import type { SecurityTier } from '../types';

const settingsStore = useSettingsStore();
const securityStore = useSecurityStore();
const userStore = useUserStore();
const toast = useToast();

const activeTab = ref<'General' | 'Profile' | 'Security' | 'Ransomware' | 'Notifications' | 'Updates' | 'Privacy'>('General');

const showPinDialog = ref(false);
const pinInput = ref('');
const isPinSubmitting = ref(false);

const copiedId = ref(false);
const isProfileSaving = ref(false);
const editUserName = ref(userStore.profile.userName);
const editUserRole = ref(userStore.profile.userRole);
const editSecurityTier = ref<SecurityTier>(userStore.profile.securityTier);

watch(() => userStore.profile, (newProf) => {
  if (newProf) {
    editUserName.value = newProf.userName;
    editUserRole.value = newProf.userRole;
    editSecurityTier.value = newProf.securityTier;
  }
}, { immediate: true, deep: true });

async function handleSaveProfile() {
  isProfileSaving.value = true;
  try {
    await userStore.updateProfile({
      userName: editUserName.value,
      userRole: editUserRole.value,
      securityTier: editSecurityTier.value,
    });
    toast.success('Profile Saved', 'Your dynamic security persona has been updated.');
  } catch (err: any) {
    toast.error('Update Failed', err.message);
  } finally {
    isProfileSaving.value = false;
  }
}

async function copyInstallId() {
  if (navigator.clipboard) {
    await navigator.clipboard.writeText(userStore.profile.installationId || '');
    copiedId.value = true;
    setTimeout(() => (copiedId.value = false), 2000);
  }
}

const tabs = [
  { id: 'General', label: 'General', icon: Sliders },
  { id: 'Profile', label: 'Dynamic Profile', icon: User },
  { id: 'Security', label: 'Security & PIN', icon: Shield },
  { id: 'Ransomware', label: 'Ransomware Protection', icon: ShieldAlert },
  { id: 'Notifications', label: 'Notifications', icon: Bell },
  { id: 'Updates', label: 'Updates', icon: RefreshCw },
  { id: 'Privacy', label: 'Privacy', icon: Eye },
] as const;

async function handleSetupPin() {
  if (!pinInput.value || pinInput.value.length < 4) {
    toast.error('Invalid PIN', 'Master PIN must be at least 4 digits.');
    return;
  }
  isPinSubmitting.value = true;
  try {
    await nativeBridge.pin.setup(pinInput.value);
    await settingsStore.updateSetting('masterPinConfigured', true);
    toast.success('Master PIN Configured', 'Your administrative master PIN is now active.');
    showPinDialog.value = false;
    pinInput.value = '';
  } catch (err: any) {
    toast.error('PIN Setup Failed', err.message);
  } finally {
    isPinSubmitting.value = false;
  }
}
</script>

<template>
  <div class="space-y-6 max-w-4xl mx-auto">
    <!-- Header -->
    <PageHeader
      title="Settings"
      subtitle="Configure application security, privacy, and system preferences."
    />

    <div class="grid grid-cols-1 md:grid-cols-4 gap-6">
      <!-- Secondary Settings Navigation (Section 20) -->
      <div class="space-y-1 select-none">
        <button
          v-for="tab in tabs"
          :key="tab.id"
          @click="activeTab = tab.id"
          :class="[
            'w-full flex items-center gap-2.5 px-3.5 py-2.5 rounded-lg text-xs font-semibold text-left transition-all',
            activeTab === tab.id
              ? 'bg-[#122D55] text-white shadow-xs'
              : 'text-[#667085] dark:text-[#94A3B8] hover:bg-[#E4E7EC]/60 dark:hover:bg-[#1E293B] hover:text-[#101828] dark:hover:text-[#F8FAFC]'
          ]"
        >
          <component :is="tab.icon" class="w-4 h-4 shrink-0" />
          <span>{{ tab.label }}</span>
        </button>
      </div>

      <!-- Settings Content Panel -->
      <div class="md:col-span-3 space-y-4">
        <!-- GENERAL TAB -->
        <div v-if="activeTab === 'General'" class="sec-card p-6 bg-white dark:bg-[#0F1E30] space-y-6">
          <h3 class="text-sm font-bold text-[#101828] dark:text-[#F8FAFC] pb-3 border-b border-[#F2F4F7] dark:border-[#1E293B]">
            General System Preferences
          </h3>

          <!-- Row: Windows Startup -->
          <div class="flex items-center justify-between gap-4">
            <div>
              <div class="text-xs font-semibold text-[#101828] dark:text-[#F8FAFC]">Start with Windows</div>
              <div class="text-xs text-[#667085] dark:text-[#94A3B8]">Automatically start SecApper in the system tray when logging in.</div>
            </div>
            <button
              @click="settingsStore.updateSetting('startWithWindows', !settingsStore.settings.startWithWindows)"
              :class="[
                'w-11 h-6 flex items-center rounded-full p-1 transition-colors duration-200 cursor-pointer',
                settingsStore.settings.startWithWindows ? 'bg-[#122D55]' : 'bg-[#E4E7EC] dark:bg-[#1E293B]'
              ]"
            >
              <div
                :class="[
                  'bg-white w-4 h-4 rounded-full shadow-md transform transition-transform duration-200',
                  settingsStore.settings.startWithWindows ? 'translate-x-5' : 'translate-x-0'
                ]"
              ></div>
            </button>
          </div>

          <!-- Row: Minimize to Tray -->
          <div class="flex items-center justify-between gap-4 pt-4 border-t border-[#F2F4F7] dark:border-[#1E293B]">
            <div>
              <div class="text-xs font-semibold text-[#101828] dark:text-[#F8FAFC]">Minimize to System Tray</div>
              <div class="text-xs text-[#667085] dark:text-[#94A3B8]">Closing the main window keeps background monitoring active in the notification area.</div>
            </div>
            <button
              @click="settingsStore.updateSetting('minimizeToTray', !settingsStore.settings.minimizeToTray)"
              :class="[
                'w-11 h-6 flex items-center rounded-full p-1 transition-colors duration-200 cursor-pointer',
                settingsStore.settings.minimizeToTray ? 'bg-[#122D55]' : 'bg-[#E4E7EC] dark:bg-[#1E293B]'
              ]"
            >
              <div
                :class="[
                  'bg-white w-4 h-4 rounded-full shadow-md transform transition-transform duration-200',
                  settingsStore.settings.minimizeToTray ? 'translate-x-5' : 'translate-x-0'
                ]"
              ></div>
            </button>
          </div>

          <!-- Row: Dark Mode -->
          <div class="flex items-center justify-between gap-4 pt-4 border-t border-[#F2F4F7] dark:border-[#1E293B]">
            <div>
              <div class="text-xs font-semibold text-[#101828] dark:text-[#F8FAFC]">Dark Theme</div>
              <div class="text-xs text-[#667085] dark:text-[#94A3B8]">Use dark cybersecurity appearance for low-light environments.</div>
            </div>
            <button
              @click="settingsStore.updateSetting('darkMode', !settingsStore.settings.darkMode)"
              :class="[
                'w-11 h-6 flex items-center rounded-full p-1 transition-colors duration-200 cursor-pointer',
                settingsStore.settings.darkMode ? 'bg-[#122D55]' : 'bg-[#E4E7EC] dark:bg-[#1E293B]'
              ]"
            >
              <div
                :class="[
                  'bg-white w-4 h-4 rounded-full shadow-md transform transition-transform duration-200',
                  settingsStore.settings.darkMode ? 'translate-x-5' : 'translate-x-0'
                ]"
              ></div>
            </button>
          </div>
        </div>

        <!-- PROFILE TAB -->
        <div v-if="activeTab === 'Profile'" class="sec-card p-6 bg-white dark:bg-[#0F1E30] space-y-6">
          <div class="flex items-center justify-between pb-3 border-b border-[#F2F4F7] dark:border-[#1E293B]">
            <div>
              <h3 class="text-sm font-bold text-[#101828] dark:text-[#F8FAFC]">
                Dynamic User Persona &amp; Installation
              </h3>
              <p class="text-xs text-[#667085] dark:text-[#94A3B8] mt-0.5">
                Every installation of SecApper adapts dynamically to each user profile and hardware context.
              </p>
            </div>
            <Button variant="secondary" size="sm" @click="userStore.relaunchSetup">
              <template #icon><RotateCcw class="w-3.5 h-3.5" /></template>
              Re-run Setup Wizard
            </Button>
          </div>

          <!-- Unique Installation ID Card -->
          <div class="p-3.5 rounded-xl bg-[#F8FAFC] dark:bg-[#061426] border border-[#EAECF0] dark:border-[#163765] flex items-center justify-between">
            <div class="flex items-center gap-3">
              <Cpu class="w-5 h-5 text-blue-500 shrink-0" />
              <div>
                <div class="text-[10px] text-[#667085] dark:text-[#94A3B8] uppercase font-bold tracking-wider">
                  Unique Machine Installation ID
                </div>
                <div class="font-mono text-xs font-bold text-[#101828] dark:text-blue-200">
                  {{ userStore.profile.installationId }}
                </div>
              </div>
            </div>
            <button
              @click="copyInstallId"
              class="flex items-center gap-1.5 px-2.5 py-1.5 text-xs rounded-lg bg-white dark:bg-[#0F284B] border border-[#D0D5DD] dark:border-[#1D4A88] text-[#344054] dark:text-blue-200 hover:bg-[#F9FAFB] dark:hover:bg-[#163765] transition-colors"
            >
              <Check v-if="copiedId" class="w-3.5 h-3.5 text-emerald-500" />
              <Copy v-else class="w-3.5 h-3.5" />
              <span>{{ copiedId ? 'Copied' : 'Copy ID' }}</span>
            </button>
          </div>

          <!-- Edit Profile Form -->
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label class="block text-xs font-medium text-[#344054] dark:text-[#94A3B8] mb-1.5">
                User Display Name
              </label>
              <input
                v-model="editUserName"
                type="text"
                class="w-full px-3 py-2 text-xs rounded-lg border border-[#D0D5DD] dark:border-[#1E293B] bg-white dark:bg-[#06152A] text-[#101828] dark:text-white focus:outline-none focus:ring-2 focus:ring-[#122D55]/30"
              />
            </div>
            <div>
              <label class="block text-xs font-medium text-[#344054] dark:text-[#94A3B8] mb-1.5">
                Security Role
              </label>
              <input
                v-model="editUserRole"
                type="text"
                class="w-full px-3 py-2 text-xs rounded-lg border border-[#D0D5DD] dark:border-[#1E293B] bg-white dark:bg-[#06152A] text-[#101828] dark:text-white focus:outline-none focus:ring-2 focus:ring-[#122D55]/30"
              />
            </div>
          </div>

          <!-- Security Tier Preference -->
          <div>
            <label class="block text-xs font-medium text-[#344054] dark:text-[#94A3B8] mb-1.5">
              Active Security Tier
            </label>
            <div class="grid grid-cols-3 gap-3">
              <button
                v-for="tier in (['Relaxed', 'Standard', 'Maximum'] as const)"
                :key="tier"
                type="button"
                @click="editSecurityTier = tier"
                :class="[
                  'p-3 rounded-lg border text-left transition-all',
                  editSecurityTier === tier
                    ? 'border-[#122D55] bg-[#122D55]/5 dark:bg-[#122D55]/30 dark:border-blue-400 font-bold'
                    : 'border-[#EAECF0] dark:border-[#1E293B] bg-white dark:bg-[#06152A] text-[#667085] dark:text-[#94A3B8]'
                ]"
              >
                <div class="text-xs text-[#101828] dark:text-white">{{ tier }}</div>
                <div class="text-[10px] text-[#667085] dark:text-[#94A3B8]">
                  {{ tier === 'Maximum' ? 'Strict isolation' : tier === 'Relaxed' ? 'Developer' : 'Recommended' }}
                </div>
              </button>
            </div>
          </div>

          <div class="flex justify-end pt-3 border-t border-[#F2F4F7] dark:border-[#1E293B]">
            <Button variant="navy" size="md" :loading="isProfileSaving" @click="handleSaveProfile">
              Save Persona
            </Button>
          </div>
        </div>

        <!-- SECURITY TAB -->
        <div v-if="activeTab === 'Security'" class="sec-card p-6 bg-white dark:bg-[#0F1E30] space-y-6">
          <h3 class="text-sm font-bold text-[#101828] dark:text-[#F8FAFC] pb-3 border-b border-[#F2F4F7] dark:border-[#1E293B]">
            Security &amp; Master PIN
          </h3>

          <!-- Windows Explorer Integration -->
          <div class="flex items-center justify-between gap-4">
            <div>
              <div class="text-xs font-semibold text-[#101828] dark:text-[#F8FAFC]">Windows Explorer Double-Click Interception</div>
              <div class="text-xs text-[#667085] dark:text-[#94A3B8]">Display automatic unlock dialog when double-clicking a locked folder in Explorer.</div>
            </div>
            <button
              @click="settingsStore.updateSetting('explorerIntegrationEnabled', !settingsStore.settings.explorerIntegrationEnabled)"
              :class="[
                'w-11 h-6 flex items-center rounded-full p-1 transition-colors duration-200 cursor-pointer',
                settingsStore.settings.explorerIntegrationEnabled ? 'bg-[#122D55]' : 'bg-[#E4E7EC] dark:bg-[#1E293B]'
              ]"
            >
              <div
                :class="[
                  'bg-white w-4 h-4 rounded-full shadow-md transform transition-transform duration-200',
                  settingsStore.settings.explorerIntegrationEnabled ? 'translate-x-5' : 'translate-x-0'
                ]"
              ></div>
            </button>
          </div>

          <!-- Master PIN Setup -->
          <div class="flex items-center justify-between gap-4 pt-4 border-t border-[#F2F4F7] dark:border-[#1E293B]">
            <div>
              <div class="text-xs font-semibold text-[#101828] dark:text-[#F8FAFC]">Master Administrative PIN</div>
              <div class="text-xs text-[#667085] dark:text-[#94A3B8]">Allows unlocking any folder if an individual folder password is forgotten.</div>
            </div>
            <Button variant="secondary" size="sm" @click="showPinDialog = true">
              <template #icon><KeyRound class="w-3.5 h-3.5" /></template>
              {{ settingsStore.settings.masterPinConfigured ? 'Change PIN' : 'Set Up PIN' }}
            </Button>
          </div>
        </div>

        <!-- RANSOMWARE TAB -->
        <div v-if="activeTab === 'Ransomware'" class="sec-card p-6 bg-white dark:bg-[#0F1E30] space-y-6">
          <h3 class="text-sm font-bold text-[#101828] dark:text-[#F8FAFC] pb-3 border-b border-[#F2F4F7] dark:border-[#1E293B]">
            Ransomware Heuristic Parameters
          </h3>

          <!-- Compact Setting Row from Section 20 -->
          <div class="flex items-center justify-between gap-4">
            <div>
              <div class="text-xs font-semibold text-[#101828] dark:text-[#F8FAFC]">Ransomware Monitoring</div>
              <div class="text-xs text-[#667085] dark:text-[#94A3B8]">Monitor protected folders for suspicious file activity.</div>
            </div>
            <button
              @click="settingsStore.updateSetting('ransomwareProtectionEnabled', !settingsStore.settings.ransomwareProtectionEnabled)"
              :class="[
                'w-11 h-6 flex items-center rounded-full p-1 transition-colors duration-200 cursor-pointer',
                settingsStore.settings.ransomwareProtectionEnabled ? 'bg-[#122D55]' : 'bg-[#E4E7EC] dark:bg-[#1E293B]'
              ]"
            >
              <div
                :class="[
                  'bg-white w-4 h-4 rounded-full shadow-md transform transition-transform duration-200',
                  settingsStore.settings.ransomwareProtectionEnabled ? 'translate-x-5' : 'translate-x-0'
                ]"
              ></div>
            </button>
          </div>

          <!-- Mass Modification Threshold -->
          <div class="flex items-center justify-between gap-4 pt-4 border-t border-[#F2F4F7] dark:border-[#1E293B]">
            <div>
              <div class="text-xs font-semibold text-[#101828] dark:text-[#F8FAFC]">Mass Modification Threshold</div>
              <div class="text-xs text-[#667085] dark:text-[#94A3B8]">Number of file modifications within 5 seconds before triggering high threat alert.</div>
            </div>
            <select
              v-model="settingsStore.settings.massModificationThreshold"
              class="px-2.5 py-1 text-xs bg-white dark:bg-[#06152A] border border-[#D0D5DD] dark:border-[#1E293B] rounded-md font-mono"
            >
              <option :value="15">15 files</option>
              <option :value="30">30 files (Recommended)</option>
              <option :value="50">50 files</option>
            </select>
          </div>
        </div>

        <!-- NOTIFICATIONS TAB -->
        <div v-if="activeTab === 'Notifications'" class="sec-card p-6 bg-white dark:bg-[#0F1E30] space-y-6">
          <h3 class="text-sm font-bold text-[#101828] dark:text-[#F8FAFC] pb-3 border-b border-[#F2F4F7] dark:border-[#1E293B]">
            Desktop Security Alerts
          </h3>
          <div class="text-xs text-[#667085] dark:text-[#94A3B8]">
            SecApper sends native Windows notifications when:
          </div>
          <div class="space-y-2 text-xs">
            <div class="p-3 rounded-lg bg-[#F8FAFC] dark:bg-[#06152A] border border-[#EAECF0] dark:border-[#1E293B] flex items-center gap-2">
              <CheckCircle2 class="w-4 h-4 text-[#12B76A]" />
              <span>A protected folder is accessed or unlocked</span>
            </div>
            <div class="p-3 rounded-lg bg-[#F8FAFC] dark:bg-[#06152A] border border-[#EAECF0] dark:border-[#1E293B] flex items-center gap-2">
              <CheckCircle2 class="w-4 h-4 text-[#12B76A]" />
              <span>Suspicious mass renaming or file extension tamper is detected</span>
            </div>
            <div class="p-3 rounded-lg bg-[#F8FAFC] dark:bg-[#06152A] border border-[#EAECF0] dark:border-[#1E293B] flex items-center gap-2">
              <CheckCircle2 class="w-4 h-4 text-[#12B76A]" />
              <span>Verified software updates are ready for in-place installation</span>
            </div>
          </div>
        </div>

        <!-- UPDATES TAB -->
        <div v-if="activeTab === 'Updates'" class="sec-card p-6 bg-white dark:bg-[#0F1E30] space-y-6">
          <h3 class="text-sm font-bold text-[#101828] dark:text-[#F8FAFC] pb-3 border-b border-[#F2F4F7] dark:border-[#1E293B]">
            Update Settings
          </h3>
          <div class="flex items-center justify-between gap-4">
            <div>
              <div class="text-xs font-semibold text-[#101828] dark:text-[#F8FAFC]">Automatic Update Checks</div>
              <div class="text-xs text-[#667085] dark:text-[#94A3B8]">Check remote cryptographic manifest for official releases.</div>
            </div>
            <button
              @click="settingsStore.updateSetting('autoCheckUpdates', !settingsStore.settings.autoCheckUpdates)"
              :class="[
                'w-11 h-6 flex items-center rounded-full p-1 transition-colors duration-200 cursor-pointer',
                settingsStore.settings.autoCheckUpdates ? 'bg-[#122D55]' : 'bg-[#E4E7EC] dark:bg-[#1E293B]'
              ]"
            >
              <div
                :class="[
                  'bg-white w-4 h-4 rounded-full shadow-md transform transition-transform duration-200',
                  settingsStore.settings.autoCheckUpdates ? 'translate-x-5' : 'translate-x-0'
                ]"
              ></div>
            </button>
          </div>
        </div>

        <!-- PRIVACY TAB -->
        <div v-if="activeTab === 'Privacy'" class="sec-card p-6 bg-white dark:bg-[#0F1E30] space-y-6">
          <h3 class="text-sm font-bold text-[#101828] dark:text-[#F8FAFC] pb-3 border-b border-[#F2F4F7] dark:border-[#1E293B]">
            Offline Privacy Architecture
          </h3>
          <p class="text-xs text-[#475467] dark:text-[#CBD5E1] leading-relaxed">
            SecApper is an <strong>offline-first</strong> Windows cybersecurity application. Password hashes, SDDL permission backups, folder paths, and audit events never leave your local machine.
          </p>
          <div class="p-3.5 rounded-lg bg-[#ECFDF3] dark:bg-[#064E3B]/20 border border-[#ABEFC6] text-xs text-[#027A48] dark:text-[#34D399]">
            ✓ Zero telemetry • Zero cloud dependencies • 100% Local SQLite Persistence
          </div>
        </div>
      </div>
    </div>

    <!-- PIN Setup Dialog -->
    <div
      v-if="showPinDialog"
      class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-xs select-none"
      @click="showPinDialog = false"
    >
      <div
        class="bg-white dark:bg-[#0F1E30] border border-[#E4E7EC] dark:border-[#1E293B] rounded-xl shadow-modal max-w-sm w-full p-6 animate-scale-in"
        @click.stop
      >
        <div class="w-10 h-10 rounded-xl bg-[#122D55]/10 dark:bg-[#122D55]/30 flex items-center justify-center text-[#122D55] dark:text-[#93C5FD] mb-3">
          <KeyRound class="w-5 h-5 stroke-[2]" />
        </div>
        <h3 class="text-base font-bold text-[#101828] dark:text-[#F8FAFC]">Set Master PIN</h3>
        <p class="text-xs text-[#667085] dark:text-[#94A3B8] mt-1 mb-4">
          Enter a 4 to 8 digit Master PIN to manage recovery operations and emergency unlocking.
        </p>

        <input
          type="password"
          v-model="pinInput"
          placeholder="Enter Master PIN"
          maxlength="8"
          class="w-full px-3 py-2 text-center text-lg tracking-widest font-mono bg-white dark:bg-[#06152A] border border-[#D0D5DD] dark:border-[#1E293B] rounded-lg text-[#101828] dark:text-[#F8FAFC] focus:outline-none focus:ring-2 focus:ring-[#122D55]/30 mb-4"
        />

        <div class="flex justify-end gap-2">
          <Button variant="secondary" size="md" @click="showPinDialog = false">Cancel</Button>
          <Button variant="navy" size="md" :loading="isPinSubmitting" @click="handleSetupPin">Save PIN</Button>
        </div>
      </div>
    </div>
  </div>
</template>
