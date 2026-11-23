using CSharpFunctionalExtensions;
using LedgerCore.Application.Common.Interfaces;
using MediatR;

namespace LedgerCore.Application.Features.Users.Queries.GetAllUsers
{
    public static class GetAllUsers
    {
        public record Query() : IRequest<Result<Response>>;
        public record Response(List<UserInfo> Users);

        public class Handler(IIdentityService identity) : IRequestHandler<Query, Result<Response>>
        {
            public async Task<Result<Response>> Handle(Query request, CancellationToken ct) =>
                Result.Success(new Response(await identity.ListUsers(ct)));
        }
    }
}
