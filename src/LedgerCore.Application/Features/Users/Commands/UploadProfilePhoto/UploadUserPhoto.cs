using CSharpFunctionalExtensions;
using FluentValidation;
using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Application.Common.Settings;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;


namespace LedgerCore.Application.Features.Users.Commands.UploadProfilePhoto
{

    public static class UploadProfilePhoto { 
        public record Command(IFormFile ProfilePhoto) : IRequest<Result<Response>>;
        public record Response();

        public class Validator : AbstractValidator<Command>
        {
            const long MaxSize = 2 * 1024 * 1024; // 2 MB

            public Validator()
            {
                RuleFor(x => x.ProfilePhoto).NotEmpty()
                    .WithMessage("File is required")
                    .Must(file => file.Length > 0)
                    .WithMessage("File is empty")
                    .Must(file => file.Length <= MaxSize)
                    .WithMessage("File is too big");

            }
        }

        public class UploadProfilePhotoHandler : IRequestHandler<Command, Result<Response>>
        {
            private readonly IIdentityService _identityService;
            private readonly IUserDetails _userDetails;
            private readonly IFileStorageService _fileStorageService;
            private readonly FileStorageSettings _fileStorageSettings;
            public UploadProfilePhotoHandler(IIdentityService identityService,IUserDetails userDetails,IFileStorageService fileStorageService,IOptions<FileStorageSettings> fileStorageSettings)
            {
                _identityService = identityService;
                _userDetails = userDetails;
                _fileStorageService = fileStorageService;
                _fileStorageSettings = fileStorageSettings.Value;
            }

            public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
            {
                var userId = _userDetails.User.Id;

                _fileStorageService.DeleteFile(_fileStorageSettings.BaseFolder,userId.ToString());

                string profilePhotoPath = await _fileStorageService.SaveFileAsync(_fileStorageSettings.BaseFolder,userId.ToString(), request.ProfilePhoto, cancellationToken);

                var result = await _identityService.UpdateUserProfilePhoto(userId, profilePhotoPath);

                if (result.IsFailure)
                {
                    _fileStorageService.DeleteFile(_fileStorageSettings.BaseFolder, userId.ToString());
                    return Result.Failure<Response>("Failed to update user profile photo");
                }

                return Result.Success(new Response());
            }
        }
    }
}
