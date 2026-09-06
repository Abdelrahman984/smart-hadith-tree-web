$ErrorActionPreference = "Stop"

echo "Step 1: Create the console app"
dotnet new console -n SmartHadithTree.Etl -o d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Etl -f net9.0

echo "Step 2: Add it to the solution"
dotnet sln d:\Programming\Full-Stack\Smart-Hadith-Tree\SmartHadithTree.sln add d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Etl\SmartHadithTree.Etl.csproj

echo "Step 3: Add project references"
dotnet add d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Etl\SmartHadithTree.Etl.csproj reference d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Domain\SmartHadithTree.Domain.csproj d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Infrastructure\SmartHadithTree.Infrastructure.csproj

echo "Step 4: Add NuGet packages"
dotnet add d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Etl\SmartHadithTree.Etl.csproj package EFCore.BulkExtensions
dotnet add d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Etl\SmartHadithTree.Etl.csproj package Microsoft.Extensions.Hosting
dotnet add d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Etl\SmartHadithTree.Etl.csproj package Microsoft.EntityFrameworkCore.SqlServer -v "9.0.*"

echo "Step 5: Verify it builds"
dotnet build d:\Programming\Full-Stack\Smart-Hadith-Tree\SmartHadithTree.sln
