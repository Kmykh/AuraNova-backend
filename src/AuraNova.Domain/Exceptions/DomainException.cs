using System;

namespace AuraNova.Domain.Exceptions
{
    public class DomainException : Exception
    {
        public string ErrorCode { get; }
        public int StatusCode { get; }

        public DomainException(string message, string errorCode = "BAD_REQUEST", int statusCode = 400) : base(message)
        {
            ErrorCode = errorCode;
            StatusCode = statusCode;
        }
    }
}
