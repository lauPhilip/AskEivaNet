using Microsoft.AspNetCore.Identity;
using AskEiva.Domain.Entities;

namespace AskEiva.Infrastructure.Repositories;

public class WeaviateUserStore : IUserStore<ApplicationUser>, IUserPasswordStore<ApplicationUser>, IUserEmailStore<ApplicationUser>
{
    private readonly UserRepository _userRepository;

    public WeaviateUserStore(UserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    // --- CORE USER STORE MANDATES ---
    public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken) 
        => Task.FromResult(user.Id);

    public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) 
        => Task.FromResult<string?>(user.Email);

    public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken) 
    { 
        user.Email = userName ?? string.Empty; 
        return Task.CompletedTask; 
    }

    // --- NORMALIZED USERNAME STORAGE HANDLERS ---
    public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) 
        => Task.FromResult<string?>(user.Email?.ToUpperInvariant());

    public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken) 
    { 
        if (string.IsNullOrEmpty(user.Email) && !string.IsNullOrEmpty(normalizedName))
        {
            user.Email = normalizedName.ToLowerInvariant();
        }
        return Task.CompletedTask; 
    }

    // --- DATA PERSISTENCE PIPELINES ---
    public async Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        await _userRepository.CreateAsync(user);
        return IdentityResult.Success;
    }

    public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken) 
        => Task.FromResult(IdentityResult.Success);

    public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken) 
        => Task.FromResult(IdentityResult.Success);

    // --- DATA RECOVERY PIPELINES ---
    public async Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        return await _userRepository.FindByEmailAsync(userId);
    }

    public async Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        return await _userRepository.FindByEmailAsync(normalizedUserName.ToLowerInvariant());
    }

    // --- PASSWORD STORE IMPLEMENTATION ---
    public Task SetPasswordHashAsync(ApplicationUser user, string? passwordHash, CancellationToken cancellationToken)
    {
        user.PasswordHash = passwordHash ?? string.Empty;
        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(ApplicationUser user, CancellationToken cancellationToken) 
        => Task.FromResult<string?>(user.PasswordHash);

    public Task<bool> HasPasswordAsync(ApplicationUser user, CancellationToken cancellationToken) 
        => Task.FromResult(!string.IsNullOrEmpty(user.PasswordHash));

    // --- EMAIL STORE IMPLEMENTATION ---
    public Task SetEmailAsync(ApplicationUser user, string? email, CancellationToken cancellationToken) 
    { 
        user.Email = email ?? string.Empty; 
        return Task.CompletedTask; 
    }

    public Task<string?> GetEmailAsync(ApplicationUser user, CancellationToken cancellationToken) 
        => Task.FromResult<string?>(user.Email);

    public Task<bool> GetEmailConfirmedAsync(ApplicationUser user, CancellationToken cancellationToken) 
        => Task.FromResult(true);

    public Task SetEmailConfirmedAsync(ApplicationUser user, bool confirmed, CancellationToken cancellationToken) 
        => Task.CompletedTask;

    public async Task<ApplicationUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        return await _userRepository.FindByEmailAsync(normalizedEmail.ToLowerInvariant());
    }

    // --- NORMALIZED EMAIL STORAGE HANDLERS ---
    public Task<string?> GetNormalizedEmailAsync(ApplicationUser user, CancellationToken cancellationToken) 
        => Task.FromResult<string?>(user.Email?.ToUpperInvariant());

    public Task SetNormalizedEmailAsync(ApplicationUser user, string? normalizedEmail, CancellationToken cancellationToken) 
    { 
        if (string.IsNullOrEmpty(user.Email) && !string.IsNullOrEmpty(normalizedEmail))
        {
            user.Email = normalizedEmail.ToLowerInvariant();
        }
        return Task.CompletedTask; 
    }

    public void Dispose() { }
}