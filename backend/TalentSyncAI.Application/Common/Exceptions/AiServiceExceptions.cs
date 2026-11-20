namespace TalentSyncAI.Application.Common.Exceptions;

public class AiServiceUnavailableException : Exception
{
    public AiServiceUnavailableException(string message) : base(message) { }
    public AiServiceUnavailableException(string message, Exception innerException) : base(message, innerException) { }
}

public class AiServiceBadResponseException : Exception
{
    public AiServiceBadResponseException(string message) : base(message) { }
    public AiServiceBadResponseException(string message, Exception innerException) : base(message, innerException) { }
}
