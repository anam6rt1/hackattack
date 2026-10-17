namespace MarkAi.Data.Trivia;

// Versioned SQL for PostgreSQL-only invariants that EF check constraints cannot express.
internal static class TriviaDatabaseRules
{
    internal const string Create = """
CREATE FUNCTION mark_ai.trivia_check_participant() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE r mark_ai.trivia_camera%ROWTYPE;
BEGIN
 IF TG_OP <> 'INSERT' THEN
  RAISE EXCEPTION 'Participant membership is immutable' USING ERRCODE='23514';
 END IF;
 SELECT * INTO STRICT r FROM mark_ai.trivia_camera WHERE id=NEW.camera_id FOR UPDATE;
 IF r.status <> 'waiting' THEN RAISE EXCEPTION 'Room is not waiting' USING ERRCODE='23514'; END IF;
 IF (SELECT count(*) FROM mark_ai.trivia_participant WHERE camera_id=NEW.camera_id) >= r.capacitate THEN
  RAISE EXCEPTION 'Room is full' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER trivia_participant_guard BEFORE INSERT OR UPDATE OR DELETE ON mark_ai.trivia_participant
 FOR EACH ROW EXECUTE FUNCTION mark_ai.trivia_check_participant();

CREATE FUNCTION mark_ai.trivia_check_round() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE r mark_ai.trivia_camera%ROWTYPE;
BEGIN
 IF TG_OP <> 'INSERT' THEN RAISE EXCEPTION 'Round snapshots are immutable' USING ERRCODE='23514'; END IF;
 SELECT * INTO STRICT r FROM mark_ai.trivia_camera WHERE id=NEW.camera_id FOR UPDATE;
 IF r.status = 'finished' THEN RAISE EXCEPTION 'Room is finished' USING ERRCODE='23514'; END IF;
 IF EXISTS (SELECT 1 FROM mark_ai.trivia_runda WHERE camera_id=NEW.camera_id AND termen_la > clock_timestamp()) THEN
  RAISE EXCEPTION 'Previous round is still open' USING ERRCODE='23514';
 END IF;
 IF (SELECT count(*) FROM mark_ai.trivia_participant WHERE camera_id=NEW.camera_id) < 2 THEN
  RAISE EXCEPTION 'At least two participants required' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER trivia_runda_guard BEFORE INSERT OR UPDATE OR DELETE ON mark_ai.trivia_runda
 FOR EACH ROW EXECUTE FUNCTION mark_ai.trivia_check_round();

CREATE FUNCTION mark_ai.trivia_check_answer() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE r mark_ai.trivia_runda%ROWTYPE; s text; correct boolean;
BEGIN
 IF TG_OP <> 'INSERT' THEN RAISE EXCEPTION 'Answers are immutable' USING ERRCODE='23514'; END IF;
 -- Shared room lock orders submission against finish/start operations; other answers may run concurrently.
 SELECT status INTO STRICT s FROM mark_ai.trivia_camera WHERE id=NEW.camera_id FOR SHARE;
 SELECT * INTO STRICT r FROM mark_ai.trivia_runda WHERE id=NEW.runda_id AND camera_id=NEW.camera_id FOR SHARE;
 NEW.trimis_la := clock_timestamp();
 IF s <> 'active' OR NEW.trimis_la < r.inceputa_la OR NEW.trimis_la >= r.termen_la THEN
  RAISE EXCEPTION 'Round is closed or deadline expired' USING ERRCODE='23514';
 END IF;
 SELECT corecta INTO STRICT correct FROM mark_ai.trivia_runda_varianta WHERE id=NEW.varianta_id AND runda_id=NEW.runda_id;
 NEW.scor := CASE WHEN correct THEN r.puncte ELSE 0 END;
 RETURN NEW;
END $$;
CREATE TRIGGER trivia_raspuns_guard BEFORE INSERT OR UPDATE OR DELETE ON mark_ai.trivia_raspuns
 FOR EACH ROW EXECUTE FUNCTION mark_ai.trivia_check_answer();

CREATE FUNCTION mark_ai.trivia_immutable_option() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 RAISE EXCEPTION 'Round option snapshots are immutable' USING ERRCODE='23514';
END $$;
CREATE TRIGGER trivia_runda_varianta_guard BEFORE UPDATE OR DELETE ON mark_ai.trivia_runda_varianta
 FOR EACH ROW EXECUTE FUNCTION mark_ai.trivia_immutable_option();
""";

    internal const string Drop = """
DROP TRIGGER trivia_raspuns_guard ON mark_ai.trivia_raspuns;
DROP TRIGGER trivia_runda_varianta_guard ON mark_ai.trivia_runda_varianta;
DROP TRIGGER trivia_runda_guard ON mark_ai.trivia_runda;
DROP TRIGGER trivia_participant_guard ON mark_ai.trivia_participant;
DROP FUNCTION mark_ai.trivia_check_answer();
DROP FUNCTION mark_ai.trivia_immutable_option();
DROP FUNCTION mark_ai.trivia_check_round();
DROP FUNCTION mark_ai.trivia_check_participant();
""";
}
