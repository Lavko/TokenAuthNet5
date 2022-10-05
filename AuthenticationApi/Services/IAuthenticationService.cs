using AuthenticationApi.Dtos;
using FluentResults;

namespace AuthenticationApi.Services;

public interface IAuthenticationService
{
    Task<Result<string>> Register(RegisterRequest request);
    Task<Result<string>> Login(LoginRequest request);
}

