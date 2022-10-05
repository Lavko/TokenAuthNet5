using AuthenticationApi.Dtos;
using FluentResults;

namespace AuthenticationApi.Extensions;

public static class ResultDtoExtensions
{
    public static ResultDto<TResponse> ToResultDto<TResponse>(this Result<TResponse> result)
    {
        var errorMessages = result.Errors?.Select(error => error.Message);
        
        return new ResultDto<TResponse>(result.IsSuccess, result.ValueOrDefault, errorMessages);
    }
}