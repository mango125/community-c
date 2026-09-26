using Community_C.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Community_C.Utility.Security
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class AccessTokenOnlyAttribute : Attribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            if (context.HttpContext.User.Identity?.IsAuthenticated != true)
            {
                return;
            }

            string? tokenType = context.HttpContext.User
                .FindFirst(AuthTokenConstants.TokenTypeClaim)?.Value;

            if (tokenType != AuthTokenConstants.AccessTokenType)
            {
                context.Result = new UnauthorizedResult();
            }
        }
    }
}
