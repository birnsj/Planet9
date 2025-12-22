# Build script for galaxytile.png
# This works around MGCB's path resolution bug

$contentDir = "Content"
$sourceFile = "$contentDir\png\galaxytile.png"
$tempFile = "$contentDir\galaxytile.png"
$outputDir = "$contentDir\xnb"

# Copy file to Content root temporarily
Copy-Item $sourceFile $tempFile -Force

# Build using MGCB (output goes to Content/Content/xnb, we'll move it)
mgcb "$contentDir\galaxytile-only.mgcb" /build /workingDir:$contentDir /outputDir:$outputDir /platform:DesktopGL

# Move file from Content/Content/xnb to Content/xnb if it was created there
if (Test-Path "$contentDir\Content\xnb\galaxytile.xnb") {
    Move-Item "$contentDir\Content\xnb\galaxytile.xnb" "$outputDir\galaxytile.xnb" -Force
    Remove-Item "$contentDir\Content" -Recurse -ErrorAction SilentlyContinue
}

# Remove temporary file
Remove-Item $tempFile -ErrorAction SilentlyContinue

Write-Host "Build complete! Check $outputDir\galaxytile.xnb"

