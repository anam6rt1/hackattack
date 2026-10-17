-- Read-only fingerprints of the original application tables; no row contents are returned.
SELECT format(
 'SELECT %L || ''|'' || count(*)::text || ''|'' || md5(coalesce(string_agg(to_jsonb(t)::text, '''' ORDER BY to_jsonb(t)::text), '''')) FROM %I.%I t',
 tablename, schemaname, tablename)
FROM pg_tables
WHERE schemaname='mark_ai' AND tablename <> '__EFMigrationsHistory' AND tablename NOT LIKE 'trivia_%'
ORDER BY tablename
\gexec
