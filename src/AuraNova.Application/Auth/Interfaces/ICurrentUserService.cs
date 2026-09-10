using System;

namespace AuraNova.Application.Auth.Interfaces
{
    public interface ICurrentUserService
    {
        Guid? UserId { get; }
        string? Name { get; }
        bool IsAuthenticated { get; }
    }
}
