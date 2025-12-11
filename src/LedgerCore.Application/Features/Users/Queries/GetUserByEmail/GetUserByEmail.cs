using AutoMapper;
using AutoMapper.QueryableExtensions;
using CSharpFunctionalExtensions;
using FluentValidation;
using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Application.Features.Users.Queries.GetUserByEmail
{
    public static class GetUserByEmail
    {
        public record Query(string Email) : IRequest<Result<Response>>;
        public record Response(
        Guid Id,
        string FirstName,
        string LastName,
        string Email,
        string Role,
        bool IsActive,
        string? PhoneNumber,
        string? AvatarUrl,
        DateTime CreatedAt,
        DateTime LastLogin
                    );

        public class Validator : AbstractValidator<Query>
        {
            public Validator()
            {
                RuleFor(x => x.Email).NotEmpty().WithMessage("Email cannot be empty").EmailAddress().WithMessage("Incorrect email adress");
            }
        }

        public class ResponseProfile : Profile
        {
            public ResponseProfile()
            {
                CreateMap<User, Response>();
            }
        }

        public class Handler : IRequestHandler<Query, Result<Response>>
        {
            private readonly IAppDbContext _appDbContext;
            private readonly IMapper _mapper;

            public Handler(IAppDbContext appDbContext, IMapper mapper)
            {
                _appDbContext = appDbContext;
                _mapper = mapper;
            }


            public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
            {
                var userResponse = await _appDbContext.Users.AsNoTracking().Where(user => user.Email == request.Email).ProjectTo<Response>(_mapper.ConfigurationProvider).FirstOrDefaultAsync(cancellationToken);

                if (userResponse == null)
                {
                    return Result.Failure<Response>("User with this email don't exist");
                }

                return Result.Success(userResponse);
            }
        }
    }
}
