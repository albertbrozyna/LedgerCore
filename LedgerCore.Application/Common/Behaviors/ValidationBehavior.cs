using FluentValidation;
using MediatR;

namespace LedgerCore.Application.Common.Behaviors;

// To klasa, która "opakowuje" każde żądanie wysłane przez MediatR
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    // MediatR automatycznie wstrzyknie tu wszystkie walidatory pasujące do danej komendy
    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // 1. Jeśli nie ma walidatorów dla tej komendy, idź dalej
        if (!_validators.Any())
        {
            return await next();
        }

        // 2. Przygotuj kontekst walidacji
        var context = new ValidationContext<TRequest>(request);

        // 3. Uruchom wszystkie walidatory
        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        // 4. Zbierz błędy
        var failures = validationResults
            .Where(r => r.Errors.Any())
            .SelectMany(r => r.Errors)
            .ToList();

        // 5. JEŚLI SĄ BŁĘDY -> Rzuć wyjątek i ZATRZYMAJ proces
        if (failures.Any())
        {
            // Ten wyjątek pochodzi z FluentValidation
            throw new ValidationException(failures);
        }

        // 6. Jeśli jest czysto -> Puść ruch dalej do Handlera
        return await next();
    }
}