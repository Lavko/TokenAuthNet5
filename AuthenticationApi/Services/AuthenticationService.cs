using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AuthenticationApi.Dtos;
using AuthenticationApi.Entities;
using FluentResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace AuthenticationApi.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly UserManager<User> _userManager;
    private readonly IConfiguration _configuration;

    public AuthenticationService (UserManager<User> userManager, IConfiguration configuration)
    {
        _userManager = userManager;
        _configuration = configuration;
    }

    public async Task<Result<string>> Register(RegisterRequest request)
    {
        var userByEmail = await _userManager.FindByEmailAsync(request.Email);
        var userByUsername = await _userManager.FindByNameAsync(request.Username);
        
        if (userByEmail is not null || userByUsername is not null)
        {
            return Result.Fail(new Error($"User with email {request.Email} or username {request.Username} already exists."));
        }

        User user = new()
        {
            Email = request.Email,
            UserName = request.Username,
            SecurityStamp = Guid.NewGuid().ToString()
        };

        var result = await _userManager.CreateAsync(user, request.Password);

        await _userManager.AddToRoleAsync(user, Role.User);

        if(!result.Succeeded)
        {
            return Result.Fail($"Unable to register user {request.Username}, errors: {GetErrorsText(result.Errors)}");
        }

        return await Login(new LoginRequest { Username = request.Email, Password = request.Password });
    }

    public async Task<Result<string>> Login(LoginRequest request)
    {
        var user = await _userManager.FindByNameAsync(request.Username) ?? await _userManager.FindByEmailAsync(request.Username);

        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return Result.Fail($"Unable to authenticate user {request.Username}");
        }

        var authClaims = new List<Claim>
        {
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        
        var userRoles = await _userManager.GetRolesAsync(user);

        if (userRoles is not null && userRoles.Any())
        {
            authClaims.AddRange(userRoles.Select(userRole => new Claim(ClaimTypes.Role, userRole)));
        }

        var token = GetToken(authClaims);

        return Result.Ok(new JwtSecurityTokenHandler().WriteToken(token));
    }

    private JwtSecurityToken GetToken(IEnumerable<Claim> authClaims)
    {
        var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Secret"]));

        var token = new JwtSecurityToken(
            issuer: _configuration["JWT:ValidIssuer"],
            audience: _configuration["JWT:ValidAudience"],
            expires: DateTime.Now.AddHours(3),
            claims: authClaims,
            signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256));

        return token;
    }

    private string GetErrorsText(IEnumerable<IdentityError> errors)
    {
        return string.Join(", ", errors.Select(error => error.Description).ToArray());
    }
}

