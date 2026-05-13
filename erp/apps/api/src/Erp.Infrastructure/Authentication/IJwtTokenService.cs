using Erp.Infrastructure.Identity;

namespace Erp.Infrastructure.Authentication;

public interface IJwtTokenService
{
    JwtToken CreateAccessToken(ApplicationUser user, IEnumerable<string> roles);
}
