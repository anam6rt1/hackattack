'use client'
// Comportamentul specific aplicației mobile (butonul „înapoi” de pe Android); pe site și pe desktop nu face nimic.
import { useEffect } from 'react'
import { pornesteMobil } from '@/lib/mobil'

export default function IntegrareMobil() {
  useEffect(() => pornesteMobil(), [])
  return null
}
