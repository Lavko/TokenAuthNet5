using AuthenticationApi.Dtos;
using AuthenticationApi.Extensions;
using AuthenticationApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthenticationApi.Controllers;

[ApiController]
[Route("[controller]")]
public class UserController : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;

    public UserController(IAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ResultDto<string>))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ResultDto<string>))]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authenticationService.Login(request);

        var resultDto = result.ToResultDto();

        if (!resultDto.IsSuccess)
        {
            return BadRequest(resultDto);
        }
        return Ok(resultDto);
    }

    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ResultDto<string>))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ResultDto<string>))]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _authenticationService.Register(request);

        var resultDto = result.ToResultDto();

        if (!resultDto.IsSuccess)
        {
            return BadRequest(resultDto);
        }
        return Ok(resultDto);
    }
}

