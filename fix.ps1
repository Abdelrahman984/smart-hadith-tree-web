$ErrorActionPreference = "Stop"

echo "Fixing solution file"
if (Test-Path d:\Programming\Full-Stack\Smart-Hadith-Tree\SmartHadithTree.slnx) {
    Remove-Item d:\Programming\Full-Stack\Smart-Hadith-Tree\SmartHadithTree.slnx
}
if (-not (Test-Path d:\Programming\Full-Stack\Smart-Hadith-Tree\SmartHadithTree.sln)) {
    dotnet new sln -n SmartHadithTree -o d:\Programming\Full-Stack\Smart-Hadith-Tree -f sln
}

echo "Step 6: Add projects to solution"
dotnet sln d:\Programming\Full-Stack\Smart-Hadith-Tree\SmartHadithTree.sln add d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Domain\SmartHadithTree.Domain.csproj d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Infrastructure\SmartHadithTree.Infrastructure.csproj d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Application\SmartHadithTree.Application.csproj d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Api\SmartHadithTree.Api.csproj

echo "Fixing packages (removing 10.x, adding 9.x)"
dotnet remove d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Infrastructure\SmartHadithTree.Infrastructure.csproj package Microsoft.EntityFrameworkCore.Tools
dotnet remove d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Infrastructure\SmartHadithTree.Infrastructure.csproj package Microsoft.EntityFrameworkCore.SqlServer
dotnet remove d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Api\SmartHadithTree.Api.csproj package Microsoft.EntityFrameworkCore.Design

echo "Step 8: Add NuGet packages to Infrastructure (v9)"
dotnet add d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Infrastructure\SmartHadithTree.Infrastructure.csproj package Microsoft.EntityFrameworkCore.SqlServer -v "9.0.*"
dotnet add d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Infrastructure\SmartHadithTree.Infrastructure.csproj package Microsoft.EntityFrameworkCore.Tools -v "9.0.*"

echo "Step 9: Add EF Design package to Api (v9)"
dotnet add d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Api\SmartHadithTree.Api.csproj package Microsoft.EntityFrameworkCore.Design -v "9.0.*"

echo "Step 10: Delete default Class1.cs files"
$ErrorActionPreference = "SilentlyContinue"
Remove-Item d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Domain\Class1.cs
Remove-Item d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Infrastructure\Class1.cs
Remove-Item d:\Programming\Full-Stack\Smart-Hadith-Tree\src\SmartHadithTree.Application\Class1.cs
$ErrorActionPreference = "Stop"

echo "Step 11: Verify solution builds"
dotnet build d:\Programming\Full-Stack\Smart-Hadith-Tree\SmartHadithTree.sln
