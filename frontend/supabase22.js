// lib/supabase.js — conexiunea la Supabase (dacă P1 a făcut deja un fișier ca ăsta, folosiți-l pe al lui)
import { createClient } from "@supabase/supabase-js";

const url = process.env.NEXT_PUBLIC_SUPABASE_URL;
const cheie = process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY;

// null dacă lipsesc cheile din .env.local, ca aplicația să nu se blocheze
export const supabase = url && cheie ? createClient(url, cheie) : null;
