$ErrorActionPreference = "Stop"
echo "Step 2: Create Domain"
dotnet new classlib -n SmartHadithTree.Domain -o d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Domain -f net9.0

echo "Step 3: Create Infrastructure"
dotnet new classlib -n SmartHadithTree.Infrastructure -o d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Infrastructure -f net9.0

echo "Step 4: Create Application"
dotnet new classlib -n SmartHadithTree.Application -o d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Application -f net9.0

echo "Step 5: Create Web API"
dotnet new webapi -n SmartHadithTree.Api -o d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Api -f net9.0 --no-openapi

echo "Step 6: Add projects to solution"
dotnet sln d:\Programming\Full-Stack\Smart-Hadith-Tree\SmartHadithTree.sln add d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Domain\SmartHadithTree.Domain.csproj d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Infrastructure\SmartHadithTree.Infrastructure.csproj d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Application\SmartHadithTree.Application.csproj d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Api\SmartHadithTree.Api.csproj

echo "Step 7: Add project references"
dotnet add d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Infrastructure\SmartHadithTree.Infrastructure.csproj reference d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Domain\SmartHadithTree.Domain.csproj
dotnet add d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Application\SmartHadithTree.Application.csproj reference d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Domain\SmartHadithTree.Domain.csproj
dotnet add d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Api\SmartHadithTree.Api.csproj reference d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Domain\SmartHadithTree.Domain.csproj d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Infrastructure\SmartHadithTree.Infrastructure.csproj d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Application\SmartHadithTree.Application.csproj

echo "Step 8: Add NuGet packages to Infrastructure"
dotnet add d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Infrastructure\SmartHadithTree.Infrastructure.csproj package Microsoft.EntityFrameworkCore.SqlServer
dotnet add d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Infrastructure\SmartHadithTree.Infrastructure.csproj package Microsoft.EntityFrameworkCore.Tools

echo "Step 9: Add EF Design package to Api"
dotnet add d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Api\SmartHadithTree.Api.csproj package Microsoft.EntityFrameworkCore.Design

echo "Step 10: Delete default Class1.cs files"
Remove-Item d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Domain\Class1.cs
Remove-Item d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Infrastructure\Class1.cs
Remove-Item d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Application\Class1.cs

echo "Step 11: Verify solution builds"
dotnet build d:\Programming\Full-Stack\Smart-Hadith-Tree\SmartHadithTree.sln
