<script setup lang="ts">
import PageHeader from '../components/layout/PageHeader.vue';
import logoUrl from '../assets/branding/secapper-logo.png';
import shieldUrl from '../assets/branding/secapper-shield.png';
import { Shield, Lock, Database, Cpu, CheckCircle2, ShieldCheck } from 'lucide-vue-next';
import { useSecurityStore } from '../stores/security';

const securityStore = useSecurityStore();
</script>

<template>
  <div class="space-y-6 max-w-4xl mx-auto">
    <!-- Header -->
    <PageHeader
      title="About SecApper"
      subtitle="Commercial-grade Windows folder security &amp; ransomware protection."
    />

    <!-- Brand Card -->
    <div class="sec-card p-8 sm:p-10 text-center bg-white dark:bg-[#0F1E30] border border-[#E4E7EC] dark:border-[#1E293B]">
      <!-- Official Logo exactly as supplied -->
      <div class="flex justify-center mb-4">
        <img
          :src="logoUrl"
          alt="SecApper — Secure Your World"
          class="h-16 w-auto object-contain"
        />
      </div>

      <div class="text-xs font-bold uppercase tracking-[0.25em] text-[#122D55] dark:text-[#93C5FD] mb-2">
        Secure Your World
      </div>

      <div class="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-[#122D55]/5 dark:bg-[#122D55]/30 text-xs font-mono text-[#122D55] dark:text-[#93C5FD] mb-6">
        Version {{ securityStore.status.version || '1.1.0' }} (Commercial Build)
      </div>

      <p class="text-xs sm:text-sm text-[#667085] dark:text-[#94A3B8] max-w-lg mx-auto leading-relaxed">
        SecApper is a desktop cybersecurity application engineered for Windows 10 and Windows 11. It provides offline folder access protection using native Windows NTFS Access Control Lists (ACLs), cryptographic password verification, real-time ransomware heuristic monitoring, and automated state recovery.
      </p>
    </div>

    <!-- Security Engine Architecture Grid -->
    <div>
      <div class="text-xs font-bold uppercase tracking-wider text-[#667085] dark:text-[#94A3B8] mb-3">
        Engine Architecture &amp; Cryptographic Specifications
      </div>

      <div class="grid grid-cols-1 sm:grid-cols-2 gap-4 text-xs">
        <div class="sec-card p-4 bg-white dark:bg-[#0F1E30] border border-[#E4E7EC] dark:border-[#1E293B] space-y-2">
          <div class="flex items-center gap-2 text-[#122D55] dark:text-[#93C5FD] font-bold">
            <Lock class="w-4 h-4" />
            <span>Filesystem Access Control</span>
          </div>
          <p class="text-[#667085] dark:text-[#94A3B8] leading-relaxed">
            Direct manipulation of Windows NTFS Security Descriptors. Strips inheritance flags and injects explicit Deny Access Control Entries for Read, Write, ListDirectory, Delete, and Execute.
          </p>
        </div>

        <div class="sec-card p-4 bg-white dark:bg-[#0F1E30] border border-[#E4E7EC] dark:border-[#1E293B] space-y-2">
          <div class="flex items-center gap-2 text-[#122D55] dark:text-[#93C5FD] font-bold">
            <ShieldCheck class="w-4 h-4" />
            <span>Key Derivation &amp; Verification</span>
          </div>
          <p class="text-[#667085] dark:text-[#94A3B8] leading-relaxed">
            PBKDF2-SHA256 derivation with 310,000 iterations and cryptographically secure random salts. Master PIN overrides backed by salted HMAC hashing.
          </p>
        </div>

        <div class="sec-card p-4 bg-white dark:bg-[#0F1E30] border border-[#E4E7EC] dark:border-[#1E293B] space-y-2">
          <div class="flex items-center gap-2 text-[#122D55] dark:text-[#93C5FD] font-bold">
            <Database class="w-4 h-4" />
            <span>Persistence &amp; Backups</span>
          </div>
          <p class="text-[#667085] dark:text-[#94A3B8] leading-relaxed">
            Zero-cloud, offline-first SQLite database in Write-Ahead Logging (WAL) mode. Preserves raw SDDL descriptor backups with SHA-256 integrity verification.
          </p>
        </div>

        <div class="sec-card p-4 bg-white dark:bg-[#0F1E30] border border-[#E4E7EC] dark:border-[#1E293B] space-y-2">
          <div class="flex items-center gap-2 text-[#122D55] dark:text-[#93C5FD] font-bold">
            <Cpu class="w-4 h-4" />
            <span>Heuristic Ransomware Shield</span>
          </div>
          <p class="text-[#667085] dark:text-[#94A3B8] leading-relaxed">
            Real-time FileSystemWatcher kernel notifications paired with rolling-window threat scoring to detect rapid mass file modifications, rapid renaming, and known ransomware extensions.
          </p>
        </div>
      </div>
    </div>

    <!-- Copyright Footer -->
    <div class="text-center text-[11px] text-[#98A2B3] pt-4">
      &copy; SecApper Cybersecurity. All rights reserved. Designed for Windows 10 &amp; 11 (64-bit).
    </div>
  </div>
</template>
