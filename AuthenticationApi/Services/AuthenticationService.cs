using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AuthenticationApi.Dtos;
using AuthenticationApi.Entities;
using FluentResults;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace AuthenticationApi.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly UserManager<User> _userManager;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public AuthenticationService (
        UserManager<User> userManager,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _userManager = userManager;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
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
            SecurityStamp = Guid.NewGuid().ToString(),
            Provider = Consts.LoginProviders.Password
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
        
        if (user.Provider != Consts.LoginProviders.Password)
        {
            return Result.Fail($"User was registered via {user.Provider} and cannot be logged via {Consts.LoginProviders.Password}.");
        }

        var token = GetToken(await GetClaims(user));

        return Result.Ok(new JwtSecurityTokenHandler().WriteToken(token));
    }

    public async Task<Result<string>> SocialLogin(SocialLoginRequest request)
    {
        var tokenValidationResult = await ValidateSocialToken(request);

        if (tokenValidationResult.IsFailed)
        {
            return tokenValidationResult;
        }

        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user is null)
        {
            var registerResult = await RegisterSocialUser(request);
            
            if (registerResult.IsFailed)
            {
                return tokenValidationResult;
            }

            user = registerResult.Value;
        }
            
        if (user.Provider != request.Provider)
        {
            return Result.Fail($"User was registered via {user.Provider} and cannot be logged via {request.Provider}.");
        }

        var token = GetToken(await GetClaims(user));

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

    private async Task<Result> ValidateSocialToken(SocialLoginRequest request)
    {
        return request.Provider switch
        {
            Consts.LoginProviders.Facebook => await ValidateFacebookToken(request),
            Consts.LoginProviders.Google => await ValidateGoogleToken(request),
            _ => Result.Fail($"{request.Provider} provider is not supported.")
        };
    }

    private async Task<Result> ValidateFacebookToken(SocialLoginRequest request)
    {
        var httpClient = _httpClientFactory.CreateClient();
        var appAccessTokenResponse = await httpClient.GetFromJsonAsync<FacebookAppAccessTokenResponse>($"https://graph.facebook.com/oauth/access_token?client_id={_configuration["SocialLogin:Facebook:ClientId"]}&client_secret={_configuration["SocialLogin:Facebook:ClientSecret"]}&grant_type=client_credentials");
        var response =
            await httpClient.GetFromJsonAsync<FacebookTokenValidationResult>(
                $"https://graph.facebook.com/debug_token?input_token={request.AccessToken}&access_token={appAccessTokenResponse!.AccessToken}");

        if (response is null || !response.Data.IsValid)
        {
            return Result.Fail($"{request.Provider} access token is not valid.");
        }
        
        return Result.Ok();
    }

    private async Task<Result> ValidateGoogleToken(SocialLoginRequest request)
    {
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new List<string> { _configuration["SocialLogin:Google:TokenAudience"] }
            };
            await GoogleJsonWebSignature.ValidateAsync(request.AccessToken, settings);
                
        }
        catch (InvalidJwtException _)
        {
            return Result.Fail($"{request.Provider} access token is not valid.");
        }
        
        return Result.Ok();
    }

    private async Task<Result<User>> RegisterSocialUser(SocialLoginRequest request)
    {
        var user = new User()
        {
            Email = request.Email,
            UserName = request.Email,
            SecurityStamp = Guid.NewGuid().ToString(),
            Provider = request.Provider!
        };
                
        var result = await _userManager.CreateAsync(user, $"Pass!1{Guid.NewGuid().ToString()}");
            
        if(!result.Succeeded)
        {
            return Result.Fail($"Unable to register user {request.Email}, errors: {GetErrorsText(result.Errors)}");
        }

        await _userManager.AddToRoleAsync(user, Role.User);
        
        return Result.Ok(user);
    }

    private async Task<List<Claim>> GetClaims(User user)
    {
        var authClaims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Email!),
            new(ClaimTypes.Email, user.Email!),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        
        var userRoles = await _userManager.GetRolesAsync(user);

        if (userRoles is not null && userRoles.Any())
        {
            authClaims.AddRange(userRoles.Select(userRole => new Claim(ClaimTypes.Role, userRole)));
        }

        return authClaims;
    }
}

