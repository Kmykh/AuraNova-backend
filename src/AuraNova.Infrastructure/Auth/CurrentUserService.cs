using System;
using System.Security.Claims;
using AuraNova.Application.Auth.Interfaces;
using Microsoft.AspNetCore.Http;

namespace AuraNova.Infrastructure.Auth
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public Guid? UserId
        {
            get
            {
                var userIdStr = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (Guid.TryParse(userIdStr, out var userId))
                {
                    return userId;
                }
                return null;
            }
        }

        public string? Name => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name)
                            ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue("name");

        public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
    }
}
