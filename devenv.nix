{ pkgs, ... }:

{
  # .NET 3.5 development environment for BroforceMods
  packages = with pkgs; [
    mono
    msbuild
    nuget
    gnumake
    inotify-tools
    zip
    unzip
    python3
  ];

  # Environment variables
  env = {
    MONO_PATH = "${pkgs.mono}/lib/mono/4.5";
    DOTNET_CLI_TELEMETRY_OPTOUT = "1";
  };


  # Disable cachix to avoid trust issues
  cachix.enable = false;

  # Shell hook for when environment is activated
  enterShell = ''
    echo "🎮 BroforceMods Development Environment"
    echo ""
    echo "Available commands:"
    echo "  make         - Build only changed mods"
    echo "  make -j8     - Parallel build"
    echo "  make list    - Show all mods"
    echo "  make clean   - Clean build artifacts"
    echo "  make help    - Show all make targets"
  '';
}