'use client'
import { useSyncExternalStore } from 'react'
function subscribe(callback) { window.addEventListener('hashchange', callback); return () => window.removeEventListener('hashchange', callback) }
function snapshot() { return window.location.hash.slice(1) }
export function useHash() { return useSyncExternalStore(subscribe, snapshot, () => '') }
