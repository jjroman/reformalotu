using Microsoft.AspNetCore.Identity;

namespace Reformk.Identity;

public sealed class SpanishIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() =>
        Describe(nameof(DefaultError), "Se produjo un error inesperado.");

    public override IdentityError ConcurrencyFailure() =>
        Describe(nameof(ConcurrencyFailure), "El registro fue modificado por otra operación.");

    public override IdentityError PasswordMismatch() =>
        Describe(nameof(PasswordMismatch), "La contraseña no es correcta.");

    public override IdentityError InvalidToken() =>
        Describe(nameof(InvalidToken), "El código no es válido.");

    public override IdentityError RecoveryCodeRedemptionFailed() =>
        Describe(nameof(RecoveryCodeRedemptionFailed), "El código de recuperación no es válido.");

    public override IdentityError LoginAlreadyAssociated() =>
        Describe(nameof(LoginAlreadyAssociated), "Ya existe una cuenta asociada con este inicio de sesión.");

    public override IdentityError UserAlreadyHasPassword() =>
        Describe(nameof(UserAlreadyHasPassword), "La cuenta ya tiene una contraseña.");

    public override IdentityError UserLockoutNotEnabled() =>
        Describe(nameof(UserLockoutNotEnabled), "El bloqueo de cuentas no está habilitado.");

    public override IdentityError UserAlreadyInRole(string role) =>
        Describe(nameof(UserAlreadyInRole), $"La cuenta ya pertenece al rol «{role}».");

    public override IdentityError UserNotInRole(string role) =>
        Describe(nameof(UserNotInRole), $"La cuenta no pertenece al rol «{role}».");

    public override IdentityError PasswordTooShort(int length) =>
        Describe(nameof(PasswordTooShort), $"La contraseña debe tener al menos {length} caracteres.");

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        Describe(nameof(PasswordRequiresNonAlphanumeric), "La contraseña debe incluir al menos un símbolo.");

    public override IdentityError PasswordRequiresDigit() =>
        Describe(nameof(PasswordRequiresDigit), "La contraseña debe incluir al menos un número.");

    public override IdentityError PasswordRequiresLower() =>
        Describe(nameof(PasswordRequiresLower), "La contraseña debe incluir al menos una letra minúscula.");

    public override IdentityError PasswordRequiresUpper() =>
        Describe(nameof(PasswordRequiresUpper), "La contraseña debe incluir al menos una letra mayúscula.");

    public override IdentityError InvalidUserName(string? userName) =>
        Describe(nameof(InvalidUserName), $"El nombre de usuario «{userName}» no es válido.");

    public override IdentityError InvalidEmail(string? email) =>
        Describe(nameof(InvalidEmail), $"El correo electrónico «{email}» no es válido.");

    public override IdentityError DuplicateUserName(string userName) =>
        Describe(nameof(DuplicateUserName), $"El nombre de usuario «{userName}» ya está en uso.");

    public override IdentityError DuplicateEmail(string email) =>
        Describe(nameof(DuplicateEmail), $"El correo electrónico «{email}» ya está registrado.");

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        Describe(nameof(PasswordRequiresUniqueChars), $"La contraseña debe incluir al menos {uniqueChars} caracteres distintos.");

    private static IdentityError Describe(string code, string description) =>
        new() { Code = code, Description = description };
}
