namespace MarkAi.Data.Trivia;

public sealed class TriviaRoom
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public int HostId { get; set; }
    public int? SubjectId { get; set; }
    public int Capacity { get; set; } = 8;
    public string Status { get; set; } = "waiting";
    public DateTime CreatedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}

public sealed class TriviaParticipant
{
    public int RoomId { get; set; }
    public int UserId { get; set; }
    public DateTime JoinedAt { get; set; }
}

public sealed class TriviaQuestion
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public string Text { get; set; } = "";
    public bool Active { get; set; } = true;
}

public sealed class TriviaOption
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public string Text { get; set; } = "";
    public bool IsCorrect { get; set; }
}

// Immutable question/option snapshots keep ongoing games independent of bank edits.
public sealed class TriviaRound
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public int Number { get; set; }
    public int QuestionId { get; set; }
    public string QuestionText { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime DeadlineAt { get; set; }
    public int Points { get; set; } = 100;
}

public sealed class TriviaRoundOption
{
    public int Id { get; set; }
    public int RoundId { get; set; }
    public string Text { get; set; } = "";
    public bool IsCorrect { get; set; }
}

public sealed class TriviaAnswer
{
    public int RoundId { get; set; }
    public int UserId { get; set; }
    public int RoomId { get; set; }
    public int OptionId { get; set; }
    public DateTime SubmittedAt { get; set; }
    public int Score { get; set; }
}
