REM # 1. Создаем решение
dotnet new sln -n AudioRestoration

REM # 2. Создаем проект библиотеки (Ядро)
dotnet new classlib -o AudioRestoration.Core

REM # 3. Создаем проект консольного приложения
dotnet new console -o AudioRestoration.ConsoleApp

REM # 4. Добавляем проекты в решение
dotnet sln add AudioRestoration.Core/AudioRestoration.Core.csproj
dotnet sln add AudioRestoration.ConsoleApp/AudioRestoration.ConsoleApp.csproj

REM # 5. Устанавливаем связь: ConsoleApp зависит от Core
dotnet add AudioRestoration.ConsoleApp/AudioRestoration.ConsoleApp.csproj reference AudioRestoration.Core/AudioRestoration.Core.csproj

REM # 6. Устанавливаем необходимые NuGet пакеты в Core (Математика и AI)
cd AudioRestoration.Core
dotnet add package Microsoft.ML.OnnxRuntime --version 1.17.1
dotnet add package NAudio --version 2.2.1
dotnet add package MathNet.Numerics --version 5.0.0
dotnet add package FFMpegCore --version 5.1.0

REM # 7. Устанавливаем пакеты в ConsoleApp (Интерфейс)
cd ../AudioRestoration.ConsoleApp
dotnet add package ShellProgressBar --version 5.2.0