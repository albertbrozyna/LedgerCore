using AutoMapper;
using AutoMapper.QueryableExtensions;
using CSharpFunctionalExtensions;
using FluentValidation;
using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Application.Features.Users.Queries.GetAllUsers
{
    public static class GetAllUsers
    {
        public record Query() : IRequest<Result<Response>>;

        public record UserDto(
             Guid Id,
             string FirstName,
             string LastName,
             string Email,
             UserRole Role,
             bool IsActive,
             bool IsDeleted,
             DateTime CreatedAt,
             DateTime LastLogin,
             string? PhoneNumber,
             string AvatarUrl
            );

        public record Response(List<UserDto> Users);

        public class Validator : AbstractValidator<Query>
        {
            public Validator()
            {
            }
        }

        public class ResponseProfile : Profile
        {
            public ResponseProfile()
            {
                CreateMap<User, UserDto>();
            }
        }

        public class Handler(IAppDbContext appDbContext, IMapper mapper) : IRequestHandler<Query, Result<Response>>
        {
            private readonly IAppDbContext _appDbContext = appDbContext;
            private readonly IMapper _mapper = mapper;

            public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
            {
                var users = await _appDbContext.Users.AsNoTracking().ProjectTo<UserDto>(_mapper.ConfigurationProvider).ToListAsync(cancellationToken);

                return Result.Success(new Response(users));
            }
        }
    }
}
