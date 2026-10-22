namespace SineUyum.Api.Exceptions
{
    // For expected, business-rule errors. They carry a locale key (and its
    // arguments) instead of a sentence; GlobalExceptionHandler renders it in
    // the request language (e.g. "matching.notEnoughParticipants") and returns
    // the given HTTP status code. Every other error becomes a generic 500 whose
    // message never leaks.
    public class DomainException : Exception
    {
        public int StatusCode { get; }
        public string MessageKey { get; }
        public object?[] MessageArgs { get; }

        public DomainException(string messageKey, int statusCode = StatusCodes.Status400BadRequest)
            : this(messageKey, Array.Empty<object?>(), statusCode)
        {
        }

        // Arguments are an explicit array so they can never be mistaken for the status code.
        public DomainException(string messageKey, object?[] messageArgs, int statusCode = StatusCodes.Status400BadRequest)
            : base(messageKey)
        {
            MessageKey = messageKey;
            MessageArgs = messageArgs;
            StatusCode = statusCode;
        }

        public static DomainException NotFound(string messageKey, params object?[] messageArgs) =>
            new(messageKey, messageArgs, StatusCodes.Status404NotFound);
    }
}
