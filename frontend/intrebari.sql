-- supabase/intrebari.sql
-- Rulează tot fișierul o singură dată în Supabase: SQL Editor → New query → Run.

create table if not exists public.intrebari (
  id          text primary key,          -- ex. harap-1-a
  nivel_id    text not null,             -- același id ca pe hartă: harap-1, moara-1 ...
  intrebare   text not null,
  variante    jsonb not null,            -- lista cu cele 4 variante
  corect      int  not null,             -- poziția variantei corecte, de la 0
  explicatie  text
);

create index if not exists intrebari_nivel_idx on public.intrebari (nivel_id);

-- Oricine poate CITI întrebările (nimeni nu le poate modifica din aplicație).
alter table public.intrebari enable row level security;
drop policy if exists "citire publica intrebari" on public.intrebari;
create policy "citire publica intrebari" on public.intrebari for select using (true);

insert into public.intrebari (id, nivel_id, intrebare, variante, corect, explicatie) values
  ('harap-1-a', 'harap-1', 'În ce an și în ce revistă a fost publicată „Povestea lui Harap-Alb”?', '["1881, în volumul „Novele din popor”", "1877, în revista „Convorbiri literare”", "1883, în „Almanahul România Jună”", "1890, în revista „Familia”"]'::jsonb, 1, 'Basmul lui Creangă a apărut la 1 august 1877, în „Convorbiri literare”, revista Junimii.'),
  ('harap-1-b', 'harap-1', 'Cărei specii literare îi aparține „Povestea lui Harap-Alb”?', '["Basm popular", "Nuvelă", "Basm cult", "Legendă"]'::jsonb, 2, 'Este un basm cult: păstrează tiparul basmului, dar are un autor cunoscut, Ion Creangă.'),
  ('harap-1-c', 'harap-1', 'De ce „Povestea lui Harap-Alb” este un basm cult, nu unul popular?', '["Pentru că are personaje fabuloase", "Pentru că binele învinge răul", "Pentru că s-a transmis oral, din generație în generație", "Pentru că are un autor cunoscut și o formă fixată în scris"]'::jsonb, 3, 'Basmul popular e anonim și oral. Cel cult are autor și formă scrisă; personajele fabuloase și victoria binelui apar în ambele.'),
  ('harap-2-a', 'harap-2', 'Unde îl păcălește Spânul pe fiul craiului?', '["La pod", "La o fântână", "În Grădina Ursului", "La curtea Împăratului Roș"]'::jsonb, 1, 'Spânul îl convinge să coboare în fântână, apoi îl închide acolo până acceptă să-i devină slugă.'),
  ('harap-2-b', 'harap-2', 'Ce nume primește fiul craiului după ce devine sluga Spânului?', '["Harap-Alb", "Făt-Frumos", "Ochilă", "Setilă"]'::jsonb, 0, 'Spânul îi dă numele de Harap-Alb, adică „rob alb”, și îi ia locul de nepot al Împăratului Verde.'),
  ('harap-2-c', 'harap-2', 'Care este prima probă dată de Spân lui Harap-Alb?', '["Să aducă pielea cerbului", "Să o aducă pe fata Împăratului Roș", "Să aducă salăți din Grădina Ursului", "Să treacă prin casa de aramă"]'::jsonb, 2, 'Prima încercare este aducerea salăților din Grădina Ursului, apoi pielea cerbului și, la final, fata Împăratului Roș.'),
  ('moara-1-a', 'moara-1', 'Când și în ce volum a apărut nuvela „Moara cu noroc”?', '["1877, în „Convorbiri literare”", "1881, în volumul „Novele din popor”", "1883, în „Almanahul România Jună”", "1887, în revista „Familia”"]'::jsonb, 1, 'Nuvela lui Slavici a fost publicată în 1881, în volumul „Novele din popor”.'),
  ('moara-1-b', 'moara-1', 'Ce fel de nuvelă este „Moara cu noroc”?', '["Nuvelă istorică", "Nuvelă fantastică", "Nuvelă psihologică", "Nuvelă romantică"]'::jsonb, 2, 'Este o nuvelă psihologică: urmărește transformările interioare ale lui Ghiță, măcinat de patima banului.'),
  ('moara-1-c', 'moara-1', 'Cărui curent literar îi aparține „Moara cu noroc”?', '["Romantism", "Realism", "Simbolism", "Modernism"]'::jsonb, 1, 'Opera e realistă: lume verosimilă, personaje tipice, narator obiectiv și omniscient, stil sobru.'),
  ('luceafarul-1-a', 'luceafarul-1', 'Unde a fost publicat pentru prima dată poemul „Luceafărul”?', '["În revista „Convorbiri literare”, în 1877", "În volumul „Novele din popor”, în 1881", "În „Almanahul Societății România Jună” din Viena, în 1883", "În revista „Familia”, în 1866"]'::jsonb, 2, 'Poemul a apărut în aprilie 1883, în almanahul societății „România Jună” din Viena, apoi în „Convorbiri literare”.'),
  ('luceafarul-1-b', 'luceafarul-1', 'Cărui curent literar îi aparține „Luceafărul”?', '["Realism", "Clasicism", "Simbolism", "Romantism"]'::jsonb, 3, 'E un poem romantic: antiteza dintre lumi, planul cosmic și cel terestru, motive ca noaptea, visul și fereastra.'),
  ('luceafarul-1-c', 'luceafarul-1', 'Care este tema principală a poemului „Luceafărul”?', '["Patima banului", "Condiția omului de geniu", "Lupta pentru pământ", "Iubirea pentru natura satului"]'::jsonb, 1, 'Poemul e o alegorie despre condiția geniului, care nu poate fi fericit în lumea oamenilor.')
on conflict (id) do update set
  nivel_id = excluded.nivel_id,
  intrebare = excluded.intrebare,
  variante = excluded.variante,
  corect = excluded.corect,
  explicatie = excluded.explicatie;
