using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace LedgerCore.Application.Common.Interfaces
{
    public interface IFileStorageService
    {
        Task<string> SaveFileAsync(string folderName, string fileName,IFormFile file,CancellationToken cancellationToken);
        void DeleteFile(string folderName, string filename);

        bool FileExist(string folderName, string filename);

    }
}
