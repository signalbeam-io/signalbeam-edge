---
paths:
  - "src/**/*Validator*.cs"
---

# Validator Rules (FluentValidation)

## Structure
- Inherit from `AbstractValidator<TCommand>` or `AbstractValidator<TRequest>`
- Rules defined in constructor
- One validator per command/request

## Registration
- Register all validators from assembly: `services.AddValidatorsFromAssemblyContaining<TValidator>()`
- Validators injected into handlers or run via middleware

## Conventions
- Use `.NotEmpty()`, `.MaximumLength()`, `.Must()` for rules
- Provide `.WithMessage()` for user-facing messages
- Provide `.WithErrorCode()` using SCREAMING_SNAKE_CASE codes matching domain error codes
- Validate at application boundary (commands/requests), not deep in domain

## Example
```csharp
public class RegisterDeviceValidator : AbstractValidator<RegisterDeviceCommand>
{
    public RegisterDeviceValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithErrorCode("TENANT_ID_REQUIRED");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).WithErrorCode("INVALID_DEVICE_NAME");
    }
}
```
