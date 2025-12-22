using LedgerCore.Application.Common.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace LedgerCore.Infrastructure.Services
{
    public class LocalFileStorageService(IWebHostEnvironment environment, ILogger<LocalFileStorageService> logger) : IFileStorageService
    {

        public void DeleteFile(string folderName, string filename)
        {
            string rootPath = environment.ContentRootPath;
            string fullPath = Path.Combine(rootPath, folderName, filename);

            if (File.Exists(fullPath))
            {
                try
                {
                    File.Delete(fullPath);
                }
                catch (IOException e)
                {
                    logger.LogError(e, e.Message);
                }
            }
        }

        public bool FileExist(string folderName, string filename)
        {
            string rootPath = environment.ContentRootPath;
            string fullPath = Path.Combine(rootPath, folderName, filename);

            return File.Exists(fullPath);
        }

        public async Task<string> SaveFileAsync(string folderName,string fileName, IFormFile file, CancellationToken cancellationToken)
        {
            string rootPath = environment.ContentRootPath;

            // 2. Tworzymy podfolder, żeby nie śmiecić w root
            string subFolder = Path.Combine("uploads", "profile-photos");
            string fullFolderPath = Path.Combine(rootPath, subFolder);

            if (!Directory.Exists(fullFolderPath))
            {
                Directory.CreateDirectory(fullFolderPath);
            }

            string fullFileName = $"{fileName}{Path.GetExtension(file.FileName)}";
            string fullPath = Path.Combine(fullFolderPath, fullFileName);

            // 4. Zapis fizyczny
            using var fileStream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(fileStream, cancellationToken);

            // 5. ZWRACAMY ścieżkę relatywną (to zapisujesz w bazie danych)
            // Zwróć: "/uploads/profile-photos/nazwa-pliku.jpg"
            return Path.Combine("/", subFolder, fileName).Replace("\\", "/");
        }
    }
}
