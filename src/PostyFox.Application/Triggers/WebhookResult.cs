namespace PostyFox.Application.Triggers;

public enum WebhookOutcome { UnknownSource, Unauthorized, Challenge, AlreadyProcessed, Processed }

public sealed record WebhookResult(WebhookOutcome Outcome, string? Challenge = null, int FiredCount = 0);
