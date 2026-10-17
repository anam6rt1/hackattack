-- Semantic schema comparison: normalize unique constraints/indexes and ignore object names.
WITH objects AS (
 SELECT 'column' AS kind, c.table_name AS tbl,
  concat_ws('|',c.column_name,c.ordinal_position,c.data_type,c.udt_name,c.is_nullable,
   c.column_default,c.is_identity,c.identity_generation,c.numeric_precision,c.numeric_scale,c.character_maximum_length) AS definition
 FROM information_schema.columns c WHERE c.table_schema='mark_ai' AND c.table_name <> '__EFMigrationsHistory'
 UNION ALL
 SELECT 'constraint',t.relname,pg_get_constraintdef(c.oid)
 FROM pg_constraint c JOIN pg_class t ON t.oid=c.conrelid
 WHERE c.connamespace='mark_ai'::regnamespace AND c.contype IN ('p','f','c') AND t.relname <> '__EFMigrationsHistory'
 UNION ALL
 SELECT 'index',t.relname,
  concat_ws('|',i.indisunique,am.amname,
   (SELECT string_agg(pg_get_indexdef(i.indexrelid,k,false),',' ORDER BY k) FROM generate_series(1,i.indnatts) k),
   pg_get_expr(i.indpred,i.indrelid))
 FROM pg_index i JOIN pg_class t ON t.oid=i.indrelid JOIN pg_class idx ON idx.oid=i.indexrelid
 JOIN pg_am am ON am.oid=idx.relam
 WHERE t.relnamespace='mark_ai'::regnamespace AND t.relname <> '__EFMigrationsHistory'
)
SELECT json_agg(x ORDER BY kind,tbl,definition) FROM (SELECT DISTINCT * FROM objects) x;
