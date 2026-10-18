// lib/intrebari.js — alegerea întrebării pentru un nivel
// Citește întrebările din Supabase (tabela „intrebari”).
// Dacă Supabase nu e configurat sau nu răspunde, folosește întrebările din data/intrebari.json.
import { supabase } from "@/lib/supabase";
import INTREBARI_LOCALE from "@/data/intrebari.json";

async function intrebariDinBaza(nivelId) {
  if (!supabase) return null;
  const { data, error } = await supabase
    .from("intrebari")
    .select("id, intrebare, variante, corect, explicatie")
    .eq("nivel_id", nivelId);
  if (error || !data || data.length === 0) {
    if (error) console.warn("Supabase: nu am putut citi întrebările, folosesc JSON-ul local.", error.message);
    return null;
  }
  // aducem rândurile la același format ca în JSON
  return data.map((r) => ({ id: r.id, text: r.intrebare, variante: r.variante, corect: r.corect, explicatie: r.explicatie }));
}

// Alege aleator una dintre întrebările nivelului.
// Nu ține minte nimic: la fiecare apel alege din nou dintre toate (poate pica aceeași de două ori).
// E asincronă: se folosește cu await sau .then(), doar în browser (useEffect sau la click).
export async function alegeIntrebare(nivelId) {
  let lista = null;
  try {
    lista = await intrebariDinBaza(nivelId);
  } catch (e) {
    console.warn("Supabase indisponibil, folosesc JSON-ul local.", e);
  }
  if (!lista) lista = INTREBARI_LOCALE[nivelId] ?? [];
  if (lista.length === 0) return null;
  return lista[Math.floor(Math.random() * lista.length)];
}
