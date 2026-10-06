<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import {
  Shield,
  ShieldCheck,
  ShieldAlert,
  KeyRound,
  CheckCircle2,
  Copy,
  Check,
  ArrowRight,
  ArrowLeft,
  Sparkles,
  Lock,
  User,
  Cpu,
  Download,
  Fingerprint,
  RefreshCw,
  FolderPlus,
  Eye,
  EyeOff,
  Flame,
  Award,
} from 'lucide-vue-next';
import { useUserStore } from '../../stores/user';
import { useFolderStore } from '../../stores/folders';
import { nativeBridge } from '../../services/nativeBridge';
import type { SecurityTier } from '../../types';
import logoUrl from '../../assets/branding/secapper-logo.png';
import shieldUrl from '../../assets/branding/secapper-shield.png';

const userStore = useUserStore();
const folderStore = useFolderStore();

const step = ref<number>(1);
const isSubmitting = ref<boolean>(false);
const copiedId = ref<boolean>(false);
const copiedKey = ref<boolean>(false);
const showPin = ref<boolean>(false);
const selectedFolderToLock = ref<string>('');

// Form model
const userName = ref<string>('');
const userRole = ref<string>('Security Administrator');
const selectedAvatar = ref<string>('sentinel');
const securityTier = ref<SecurityTier>('Standard');
const masterPin = ref<string>('');
const confirmPin = ref<string>('');
const pinError = ref<string | null>(null);
const recoveryCode = ref<string>('');
const isDarkMode = ref<boolean>(true);

// Pre-defined dynamic avatar choices
const avatars = [
  { id: 'sentinel', name: 'Sentinel Shield', icon: Shield, color: 'text-blue-400 bg-blue-500/10 border-blue-500/30' },
  { id: 'fortress', name: 'Fortress Core', icon: Lock, color: 'text-emerald-400 bg-emerald-500/10 border-emerald-500/30' },
  { id: 'keymaster', name: 'Keymaster', icon: KeyRound, color: 'text-amber-400 bg-amber-500/10 border-amber-500/30' },
  { id: 'sentinel-red', name: 'Cyber Guardian', icon: ShieldCheck, color: 'text-rose-400 bg-rose-500/10 border-rose-500/30' },
  { id: 'cyber-eye', name: 'Watchdog', icon: Fingerprint, color: 'text-purple-400 bg-purple-500/10 border-purple-500/30' },
  { id: 'quantum', name: 'Quantum Core', icon: Cpu, color: 'text-cyan-400 bg-cyan-500/10 border-cyan-500/30' },
];

const roles = [
  'Security Administrator',
  'Lead Developer',
  'Privacy Enthusiast',
  'Personal User',
  'Enterprise Auditor',
];

const tiers = [
  {
    id: 'Standard' as SecurityTier,
    name: 'Standard Defense',
    badge: 'RECOMMENDED',
    badgeClass: 'bg-blue-500/20 text-blue-300 border-blue-500/40',
    description: 'Optimal balance of non-destructive NTFS ACL access controls and seamless workflow.',
    features: [
      'Full NTFS inheritance stripping & Deny ACE injection',
      'Auto-lock on Windows Explorer window close',
      'Real-time ransomware heuristics (30 event trigger)',
      'System tray quick-protection integration',
    ],
  },
  {
    id: 'Maximum' as SecurityTier,
    name: 'Fortress Strict',
    badge: 'HIGH SECURITY',
    badgeClass: 'bg-rose-500/20 text-rose-300 border-rose-500/40',
    description: 'Strict lockdown with mandatory master PIN verification and sensitive heuristics.',
    features: [
      'Strict NTFS ACL with zero background access',
      'Instant lock & aggressive ransomware trigger (15 events)',
      'Master PIN required for every unlock & policy change',
      'Emergency Panic Lock one-click defense shortcut',
    ],
  },
  {
    id: 'Relaxed' as SecurityTier,
    name: 'Developer Friendly',
    badge: 'FLEXIBLE',
    badgeClass: 'bg-slate-500/20 text-slate-300 border-slate-500/40',
    description: 'Lightweight protection ideal for active coding workspaces and frequent builds.',
    features: [
      'Standard folder access restriction',
      'Higher modification threshold (50 events) for builds',
      'Extended grace period before automatic re-locking',
      'Explorer right-click context menu integration',
    ],
  },
];

const installationId = computed(() => userStore.initialData?.installationId || userStore.profile.installationId || 'SEC-DYNAMIC-INSTALL');
const machineName = computed(() => userStore.initialData?.machineName || 'Windows PC');
const osVersion = computed(() => userStore.initialData?.osVersion || 'Windows 10/11');

// Pin strength calculator
const pinStrength = computed(() => {
  const p = masterPin.value;
  if (!p) return { level: 0, label: 'None', color: 'bg-gray-600' };
  if (p.length < 4) return { level: 1, label: 'Too short (min 4)', color: 'bg-rose-500' };
  if (p.length >= 4 && /^\d+$/.test(p) && p.length < 6) return { level: 2, label: 'Fair (Numeric)', color: 'bg-amber-500' };
  if (p.length >= 6 && /^\d+$/.test(p)) return { level: 3, label: 'Good (Strong PIN)', color: 'bg-blue-500' };
  return { level: 4, label: 'Military-Grade (Alphanumeric)', color: 'bg-emerald-500' };
});

onMounted(async () => {
  await userStore.fetchInitialData();
  userName.value = userStore.initialData?.suggestedUsername || userStore.profile.userName || 'Security User';
  generateDynamicRecoveryCode();
});

function generateDynamicRecoveryCode() {
  const randPart = () => Math.random().toString(36).substring(2, 6).toUpperCase();
  recoveryCode.value = `SEC-${randPart()}-${randPart()}-${randPart()}`;
}

async function copyInstallationId() {
  if (navigator.clipboard) {
    await navigator.clipboard.writeText(installationId.value);
    copiedId.value = true;
    setTimeout(() => (copiedId.value = false), 2000);
  }
}

async function copyRecoveryCode() {
  if (navigator.clipboard) {
    await navigator.clipboard.writeText(recoveryCode.value);
    copiedKey.value = true;
    setTimeout(() => (copiedKey.value = false), 2000);
  }
}

function downloadRecoveryCertificate() {
  const content = `=====================================================
SECAPPER SECURE FOLDER LOCKER - RECOVERY CERTIFICATE
=====================================================
Installation ID: ${installationId.value}
User Profile   : ${userName.value} (${userRole.value})
Security Tier  : ${securityTier.value}
Recovery Key   : ${recoveryCode.value}
Generated Date : ${new Date().toISOString()}
Machine        : ${machineName.value} (${osVersion.value})
=====================================================
IMPORTANT: Store this recovery certificate safely offline.
If you forget your folder passwords or Master PIN, this key
can be used by SecApper Recovery Center to restore access.
=====================================================`;

  const blob = new Blob([content], { type: 'text/plain;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `SecApper-Recovery-${userName.value.replace(/[^a-zA-Z0-9]/g, '_')}.txt`;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}

async function browseInitialFolder() {
  try {
    const selected = await nativeBridge.folders.browseFolder();
    if (selected) {
      selectedFolderToLock.value = selected;
    }
  } catch (err) {
    console.warn('Folder browse cancelled or failed', err);
  }
}

function validateAndProceed() {
  pinError.value = null;

  if (step.value === 1) {
    if (!userName.value.trim()) {
      userName.value = 'Security User';
    }
    step.value = 2;
    return;
  }

  if (step.value === 2) {
    step.value = 3;
    return;
  }

  if (step.value === 3) {
    if (masterPin.value.length > 0) {
      if (masterPin.value.length < 4) {
        pinError.value = 'Master PIN must be at least 4 digits or characters.';
        return;
      }
      if (masterPin.value !== confirmPin.value) {
        pinError.value = 'Master PIN confirmation does not match.';
        return;
      }
    }
    step.value = 4;
  }
}

async function handleCompleteSetup() {
  isSubmitting.value = true;
  try {
    await userStore.completeSetup({
      userName: userName.value.trim() || 'Security User',
      userRole: userRole.value,
      avatar: selectedAvatar.value,
      securityTier: securityTier.value,
      masterPin: masterPin.value ? masterPin.value.trim() : undefined,
      darkMode: isDarkMode.value,
      autoLockOnWindowClose: securityTier.value !== 'Relaxed',
      ransomwareThreshold: securityTier.value === 'Maximum' ? 15 : securityTier.value === 'Relaxed' ? 50 : 30,
      recoveryCode: recoveryCode.value,
    });

    // If user selected an initial folder to lock, lock it now
    if (selectedFolderToLock.value && masterPin.value) {
      try {
        await folderStore.lockFolder(selectedFolderToLock.value, masterPin.value);
      } catch (err) {
        console.warn('Initial folder lock error:', err);
      }
    }
  } catch (err: any) {
    pinError.value = err.message || 'Setup finalization failed.';
  } finally {
    isSubmitting.value = false;
  }
}
</script>

<template>
  <div class="fixed inset-0 z-50 flex items-center justify-center bg-[#030914]/90 backdrop-blur-xl p-4 sm:p-6 overflow-y-auto">
    <!-- Outer Glow Container -->
    <div
      class="relative w-full max-w-3xl bg-[#09182E] border border-[#163B6B] rounded-2xl shadow-2xl shadow-blue-950/80 overflow-hidden text-white my-auto"
    >
      <!-- Top Decorative Cyber Bar -->
      <div class="h-1.5 w-full bg-gradient-to-r from-[#122D55] via-[#C5202B] to-[#38BDF8]"></div>

      <!-- Header Section -->
      <div class="px-6 py-5 border-b border-[#14325C] flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 bg-[#0A1D38]/50">
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-xl bg-[#122D55] border border-[#1E4E8C] flex items-center justify-center p-2 shadow-inner">
            <img :src="shieldUrl" alt="SecApper Shield" class="w-full h-full object-contain" />
          </div>
          <div>
            <div class="flex items-center gap-2">
              <h2 class="text-lg font-bold tracking-tight text-white">SecApper Dynamic Setup</h2>
              <span class="text-[10px] font-mono px-2 py-0.5 rounded-full bg-blue-500/20 text-blue-300 border border-blue-500/30">
                INSTALLATION ENGINE
              </span>
            </div>
            <p class="text-xs text-[#94A3B8]">Personalizing your Windows filesystem security profile</p>
          </div>
        </div>

        <!-- Step Indicator -->
        <div class="flex items-center gap-1.5 self-center sm:self-auto bg-[#061426] p-1.5 rounded-lg border border-[#14325C]">
          <div
            v-for="s in [1, 2, 3, 4]"
            :key="s"
            :class="[
              'w-7 h-7 rounded-md flex items-center justify-center text-xs font-semibold transition-all duration-200',
              step === s
                ? 'bg-[#C5202B] text-white shadow-md shadow-red-950/50 scale-105'
                : step > s
                ? 'bg-[#122D55] text-blue-300'
                : 'text-[#64748B] hover:text-[#94A3B8]'
            ]"
          >
            <Check v-if="step > s" class="w-3.5 h-3.5" />
            <span v-else>{{ s }}</span>
          </div>
        </div>
      </div>

      <!-- Body / Wizard Steps -->
      <div class="p-6 sm:p-8">
        <!-- ============================================================= -->
        <!-- STEP 1: USER IDENTITY & DYNAMIC PROFILE                      -->
        <!-- ============================================================= -->
        <div v-if="step === 1" class="space-y-6">
          <div class="border-b border-[#14325C]/80 pb-4">
            <h3 class="text-base font-semibold text-white flex items-center gap-2">
              <User class="w-4 h-4 text-blue-400" />
              User Identity &amp; Machine Profile
            </h3>
            <p class="text-xs text-[#94A3B8] mt-1">
              SecApper dynamically detected your Windows environment. Choose your display name, role, and cybersecurity badge.
            </p>
          </div>

          <!-- Dynamic Machine ID Card -->
          <div class="flex items-center justify-between p-3.5 rounded-xl bg-[#061426] border border-[#163765]">
            <div class="flex items-center gap-3">
              <Cpu class="w-5 h-5 text-blue-400 shrink-0" />
              <div>
                <div class="text-[11px] text-[#64748B] uppercase tracking-wider font-semibold">
                  Unique Installation ID
                </div>
                <div class="font-mono text-xs font-bold text-blue-200 tracking-wide">
                  {{ installationId }}
                </div>
                <div class="text-[10px] text-[#94A3B8] mt-0.5">
                  Bound to: {{ machineName }} • {{ osVersion }}
                </div>
              </div>
            </div>
            <button
              @click="copyInstallationId"
              class="flex items-center gap-1.5 px-2.5 py-1.5 text-xs rounded-lg bg-[#0F284B] hover:bg-[#163765] text-blue-200 transition-colors border border-[#1D4A88]"
            >
              <Check v-if="copiedId" class="w-3.5 h-3.5 text-emerald-400" />
              <Copy v-else class="w-3.5 h-3.5" />
              <span>{{ copiedId ? 'Copied' : 'Copy' }}</span>
            </button>
          </div>

          <!-- Name & Role Input Grid -->
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label class="block text-xs font-medium text-[#94A3B8] mb-1.5">
                Display Name / Security Owner
              </label>
              <input
                v-model="userName"
                type="text"
                placeholder="Enter your name"
                class="w-full px-3.5 py-2.5 rounded-lg bg-[#061426] border border-[#163765] text-white placeholder-[#64748B] text-sm focus:outline-none focus:border-blue-400 transition-colors"
              />
            </div>

            <div>
              <label class="block text-xs font-medium text-[#94A3B8] mb-1.5">
                Security Role / Persona
              </label>
              <select
                v-model="userRole"
                class="w-full px-3.5 py-2.5 rounded-lg bg-[#061426] border border-[#163765] text-white text-sm focus:outline-none focus:border-blue-400 transition-colors"
              >
                <option v-for="r in roles" :key="r" :value="r">{{ r }}</option>
              </select>
            </div>
          </div>

          <!-- Avatar Picker -->
          <div>
            <label class="block text-xs font-medium text-[#94A3B8] mb-2">
              Select Your Cybersecurity Badge
            </label>
            <div class="grid grid-cols-2 sm:grid-cols-3 gap-3">
              <button
                v-for="av in avatars"
                :key="av.id"
                @click="selectedAvatar = av.id"
                :class="[
                  'flex items-center gap-3 p-3 rounded-xl border text-left transition-all duration-200',
                  selectedAvatar === av.id
                    ? 'border-[#C5202B] bg-[#122D55] ring-2 ring-[#C5202B]/30'
                    : 'border-[#14325C] bg-[#07172B] hover:border-[#1E4E8C] hover:bg-[#0B1E38]'
                ]"
              >
                <div :class="['w-9 h-9 rounded-lg flex items-center justify-center shrink-0 border', av.color]">
                  <component :is="av.icon" class="w-5 h-5" />
                </div>
                <div class="truncate">
                  <div class="text-xs font-semibold text-white truncate">{{ av.name }}</div>
                  <div class="text-[10px] text-[#94A3B8]">Active Badge</div>
                </div>
              </button>
            </div>
          </div>
        </div>

        <!-- ============================================================= -->
        <!-- STEP 2: DYNAMIC SECURITY TIER                                -->
        <!-- ============================================================= -->
        <div v-if="step === 2" class="space-y-6">
          <div class="border-b border-[#14325C]/80 pb-4">
            <h3 class="text-base font-semibold text-white flex items-center gap-2">
              <ShieldAlert class="w-4 h-4 text-blue-400" />
              Dynamic Security Posture
            </h3>
            <p class="text-xs text-[#94A3B8] mt-1">
              Select the defense sensitivity tailored to your computer usage. You can adjust this anytime in Settings.
            </p>
          </div>

          <!-- Tiers Cards -->
          <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div
              v-for="t in tiers"
              :key="t.id"
              @click="securityTier = t.id"
              :class="[
                'cursor-pointer rounded-xl border p-4 flex flex-col justify-between transition-all duration-200 relative',
                securityTier === t.id
                  ? 'border-[#C5202B] bg-[#0F284B] ring-2 ring-[#C5202B]/40 shadow-lg shadow-blue-950/60'
                  : 'border-[#14325C] bg-[#061426] hover:border-[#1E4E8C] hover:bg-[#081B33]'
              ]"
            >
              <div>
                <div class="flex items-center justify-between gap-2 mb-2">
                  <span :class="['text-[9px] font-bold px-2 py-0.5 rounded-full border', t.badgeClass]">
                    {{ t.badge }}
                  </span>
                  <div
                    :class="[
                      'w-4 h-4 rounded-full border flex items-center justify-center',
                      securityTier === t.id ? 'border-[#C5202B] bg-[#C5202B]' : 'border-[#64748B]'
                    ]"
                  >
                    <Check v-if="securityTier === t.id" class="w-2.5 h-2.5 text-white" />
                  </div>
                </div>

                <h4 class="text-sm font-bold text-white mb-1.5">{{ t.name }}</h4>
                <p class="text-[11px] text-[#94A3B8] mb-3 leading-relaxed">
                  {{ t.description }}
                </p>

                <div class="space-y-1.5 pt-2 border-t border-[#14325C]/60">
                  <div
                    v-for="(f, i) in t.features"
                    :key="i"
                    class="text-[10px] text-blue-200 flex items-start gap-1.5"
                  >
                    <CheckCircle2 class="w-3 h-3 text-emerald-400 shrink-0 mt-0.5" />
                    <span>{{ f }}</span>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- Theme Preference -->
          <div class="p-3.5 rounded-xl bg-[#061426] border border-[#163765] flex items-center justify-between">
            <div class="flex items-center gap-3">
              <Sparkles class="w-5 h-5 text-amber-400 shrink-0" />
              <div>
                <div class="text-xs font-semibold text-white">Visual Mode</div>
                <div class="text-[11px] text-[#94A3B8]">Deep Cyber Navy theme optimized for security operations</div>
              </div>
            </div>
            <div class="flex items-center gap-2">
              <button
                @click="isDarkMode = true"
                :class="[
                  'px-3 py-1.5 rounded-lg text-xs font-medium border transition-colors',
                  isDarkMode
                    ? 'bg-[#122D55] text-white border-blue-400'
                    : 'bg-[#061426] text-[#94A3B8] border-[#14325C]'
                ]"
              >
                Cyber Dark
              </button>
              <button
                @click="isDarkMode = false"
                :class="[
                  'px-3 py-1.5 rounded-lg text-xs font-medium border transition-colors',
                  !isDarkMode
                    ? 'bg-[#122D55] text-white border-blue-400'
                    : 'bg-[#061426] text-[#94A3B8] border-[#14325C]'
                ]"
              >
                Clean Light
              </button>
            </div>
          </div>
        </div>

        <!-- ============================================================= -->
        <!-- STEP 3: MASTER SECURITY PIN & RECOVERY KEY                   -->
        <!-- ============================================================= -->
        <div v-if="step === 3" class="space-y-6">
          <div class="border-b border-[#14325C]/80 pb-4">
            <h3 class="text-base font-semibold text-white flex items-center gap-2">
              <KeyRound class="w-4 h-4 text-blue-400" />
              Master Security PIN &amp; Offline Recovery Key
            </h3>
            <p class="text-xs text-[#94A3B8] mt-1">
              Your Master PIN acts as the primary recovery authenticator for all locked folders. It is encrypted offline with PBKDF2-SHA256.
            </p>
          </div>

          <!-- PIN Inputs -->
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <div class="flex items-center justify-between mb-1.5">
                <label class="text-xs font-medium text-[#94A3B8]">
                  Set Master PIN (Optional or Recommended)
                </label>
                <button
                  type="button"
                  @click="showPin = !showPin"
                  class="text-[11px] text-blue-400 hover:text-blue-300 flex items-center gap-1"
                >
                  <Eye v-if="!showPin" class="w-3 h-3" />
                  <EyeOff v-else class="w-3 h-3" />
                  <span>{{ showPin ? 'Hide' : 'Show' }}</span>
                </button>
              </div>
              <input
                v-model="masterPin"
                :type="showPin ? 'text' : 'password'"
                placeholder="Enter 4 to 12 digits or characters"
                class="w-full px-3.5 py-2.5 rounded-lg bg-[#061426] border border-[#163765] text-white placeholder-[#64748B] text-sm focus:outline-none focus:border-blue-400 transition-colors tracking-wider"
              />

              <!-- Strength indicator -->
              <div v-if="masterPin" class="mt-2 flex items-center gap-2">
                <div class="flex-1 h-1.5 bg-[#061426] rounded-full overflow-hidden flex gap-1">
                  <div
                    v-for="lvl in 4"
                    :key="lvl"
                    :class="[
                      'h-full flex-1 rounded-full transition-all duration-300',
                      pinStrength.level >= lvl ? pinStrength.color : 'bg-[#14325C]'
                    ]"
                  ></div>
                </div>
                <span class="text-[10px] font-mono text-[#94A3B8] shrink-0">
                  {{ pinStrength.label }}
                </span>
              </div>
            </div>

            <div>
              <label class="block text-xs font-medium text-[#94A3B8] mb-1.5">
                Confirm Master PIN
              </label>
              <input
                v-model="confirmPin"
                :type="showPin ? 'text' : 'password'"
                placeholder="Re-enter your Master PIN"
                class="w-full px-3.5 py-2.5 rounded-lg bg-[#061426] border border-[#163765] text-white placeholder-[#64748B] text-sm focus:outline-none focus:border-blue-400 transition-colors tracking-wider"
              />
            </div>
          </div>

          <div v-if="pinError" class="p-3 rounded-lg bg-rose-500/10 border border-rose-500/30 text-rose-300 text-xs">
            {{ pinError }}
          </div>

          <!-- Offline Dynamic Recovery Key Card -->
          <div class="p-4 rounded-xl bg-[#061426] border border-[#163765] space-y-3">
            <div class="flex items-center justify-between">
              <div class="flex items-center gap-2 text-xs font-semibold text-white">
                <Award class="w-4 h-4 text-emerald-400" />
                Offline Emergency Recovery Certificate Key
              </div>
              <button
                @click="generateDynamicRecoveryCode"
                class="text-[10px] text-blue-400 hover:text-blue-300 flex items-center gap-1"
                title="Regenerate random key"
              >
                <RefreshCw class="w-3 h-3" />
                <span>Regenerate</span>
              </button>
            </div>

            <p class="text-[11px] text-[#94A3B8] leading-relaxed">
              In case you forget your folder passwords or Master PIN, this offline certificate key can restore access. Save it now.
            </p>

            <div class="flex flex-col sm:flex-row items-center gap-3">
              <div class="w-full sm:flex-1 px-4 py-2.5 bg-[#091D38] border border-[#1E4E8C] rounded-lg font-mono text-sm text-center sm:text-left text-emerald-400 tracking-widest font-bold">
                {{ recoveryCode }}
              </div>
              <div class="flex items-center gap-2 w-full sm:w-auto">
                <button
                  @click="copyRecoveryCode"
                  class="flex-1 sm:flex-initial px-3 py-2 rounded-lg bg-[#0F284B] hover:bg-[#163765] text-blue-200 text-xs flex items-center justify-center gap-1.5 border border-[#1D4A88] transition-colors"
                >
                  <Check v-if="copiedKey" class="w-3.5 h-3.5 text-emerald-400" />
                  <Copy v-else class="w-3.5 h-3.5" />
                  <span>{{ copiedKey ? 'Copied' : 'Copy Key' }}</span>
                </button>
                <button
                  @click="downloadRecoveryCertificate"
                  class="flex-1 sm:flex-initial px-3 py-2 rounded-lg bg-emerald-500/20 hover:bg-emerald-500/30 text-emerald-300 text-xs flex items-center justify-center gap-1.5 border border-emerald-500/40 transition-colors"
                >
                  <Download class="w-3.5 h-3.5" />
                  <span>Download .txt</span>
                </button>
              </div>
            </div>
          </div>
        </div>

        <!-- ============================================================= -->
        <!-- STEP 4: ACTIVATION & SUMMARY                                 -->
        <!-- ============================================================= -->
        <div v-if="step === 4" class="space-y-6">
          <div class="border-b border-[#14325C]/80 pb-4 text-center sm:text-left">
            <h3 class="text-base font-semibold text-white flex items-center justify-center sm:justify-start gap-2">
              <Sparkles class="w-4 h-4 text-amber-400" />
              Ready for Activation
            </h3>
            <p class="text-xs text-[#94A3B8] mt-1">
              Review your customized configuration. Click "Activate My SecApper" to seal the filesystem protection hooks.
            </p>
          </div>

          <!-- Summary Grid -->
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div class="p-3.5 rounded-xl bg-[#061426] border border-[#163765] flex items-center gap-3">
              <div class="w-10 h-10 rounded-lg bg-blue-500/10 border border-blue-500/30 flex items-center justify-center text-blue-400">
                <User class="w-5 h-5" />
              </div>
              <div>
                <div class="text-[10px] text-[#64748B] uppercase font-bold">Security Owner</div>
                <div class="text-sm font-semibold text-white">{{ userName }}</div>
                <div class="text-[11px] text-blue-300">{{ userRole }}</div>
              </div>
            </div>

            <div class="p-3.5 rounded-xl bg-[#061426] border border-[#163765] flex items-center gap-3">
              <div class="w-10 h-10 rounded-lg bg-rose-500/10 border border-rose-500/30 flex items-center justify-center text-rose-400">
                <ShieldCheck class="w-5 h-5" />
              </div>
              <div>
                <div class="text-[10px] text-[#64748B] uppercase font-bold">Selected Posture</div>
                <div class="text-sm font-semibold text-white">{{ securityTier }} Protection</div>
                <div class="text-[11px] text-emerald-400">NTFS Access Control Active</div>
              </div>
            </div>

            <div class="p-3.5 rounded-xl bg-[#061426] border border-[#163765] flex items-center gap-3">
              <div class="w-10 h-10 rounded-lg bg-amber-500/10 border border-amber-500/30 flex items-center justify-center text-amber-400">
                <KeyRound class="w-5 h-5" />
              </div>
              <div>
                <div class="text-[10px] text-[#64748B] uppercase font-bold">Master PIN</div>
                <div class="text-sm font-semibold text-white">
                  {{ masterPin ? 'Configured & Encrypted' : 'Not set (Can set later)' }}
                </div>
                <div class="text-[11px] text-[#94A3B8]">PBKDF2-SHA256 • 310,000 rounds</div>
              </div>
            </div>

            <div class="p-3.5 rounded-xl bg-[#061426] border border-[#163765] flex items-center gap-3">
              <div class="w-10 h-10 rounded-lg bg-emerald-500/10 border border-emerald-500/30 flex items-center justify-center text-emerald-400">
                <Award class="w-5 h-5" />
              </div>
              <div>
                <div class="text-[10px] text-[#64748B] uppercase font-bold">Recovery Certificate</div>
                <div class="font-mono text-xs font-bold text-emerald-400">{{ recoveryCode }}</div>
                <div class="text-[10px] text-[#94A3B8]">Saved offline</div>
              </div>
            </div>
          </div>

          <!-- Quick First Folder Selection (Optional) -->
          <div class="p-4 rounded-xl bg-[#081C38] border border-[#1E4E8C] space-y-2">
            <div class="flex items-center justify-between">
              <div class="flex items-center gap-2 text-xs font-semibold text-white">
                <FolderPlus class="w-4 h-4 text-blue-400" />
                Protect Your First Folder Now (Optional)
              </div>
              <button
                @click="browseInitialFolder"
                class="px-3 py-1 rounded bg-[#122D55] hover:bg-[#1C467F] text-blue-200 text-xs border border-blue-400/40 transition-colors"
              >
                Browse...
              </button>
            </div>
            <div v-if="selectedFolderToLock" class="text-xs font-mono text-emerald-400 truncate bg-[#061426] p-2 rounded border border-[#163765]">
              {{ selectedFolderToLock }}
            </div>
            <p v-else class="text-[11px] text-[#94A3B8]">
              You can immediately select a sensitive directory to lock, or skip this and use the Folder Locker tab later.
            </p>
          </div>
        </div>
      </div>

      <!-- Footer Buttons -->
      <div class="px-6 py-4 border-t border-[#14325C] flex items-center justify-between bg-[#0A1D38]/50">
        <div>
          <button
            v-if="step > 1"
            @click="step--"
            :disabled="isSubmitting"
            class="px-4 py-2 rounded-xl text-xs font-medium text-[#94A3B8] hover:text-white hover:bg-[#0F284B] flex items-center gap-1.5 transition-colors border border-transparent hover:border-[#14325C]"
          >
            <ArrowLeft class="w-3.5 h-3.5" />
            <span>Back</span>
          </button>
        </div>

        <div class="flex items-center gap-3">
          <button
            v-if="step < 4"
            @click="validateAndProceed"
            class="px-5 py-2.5 rounded-xl text-xs font-bold text-white bg-[#122D55] hover:bg-[#1A3E75] border border-blue-400/40 shadow-md shadow-blue-950/50 flex items-center gap-2 transition-all hover:scale-105 active:scale-95"
          >
            <span>Continue</span>
            <ArrowRight class="w-3.5 h-3.5" />
          </button>

          <button
            v-else
            @click="handleCompleteSetup"
            :disabled="isSubmitting"
            class="px-6 py-2.5 rounded-xl text-xs font-bold text-white bg-gradient-to-r from-[#C5202B] to-[#E63946] hover:from-[#B01B24] hover:to-[#D62828] border border-red-400/50 shadow-lg shadow-red-950/80 flex items-center gap-2 transition-all hover:scale-105 active:scale-95 disabled:opacity-50"
          >
            <Sparkles v-if="!isSubmitting" class="w-4 h-4 text-amber-300" />
            <RefreshCw v-else class="w-4 h-4 animate-spin" />
            <span>{{ isSubmitting ? 'Activating Defense Engine...' : 'Activate My SecApper' }}</span>
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
