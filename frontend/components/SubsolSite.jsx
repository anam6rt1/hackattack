'use client'
// Subsolul tuturor paginilor: legătura spre pagina de contact (cu pagina de pe care se pleacă), în culorile meniului de sus.
import Link from 'next/link'
import { usePathname } from 'next/navigation'
import { linkContact } from '@/lib/contact'

export default function SubsolSite() {
  const cale = usePathname() || '/'
  return (
    <footer className="mn-subsol">
      <div className="mn-subsol-interior">
        <span>TeleBac · Bacul la română, insulă cu insulă</span>
        <nav aria-label="Subsol">
          <Link href={linkContact(cale)} className="mn-subsol-link" aria-current={cale.startsWith('/contact') ? 'page' : undefined}>
            Contact · Scrie-ne
          </Link>
        </nav>
      </div>
    </footer>
  )
}
