#!/usr/bin/env bash

# Create symbolic links for BroforceMods development on Linux/NixOS
# This creates the same links as CREATE LINKS.bat but for Linux

# Get the script's directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
cd "$SCRIPT_DIR"

# Define paths
GAME_PATH="$HOME/.local/share/Steam/steamapps/common/Broforce"
CORE_LIBS="$SCRIPT_DIR/libs/Core Libs"
EXTRA_LIBS="$SCRIPT_DIR/libs/Extra Libs"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo "Creating symbolic links for BroforceMods development..."
echo ""

# Check if game directory exists
if [ ! -d "$GAME_PATH" ]; then
    echo -e "${RED}Error: Broforce installation not found at $GAME_PATH${NC}"
    echo "Please ensure Broforce is installed via Steam"
    exit 1
fi

# Create libs directories if they don't exist
mkdir -p "$CORE_LIBS"
mkdir -p "$EXTRA_LIBS"

echo "Creating Core Library links..."
echo "--------------------------------"

# Core Libraries from Broforce installation
CORE_FILES=(
    "Assembly-CSharp.dll"
    "UnityEngine.dll"
    "UnityEngine.CoreModule.dll"
    "UnityEngine.PhysicsModule.dll"
    "UnityEngine.Physics2DModule.dll"
    "UnityEngine.AnimationModule.dll"
    "UnityEngine.InputLegacyModule.dll"
    "UnityEngine.InputModule.dll"
    "UnityEngine.UI.dll"
    "UnityEngine.UIModule.dll"
    "UnityEngine.TextRenderingModule.dll"
    "UnityEngine.IMGUIModule.dll"
    "UnityEngine.UnityWebRequestModule.dll"
    "UnityEngine.AudioModule.dll"
    "UnityEngine.ParticleSystemModule.dll"
)

# Unity Mod Manager libraries
UMM_FILES=(
    "0Harmony.dll"
    "UnityModManager.dll"
)

# Create symlinks for core game files
for file in "${CORE_FILES[@]}"; do
    source="$GAME_PATH/Broforce_beta_Data/Managed/$file"
    target="$CORE_LIBS/$file"

    if [ -f "$source" ]; then
        if [ -L "$target" ] || [ -f "$target" ]; then
            rm -f "$target"
        fi
        ln -s "$source" "$target"
        echo -e "${GREEN}✓${NC} Linked $file"
    else
        echo -e "${YELLOW}⚠${NC} Source not found: $file"
    fi
done

# Create symlinks for Unity Mod Manager files
for file in "${UMM_FILES[@]}"; do
    source="$GAME_PATH/Broforce_beta_Data/Managed/UnityModManager/$file"
    target="$CORE_LIBS/$file"

    if [ -f "$source" ]; then
        if [ -L "$target" ] || [ -f "$target" ]; then
            rm -f "$target"
        fi
        ln -s "$source" "$target"
        echo -e "${GREEN}✓${NC} Linked $file"
    else
        echo -e "${YELLOW}⚠${NC} Source not found: $file"
    fi
done

echo ""
echo "Creating Extra Library links..."
echo "--------------------------------"

# Extra Libraries
EXTRA_FILES=(
    "Rewired_Core.dll"
    "Rewired_Windows.dll"
    "Steamworks.NET.dll"
    "Unity.Analytics.DataPrivacy.dll"
    "Unity.TextMeshPro.dll"
    "UnityEngine.AccessibilityModule.dll"
    "UnityEngine.AIModule.dll"
    "UnityEngine.AndroidJNIModule.dll"
    "UnityEngine.AssetBundleModule.dll"
    "UnityEngine.ClothModule.dll"
    "UnityEngine.ClusterInputModule.dll"
    "UnityEngine.ClusterRendererModule.dll"
    "UnityEngine.CrashReportingModule.dll"
    "UnityEngine.DirectorModule.dll"
    "UnityEngine.DSPGraphModule.dll"
    "UnityEngine.GameCenterModule.dll"
    "UnityEngine.GridModule.dll"
    "UnityEngine.HotReloadModule.dll"
    "UnityEngine.ImageConversionModule.dll"
    "UnityEngine.JSONSerializeModule.dll"
    "UnityEngine.LocalizationModule.dll"
    "UnityEngine.PerformanceReportingModule.dll"
    "UnityEngine.ScreenCaptureModule.dll"
    "UnityEngine.SharedInternalsModule.dll"
    "UnityEngine.SpriteMaskModule.dll"
    "UnityEngine.SpriteShapeModule.dll"
    "UnityEngine.StreamingModule.dll"
    "UnityEngine.SubstanceModule.dll"
    "UnityEngine.SubsystemsModule.dll"
    "UnityEngine.TerrainModule.dll"
    "UnityEngine.TerrainPhysicsModule.dll"
    "UnityEngine.TilemapModule.dll"
    "UnityEngine.TLSModule.dll"
    "UnityEngine.UmbraModule.dll"
    "UnityEngine.UNETModule.dll"
    "UnityEngine.UnityAnalyticsModule.dll"
    "UnityEngine.UnityConnectModule.dll"
    "UnityEngine.UnityTestProtocolModule.dll"
    "UnityEngine.UnityWebRequestAssetBundleModule.dll"
    "UnityEngine.UnityWebRequestAudioModule.dll"
    "UnityEngine.UnityWebRequestTextureModule.dll"
    "UnityEngine.UnityWebRequestWWWModule.dll"
    "UnityEngine.VehiclesModule.dll"
    "UnityEngine.VFXModule.dll"
    "UnityEngine.VideoModule.dll"
    "UnityEngine.VirtualTexturingModule.dll"
    "UnityEngine.VRModule.dll"
    "UnityEngine.WindModule.dll"
    "UnityEngine.XRModule.dll"
)

# BitCode libraries
BITCODE_FILES=(
    "BitCode.dll"
    "BitCode.AssetManagement.dll"
)

# Create symlinks for extra game files
for file in "${EXTRA_FILES[@]}"; do
    source="$GAME_PATH/Broforce_beta_Data/Managed/$file"
    target="$EXTRA_LIBS/$file"

    if [ -f "$source" ]; then
        if [ -L "$target" ] || [ -f "$target" ]; then
            rm -f "$target"
        fi
        ln -s "$source" "$target"
        echo -e "${GREEN}✓${NC} Linked $file"
    else
        # These are optional, so we don't warn
        :
    fi
done

# Create symlinks for BitCode files
for file in "${BITCODE_FILES[@]}"; do
    source="$GAME_PATH/Mods/BitCode/$file"
    target="$EXTRA_LIBS/$file"

    if [ -f "$source" ]; then
        if [ -L "$target" ] || [ -f "$target" ]; then
            rm -f "$target"
        fi
        ln -s "$source" "$target"
        echo -e "${GREEN}✓${NC} Linked $file"
    else
        echo -e "${YELLOW}⚠${NC} BitCode not found: $file (optional)"
    fi
done

# Copy Newtonsoft.Json if it exists (this one is usually copied, not linked)
NEWTONSOFT="$GAME_PATH/Broforce_beta_Data/Managed/Newtonsoft.Json.dll"
if [ -f "$NEWTONSOFT" ]; then
    cp -f "$NEWTONSOFT" "$EXTRA_LIBS/Newtonsoft.Json.dll"
    echo -e "${GREEN}✓${NC} Copied Newtonsoft.Json.dll"
fi

echo ""
echo "================================"
echo -e "${GREEN}Link creation complete!${NC}"
echo ""
echo "Core libs in: libs/Core Libs/"
echo "Extra libs in: libs/Extra Libs/"
echo ""
echo "You can now build projects with: make all"