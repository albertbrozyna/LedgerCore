using CSharpFunctionalExtensions;
using FluentValidation;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace LedgerCore.Application.Features.Users.Commands.Block
{
    public partial class BlockUser
    {

        public class BlockUserHandler : IRequestHandler<Command, Result<Response>>
        {
            public Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }
    }
}
