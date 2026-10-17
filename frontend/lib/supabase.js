// Conexiunea la Supabase. Fișier COMUN: îl folosește și P1 pentru login.
// Cheile vin din fișierul .env.local (vezi GHID-P3.md, pasul 3).
import { createClient } from '@supabase/supabase-js'

export const supabase = createClient(
  process.env.NEXT_PUBLIC_SUPABASE_URL,
  process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY
)
